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
using System.IO.Pipes;
using LibreOfficeKit.Enums;
using LibreOfficeKit.Logging;

// ReSharper disable UnusedMember.Global

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
    /// <returns>Exit code: 0 = clean shutdown, 1 = error.</returns>
    public static async Task<int> RunAsync(string pipeName)
    {
#if NETSTANDARD2_0
        using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
#else
        await using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
#endif

        try
        {
            await pipeClient.ConnectAsync(5000).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return 1;
        }

#if NETSTANDARD2_0
        using var reader = new StreamReader(pipeClient, System.Text.Encoding.UTF8, false, 4096, true);
        using var writer = new StreamWriter(pipeClient, System.Text.Encoding.UTF8, 4096, true);
#else
        using var reader = new StreamReader(pipeClient, System.Text.Encoding.UTF8, false, 4096, true);
        await using var writer = new StreamWriter(pipeClient, System.Text.Encoding.UTF8, 4096, true);
#endif
        writer.AutoFlush = true;

        // Now that we have the pipe connected, create the logger
        ILogger logger = new PipeLogger("WorkerProcess", writer);

        Instance? office;

        try
        {
            logger.LogInformation("Initializing LibreOffice instance");
            var installPath = Instance.FindInstallPath();
            if (installPath == null)
            {
                logger.LogError("LibreOffice installation not found");
                await SendAsync(writer, new ErrorResponse("LibreOffice installation not found.")).ConfigureAwait(false);
                return 1;
            }

            logger.LogDebug("Found LibreOffice at '{InstallPath}'", installPath);
            office = Instance.Create(installPath);
            logger.LogInformation("LibreOffice initialized successfully");
            await SendAsync(writer, new ReadyResponse()).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize LibreOffice");
            await SendAsync(writer, new ErrorResponse($"Init failed: '{exception.Message}'")).ConfigureAwait(false);
            return 1;
        }

        try
        {
            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(line))
                    continue;

                logger.LogDebug("Received line: {Line}", line);

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
                        await SendAsync(writer, new PongResponse()).ConfigureAwait(false);
                        break;

                    case ConvertRequest convert:
                        logger.LogInformation("Received conversion request: '{InputFile}' -> '{OutputFile}'", convert.InputFile, convert.OutputFile);
                        await HandleConvertAsync(office, convert, writer, logger).ConfigureAwait(false);
                        break;

                    case ShutdownRequest:
                        logger.LogInformation("Received shutdown request. Exiting.");
                        await SendAsync(writer, new PongResponse()).ConfigureAwait(false);
                        return 0;

                    default:
                        logger.LogWarning("Received unknown request type: {RequestType}", request.GetType().Name);
                        break;
                }
            }
        }
        finally
        {
            office.Dispose();
            pipeClient.Flush();
            pipeClient.WaitForPipeDrain();
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
    /// <param name="logger">The logger for logging messages.</param>
    private static async Task HandleConvertAsync(Instance office, ConvertRequest request, StreamWriter writer, ILogger logger)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            logger.LogDebug("Loading document '{InputFile}'", request.InputFile);
            var inputUrl = Instance.PathToFileUrl(request.InputFile);
            var outputUrl = Instance.PathToFileUrl(request.OutputFile);

            using var document = office.DocumentLoad(inputUrl);
            logger.LogDebug("Document loaded in {ElapsedMs} ms, saving as PDF to '{OutputFile}'", stopwatch.ElapsedMilliseconds, request.OutputFile);

            var saveStart = stopwatch.ElapsedMilliseconds;
            var success = document.SaveAs(outputUrl, SaveFormat.Pdf, request.FilterOptions);
            logger.LogDebug("SaveAs completed in {SaveMs} ms (total: {TotalMs} ms)", stopwatch.ElapsedMilliseconds - saveStart, stopwatch.ElapsedMilliseconds);

            if (success)
            {
                logger.LogInformation("Conversion succeeded: '{OutputFile}' (total time: {TotalMs} ms)", request.OutputFile, stopwatch.ElapsedMilliseconds);
                await SendAsync(writer, new ConvertResponse(true)).ConfigureAwait(false);
            }
            else
            {
                var error = office.GetError();
                logger.LogError("Conversion failed for '{InputFile}': '{Error}' (total time: {TotalMs} ms)", request.InputFile, error ?? "SaveAs returned failure.", stopwatch.ElapsedMilliseconds);
                await SendAsync(writer, new ConvertResponse(false, error ?? "SaveAs returned failure.")).ConfigureAwait(false);
            }
        }
        catch (Exceptions.FilePasswordProtectedException exception)
        {
            logger.LogError(exception, "Document is password-protected: '{InputFile}' (failed after {ElapsedMs} ms)", request.InputFile, stopwatch.ElapsedMilliseconds);
            await SendAsync(writer, new ConvertResponse(false, exception.Message, nameof(Exceptions.FilePasswordProtectedException))).ConfigureAwait(false);
        }
        catch (Exceptions.FileTypeNotSupportedException exception)
        {
            logger.LogError(exception, "File type not supported for '{InputFile}' (failed after {ElapsedMs} ms)", request.InputFile, stopwatch.ElapsedMilliseconds);
            await SendAsync(writer, new ConvertResponse(false, exception.Message, nameof(Exceptions.FileTypeNotSupportedException))).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Exception during conversion of '{InputFile}' (failed after {ElapsedMs} ms)", request.InputFile, stopwatch.ElapsedMilliseconds);
            await SendAsync(writer, new ConvertResponse(false, exception.Message)).ConfigureAwait(false);
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
        await writer.WriteLineAsync(json).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }
    #endregion
}