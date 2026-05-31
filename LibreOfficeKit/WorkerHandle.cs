//
// WorkerHandle.cs
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

using LibreOfficeKit.Exceptions;
using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using TimeoutException = LibreOfficeKit.Exceptions.TimeoutException;

namespace LibreOfficeKit;

/// <summary>
///     Represents a handle to a single LibreOffice worker process,
///     encapsulating its OS process, named pipe, and communication channels.
/// </summary>
#if NETSTANDARD2_0
internal class WorkerHandle : IDisposable
#else
internal class WorkerHandle : IDisposable, IAsyncDisposable
#endif
{
    #region Fields
    /// <summary>The OS process running the worker.</summary>
    private readonly Process _process;

    /// <summary>The named pipe server stream for IPC.</summary>
    private readonly NamedPipeServerStream _pipe;

    /// <summary>Stream reader for receiving messages from the worker.</summary>
    private readonly StreamReader _reader;

    /// <summary>Stream writer for sending messages to the worker.</summary>
    private readonly StreamWriter _writer;

    /// <summary>Semaphore ensuring only one request is sent at a time.</summary>
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    /// <summary>Indicates whether the worker is currently processing a request.</summary>
    private volatile bool _isBusy;

    /// <summary>The timestamp when the worker last became idle.</summary>
    private DateTime _idleSince;

    /// <summary>Indicates whether the worker has been disposed.</summary>
    private bool _disposed;

    /// <summary>Logger for this worker handle.</summary>
    private readonly ILogger _logger;
    #endregion

    #region Properties
    /// <summary>
    ///     Gets the named pipe identifier for this worker.
    /// </summary>
    public string PipeName { get; }

    /// <summary>
    ///     Gets a value indicating whether the worker is currently processing a request.
    /// </summary>
    public bool IsBusy => _isBusy;

    /// <summary>
    ///     Gets a value indicating whether the worker process is still alive.
    /// </summary>
    public bool IsAlive => !_disposed && !_process.HasExited;

    /// <summary>
    ///     Gets the duration the worker has been idle. Returns <see cref="TimeSpan.Zero" /> if busy.
    /// </summary>
    public TimeSpan IdleDuration => _isBusy ? TimeSpan.Zero : DateTime.UtcNow - _idleSince;
    #endregion

    #region Constructor
    /// <summary>
    ///     Initializes a new instance of <see cref="WorkerHandle" /> for the given worker process and pipe.
    /// </summary>
    /// <param name="pipeName">The named pipe identifier.</param>
    /// <param name="process">The OS process running the worker.</param>
    /// <param name="pipe">The named pipe server stream for IPC.</param>
    /// <param name="logger">Optional logger.</param>
    public WorkerHandle(string pipeName, Process process, NamedPipeServerStream pipe, ILogger? logger = null)
    {
        PipeName = pipeName;
        _process = process;
        _pipe = pipe;
        _reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, true);
        _writer = new StreamWriter(pipe, Encoding.UTF8, 4096, true);
        _idleSince = DateTime.UtcNow;
        _logger = logger ?? NullLogger.Instance;
    }
    #endregion

    #region MarkBusy
    /// <summary>
    ///     Marks the worker as busy (currently processing a request).
    /// </summary>
    public void MarkBusy()
    {
        _isBusy = true;
    }
    #endregion

    #region MarkIdle
    /// <summary>
    ///     Marks the worker as idle and records the current time.
    /// </summary>
    public void MarkIdle()
    {
        _idleSince = DateTime.UtcNow;
        _isBusy = false;
    }
    #endregion

    #region SendRequestAsync
    /// <summary>
    ///     Sends a request to the worker and reads the typed response.
    /// </summary>
    /// <typeparam name="T">The expected response type.</typeparam>
    /// <param name="request">The request to send.</param>
    /// <param name="timeout">Optional timeout (defaults to 5 minutes).</param>
    /// <returns>The typed response from the worker.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the response type is unexpected or an error occurs.</exception>
    /// <exception cref="System.TimeoutException">Thrown when the worker does not respond in time.</exception>
    /// <remarks>
    ///     Default timeout is 5 minutes
    /// </remarks>
    public async Task<T> SendRequestAsync<T>(WorkerRequest request, TimeSpan? timeout = null) where T : WorkerResponse
    {
        timeout ??= TimeSpan.FromMinutes(5);

        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _logger.LogTrace("Sending {RequestType} to worker '{PipeName}'", request.GetType().Name, PipeName);
            var json = IpcSerializer.Serialize(request);
            await _writer.WriteLineAsync(json).ConfigureAwait(false);
            await _writer.FlushAsync().ConfigureAwait(false);
            var response = await ReadResponseAsync(timeout.Value).ConfigureAwait(false);

            switch (response)
            {
                case T typed:
                    _logger.LogTrace("Received {ResponseType} from worker '{PipeName}'", typeof(T).Name, PipeName);
                    return typed;
                case ErrorResponse err:
                    _logger.LogError("Worker '{PipeName}' returned error: {Error}", PipeName, err.Message);
                    throw new ConversionFailedException($"Worker error: {err.Message}");
                default:
                    _logger.LogError("Unexpected response type '{ResponseType}' from worker '{PipeName}'", response?.GetType().Name ?? "null", PipeName);
                    throw new ConversionFailedException($"Unexpected response type: {response?.GetType().Name ?? "null"}");
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }
    #endregion

    #region ReadResponseAsync
    /// <summary>
    ///     Reads a single response from the worker with a timeout.
    ///     Automatically processes and logs any <see cref="LogResponse"/> messages,
    ///     then continues reading until a non-log response is received.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for a response.</param>
    /// <returns>The deserialized response, or <c>null</c> if the pipe was closed.</returns>
    /// <exception cref="System.TimeoutException">Thrown when the worker does not respond in time.</exception>
    public async Task<WorkerResponse?> ReadResponseAsync(TimeSpan timeout)
    {
        using var cancellationTokenSource = new CancellationTokenSource(timeout);

        while (true)
        {
            try
            {
                // ReSharper disable once MethodSupportsCancellation
                var readTask = _reader.ReadLineAsync();
                var delayTask = Task.Delay(timeout, cancellationTokenSource.Token);
                if (await Task.WhenAny(readTask, delayTask).ConfigureAwait(false) == delayTask)
                    throw new TimeoutException("Worker did not respond in time.");

                var line = await readTask.ConfigureAwait(false);

                if (line == null)
                    return null;

                _logger.LogTrace("[Worker '{PipeName}'] Received line: {Line}", PipeName, line);

                WorkerResponse? response;
                try
                {
                    response = IpcSerializer.Deserialize<WorkerResponse>(line);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "[Worker '{PipeName}'] Failed to deserialize response: {Line}", PipeName, line);
                    continue;
                }

                // If it's a log message, process it and continue reading
                if (response is not LogResponse logResponse) return response;
                ProcessLogResponse(logResponse);

                // Otherwise return the response
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException("Worker did not respond in time.");
            }
        }
    }
    #endregion

    #region ProcessLogResponse
    /// <summary>
    ///     Processes a log response from the worker and logs it with the appropriate level.
    /// </summary>
    /// <param name="logResponse">The log response to process.</param>
    private void ProcessLogResponse(LogResponse logResponse)
    {
        if (string.IsNullOrEmpty(logResponse.Exception))
            _logger.Log(logResponse.LogLevel, "[Worker '{PipeName}'] {Message}", PipeName, logResponse.Message);
        else
            _logger.Log(logResponse.LogLevel, "[Worker '{PipeName}'] {Message}\n{Exception}", PipeName, logResponse.Message, logResponse.Exception);
    }
    #endregion

    #region PingAsync
    /// <summary>
    ///     Sends a ping to the worker and waits for a pong response.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the pong.</param>
    /// <returns><c>true</c> if the worker responded with a pong; otherwise <c>false</c>.</returns>
    public async Task<bool> PingAsync(TimeSpan timeout)
    {
        await _sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = IpcSerializer.Serialize<WorkerRequest>(new PingRequest());
            await _writer.WriteLineAsync(json).ConfigureAwait(false);

            var response = await ReadResponseAsync(timeout).ConfigureAwait(false);
            return response is PongResponse;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Ping to worker '{PipeName}' failed", PipeName);
            return false;
        }
        finally
        {
            _sendLock.Release();
        }
    }
    #endregion

    #region SendShutdownAsync
    /// <summary>
    ///     Sends a graceful shutdown request to the worker.
    /// </summary>
    public async Task SendShutdownAsync()
    {
        try
        {
            var json = IpcSerializer.Serialize<WorkerRequest>(new ShutdownRequest());
            await _writer.WriteLineAsync(json).ConfigureAwait(false);
            await _writer.FlushAsync().ConfigureAwait(false);
        }
        catch
        {
            // ignored
        }
    }
    #endregion

    #region Dispose
    /// <summary>
    ///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    ///     Attempts a graceful shutdown first, then forcefully terminates the worker if needed.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Releases the unmanaged resources used by the <see cref="WorkerHandle"/> and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        _disposed = true;

        if (!disposing) return;
        _logger.LogDebug("Disposing worker '{PipeName}'", PipeName);

        // Try graceful shutdown first
        try
        {
            if (!_process.HasExited)
            {
                var shutdownTask = SendShutdownAsync();
                if (!shutdownTask.Wait(TimeSpan.FromSeconds(2)))
                {
                    _logger.LogWarning("Worker '{PipeName}' did not respond to shutdown request within 2 seconds", PipeName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error sending shutdown to worker '{PipeName}'", PipeName);
        }

        // Dispose managed resources
        _sendLock.Dispose();
        _reader.Dispose();
        _writer.Dispose();
        _pipe.Dispose();

        // Forcefully kill if still running
        try
        {
            if (!_process.HasExited)
            {
                _logger.LogDebug("Forcefully killing worker '{PipeName}'", PipeName);
#if NETSTANDARD2_0
                    _process.Kill();
#else
                _process.Kill(true);
#endif
                if (!_process.WaitForExit(1000))
                {
                    _logger.LogWarning("Worker '{PipeName}' did not exit within 1 second after kill", PipeName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error killing worker '{PipeName}'", PipeName);
        }

        _process.Dispose();
    }
    #endregion

#if !NETSTANDARD2_0
    #region DisposeAsync
    /// <summary>
    ///     Asynchronously releases the unmanaged resources used by the <see cref="WorkerHandle"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogDebug("Disposing worker '{PipeName}' asynchronously", PipeName);

        // Try graceful shutdown first
        try
        {
            if (!_process.HasExited)
            {
                await SendShutdownAsync().ConfigureAwait(false);

                // Give the worker a chance to shut down gracefully
                var shutdownDelay = Task.Delay(2000);
                var exitTask = Task.Run(() =>
                {
                    try
                    {
                        _process.WaitForExit(2000);
                    }
                    catch { /* ignored */ }
                });

                await Task.WhenAny(exitTask, shutdownDelay).ConfigureAwait(false);

                if (!_process.HasExited)
                {
                    _logger.LogWarning("Worker '{PipeName}' did not respond to shutdown request within 2 seconds", PipeName);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error sending shutdown to worker '{PipeName}'", PipeName);
        }

        // Dispose managed resources
        _sendLock?.Dispose();

        await _writer.DisposeAsync().ConfigureAwait(false);
        _reader?.Dispose();
        await _pipe.DisposeAsync().ConfigureAwait(false);

        // Forcefully kill if still running
        try
        {
            if (!_process.HasExited)
            {
                _logger.LogDebug("Forcefully killing worker '{PipeName}'", PipeName);
                _process.Kill(true);

                await Task.Run(() =>
                {
                    try
                    {
                        _process.WaitForExit(1000);
                    }
                    catch { /* ignored */ }
                }).ConfigureAwait(false);

                if (!_process.HasExited)
                {
                    _logger.LogWarning("Worker '{PipeName}' did not exit within 1 second after kill", PipeName);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Error killing worker '{PipeName}'", PipeName);
        }

        _process.Dispose();

        GC.SuppressFinalize(this);
    }
    #endregion
#endif
}