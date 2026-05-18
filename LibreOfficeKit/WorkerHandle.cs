//
// LoDocument.cs
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

using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.IO.Pipes;

namespace LibreOfficeKit;

/// <summary>
///     Represents a handle to a single LibreOffice worker process,
///     encapsulating its OS process, named pipe, and communication channels.
/// </summary>
internal sealed class WorkerHandle
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

    /// <summary>Indicates whether the worker has been forcefully killed.</summary>
    private bool _killed;

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
    public bool IsAlive => !_killed && !_process.HasExited;

    /// <summary>
    ///     Gets the duration the worker has been idle. Returns <see cref="TimeSpan.Zero" /> if busy.
    /// </summary>
    public TimeSpan IdleDuration => _isBusy ? TimeSpan.Zero : DateTime.UtcNow - _idleSince;
    #endregion

    #region WorkerHandle
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
        _reader = new StreamReader(pipe, System.Text.Encoding.UTF8, false, 4096, leaveOpen: true);
        _writer = new StreamWriter(pipe, System.Text.Encoding.UTF8, 4096, leaveOpen: true) { AutoFlush = true };
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
    /// <exception cref="TimeoutException">Thrown when the worker does not respond in time.</exception>
    public async Task<T> SendRequestAsync<T>(WorkerRequest request, TimeSpan? timeout = null)
        where T : WorkerResponse
    {
        timeout ??= TimeSpan.FromMinutes(5);

        await _sendLock.WaitAsync();
        try
        {
            _logger.LogDebug("Sending {RequestType} to worker '{PipeName}'", request.GetType().Name, PipeName);
            var json = IpcSerializer.Serialize(request);
            await _writer.WriteLineAsync(json);

            var response = await ReadResponseAsync(timeout.Value);

            if (response is T typed)
            {
                _logger.LogDebug("Received {ResponseType} from worker '{PipeName}'", typeof(T).Name, PipeName);
                return typed;
            }

            if (response is ErrorResponse err)
            {
                _logger.LogError("Worker '{PipeName}' returned error: {Error}", PipeName, err.Message);
                throw new InvalidOperationException($"Worker error: {err.Message}");
            }

            _logger.LogError("Unexpected response type '{ResponseType}' from worker '{PipeName}'", response?.GetType().Name ?? "null", PipeName);
            throw new InvalidOperationException(
                $"Unexpected response type: {response?.GetType().Name ?? "null"}");
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
    /// </summary>
    /// <param name="timeout">Maximum time to wait for a response.</param>
    /// <returns>The deserialized response, or <c>null</c> if the pipe was closed.</returns>
    /// <exception cref="TimeoutException">Thrown when the worker does not respond in time.</exception>
    public async Task<WorkerResponse?> ReadResponseAsync(TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        try
        {
#if NETSTANDARD2_0
            var readTask = _reader.ReadLineAsync();
            var delayTask = Task.Delay(timeout, cts.Token);
            if (await Task.WhenAny(readTask, delayTask) == delayTask)
                throw new TimeoutException("Worker did not respond in time.");
            var line = await readTask;
#else
            var line = await _reader.ReadLineAsync(cts.Token);
#endif
            if (line == null) return null;
            return IpcSerializer.Deserialize<WorkerResponse>(line);
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException("Worker did not respond in time.");
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
        await _sendLock.WaitAsync();
        try
        {
            var json = IpcSerializer.Serialize<WorkerRequest>(new PingRequest());
            await _writer.WriteLineAsync(json);

            var response = await ReadResponseAsync(timeout);
            return response is PongResponse;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ping to worker '{PipeName}' failed", PipeName);
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
            await _writer.WriteLineAsync(json);
        }
        catch
        {
            // ignored
        }
    }
    #endregion

    #region Kill
    /// <summary>
    ///     Forcefully kills the worker process and releases all associated resources.
    /// </summary>
    public void Kill()
    {
        if (_killed) return;
        _killed = true;

        _logger.LogDebug("Killing worker '{PipeName}'", PipeName);

        try
        {
            _reader.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            _writer.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            _pipe.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            _sendLock.Dispose();
        }
        catch
        {
            // ignored
        }

        try
        {
            if (!_process.HasExited)
            {
#if NETSTANDARD2_0
                _process.Kill();
#else
                _process.Kill(true);
#endif
                _process.WaitForExit(3000);
            }
        }
        catch
        {
            // ignored
        }

        try
        {
            _process.Dispose();
        }
        catch
        {
            // ignored
        }
    }
    #endregion
}