//
// WorkerProcess.cs
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

using LibreOfficeKit.Enums;
using LibreOfficeKit.Logging;
using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;
using System.IO.Pipes;

// ReSharper disable UnusedMember.Global

namespace LibreOfficeKit;

/// <summary>
///     Runs the worker loop: connects to the host pipe, initializes LibreOfficeKit,
///     and processes conversion requests until shutdown or pipe disconnect.
/// </summary>
internal static class WorkerProcess
{
    #region RunAsync
    /// <summary>
    ///     Main entry point for worker mode. Connects to the named pipe, initializes
    ///     LibreOfficeKit, and enters the request-response loop.
    /// </summary>
    /// <param name="pipeName">The named pipe to connect to (created by <see cref="Converter" />).</param>
    /// <param name="logLevel">The minimum log level for the worker process.</param>
    /// <param name="installPath">The custom installation path for LibreOffice.</param>
    /// <returns>Exit code: 0 = clean shutdown, 1 = error.</returns>
    public static async Task<int> RunAsync(string pipeName, LogLevel logLevel = LogLevel.None, string? installPath = null)
    {
        var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await pipeClient.ConnectAsync(5000).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return 1;
        }

        var reader = new StreamReader(pipeClient, System.Text.Encoding.UTF8, false, 4096, true);
        var writer = new StreamWriter(pipeClient, System.Text.Encoding.UTF8, 4096, true) { AutoFlush = true };
        
        ILogger logger = new PipeLogger("WorkerProcess", writer, logLevel);

        Instance? office;
        
        try
        {
            logger.LogInformation("Initializing LibreOffice instance");
            if (string.IsNullOrWhiteSpace(installPath))
                installPath = Instance.FindInstallPath();

            if (installPath == null)
            {
                logger.LogError("LibreOffice installation not found");
                await SendAsync(writer, new ErrorResponse("LibreOffice installation not found."), 0).ConfigureAwait(false);
                return 1;
            }

            logger.LogDebug("Found LibreOffice at '{InstallPath}'", installPath);
            office = Instance.Create(installPath, logger);
        }
        catch (Exception exception)
        {
            await SendAsync(writer, new ErrorResponse(exception.Message), 0).ConfigureAwait(false);
            return 1;
        }

        try
        {
            logger.LogInformation("Waiting for requests...");

            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var request = IpcSerializer.Deserialize<WorkerRequest>(line);

                switch (request)
                {
                    case PingRequest:
                        await SendAsync(writer, new PongResponse(), request.Id).ConfigureAwait(false);
                        break;

                    case ConvertRequest convert:
                        await HandleConvertAsync(office, convert, writer).ConfigureAwait(false);
                        break;

                    case ReadyRequest:
                        await SendAsync(writer, new ReadyResponse(), request.Id).ConfigureAwait(false);
                        break;

                    case ShutdownRequest:
                        await SendAsync(writer, new ShutdownResponse(), request.Id).ConfigureAwait(false);
                        return 0; // Exit loop

                    default:
                        throw new Exception($"Received unknown request type: '{request?.GetType().Name}'");
                }
            }
        }
        finally
        {
            office.Dispose();
#if NETSTANDARD2_0
            pipeClient.Flush();
            pipeClient.Dispose();
            writer.Dispose();
#else
            await pipeClient.FlushAsync().ConfigureAwait(false);
            await pipeClient.DisposeAsync().ConfigureAwait(false);
            await writer.DisposeAsync().ConfigureAwait(false);
#endif
            reader.Dispose();
        }
    }
    #endregion

    #region HandleConvertAsync
    /// <summary>
    ///     Handles a single conversion request by loading the document and saving it as PDF.
    /// </summary>
    /// <param name="office">The active LibreOffice instance.</param>
    /// <param name="request">The conversion request containing input/output paths and optional filter options.</param>
    /// <param name="writer">The stream writer for sending the response.</param>
    private static async Task HandleConvertAsync(Instance office, ConvertRequest request, StreamWriter writer)
    {
        try
        {
            using var document = office.DocumentLoad(request.InputFile);
            var success = document.SaveAs(request.OutputFile, SaveFormat.Pdf, request.FilterOptions);

            if (success)
                await SendAsync(writer, new ConvertResponse(true), request.Id).ConfigureAwait(false);
            else
            {
                var error = office.GetError();
                await SendAsync(writer, new ConvertResponse(false, error ?? "SaveAs returned failure."), request.Id).ConfigureAwait(false);
            }
        }
        catch (Exceptions.FilePasswordProtectedException exception)
        {
            await SendAsync(writer, new ConvertResponse(false, exception.Message, nameof(Exceptions.FilePasswordProtectedException)), request.Id).ConfigureAwait(false);
        }
        catch (Exceptions.FileTypeNotSupportedException exception)
        {
            await SendAsync(writer, new ConvertResponse(false, exception.Message, nameof(Exceptions.FileTypeNotSupportedException)), request.Id).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await SendAsync(writer, new ConvertResponse(false, exception.Message), request.Id).ConfigureAwait(false);
        }
    }
    #endregion

    #region SendAsync
    /// <summary>
    ///     Serializes and sends an IPC response message over the pipe.
    /// </summary>
    /// <param name="writer">The stream writer for the named pipe.</param>
    /// <param name="response">The response message to send.</param>
    /// <param name="requestId">The request ID associated with the response.</param>
    private static async Task SendAsync(StreamWriter writer, WorkerResponse response, int requestId)
    {
        response.Id = requestId;
        var json = IpcSerializer.Serialize(response);
        await writer.WriteLineAsync(json).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }
    #endregion
}