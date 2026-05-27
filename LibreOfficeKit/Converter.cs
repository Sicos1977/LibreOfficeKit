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

using LibreOfficeKit.Exceptions;
using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;

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
public class Converter : IDisposable
#else
public class Converter : IDisposable, IAsyncDisposable
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
    ///     The path to the worker console executable. If null or empty, it will be resolved automatically.
    /// </summary>
    private readonly string? _workerExePath;

    /// <summary>
    ///     Collection of idle workers available for use.
    /// </summary>
    private readonly ConcurrentBag<WorkerHandle> _availableWorkers = [];

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
    /// <param name="workerExePath">Optional path to the worker executable. If null or empty, it will be resolved automatically.</param>
    public Converter(int maxInstances, int minHotStandby, TimeSpan idleTimeout, string? workerExePath = null, ILogger<Converter>? logger = null)
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

        if (!string.IsNullOrWhiteSpace(workerExePath))
        {
            if (!File.Exists(workerExePath))
                throw new FileNotFoundException($"Specified worker executable '{workerExePath}' not found, current working directory '{Environment.CurrentDirectory}'.", workerExePath);

            _workerExePath = workerExePath;
        }
        else
            _workerExePath = ResolveWorkerExePath();

        if (string.IsNullOrWhiteSpace(_workerExePath))
            throw new InvalidOperationException("Worker executable path could not be resolved, try to specify it explicitly.");

        _logger.LogInformation("Converter initialized: maxInstances={MaxInstances}, minHotStandby={MinHotStandby}, idleTimeout={IdleTimeout}", maxInstances, minHotStandby, idleTimeout);

        for (var i = 0; i < minHotStandby; i++)
        {
            lock (_scaleLock)
            {
                _totalWorkerCount++;
            }

            SpawnWorkerAsync().ContinueWith(t =>
            {
                if (t.Status == TaskStatus.RanToCompletion && t.Result != null)
                {
                    _availableWorkers.Add(t.Result);
                    _workerAvailable.Release();
                    return;
                }

                lock (_scaleLock)
                {
                    _totalWorkerCount = Math.Max(0, _totalWorkerCount - 1);
                }
            }, TaskScheduler.Default);
        }

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
    /// <param name="timeout">Optional timeout for the conversion operation, when specified.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the timeout is less than or equal to zero.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the input file does not exist.</exception>
    /// <exception cref="TimeoutException">Thrown when the conversion times out.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the conversion fails.</exception>
    public async Task ConvertToPdfAsync(string inputFile, string outputFile, TimeSpan? timeout = null)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
            throw new TimeOutException("Must be greater than zero.");

        if (!File.Exists(inputFile))
            throw new FileNotFoundException("Input file not found.", inputFile);

        inputFile = Path.GetFullPath(inputFile);
        outputFile = Path.GetFullPath(outputFile);

        _logger.LogInformation("Converting '{InputFile}' to PDF -> '{OutputFile}'", inputFile, outputFile);

        var outputDir = Path.GetDirectoryName(outputFile);
        if (outputDir != null && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        DateTime? deadline = timeout.HasValue ? DateTime.UtcNow.Add(timeout.Value) : null;

        await ExecuteOnWorkerAsync(async worker =>
        {
            _logger.LogDebug("Dispatching conversion to worker '{PipeName}'", worker.PipeName);
            var request = new ConvertRequest(inputFile, outputFile);
            var requestTimeout = deadline.HasValue ? GetRemainingTimeout(deadline.Value) : (TimeSpan?)null;

            if (requestTimeout.HasValue && requestTimeout.Value <= TimeSpan.Zero)
                throw new TimeOutException("Conversion timed out.");

            var response = await worker.SendRequestAsync<ConvertResponse>(request, requestTimeout).ConfigureAwait(false);

            if (!response.Success)
            {
                _logger.LogError("PDF conversion failed for '{InputFile}': '{Error}'", inputFile, response.Error ?? "Unknown error");
                throw new ConversionFailedException($"PDF conversion failed: '{response.Error ?? "Unknown error"}'");
            }

            _logger.LogInformation("Conversion completed: '{OutputFile}'", outputFile);
        }, deadline).ConfigureAwait(false);
    }

    /// <summary>
    ///     Converts a document to PDF using streams.
    ///     The input stream is written to a temp file, converted, then the output
    ///     is read back into the output stream.
    /// </summary>
    /// <param name="inputStream">Stream containing the input document.</param>
    /// <param name="outputStream">Stream where the PDF will be written.</param>
    public async Task ConvertToPdfAsync(Stream inputStream, Stream outputStream, TimeSpan? timeout = null)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        var tempInputFile = Path.Combine(Path.GetTempPath(), $"lok_in_{Guid.NewGuid():N}.tmp");
        var tempOutputFile = Path.Combine(Path.GetTempPath(), $"lok_out_{Guid.NewGuid():N}.pdf");

        try
        {
#if NETSTANDARD2_0
            using (var inputFileStream = File.Create(tempInputFile))
                await inputStream.CopyToAsync(inputFileStream).ConfigureAwait(false);

            await ConvertToPdfAsync(tempInputFile, tempOutputFile, timeout).ConfigureAwait(false);

            using (var outputFileStream = File.OpenRead(tempOutputFile))
                await outputFileStream.CopyToAsync(outputStream).ConfigureAwait(false);
#else
            await using var inputFileStream = File.Create(tempInputFile);
            await inputStream.CopyToAsync(inputFileStream).ConfigureAwait(false);

            await ConvertToPdfAsync(tempInputFile, tempOutputFile, timeout).ConfigureAwait(false);

            await using var outputFileStream = File.OpenRead(tempOutputFile);
            await outputFileStream.CopyToAsync(outputStream).ConfigureAwait(false);

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
    private async Task ExecuteOnWorkerAsync(Func<WorkerHandle, Task> action, DateTime? deadline = null)
    {
        WorkerHandle? worker = null;

        try
        {
            if (!_availableWorkers.TryTake(out worker))
            {
                while (worker == null)
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
                        worker = await SpawnWorkerAsync(deadline).ConfigureAwait(false);
                        if (worker != null)
                            break;

                        lock (_scaleLock)
                        {
                            _totalWorkerCount = Math.Max(0, _totalWorkerCount - 1);
                        }

                        if (deadline.HasValue && DateTime.UtcNow >= deadline.Value)
                            throw new TimeOutException("Conversion timed out.");
                    }
                    else
                    {
                        if (deadline.HasValue)
                        {
                            var remaining = GetRemainingTimeout(deadline.Value);
                            if (remaining <= TimeSpan.Zero)
                                throw new TimeOutException("Conversion timed out.");

                            var signaled = await _workerAvailable.WaitAsync(remaining, _cancellationTokenSource.Token).ConfigureAwait(false);
                            if (!signaled)
                                throw new TimeOutException("Conversion timed out.");

                            _availableWorkers.TryTake(out worker);
                        }
                        else
                        {
                            await _workerAvailable.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                            _availableWorkers.TryTake(out worker);
                        }
                    }
                }
            }

            if (!worker.IsAlive)
            {
                RemoveWorker(worker);
                await ExecuteOnWorkerAsync(action, deadline).ConfigureAwait(false);
                return;
            }

            worker.MarkBusy();
            await action(worker).ConfigureAwait(false);
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
    private async Task<WorkerHandle?> SpawnWorkerAsync(DateTime? deadline = null)
    {
        var defaultTimeout = TimeSpan.FromSeconds(10);
        var pipeName = $"lok_worker_{Guid.NewGuid():N}";
        var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var processStartInfo = new ProcessStartInfo
        {
            FileName = _workerExePath!,
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
            process = Process.Start(processStartInfo);
            if (process == null || process.HasExited)
            {
                _logger.LogError("Failed to start worker process for pipe '{PipeName}'", pipeName);
#if (NETSTANDARD2_0)
                pipeServer.Dispose();
#else
                await pipeServer.DisposeAsync().ConfigureAwait(false);
#endif
                throw new ConversionFailedException("Failed to start worker process.");
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Exception starting worker process for pipe '{PipeName}'", pipeName);
#if (NETSTANDARD2_0)
            pipeServer.Dispose();
#else
            await pipeServer.DisposeAsync().ConfigureAwait(false);
#endif
            throw new ConversionFailedException("Exception starting worker process.", exception);
        }

        try
        {
            var connectionTimeout = deadline.HasValue ? GetRemainingTimeout(deadline.Value) : defaultTimeout;
            if (connectionTimeout <= TimeSpan.Zero)
                throw new TimeOutException("Conversion timed out.");

            using var cancellationTokenSource = new CancellationTokenSource(connectionTimeout);
            await pipeServer.WaitForConnectionAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            _logger.LogInformation("Connected to pipe '{PipeName}'", pipeName);

            var handle = new WorkerHandle(pipeName, process, pipeServer, _logger);
            if (!pipeServer.IsConnected || process.HasExited)
                throw new IOException("Could not establish connection to worker process.");

            var responseTimeout = deadline.HasValue ? GetRemainingTimeout(deadline.Value) : defaultTimeout;
            if (responseTimeout <= TimeSpan.Zero)
                throw new TimeOutException("Conversion timed out.");

            var response = await handle.ReadResponseAsync(responseTimeout).ConfigureAwait(false);

            switch (response)
            {
                case ReadyResponse:
                    _allWorkers[pipeName] = handle;
                    handle.MarkIdle();
                    _logger.LogInformation("Worker spawned and ready: pipe '{PipeName}', PID '{Pid}'", pipeName, process.Id);
                    return handle;

                case ErrorResponse errorResponse:
                    _logger.LogError("Worker init error on pipe '{PipeName}': '{Error}'", pipeName, errorResponse.Message);
                    throw new ConversionFailedException($"Worker init error: '{errorResponse.Message}'");
            }

            handle.Kill();
            throw new ConversionFailedException("Worker did not report ready.");
        }
        catch (TimeOutException)
        {
            try
            {
                process.Kill();
            }
            catch
            {
                // ignored
            }

#if (NETSTANDARD2_0)
            pipeServer.Dispose();
#else
            await pipeServer.DisposeAsync().ConfigureAwait(false);
#endif
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to spawn worker for pipe '{PipeName}'", pipeName);
            try
            {
                process.Kill();
            }
            catch
            {
                // ignored
            }

#if (NETSTANDARD2_0)
            pipeServer.Dispose();
#else
            await pipeServer.DisposeAsync().ConfigureAwait(false);
#endif
            throw new ConversionFailedException("Failed to spawn worker.", exception);
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

            var worker = await SpawnWorkerAsync().ConfigureAwait(false);
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
                await Task.Delay(TimeSpan.FromSeconds(15), ct).ConfigureAwait(false);
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
                    var pong = await worker.PingAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
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
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Worker '{PipeName}' ping threw an exception. Recycling.", worker.PipeName);
                    RemoveWorker(worker);
                }
            }

            try
            {
                await EnsureHotStandbyAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error ensuring hot standby");
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
                await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
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
    ///     Resolves the path to the worker console executable.
    ///     Tries to locate LibreOfficeKit.Console in the same directory as the entry assembly,
    ///     then falls back to using 'dotnet' with the assembly path.
    /// </summary>
    /// <returns>The worker executable path string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the path cannot be determined.</exception>
    private static string ResolveWorkerExePath()
    {
        var entryAssembly = Assembly.GetEntryAssembly();

        if (entryAssembly == null)
            throw new InvalidOperationException("Cannot determine the worker executable path. Ensure the application is published or run via 'dotnet run'.");

        var entryLocation = entryAssembly.Location;

        if (string.IsNullOrEmpty(entryLocation) || !File.Exists(entryLocation))
            throw new InvalidOperationException("Cannot determine the worker executable path. Ensure the application is published or run via 'dotnet run'.");

        var currentExeName = Path.GetFileNameWithoutExtension(entryLocation);

        // If running the console app directly, use it
        if (currentExeName.Equals("LibreOfficeKit.Console", StringComparison.OrdinalIgnoreCase))
            return entryLocation;

        // Look for console app in the same directory
        var directory = Path.GetDirectoryName(entryLocation);
        if (directory == null) return $"dotnet \"{entryLocation}\"";
        var consoleExeName = "LibreOfficeKit.Console" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty);
        var consoleExePath = Path.Combine(directory, consoleExeName);

        return File.Exists(consoleExePath) ? consoleExePath : $"dotnet \"{entryLocation}\"";
    }

    #region GetRemainingTimeout
    /// <summary>
    ///     Calculates the remaining timeout duration from a deadline.
    /// </summary>
    /// <param name="deadline">The absolute deadline.</param>
    /// <returns>The remaining time until the deadline, or <see cref="TimeSpan.Zero" /> if it has passed.</returns>
    private static TimeSpan GetRemainingTimeout(DateTime deadline)
    {
        var remaining = deadline - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
    #endregion
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
        var timeout = TimeSpan.FromSeconds(1);

        _cancellationTokenSource.Cancel();

        foreach (var kvp in _allWorkers)
        {
            try
            {
                kvp.Value.SendShutdownAsync().Wait(timeout);
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
            _healthMonitorTask.Wait(timeout);
        }
        catch
        {
            // ignored
        }

        try
        {
            _idleMonitorTask.Wait(timeout);
        }
        catch
        {
            // ignored
        }

        _cancellationTokenSource.Dispose();
        _poolSemaphore.Dispose();
        _workerAvailable.Dispose();
        _disposed = true;
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
        var timeout = TimeSpan.FromSeconds(1);

        await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);

        var shutdownTasks = _allWorkers.Select(kvp => Task.Run(async () =>
            {
                try
                {
                    await kvp.Value.SendShutdownAsync().ConfigureAwait(false);
                    await Task.Delay(10).ConfigureAwait(false);
                }
                catch
                {
                    // ignored
                }

                kvp.Value.Kill();
            }))
            .ToList();

        if (shutdownTasks.Count > 0)
            await Task.WhenAll(shutdownTasks).WaitAsync(timeout).ConfigureAwait(false);

        _allWorkers.Clear();

        try
        {
            await _healthMonitorTask.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        try
        {
            await _idleMonitorTask.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }

        _cancellationTokenSource.Dispose();
        _poolSemaphore.Dispose();
        _workerAvailable.Dispose();
        _disposed = true;
    }
    #endregion
#endif
}