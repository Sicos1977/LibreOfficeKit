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
internal class WorkerHandle : IAsyncDisposable
{
    #region Fields
    /// <summary>
    ///     The OS process running the worker.
    /// </summary>
    private readonly Process _process;

    /// <summary>
    ///     The named pipe server stream for IPC.
    /// </summary>
    private readonly NamedPipeServerStream _pipe;

    /// <summary>
    ///     Stream reader for receiving messages from the worker.
    /// </summary>
    private readonly StreamReader _reader;

    /// <summary>
    ///     Stream writer for sending messages to the worker.
    /// </summary>
    private readonly StreamWriter _writer;

    /// <summary>
    ///     The current message id
    /// </summary>
    private int _messageId;

    /// <summary>
    ///     Dictionary tracking pending requests by their RequestId.
    /// </summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, PendingRequest> _pendingRequests = new();

    /// <summary>
    ///     Background task that continuously reads responses from the pipe.
    /// </summary>
    private readonly Task _responseReaderTask;

    /// <summary>
    ///     Background task that monitors pending request timeouts.
    /// </summary>
    private readonly Task _timeoutMonitorTask;

    /// <summary>
    ///     Cancellation token source for stopping the response reader task.
    /// </summary>
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    /// <summary>
    ///     Indicates whether the worker is currently processing a request.
    /// </summary>
    private volatile bool _isBusy;

    /// <summary>
    ///     The timestamp when the worker last became idle.
    /// </summary>
    private DateTime _idleSince;

    /// <summary>
    ///     Indicates whether the worker has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Logger for this worker handle.
    /// </summary>
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

    #region Private Class PendingRequest
    /// <summary>
    ///     Represents a pending request awaiting a response from the worker.
    /// </summary>
    private class PendingRequest
    {
        /// <summary>
        ///     Gets the TaskCompletionSource that will be completed when the response arrives.
        /// </summary>
        public TaskCompletionSource<WorkerResponse> TaskCompletionSource { get; }

        /// <summary>
        ///     Gets the deadline after which the request should timeout.
        /// </summary>
        public DateTime Deadline { get; }

        /// <summary>
        ///     Gets the type of request (for logging).
        /// </summary>
        public string RequestType { get; }

        /// <summary>
        ///     Initializes a new instance of <see cref="PendingRequest"/>.
        /// </summary>
        /// <param name="requestType">The type of request.</param>
        /// <param name="timeout">The timeout duration.</param>
        public PendingRequest(string requestType, TimeSpan timeout)
        {
            TaskCompletionSource = new TaskCompletionSource<WorkerResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            Deadline = DateTime.UtcNow.Add(timeout);
            RequestType = requestType;
        }
    }
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

        _responseReaderTask = Task.Run(() => ResponseReaderLoopAsync(_cancellationTokenSource.Token));
        _timeoutMonitorTask = Task.Run(() => TimeoutMonitorLoopAsync(_cancellationTokenSource.Token));
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
    ///     Sends a request to the worker and awaits the typed response via callback.
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
    private async Task<T> SendRequestAsync<T>(WorkerRequest request, TimeSpan timeout) where T : WorkerResponse
    {
        _messageId += 1;
        request.Id = _messageId;

        var pending = new PendingRequest(request.GetType().Name, timeout);

        if (!_pendingRequests.TryAdd(request.Id, pending))
        {
            throw new InvalidOperationException($"Duplicate request id '{request.Id}'");
        }

        try
        {
            _logger.LogTrace("Sending '{RequestType}' to worker '{PipeName}' with RequestId '{RequestId}'", request.GetType().Name, PipeName, request.Id);
            var json = IpcSerializer.Serialize(request);
            await _writer.WriteLineAsync(json).ConfigureAwait(false);
            await _writer.FlushAsync().ConfigureAwait(false);

            // Wait for the response via callback (TaskCompletionSource)
            using var cancellationTokenSource = new CancellationTokenSource(timeout);
#if NETSTANDARD2_0
            using var registration = cancellationTokenSource.Token.Register(() =>
#else
            await using var registration = cancellationTokenSource.Token.Register(() =>
#endif
            {
                if (_pendingRequests.TryRemove(request.Id, out var timedOutPending))
                    timedOutPending.TaskCompletionSource.TrySetException(new TimeoutException($"Worker did not respond to '{timedOutPending.RequestType}' within {timeout}"));
            });

            var response = await pending.TaskCompletionSource.Task.ConfigureAwait(false);

            switch (response)
            {
                case T typed:
                    _logger.LogTrace("Received '{ResponseType}' from worker '{PipeName}' for RequestId '{RequestId}'", typeof(T).Name, PipeName, request.Id);
                    return typed;

                case ErrorResponse errorResponse:
                    _logger.LogError("Worker '{PipeName}' returned error for RequestId '{RequestId}': '{Error}'", PipeName, request.Id, errorResponse.Message);
                    throw new ConversionFailedException($"Worker error: {errorResponse.Message}");

                default:
                    _logger.LogError("Unexpected response type '{ResponseType}' from worker '{PipeName}' for RequestId '{RequestId}'", response.GetType().Name, PipeName, request.Id);
                    throw new ConversionFailedException($"Unexpected response type: {response.GetType().Name}");
            }
        }
        finally
        {
            _pendingRequests.TryRemove(request.Id, out _);
        }
    }
    #endregion

    #region ResponseReaderLoopAsync
    /// <summary>
    ///     Background task that continuously reads responses from the pipe and completes callbacks.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the reader.</param>
    private async Task ResponseReaderLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                try
                {
#if NETSTANDARD2_0
                    var line = await _reader.ReadLineAsync().ConfigureAwait(false);
#else
                    var line = await _reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
#endif

                    if (line == null)
                    {
                        _logger.LogDebug("[Worker '{PipeName}'] Pipe closed", PipeName);
                        break; // Pipe closed
                    }

                    _logger.LogTrace("[Worker '{PipeName}'] Read line: '{Line}'", PipeName, line);

                    var response = IpcSerializer.Deserialize<WorkerResponse>(line);

                    switch (response)
                    {
                        case null:
                            continue;

                        case LogResponse logResponse:
                            ProcessLogResponse(logResponse);
                            break;

                        case ErrorResponse errorResponse:
                            _logger.LogError("Worker '{PipeName}' sent error response: '{Error}'", PipeName, errorResponse.Message);
                            break;

                        default:
                            // Try to match response to pending request
                            if (response.Id.HasValue && _pendingRequests.TryRemove(response.Id.Value, out var pending))
                            {
                                _logger.LogTrace("[Worker '{PipeName}'] Matched response '{ResponseType}' to RequestId '{RequestId}'", PipeName, response.GetType().Name, response.Id);
                                pending.TaskCompletionSource.TrySetResult(response);
                            }
                            else
                                _logger.LogWarning("[Worker '{PipeName}'] Received response '{ResponseType}' with no matching pending request (id: '{RequestId}')", PipeName, response.GetType().Name, response.Id?.ToString() ?? "null");

                            break;
                    }
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(exception, "[Worker '{PipeName}'] Error reading from pipe", PipeName);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        finally
        {
            // Fail all pending requests when pipe closes
            foreach (var pending in _pendingRequests.Values)
                pending.TaskCompletionSource.TrySetException(new InvalidOperationException("Worker pipe closed before response was received"));

            _pendingRequests.Clear();

            _logger.LogDebug("[Worker '{PipeName}'] Response reader task ended", PipeName);
        }
    }
    #endregion

    #region TimeoutMonitorLoopAsync
    /// <summary>
    ///     Background task that monitors pending requests and fails them when they timeout.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token to stop the monitor.</param>
    private async Task TimeoutMonitorLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && !_disposed)
            {
                var now = DateTime.UtcNow;
                var timedOut = _pendingRequests.Where(kvp => kvp.Value.Deadline <= now).ToList();

                // Fail timed-out requests
                foreach (var kvp in timedOut)
                {
                    if (!_pendingRequests.TryRemove(kvp.Key, out var pending)) continue;
                    _logger.LogWarning("[Worker '{PipeName}'] Request '{RequestType}' timed out (RequestId: '{RequestId}')", PipeName, pending.RequestType, kvp.Key);
                    pending.TaskCompletionSource.TrySetException(new TimeoutException($"Worker did not respond to '{pending.RequestType}' within the specified timeout"));
                }

                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        finally
        {
            _logger.LogDebug("[Worker '{PipeName}'] Timeout monitor task ended", PipeName);
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

    #region ReadyAsync
    /// <summary>
    ///     Sends a ready request to the worker and waits for a ready response.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the ready response.</param>
    /// <returns><c>true</c> if the worker responded with a ready response; otherwise <c>false</c>.</returns>
    public async Task<bool> ReadyAsync(TimeSpan timeout)
    {
        try
        {
            await SendRequestAsync<ReadyResponse>(new ReadyRequest(), timeout).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Ready request to worker '{PipeName}' failed", PipeName);
            return false;
        }
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
        try
        {
            await SendRequestAsync<PongResponse>(new PingRequest(), timeout).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Ping to worker '{PipeName}' failed", PipeName);
            return false;
        }
    }
    #endregion

    #region ConvertAsync
    /// <summary>
    ///     Sends a request to convert a document to PDF.
    /// </summary>
    /// <param name="request">The conversion request.</param>
    /// <param name="timeout">Maximum time to wait for the conversion response.</param>
    /// <returns>The conversion response.</returns>
    public async Task<ConvertResponse> ConvertAsync(ConvertRequest request, TimeSpan timeout)
    {
        try
        {
            var response = await SendRequestAsync<ConvertResponse>(request, timeout).ConfigureAwait(false);
            return response;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Convert to PDF request to worker '{PipeName}' failed", PipeName);
            return new ConvertResponse(false, exception.Message);
        }
    }
    #endregion

    #region ShutdownAsync
    /// <summary>
    ///     Sends a graceful shutdown request to the worker.
    /// </summary>
    /// <param name="timeout">Maximum time to wait for the shutdown response.</param>
    public async Task<bool> ShutdownAsync(TimeSpan timeout)
    {
        try
        {
            await SendRequestAsync<ShutdownResponse>(new ShutdownRequest(), timeout).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Shutdown request to worker '{PipeName}' failed", PipeName);
            return false;
        }
    }
    #endregion

    #region KillProcess
    /// <summary>
    ///     Kills the given process, ignoring any exceptions that occur during killing. Used for cleanup of worker processes.
    /// </summary>
    /// <param name="process">The process to kill.</param>
    private void KillProcess(Process? process)
    {
        if (process == null || process.HasExited) return;

        _logger.LogDebug("Forcefully killing worker '{PipeName}'", PipeName);

        try
        {
#if NETSTANDARD2_0
            process.Kill();
#else
            process.Kill(true);
#endif
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Error killing worker '{PipeName}'", PipeName);
        }

        _process.Dispose();

    }
    #endregion

    #region DisposeAsync
    /// <summary>
    ///     Asynchronously releases the unmanaged resources used by the <see cref="WorkerHandle"/>.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        _logger.LogDebug("Disposing worker '{PipeName}'", PipeName);

        _pendingRequests.Clear();


        // Try graceful shutdown first
        try
        {
            if (!_process.HasExited)
            {
                var shutdown = await ShutdownAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                if (!shutdown && !_process.HasExited)
                {
                    _logger.LogWarning("Worker '{PipeName}' did not respond to shutdown request, killing it", PipeName);
                    KillProcess(_process);
                }
            }
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Error sending shutdown to worker '{PipeName}'", PipeName);
        }

        // Stop background tasks
        try
        {
#if NETSTANDARD2_0
            _cancellationTokenSource.Cancel();
#else
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
#endif
            var allTasks = Task.WhenAll(_responseReaderTask, _timeoutMonitorTask);
            await Task.WhenAny(allTasks, Task.Delay(2000)).ConfigureAwait(false);
            if (!allTasks.IsCompleted)
            {
                _logger.LogWarning("Background tasks for worker '{PipeName}' did not complete within 2 seconds", PipeName);
            }
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, "Error stopping background tasks for worker '{PipeName}'", PipeName);
        }


        // Dispose managed resources
        _cancellationTokenSource.Dispose();
        _reader.Dispose();

#if NETSTANDARD2_0
        _writer.Dispose();
        _pipe.Dispose();
#else
        await _writer.DisposeAsync().ConfigureAwait(false);
        await _pipe.DisposeAsync().ConfigureAwait(false);
#endif

        GC.SuppressFinalize(this);

        _logger.LogDebug("Worker disposed");
    }
    #endregion
}