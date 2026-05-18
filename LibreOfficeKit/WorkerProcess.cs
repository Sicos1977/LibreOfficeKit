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

using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.IO.Pipes;

namespace LibreOfficeKit;

/// <summary>
///     Runs the worker loop: connects to the host pipe, initializes LibreOfficeKit,
///     and processes conversion requests until shutdown or pipe disconnect.
/// </summary>
public static class WorkerProcess
{
    #region RunAsync
    /// <summary>
    ///     Main entry point for worker mode. Connects to the named pipe, initializes
    ///     LibreOfficeKit, and enters the request-response loop.
    /// </summary>
    /// <param name="pipeName">The named pipe to connect to (created by <see cref="Converter" />).</param>
    /// <param name="logger">Optional logger. When <c>null</c>, logging is disabled.</param>
    /// <returns>Exit code: 0 = clean shutdown, 1 = error.</returns>
    public static async Task<int> RunAsync(string pipeName, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;

        using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            logger.LogDebug("Connecting to pipe '{PipeName}'", pipeName);
            await pipeClient.ConnectAsync(10_000);
            logger.LogDebug("Connected to pipe '{PipeName}'", pipeName);
        }
        catch (TimeoutException)
        {
            logger.LogError("Timeout connecting to pipe '{PipeName}'", pipeName);
            return 1;
        }

        using var reader = new StreamReader(pipeClient, System.Text.Encoding.UTF8, false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(pipeClient, System.Text.Encoding.UTF8, 4096, leaveOpen: true);
        writer.AutoFlush = true;

        LibreOfficeInstance? office;

        try
        {
            logger.LogInformation("Initializing LibreOffice instance");
            var installPath = LibreOfficeInstance.FindInstallPath();
            if (installPath == null)
            {
                logger.LogError("LibreOffice installation not found");
                await SendAsync(writer, new ErrorResponse("LibreOffice installation not found."));
                return 1;
            }

            logger.LogDebug("Found LibreOffice at '{InstallPath}'", installPath);
            office = LibreOfficeInstance.Create(installPath);
            logger.LogInformation("LibreOffice initialized successfully");
            await SendAsync(writer, new ReadyResponse());
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize LibreOffice");
            await SendAsync(writer, new ErrorResponse($"Init failed: '{exception.Message}'"));
            return 1;
        }

        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync();
                if (line == null)
                    break;

                WorkerRequest? request;
                try
                {
                    request = IpcSerializer.Deserialize<WorkerRequest>(line);
                }
                catch
                {
                    continue;
                }

                if (request == null)
                    continue;

                switch (request)
                {
                    case PingRequest:
                        logger.LogDebug("Received ping, sending pong");
                        await SendAsync(writer, new PongResponse());
                        break;

                    case ConvertRequest convert:
                        logger.LogInformation("Received conversion request: '{InputFile}' -> '{OutputFile}'",
                            convert.InputFile, convert.OutputFile);
                        await HandleConvertAsync(office, convert, writer, logger);
                        break;

                    case ShutdownRequest:
                        logger.LogInformation("Received shutdown request. Exiting.");
                        await SendAsync(writer, new PongResponse());
                        return 0;
                }
            }
        }
        finally
        {
            office.Dispose();
        }

        return 0;
    }
    #endregion

    #region HandleConvertAsync
    /// <summary>
    ///     Handles a single conversion request by loading the document and saving it as PDF.
    /// </summary>
    /// <param name="office">The active LibreOffice instance.</param>
    /// <param name="request">The conversion request containing input/output paths.</param>
    /// <param name="writer">The stream writer for sending the response.</param>
    /// <param name="logger">The logger for logging messages.</param>
    private static async Task HandleConvertAsync(LibreOfficeInstance office, ConvertRequest request, StreamWriter writer, ILogger logger)
    {
        try
        {
            logger.LogDebug("Loading document '{InputFile}'", request.InputFile);
            var inputUrl = LibreOfficeInstance.PathToFileUrl(request.InputFile);
            var outputUrl = LibreOfficeInstance.PathToFileUrl(request.OutputFile);

            using var document = office.DocumentLoad(inputUrl);
            logger.LogDebug("Saving document as PDF to '{OutputFile}'", request.OutputFile);
            var success = document.SaveAs(outputUrl, "pdf");

            if (success)
            {
                logger.LogInformation("Conversion succeeded: '{OutputFile}'", request.OutputFile);
                await SendAsync(writer, new ConvertResponse(true));
            }
            else
            {
                var error = office.GetError();
                logger.LogError("Conversion failed for '{InputFile}': {Error}", request.InputFile, error ?? "SaveAs returned failure.");
                await SendAsync(writer, new ConvertResponse(false, error ?? "SaveAs returned failure."));
            }
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception during conversion of '{InputFile}'", request.InputFile);
            await SendAsync(writer, new ConvertResponse(false, exception.Message));
        }
    }
    #endregion

    #region SendAsync
    /// <summary>
    ///     Serializes and sends an IPC response message over the pipe.
    /// </summary>
    /// <param name="writer">The stream writer for the named pipe.</param>
    /// <param name="response">The response message to send.</param>
    private static async Task SendAsync(StreamWriter writer, WorkerResponse response)
    {
        var json = IpcSerializer.Serialize(response);
        await writer.WriteLineAsync(json);
    }
    #endregion
}