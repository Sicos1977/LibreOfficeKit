//
// Converter.cs
//
// Author: Kees van Spelde <sicos2002@hotmail.com>
//
// Copyright (c) 2026 Kees van Spelde. (www.magic-sessions.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NON INFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
// =============================================================================
//
// High-level document converter that manages a pool of worker processes.
// Each worker is a separate OS process running its own LibreOfficeKit instance,
// communicating with the host via named pipes.
//
// Features:
//   - Hot standby: pre-spawned workers ready for immediate use
//   - On-demand scaling: additional workers up to maxInstances
//   - Idle timeout: workers shut down after idleTimeout of inactivity
//   - Health monitoring: background task detects crashes/hangs
//   - Request queue: requests queue when all workers are busy
//   - IDisposable: proper cleanup of all workers and resources
// =============================================================================

using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
// ReSharper disable UnusedMember.Global

namespace LibreOfficeKit;

/// <summary>
///     Manages a pool of LibreOffice worker processes for document-to-PDF conversion.
/// </summary>
/// <remarks>
///     High-level document converter that manages a pool of worker processes.
///     Each worker is a separate OS process running its own LibreOfficeKit instance,
///     communicating with the host via named pipes.
///
///     Features:
///       - Hot standby: pre-spawned workers ready for immediate use
///       - On-demand scaling: additional workers up to maxInstances
///       - Idle timeout: workers shut down after idleTimeout of inactivity
///       - Health monitoring: background task detects crashes/hangs
///       - Request queue: requests queue when all workers are busy
///       - IDisposable: proper cleanup of all workers and resources
/// </remarks>
#if NETSTANDARD2_0
public sealed class Converter : IDisposable
#else
public sealed class Converter : IDisposable, IAsyncDisposable
#endif
{
    #region Fields
    /// <summary>
    /// Maximum number of worker processes allowed.
    /// </summary>
    private readonly int _maxInstances;

    /// <summary>
    ///     Number of workers to start immediately and keep warm.
    /// </summary>
    private readonly int _minHotStandby;

    /// <summary>
    ///     Time after which idle workers (beyond hot standby) are shut down.
    /// </summary>
    private readonly TimeSpan _idleTimeout;

    /// <summary>
    ///     Collection of idle workers available for use.
    /// </summary>
    private readonly ConcurrentBag<WorkerHandle> _availableWorkers = new();

    /// <summary>
    ///     Dictionary of all active workers keyed by pipe name.
    /// </summary>
    private readonly ConcurrentDictionary<string, WorkerHandle> _allWorkers = new();

    /// <summary>
    ///     Semaphore controlling the maximum number of concurrent workers.
    /// </summary>
    private readonly SemaphoreSlim _poolSemaphore;

    /// <summary>
    ///     Current total number of workers (active and idle).
    /// </summary>
    private int _totalWorkerCount;

#if NETSTANDARD2_0
    /// <summary>
    /// Lock for scaling operations (spawn/remove workers).
    /// </summary>
    private readonly object _scaleLock = new();
#else
    /// <summary>
    /// Lock for scaling operations (spawn/remove workers).
    /// </summary>
    private readonly Lock _scaleLock = new();
#endif

    /// <summary>
    ///     Semaphore signaled when a worker becomes available.
    /// </summary>
    private readonly SemaphoreSlim _workerAvailable;

    /// <summary>
    ///     Cancellation token source for background tasks.
    /// </summary>
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    ///     Background task for health monitoring.
    /// </summary>
    private readonly Task _healthMonitorTask;

    /// <summary>
    ///     Background task for idle worker shutdown.
    /// </summary>
    private readonly Task _idleMonitorTask;

    /// <summary>
    ///     Indicates whether this instance has been disposed.
    /// </summary>  
    private bool _disposed;

    /// <summary>
    ///     Path to the worker executable.
    /// </summary>
    private readonly string _workerExePath;

    /// <summary>
    ///     Logger for this instance.
    /// </summary>
    private readonly ILogger<Converter> _logger;
    #endregion

    #region Converter
    /// <summary>
    ///     Creates a new <see cref="Converter" /> with the specified pool configuration.
    ///     Hot standby workers are spawned immediately.
    /// </summary>
    /// <param name="maxInstances">Maximum number of worker processes.</param>
    /// <param name="minHotStandby">Number of workers to start immediately and keep warm.</param>
    /// <param name="idleTimeout">Time after which idle workers (beyond hot standby) are shut down.</param>
    /// <param name="logger">Optional logger. When <c>null</c>, logging is disabled.</param>
    public Converter(int maxInstances, int minHotStandby, TimeSpan idleTimeout, ILogger<Converter>? logger = null)
    {
        _logger = logger ?? NullLogger<Converter>.Instance;
        if (maxInstances < 1)
            throw new ArgumentOutOfRangeException(nameof(maxInstances), "Must be at least 1.");
        if (minHotStandby < 0)
            throw new ArgumentOutOfRangeException(nameof(minHotStandby), "Cannot be negative.");
        if (minHotStandby > maxInstances)
            throw new ArgumentOutOfRangeException(nameof(minHotStandby), "Cannot exceed maxInstances.");

        _maxInstances = maxInstances;
        _minHotStandby = minHotStandby;
        _idleTimeout = idleTimeout;

        _poolSemaphore = new SemaphoreSlim(maxInstances, maxInstances);
        _workerAvailable = new SemaphoreSlim(0, maxInstances);

        _workerExePath = ResolveWorkerExePath();

        _logger.LogInformation("Converter initialized: maxInstances={MaxInstances}, minHotStandby={MinHotStandby}, idleTimeout={IdleTimeout}",
            maxInstances, minHotStandby, idleTimeout);

        for (var i = 0; i < minHotStandby; i++)
            SpawnWorkerAsync().ContinueWith(t =>
            {
                if (t.Status != TaskStatus.RanToCompletion || t.Result == null) return;
                _availableWorkers.Add(t.Result);
                _workerAvailable.Release();
            }, TaskScheduler.Default);

        _healthMonitorTask = Task.Run(() => HealthMonitorLoopAsync(_cancellationTokenSource.Token));
        _idleMonitorTask = Task.Run(() => IdleMonitorLoopAsync(_cancellationTokenSource.Token));
    }
    #endregion

    #region ConvertToPdfAsync
    /// <summary>
    ///     Converts a document to PDF using file paths.
    /// </summary>
    /// <param name="inputFile">Path to the input document.</param>
    /// <param name="outputFile">Path where the PDF will be written.</param>
    public async Task ConvertToPdfAsync(string inputFile, string outputFile)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        if (!File.Exists(inputFile))
            throw new FileNotFoundException("Input file not found.", inputFile);

        inputFile = Path.GetFullPath(inputFile);
        outputFile = Path.GetFullPath(outputFile);

        _logger.LogInformation("Converting '{InputFile}' to PDF -> '{OutputFile}'", inputFile, outputFile);

        var outputDir = Path.GetDirectoryName(outputFile);
        if (outputDir != null && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        await ExecuteOnWorkerAsync(async worker =>
        {
            _logger.LogDebug("Dispatching conversion to worker {PipeName}", worker.PipeName);
            var request = new ConvertRequest(inputFile, outputFile);
            var response = await worker.SendRequestAsync<ConvertResponse>(request);

            if (!response.Success)
            {
                _logger.LogError("PDF conversion failed for '{InputFile}': {Error}", inputFile, response.Error ?? "Unknown error");
                throw new InvalidOperationException($"PDF conversion failed: '{response.Error ?? "Unknown error"}'");
            }

            _logger.LogInformation("Conversion completed: '{OutputFile}'", outputFile);
        });
    }

    /// <summary>
    ///     Converts a document to PDF using streams.
    ///     The input stream is written to a temp file, converted, then the output
    ///     is read back into the output stream.
    /// </summary>
    /// <param name="inputStream">Stream containing the input document.</param>
    /// <param name="outputStream">Stream where the PDF will be written.</param>
    public async Task ConvertToPdfAsync(Stream inputStream, Stream outputStream)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        var tempInputFile = Path.Combine(Path.GetTempPath(), $"lok_in_{Guid.NewGuid():N}.tmp");
        var tempOutputFile = Path.Combine(Path.GetTempPath(), $"lok_out_{Guid.NewGuid():N}.pdf");

        try
        {
#if NETSTANDARD2_0
            using (var inputFileStream = File.Create(tempInputFile))
                await inputStream.CopyToAsync(inputFileStream);

            await ConvertToPdfAsync(tempInputFile, tempOutputFile);

            using (var outputFileStream = File.OpenRead(tempOutputFile))
                await outputFileStream.CopyToAsync(outputStream);
#else
            await using var inputFileStream = File.Create(tempInputFile);
            await inputStream.CopyToAsync(inputFileStream);

            await ConvertToPdfAsync(tempInputFile, tempOutputFile);

            await using var outputFileStream = File.OpenRead(tempOutputFile);
            await outputFileStream.CopyToAsync(outputStream);
#endif
        }
        finally
        {
            TryDeleteFile(tempInputFile);
            TryDeleteFile(tempOutputFile);
        }
    }
    #endregion

    #region ExecuteOnWorkerAsync
    /// <summary>
    ///     Acquires an available worker, spawning one if needed, and executes the action.
    ///     If all workers are busy and maxInstances is reached, queues until one is free.
    /// </summary>
    /// <param name="action">The action to execute on the worker.</param>
    private async Task ExecuteOnWorkerAsync(Func<WorkerHandle, Task> action)
    {
        WorkerHandle? worker = null;

        try
        {
            if (!_availableWorkers.TryTake(out worker))
            {
                bool canSpawn;
                lock (_scaleLock)
                {
                    canSpawn = _totalWorkerCount < _maxInstances;
                    if (canSpawn)
                        _totalWorkerCount++;
                }

                if (canSpawn)
                {
                    worker = await SpawnWorkerAsync();
                    if (worker == null)
                    {
                        lock (_scaleLock)
                        {
                            _totalWorkerCount--;
                        }

                        throw new InvalidOperationException("Failed to spawn worker process.");
                    }
                }
                else
                {
                    await _workerAvailable.WaitAsync(_cancellationTokenSource.Token);
                    if (!_availableWorkers.TryTake(out worker))
                        throw new InvalidOperationException("No worker available after signal.");
                }
            }

            if (!worker.IsAlive)
            {
                RemoveWorker(worker);
                await ExecuteOnWorkerAsync(action);
                return;
            }

            worker.MarkBusy();
            await action(worker);
        }
        finally
        {
            if (worker is { IsAlive: true })
            {
                worker.MarkIdle();
                _availableWorkers.Add(worker);
                _workerAvailable.Release();
            }
            else if (worker != null)
            {
                RemoveWorker(worker);
                _ = EnsureHotStandbyAsync();
            }
        }
    }
    #endregion

    #region SpawnWorkerAsync
    /// <summary>
    ///     Spawns a new worker process, sets up the named pipe, and waits for the worker to report ready.
    /// </summary>
    /// <returns>A <see cref="WorkerHandle" /> for the new worker, or <c>null</c> if spawning failed.</returns>
    private async Task<WorkerHandle?> SpawnWorkerAsync()
    {
        var pipeName = $"lok_worker_{Guid.NewGuid():N}";

        var pipeServer = new NamedPipeServerStream(
            pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        var psi = new ProcessStartInfo
        {
            FileName = _workerExePath,
            Arguments = $"--worker {pipeName}",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        Process? process;
        _logger.LogDebug("Spawning worker process with pipe '{PipeName}'", pipeName);

        try
        {
            process = Process.Start(psi);
            if (process == null)
            {
                _logger.LogError("Failed to start worker process for pipe '{PipeName}'", pipeName);
                pipeServer.Dispose();
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception starting worker process for pipe '{PipeName}'", pipeName);
            pipeServer.Dispose();
            return null;
        }

        try
        {
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await pipeServer.WaitForConnectionAsync(connectCts.Token);

            var handle = new WorkerHandle(pipeName, process, pipeServer, _logger);

            var response = await handle.ReadResponseAsync(TimeSpan.FromSeconds(30));

            switch (response)
            {
                case ReadyResponse:
                    _allWorkers[pipeName] = handle;
                    handle.MarkIdle();
                    _logger.LogInformation("Worker spawned and ready: pipe '{PipeName}', PID {Pid}", pipeName, process.Id);
                    return handle;

                case ErrorResponse err:
                    _logger.LogError("Worker init error on pipe '{PipeName}': {Error}", pipeName, err.Message);
                    break;
            }

            handle.Kill();
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to spawn worker for pipe '{PipeName}'", pipeName);
            try
            {
                process.Kill();
            }
            catch
            {
                // ignored
            }

            pipeServer.Dispose();
            return null;
        }
    }
    #endregion

    #region EnsureHotStandbyAsync
    /// <summary>
    ///     Ensures the minimum number of hot standby workers is maintained by spawning replacements.
    /// </summary>
    private async Task EnsureHotStandbyAsync()
    {
        var availableCount = _availableWorkers.Count;
        var deficit = _minHotStandby - availableCount;

        for (var i = 0; i < deficit; i++)
        {
            bool canSpawn;
            lock (_scaleLock)
            {
                canSpawn = _totalWorkerCount < _maxInstances;
                if (canSpawn)
                    _totalWorkerCount++;
            }

            if (!canSpawn) break;

            var worker = await SpawnWorkerAsync();
            if (worker != null)
            {
                _availableWorkers.Add(worker);
                _workerAvailable.Release();
            }
            else
            {
                lock (_scaleLock)
                {
                    _totalWorkerCount--;
                }
            }
        }
    }
    #endregion

    #region RemoveWorker
    /// <summary>
    ///     Removes a worker from the pool and kills its process.
    /// </summary>
    /// <param name="worker">The worker to remove.</param>
    private void RemoveWorker(WorkerHandle worker)
    {
        _allWorkers.TryRemove(worker.PipeName, out _);
        lock (_scaleLock)
        {
            _totalWorkerCount = Math.Max(0, _totalWorkerCount - 1);
            _logger.LogDebug("Removing worker '{PipeName}' from pool (total remaining: {Total}).", worker.PipeName, _totalWorkerCount);
        }
        worker.Kill();
    }
    #endregion

    #region HealthMonitorLoopAsync
    /// <summary>
    ///     Background loop that periodically pings all workers and recycles unhealthy ones.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the monitoring loop.</param>
    private async Task HealthMonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            foreach (var kvp in _allWorkers)
            {
                var worker = kvp.Value;

                if (worker.IsBusy) continue;

                if (!worker.IsAlive)
                {
                    _logger.LogWarning("Worker '{PipeName}' process died. Removing from pool.", worker.PipeName);
                    RemoveWorker(worker);
                    continue;
                }

                try
                {
                    _logger.LogDebug("Pinging worker '{PipeName}'", worker.PipeName);
                    var pong = await worker.PingAsync(TimeSpan.FromSeconds(5));
                    if (!pong)
                    {
                        _logger.LogWarning("Worker '{PipeName}' did not respond to ping. Recycling.", worker.PipeName);
                        RemoveWorker(worker);
                    }
                    else
                    {
                        _logger.LogDebug("Worker '{PipeName}' ping OK.", worker.PipeName);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Worker '{PipeName}' ping threw an exception. Recycling.", worker.PipeName);
                    RemoveWorker(worker);
                }
            }

            try
            {
                await EnsureHotStandbyAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error ensuring hot standby");
            }
        }
    }
    #endregion

    #region IdleMonitorLoopAsync
    /// <summary>
    ///     Background loop that shuts down idle workers beyond the hot standby minimum.
    /// </summary>
    /// <param name="ct">Cancellation token to stop the monitoring loop.</param>
    private async Task IdleMonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var idleCount = 0;
            var workersToShutdown = new List<WorkerHandle>();

            foreach (var kvp in _allWorkers)
            {
                var worker = kvp.Value;
                if (worker.IsBusy) continue;

                idleCount++;
                if (idleCount > _minHotStandby && worker.IdleDuration >= _idleTimeout) workersToShutdown.Add(worker);
            }

            foreach (var worker in workersToShutdown)
            {
                _logger.LogInformation("Shutting down idle worker '{PipeName}' (idle for {IdleSeconds:F0}s).",
                    worker.PipeName, worker.IdleDuration.TotalSeconds);
                RemoveWorker(worker);
            }
        }
    }
    #endregion

    #region ResolveWorkerExePath
    /// <summary>
    ///     Resolves the path to the current executable, used to spawn workers in <c>--worker</c> mode.
    /// </summary>
    /// <returns>The executable path string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the path cannot be determined.</exception>
    private static string ResolveWorkerExePath()
    {
#if !NETSTANDARD2_0
        var processPath = Environment.ProcessPath;
        if (processPath != null && File.Exists(processPath))
            return processPath;
#endif

        var entryAssembly = Assembly.GetEntryAssembly()?.Location;
        if (entryAssembly != null && File.Exists(entryAssembly))
            return $"dotnet \"{entryAssembly}\"";

        throw new InvalidOperationException("Cannot determine the worker executable path. Ensure the application is published or run via 'dotnet run'.");
    }
    #endregion

    #region TryDeleteFile
    /// <summary>
    ///     Attempts to delete a file, silently ignoring any errors.
    /// </summary>
    /// <param name="path">The file path to delete.</param>
    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // ignored
        }
    }
    #endregion

    #region Dispose
    /// <summary>
    ///     Synchronously disposes the converter, shutting down all workers and releasing resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cancellationTokenSource.Cancel();

        foreach (var kvp in _allWorkers)
        {
            try
            {
                kvp.Value.SendShutdownAsync().Wait(TimeSpan.FromSeconds(3));
            }
            catch
            {
                // ignored
            }

            kvp.Value.Kill();
        }

        _allWorkers.Clear();

        try
        {
            _healthMonitorTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignored
        }

        try
        {
            _idleMonitorTask.Wait(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignored
        }

        _cancellationTokenSource.Dispose();
        _poolSemaphore.Dispose();
        _workerAvailable.Dispose();
    }
    #endregion

#if NET10_0_OR_GREATER
    #region DisposeAsync
    /// <summary>
    ///     Asynchronously disposes the converter, gracefully shutting down all workers and releasing resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        await _cancellationTokenSource.CancelAsync();

        var shutdownTasks = _allWorkers.Select(kvp => Task.Run(async () =>
            {
                try
                {
                    await kvp.Value.SendShutdownAsync();
                    await Task.Delay(500);
                }
                catch
                {
                    // ignored
                }

                kvp.Value.Kill();
            }))
            .ToList();

        if (shutdownTasks.Count > 0)
            await Task.WhenAll(shutdownTasks).WaitAsync(TimeSpan.FromSeconds(10));

        _allWorkers.Clear();

        try
        {
            await _healthMonitorTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignored
        }

        try
        {
            await _idleMonitorTask.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch
        {
            // ignored
        }

        _cancellationTokenSource.Dispose();
        _poolSemaphore.Dispose();
        _workerAvailable.Dispose();
    }
    #endregion
#endif
}