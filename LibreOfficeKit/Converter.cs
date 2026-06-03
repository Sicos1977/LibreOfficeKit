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

using LibreOfficeKit.Enums;
using LibreOfficeKit.Exceptions;
using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using TimeoutException = LibreOfficeKit.Exceptions.TimeoutException;

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
public class Converter : IAsyncDisposable
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
    private readonly ConcurrentDictionary<string, WorkerHandle> _workers = [];

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
    /// <param name="workerExePath">Optional path to the worker executable. If null or empty, it will be resolved automatically.</param>
    /// <param name="logger">Optional logger. When <c>null</c>, logging is disabled.</param>
    public Converter(int maxInstances, int minHotStandby, TimeSpan idleTimeout, string? workerExePath = null, ILogger<Converter>? logger = null)
    {
        _logger = logger ?? NullLogger<Converter>.Instance;

        // Clean up any orphaned worker processes from previous runs
        CleanupOrphanedWorkers(_logger);

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
        _logger.LogDebug("Worker executable path: '{WorkerExePath}'", _workerExePath);

        for (var i = 0; i < minHotStandby; i++)
        {
            _logger.LogDebug("Pre-spawning hot-standby worker {Index}/{Total}", i + 1, minHotStandby);
            lock (_scaleLock)
            {
                _totalWorkerCount++;
            }

            var workerIndex = i; // Capture for closure
            SpawnWorkerAsync().ContinueWith(t =>
            {
                if (t is { Status: TaskStatus.RanToCompletion, Result: not null })
                {
                    _logger.LogInformation("Hot-standby worker {Index}/{Total} spawned successfully", workerIndex + 1, minHotStandby);
                    _availableWorkers.Add(t.Result);
                    _workerAvailable.Release();
                    return;
                }

                _logger.LogError("Failed to spawn hot-standby worker {Index}/{Total}: Status={Status}, Result={Result}", workerIndex + 1, minHotStandby, t.Status, t.Result);

                lock (_scaleLock)
                    _totalWorkerCount = Math.Max(0, _totalWorkerCount - 1);

            }, TaskScheduler.Default);
        }

        _logger.LogDebug("Starting health monitor and idle monitor tasks");
        _healthMonitorTask = Task.Run(() => HealthMonitorLoopAsync(_cancellationTokenSource.Token));
        _idleMonitorTask = Task.Run(() => IdleMonitorLoopAsync(_cancellationTokenSource.Token));
        _logger.LogInformation("Converter initialization complete");
    }
    #endregion

    #region GetMinimumLogLevel
    /// <summary>
    ///     Determines the minimum enabled log level for the logger.
    /// </summary>
    /// <returns>The minimum log level that is enabled, or <see cref="LogLevel.None"/> if no logger is configured.</returns>
    private LogLevel GetMinimumLogLevel()
    {
        // If no logger is provided (NullLogger), worker should not log
        if (_logger is NullLogger<Converter>)
            return LogLevel.None;

        // Check levels from most verbose to least verbose
        if (_logger.IsEnabled(LogLevel.Trace)) return LogLevel.Trace;
        if (_logger.IsEnabled(LogLevel.Debug)) return LogLevel.Debug;
        if (_logger.IsEnabled(LogLevel.Information)) return LogLevel.Information;
        if (_logger.IsEnabled(LogLevel.Warning)) return LogLevel.Warning;
        if (_logger.IsEnabled(LogLevel.Error)) return LogLevel.Error;
        return _logger.IsEnabled(LogLevel.Critical) ? LogLevel.Critical : LogLevel.None;
    }
    #endregion

    #region ConvertToPdfAsync
    /// <summary>
    ///     Converts a document to PDF using file paths.
    /// </summary>
    /// <param name="inputFile">Path to the input document.</param>
    /// <param name="outputFile">Path where the PDF will be written.</param>
    /// <param name="timeout">Optional timeout for the conversion operation, when specified.</param>
    /// <param name="options">Optional <see cref="PdfOptions"/> controlling quality, compliance, security, and layout. When <c>null</c>, the LibreOffice defaults are used.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the timeout is less than or equal to zero.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the input file does not exist.</exception>
    /// <exception cref="LibreOfficeKit.Exceptions.TimeoutException">Thrown when the conversion times out.</exception>
    /// <exception cref="LibreOfficeKit.Exceptions.FilePasswordProtectedException">Thrown when the document is password-protected.</exception>
    /// <exception cref="LibreOfficeKit.Exceptions.FileTypeNotSupportedException">Thrown when the file type is not supported.</exception>
    /// <exception cref="LibreOfficeKit.Exceptions.ConversionFailedException">Thrown when the conversion fails.</exception>
    /// <remarks>
    ///     Default timeout is 60 minutes, which should be sufficient for even very large documents. Adjust as needed for your use case.
    /// </remarks>
    public async Task ConvertToPdfAsync(string inputFile, string outputFile, TimeSpan? timeout = null, PdfOptions? options = null)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        if (timeout.HasValue && timeout.Value <= TimeSpan.Zero)
            throw new TimeoutException("Must be greater than zero.");

        timeout ??= TimeSpan.FromMinutes(60);

        if (!File.Exists(inputFile))
            throw new FileNotFoundException("Input file not found.", inputFile);

        inputFile = Path.GetFullPath(inputFile);
        outputFile = Path.GetFullPath(outputFile);

        _logger.LogInformation("Converting '{InputFile}' to PDF -> '{OutputFile}'", inputFile, outputFile);

        var outputDir = Path.GetDirectoryName(outputFile);
        if (outputDir != null && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        DateTime? deadline = DateTime.UtcNow.Add(timeout.Value);

        await ExecuteOnWorkerAsync(async worker =>
        {
            _logger.LogDebug("Dispatching conversion to worker '{PipeName}'", worker.PipeName);
            var request = new ConvertRequest(inputFile, outputFile, options?.ToFilterOptions());
            var requestTimeout = GetRemainingTimeout(deadline.Value);

            if (requestTimeout <= TimeSpan.Zero)
                throw new TimeoutException("Conversion timed out.");

            var response = await worker.ConvertAsync(request, requestTimeout).ConfigureAwait(false);

            if (!response.Success)
            {
                _logger.LogError("PDF conversion failed for '{InputFile}': '{Error}'", inputFile, response.Error ?? "Unknown error");

                throw response.ExceptionType switch
                {
                    // Re-throw the original exception type if available
                    nameof(FilePasswordProtectedException) => new FilePasswordProtectedException($"PDF conversion failed: '{response.Error ?? "Unknown error"}'"),
                    nameof(FileTypeNotSupportedException) => new FileTypeNotSupportedException($"PDF conversion failed: '{response.Error ?? "Unknown error"}'"), _ => new ConversionFailedException($"PDF conversion failed: '{response.Error ?? "Unknown error"}'")
                };
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
    /// <param name="timeout">Optional timeout for the conversion operation, when specified.</param>
    /// <param name="options">Optional <see cref="PdfOptions"/> controlling quality, compliance, security, and layout. When <c>null</c>, the LibreOffice defaults are used.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the timeout is less than or equal to zero.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the input file does not exist.</exception>
    /// <exception cref="LibreOfficeKit.Exceptions.TimeoutException">Thrown when the conversion times out.</exception>
    /// <exception cref="LibreOfficeKit.Exceptions.ConversionFailedException">Thrown when the conversion fails.</exception>
    public async Task ConvertToPdfAsync(Stream inputStream, Stream outputStream, TimeSpan? timeout = null, PdfOptions? options = null)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        var tempInputFile = Path.Combine(Path.GetTempPath(), $"lok_in_{Guid.NewGuid():N}.tmp");
        var tempOutputFile = Path.Combine(Path.GetTempPath(), $"lok_out_{Guid.NewGuid():N}.pdf");

        try
        {
#if NETSTANDARD2_0
            using (var inputFileStream = File.Create(tempInputFile))
                await inputStream.CopyToAsync(inputFileStream).ConfigureAwait(false);

            await ConvertToPdfAsync(tempInputFile, tempOutputFile, timeout, options).ConfigureAwait(false);

            using (var outputFileStream = File.OpenRead(tempOutputFile))
                await outputFileStream.CopyToAsync(outputStream).ConfigureAwait(false);
#else
            await using var inputFileStream = File.Create(tempInputFile);
            await inputStream.CopyToAsync(inputFileStream).ConfigureAwait(false);

            await ConvertToPdfAsync(tempInputFile, tempOutputFile, timeout, options).ConfigureAwait(false);

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
    /// <param name="deadline">The optional deadline for the operation. If specified, the operation will be canceled if it exceeds this time.</param>
    private async Task ExecuteOnWorkerAsync(Func<WorkerHandle, Task> action, DateTime? deadline = null)
    {
        WorkerHandle? worker = null;
        var workerFromWait = false;

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
                            throw new TimeoutException("Conversion timed out.");
                    }
                    else
                    {
                        if (deadline.HasValue)
                        {
                            var remaining = GetRemainingTimeout(deadline.Value);
                            if (remaining <= TimeSpan.Zero)
                                throw new TimeoutException("Conversion timed out.");

                            var signaled = await _workerAvailable.WaitAsync(remaining, _cancellationTokenSource.Token).ConfigureAwait(false);
                            if (!signaled)
                                throw new TimeoutException("Conversion timed out.");

                            if (!_availableWorkers.TryTake(out worker))
                            {
                                // Race condition: semaphore was signaled but no worker available
                                // This can happen if another thread took the worker between signal and TryTake
                                // Release the semaphore signal we consumed and retry
                                _workerAvailable.Release();
                                continue;
                            }
                            workerFromWait = true;
                        }
                        else
                        {
                            await _workerAvailable.WaitAsync(_cancellationTokenSource.Token).ConfigureAwait(false);
                            if (!_availableWorkers.TryTake(out worker))
                            {
                                // Race condition: semaphore was signaled but no worker available
                                // Release the semaphore signal we consumed and retry
                                _workerAvailable.Release();
                                continue;
                            }
                            workerFromWait = true;
                        }
                    }
                }
            }

            if (!worker.IsAlive)
            {
                await RemoveWorkerAsync(worker).ConfigureAwait(false);
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
                // Try to release - this may fail if semaphore is already at max, which can happen
                // due to race conditions or if the worker was pre-spawned during initialization
                try
                {
                    _workerAvailable.Release();
                }
                catch (SemaphoreFullException)
                {
                    // Semaphore is already at maximum - this is OK, it means the worker
                    // was already counted in the semaphore (e.g., from initialization or a race condition)
                    _logger.LogTrace("Semaphore already at maximum when returning worker '{PipeName}' - this is expected in some scenarios", worker.PipeName);
                }
            }
            else if (worker != null)
            {
                await RemoveWorkerAsync(worker).ConfigureAwait(false);
                // If we consumed a semaphore signal but the worker died, we need to release it back
                if (workerFromWait)
                {
                    try
                    {
                        _workerAvailable.Release();
                    }
                    catch (SemaphoreFullException)
                    {
                        _logger.LogTrace("Semaphore already at maximum when releasing signal for dead worker '{PipeName}'", worker.PipeName);
                    }
                }
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
        var defaultTimeout = TimeSpan.FromSeconds(30);
        var pipeName = $"lok_worker_{Guid.NewGuid():N}";
        var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

        Process? process = null;

        _logger.LogDebug("Spawning worker process with pipe '{PipeName}'", pipeName);

        try
        {
            var logLevel = GetMinimumLogLevel();
            process = Process.Start(new ProcessStartInfo
            {
                FileName = _workerExePath!,
                Arguments = $"--worker {pipeName} --loglevel {logLevel}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardError = true,
            }) ?? throw new ConversionFailedException("Failed to start worker process.");

            if (process.HasExited)
            {
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                throw new ConversionFailedException($"Failed to start worker process, error: '{error}'");
            }

            var connectionTimeout = deadline.HasValue ? GetRemainingTimeout(deadline.Value) : defaultTimeout;
            if (connectionTimeout <= TimeSpan.Zero) throw new TimeoutException("Conversion timed out.");

            using var cancellationTokenSource = new CancellationTokenSource(connectionTimeout);
            await pipeServer.WaitForConnectionAsync(cancellationTokenSource.Token).ConfigureAwait(false);
            _logger.LogInformation("Connected to pipe '{PipeName}'", pipeName);

            if (!pipeServer.IsConnected || process.HasExited)
                throw new IOException("Could not establish connection to worker process.");

            var responseTimeout = deadline.HasValue ? GetRemainingTimeout(deadline.Value) : defaultTimeout;
            if (responseTimeout <= TimeSpan.Zero) throw new TimeoutException("Conversion timed out.");

            var handle = new WorkerHandle(pipeName, process, pipeServer, _logger);
            var ready = await handle.ReadyAsync(responseTimeout).ConfigureAwait(false);

            if (ready)
            {
                _workers[pipeName] = handle;
                handle.MarkIdle();
                _logger.LogInformation("Worker spawned and ready: pipe '{PipeName}', PID '{Pid}'", pipeName, process.Id);
                return handle;
            }

#if NETSTANDARD2_0
            handle.DisposeAsync().GetAwaiter().GetResult();
#else
            await handle.DisposeAsync().ConfigureAwait(false);
#endif
            throw new ConversionFailedException("Worker initialization failed.");
        }
        catch (Exception exception)
        {
            _logger.LogError("Worker init error on pipe '{PipeName}', Error: '{Error}'", pipeName, exception.Message);

            KillProcess(process);
#if NETSTANDARD2_0
            pipeServer.Dispose();
#else
       //     await pipeServer.DisposeAsync().ConfigureAwait(false);
#endif
            if (exception is ConversionFailedException or TimeoutException) throw;
            _logger.LogError(exception, "Failed to spawn worker for pipe '{PipeName}'", pipeName);
            throw new ConversionFailedException("Failed to spawn worker.", exception);
        }
    }
    #endregion

    #region KillProcess
    /// <summary>
    ///     Kills the given process, ignoring any exceptions that occur during killing. Used for cleanup of worker processes.
    /// </summary>
    /// <param name="process">The process to kill.</param>
    private static void KillProcess(Process? process)
    {
        if (process == null) return;

        try
        {

            if (!process.HasExited)
            {
#if NETSTANDARD2_0
                process.Kill();
#else
                process.Kill(true);
#endif
            }
        }
        catch
        {
            /* ignored */
        }

        process.Dispose();
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

    #region RemoveWorkerAsync
    /// <summary>
    ///     Removes a worker from the pool and disposes its resources.
    /// </summary>
    /// <param name="worker">The worker to remove.</param>
#if NETSTANDARD2_0
    private Task RemoveWorkerAsync(WorkerHandle worker)
#else
    private async Task RemoveWorkerAsync(WorkerHandle worker)
#endif
    {
        _workers.TryRemove(worker.PipeName, out _);
        lock (_scaleLock)
        {
            _totalWorkerCount = Math.Max(0, _totalWorkerCount - 1);
            _logger.LogDebug("Removing worker '{PipeName}' from pool (total remaining: {Total}).", worker.PipeName, _totalWorkerCount);
        }
#if NETSTANDARD2_0
        worker.DisposeAsync().GetAwaiter().GetResult();
        return Task.CompletedTask;
#else
        await worker.DisposeAsync().ConfigureAwait(false);
#endif
    }
    #endregion

    #region HealthMonitorLoopAsync
    /// <summary>
    ///     Background loop that periodically pings all workers and recycles unhealthy ones.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the monitoring loop.</param>
    private async Task HealthMonitorLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            foreach (var kvp in _workers)
            {
                var worker = kvp.Value;

                if (worker.IsBusy) continue;

                if (!worker.IsAlive)
                {
                    _logger.LogWarning("Worker '{PipeName}' process died. Removing from pool.", worker.PipeName);
                    await RemoveWorkerAsync(worker).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    _logger.LogDebug("Pinging worker '{PipeName}'", worker.PipeName);
                    var pong = await worker.PingAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    if (!pong)
                    {
                        _logger.LogWarning("Worker '{PipeName}' did not respond to ping. Recycling.", worker.PipeName);
                        await RemoveWorkerAsync(worker).ConfigureAwait(false);
                    }
                    else
                    {
                        _logger.LogDebug("Worker '{PipeName}' ping OK.", worker.PipeName);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Worker '{PipeName}' ping threw an exception. Recycling.", worker.PipeName);
                    await RemoveWorkerAsync(worker).ConfigureAwait(false);
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

            foreach (var kvp in _workers)
            {
                var worker = kvp.Value;
                if (worker.IsBusy) continue;

                idleCount++;
                if (idleCount > _minHotStandby && worker.IdleDuration >= _idleTimeout) workersToShutdown.Add(worker);
            }

            foreach (var worker in workersToShutdown)
            {
                _logger.LogInformation("Shutting down idle worker '{PipeName}' (idle for {IdleSeconds:F0}s).", worker.PipeName, worker.IdleDuration.TotalSeconds);
                await RemoveWorkerAsync(worker).ConfigureAwait(false);
            }
        }
    }
    #endregion

    #region CleanupOrphanedWorkers
    /// <summary>
    ///     Cleans up any orphaned worker processes that may have been left running from previous application runs.
    ///     This prevents resource leaks and potential file locking issues.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostics.</param>
    internal static void CleanupOrphanedWorkers(ILogger? logger = null)
    {
        try
        {
            var workerProcesses = Process.GetProcessesByName("LibreOfficeKit.Console");
            if (workerProcesses.Length == 0)
            {
                logger?.LogDebug("No orphaned worker processes found");
                return;
            }

            logger?.LogWarning("Found {Count} orphaned worker process(es) from previous run(s), terminating them...", workerProcesses.Length);

            var killed = 0;
            foreach (var process in workerProcesses)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        logger?.LogDebug("Killing orphaned worker process PID {ProcessId}", process.Id);
                        process.Kill();
                        process.WaitForExit(1000);
                        killed++;
                    }
                    process.Dispose();
                }
                catch (Exception ex)
                {
                    logger?.LogWarning(ex, "Failed to kill orphaned worker process PID {ProcessId}", process.Id);
                }
            }

            logger?.LogInformation("Terminated {KilledCount} orphaned worker process(es)", killed);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "Exception during orphaned worker cleanup (non-critical)");
        }
    }
    #endregion

    #region ResolveWorkerExePath
    /// <summary>
    ///     Resolves the path to the worker console executable.
    ///     Tries to locate LibreOfficeKit.Console in the same directory as the entry assembly
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
        if (currentExeName.Equals("LibreOfficeKitWorker.exe", StringComparison.OrdinalIgnoreCase))
            return entryLocation;

        // Look for console app in the same directory
        var directory = Path.GetDirectoryName(entryLocation);
        if (directory == null) return $"dotnet \"{entryLocation}\"";
        var consoleExeName = "LibreOfficeKitWorker" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty);
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
    ///     Attempts to delete a file. Logs a warning when deletion fails.
    /// </summary>
    /// <param name="path">The file path to delete.</param>
    private void TryDeleteFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            File.Delete(path);
            _logger.LogDebug("Deleted temporary file '{Path}'", path);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to delete temporary file '{Path}'", path);
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
        _logger.LogInformation("Disposing converter (synchronous), shutting down {Count} worker(s)", _workers.Count);

        _cancellationTokenSource.Cancel();

        foreach (var kvp in _workers)
        {
            try
            {
                kvp.Value.DisposeAsync().GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Failed to dispose worker '{PipeName}'", kvp.Key);
            }
        }

        _workers.Clear();

        var timeout = TimeSpan.FromSeconds(5);
        try
        {
            _healthMonitorTask.Wait(timeout);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Health monitor task did not complete within timeout");
        }

        try
        {
            _idleMonitorTask.Wait(timeout);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Idle monitor task did not complete within timeout");
        }

        _cancellationTokenSource.Dispose();
        _poolSemaphore.Dispose();
        _workerAvailable.Dispose();
        _disposed = true;
        _logger.LogInformation("Converter disposed");
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
        _logger.LogInformation("Disposing converter (asynchronous), shutting down {Count} worker(s)", _workers.Count);
        var timeout = TimeSpan.FromSeconds(60);

        await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);

        var shutdownTasks = _workers.Select(kvp => Task.Run(async () =>
            {
                try
                {
                    await kvp.Value.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Failed to dispose worker '{PipeName}'", kvp.Key);
                }
            }))
            .ToList();

        if (shutdownTasks.Count > 0)
        {
            await Task.WhenAll(shutdownTasks)
                .WaitAsync(timeout)
                .ContinueWith(t =>
                {
                    if (t is { IsFaulted: true, Exception.InnerException: TimeoutException timeoutEx })
                    {
                        _logger.LogWarning(timeoutEx, "Shutdown tasks did not complete within timeout");
                    }
                    else if (t.IsFaulted)
                    {
                        _logger.LogError(t.Exception, "An error occurred during shutdown tasks");
                    }
                }, TaskContinuationOptions.ExecuteSynchronously)
                .ConfigureAwait(false);
        }

        _workers.Clear();

        try
        {
            await _healthMonitorTask.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Health monitor task did not complete within timeout");
        }

        try
        {
            await _idleMonitorTask.WaitAsync(timeout).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Idle monitor task did not complete within timeout");
        }

        _cancellationTokenSource.Dispose();
        _poolSemaphore.Dispose();
        _workerAvailable.Dispose();
        _disposed = true;
        _logger.LogInformation("Converter disposed");
    }
    #endregion
#endif
}