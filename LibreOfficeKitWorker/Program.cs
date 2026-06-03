//
// Program.cs
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
// .NET 10 Console Application — Document to PDF Converter using LibreOfficeKit
//
// Supports two modes:
//   1. Normal mode: demonstrates document conversion via the Converter pool
//   2. Worker mode: --worker <pipeName> — runs as a worker process for the pool
//
// The Converter class spawns copies of this executable in worker mode,
// each with its own isolated LibreOfficeKit instance.
// =============================================================================

using Microsoft.Extensions.Logging;

namespace LibreOfficeKit.Console;

/// <summary>
///    Console application entry point. Supports both normal mode (demonstrating the Converter)
/// </summary>
internal static class Program
{
    #region Main
    /// <summary>
    ///     The application entry point for the LibreOffice document to PDF converter. Determines the mode of operation
    ///     based on command-line arguments and initiates the appropriate conversion workflow.
    /// </summary>
    /// <remarks>
    ///     In normal mode, the application converts documents using a worker pool. The '--direct'
    ///     argument performs conversion in a single process, while '--worker' is used internally for worker processes. If
    ///     no arguments are provided, usage instructions are displayed.
    /// </remarks>
    /// <param name="args">The command-line arguments that control the application's behavior. Supported arguments include input and output
    ///     file paths, '--direct' for direct conversion, and '--worker' for internal worker mode.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the process exit code: 0 for
    ///     success, or a non-zero value if an error occurs.
    /// </returns>
    private static async Task<int> Main(string[] args)
    {
        // ReSharper disable StringLiteralTypo
        const string disabledLibs = "abp avmediagst avmediavlc cmdmail losessioninstall OGLTrans PresenterScreen " +
                                    "syssh ucpftp1 ucpgio1 ucphier1 ucpimage updatecheckui updatefeed updchk " +
                                    "dbaxml dbmm dbp dbu deployment firebird_sdbc mork " +
                                    "mysql mysqlc odbc postgresql-sdbc postgresql-sdbc-impl sdbc2 sdbt " +
                                    "javaloader javavm jdbc rpt rptui rptxml ";
        // ReSharper restore StringLiteralTypo

        Environment.SetEnvironmentVariable("UNODISABLELIBRARY", disabledLibs, EnvironmentVariableTarget.Process);

        if (args is ["--worker", _, ..])
        {
            try
            {
                var pipeName = args[1];
                if (args[2] == "--loglevel" && Enum.TryParse<LogLevel>(args[3], true, out var logLevel))
                    return await WorkerProcess.RunAsync(pipeName, logLevel).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                await System.Console.Error.WriteLineAsync($"Invalid arguments specified '{string.Join(" ", args)}', error: '{exception.Message}'").ConfigureAwait(false);
                return 0;
            }

            await System.Console.Error.WriteLineAsync($"Invalid arguments specified '{string.Join(" ", args)}").ConfigureAwait(false);
            return 0;
        }

        System.Console.WriteLine("=== LibreOffice Document to PDF Converter ===");
        System.Console.WriteLine();

        if (args.Length >= 1)
        {
            var inputFile = args[0];
            var outputFile = args.Length > 1
                ? args[1]
                : Path.ChangeExtension(inputFile, ".pdf");

            return RunDirectConversion(inputFile, outputFile);
        }

        // No arguments — show usage
        System.Console.WriteLine("Usage:");
        System.Console.WriteLine("  LibreOfficeKit.Console <input> [output]");
        System.Console.WriteLine("  LibreOfficeKit.Console --worker <pipeName> [--loglevel <Trace|Debug|Information|Warning|Error|Critical|None>]");
        return 0;
    }
    #endregion
    
    #region RunDirectConversion
    /// <summary>
    ///     Converts the specified input file to PDF format using a direct LibreOffice invocation.
    /// </summary>
    /// <remarks>
    ///     This method requires LibreOffice to be installed and accessible on the system. If LibreOffice
    ///     is not found or the input file does not exist, the method returns 1. Any errors encountered during conversion
    ///     are written to the standard error stream.
    /// </remarks>
    /// <param name="inputFile">The path to the input file to be converted. Must refer to an existing file.</param>
    /// <param name="outputFile">The path where the output PDF file will be saved. If null, the output file will have the same name as the input file with a .pdf extension.</param>
    /// <returns>0 if the conversion succeeds; otherwise, 1.</returns>
    private static int RunDirectConversion(string inputFile, string? outputFile)
    {
        using var logger = new Logging.ConsoleLogger(minLevel: LogLevel.Debug);

        try
        {
            logger.LogInformation("Searching for LibreOffice installation...");
            var installPath = Instance.FindInstallPath();
            if (installPath == null)
            {
                logger.LogError("LibreOffice not found");
                return 1;
            }

            logger.LogInformation("Found LibreOffice at '{InstallPath}'", installPath);
            using var office = Instance.Create(installPath, logger);
            using var document = office.DocumentLoad(inputFile);
            outputFile ??= Path.ChangeExtension(inputFile, ".pdf");
            var success = document.SaveAs(outputFile, "pdf");

            if (success)
            {
                logger.LogInformation("Conversion successful");
                var fileInfo = new FileInfo(outputFile);
                logger.LogInformation("Output: '{OutputFile}' ({Size:N0} bytes)", outputFile, fileInfo.Length);
                return 0;
            }

            logger.LogError("Conversion failed");
            var error = office.GetError();
            if (error != null)
                logger.LogError("Error: '{Error}'", error);
            return 1;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Conversion failed with exception");
            return 1;
        }
    }
    #endregion
}