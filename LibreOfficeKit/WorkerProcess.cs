// =============================================================================
// WorkerProcess.cs
//
// Worker mode entry point. When the executable is launched with:
//   --worker <pipeName>
// it connects to the named pipe server (hosted by the Converter) and enters
// a request-response loop using the IPC protocol.
//
// Each worker process has its own LibreOfficeKit instance — this is safe
// because each worker IS a separate OS process.
// =============================================================================

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
    /// <returns>Exit code: 0 = clean shutdown, 1 = error.</returns>
    public static async Task<int> RunAsync(string pipeName)
    {
        await using var pipeClient = new NamedPipeClientStream(
            ".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);

        try
        {
            await pipeClient.ConnectAsync(10_000);
        }
        catch (TimeoutException)
        {
            Console.Error.WriteLine($"[Worker] Timeout connecting to pipe '{pipeName}'.");
            return 1;
        }

        using var reader = new StreamReader(pipeClient, leaveOpen: true);
        using var writer = new StreamWriter(pipeClient, leaveOpen: true) { AutoFlush = true };

        LibreOfficeInstance? office = null;
        try
        {
            var installPath = LibreOfficeInstance.FindInstallPath();
            if (installPath == null)
            {
                await SendAsync(writer, new ErrorResponse { Message = "LibreOffice installation not found." });
                return 1;
            }

            office = LibreOfficeInstance.Create(installPath);
            await SendAsync(writer, new ReadyResponse());
        }
        catch (Exception ex)
        {
            await SendAsync(writer, new ErrorResponse { Message = $"Init failed: {ex.Message}" });
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
                        await SendAsync(writer, new PongResponse());
                        break;

                    case ConvertRequest convert:
                        await HandleConvertAsync(office, convert, writer);
                        break;

                    case ShutdownRequest:
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
    private static async Task HandleConvertAsync(
        LibreOfficeInstance office, ConvertRequest request, StreamWriter writer)
    {
        try
        {
            var inputUrl = LibreOfficeInstance.PathToFileUrl(request.InputPath);
            var outputUrl = LibreOfficeInstance.PathToFileUrl(request.OutputPath);

            using var document = office.DocumentLoad(inputUrl);
            var success = document.SaveAs(outputUrl, "pdf");

            if (success)
            {
                await SendAsync(writer, new ConvertResponse { Success = true });
            }
            else
            {
                var error = office.GetError();
                await SendAsync(writer, new ConvertResponse
                {
                    Success = false,
                    Error = error ?? "SaveAs returned failure."
                });
            }
        }
        catch (Exception ex)
        {
            await SendAsync(writer, new ConvertResponse
            {
                Success = false,
                Error = ex.Message
            });
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