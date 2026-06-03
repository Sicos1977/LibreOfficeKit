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

using System.CommandLine;
using LibreOfficeKit;
using LibreOfficeKit.Logging;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace LibreOfficeKitWorker;

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

        // 1. Define Options and Arguments using 2.0.8 canonical syntax
        var workerOption = new Option<bool>("--worker") { Description = "Start the application as a worker process." };

        var pipeNameOption = new Option<string>("--pipename", "-p") { Description = "The name of the pipe for the worker process." };

        var logLevelOption = new Option<LogLevel>("--loglevel", "-l")
        {
            Description = "The log level for the worker process.",
            DefaultValueFactory = _ => LogLevel.Information
        };

        var installPathOption = new Option<string>("--installpath", "-i") { Description = "The custom installation path for LibreOffice." };
        var inputFileArgument = new Argument<FileInfo>("input") { Description = "The input file to convert." };
        var outputFileArgument = new Argument<FileInfo>("output") { Description = "The optional output file.", Arity = ArgumentArity.ZeroOrOne };

        var rootCommand = new RootCommand("LIBREOFFICE DOCUMENT TO PDF CONVERTER");
        rootCommand.Options.Add(workerOption);
        rootCommand.Options.Add(pipeNameOption);
        rootCommand.Options.Add(logLevelOption);
        rootCommand.Options.Add(installPathOption);
        rootCommand.Arguments.Add(inputFileArgument);
        rootCommand.Arguments.Add(outputFileArgument);
        rootCommand.SetAction(async parseResult =>
        {
            var isWorker = parseResult.GetValue(workerOption);
            var pipeName = parseResult.GetValue(pipeNameOption);
            var logLevel = parseResult.GetValue(logLevelOption);
            var installPath = parseResult.GetValue(installPathOption);
            var inputFile = parseResult.GetValue(inputFileArgument);
            var outputFile = parseResult.GetValue(outputFileArgument);

            if (isWorker)
            {
                if (string.IsNullOrWhiteSpace(pipeName))
                {
                    await Console.Error.WriteLineAsync("Error: '--pipename' is required when '--worker' is specified.");
                    return 1;
                }

                try
                {
                    return await WorkerProcess.RunAsync(pipeName, logLevel).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    await Console.Error.WriteLineAsync($"Worker process error: '{exception.Message}'");
                    return 1;
                }
            }

            ShowHeader();

            if (inputFile == null)
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("  LibreOfficeKit.Console <input> [output] [--installpath <path>]");
                Console.WriteLine("  LibreOfficeKit.Console --worker --pipename <pipeName> [--loglevel <Trace|Debug|...>] [--installpath <path>]");
                return 0;
            }

            if (!inputFile.Exists)
            {
                await Console.Error.WriteLineAsync($"Error: Input file '{inputFile.FullName}' does not exist.");
                return 1;
            }

            var inputPath = inputFile.FullName;
            var outputPath = outputFile?.FullName ?? Path.ChangeExtension(inputPath, ".pdf");

            return RunDirectConversion(inputPath, outputPath, installPath);
        });

        return await rootCommand.Parse(args).InvokeAsync();
    }
    #endregion

    private static void ShowHeader()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = $"Version {version?.ToString(3) ?? "1.0.1"}";

        const string title = "LIBREOFFICE DOCUMENT TO PDF CONVERTER";
        var paddedTitle = title.PadLeft((80 + title.Length) / 2).PadRight(80);
        var paddedVersion = versionText.PadLeft((80 + versionText.Length) / 2).PadRight(80);

        Console.WriteLine("================================================================================");
        Console.WriteLine(paddedTitle);
        Console.WriteLine(paddedVersion);
        Console.WriteLine("================================================================================");
        Console.WriteLine(" Developed by : Kees van Spelde ");
        Console.WriteLine(" Source code : https://github.com ");
        Console.WriteLine("================================================================================");
        Console.WriteLine();
    }
    
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
    /// <param name="installPath">Optional custom installation path for LibreOffice.</param>
    /// <returns>0 if the conversion succeeds; otherwise, 1.</returns>
    private static int RunDirectConversion(string inputFile, string? outputFile, string? installPath = null)
    {
        using var logger = new ConsoleLogger(minLevel: LogLevel.Debug);

        try
        {
            logger.LogInformation("Searching for LibreOffice installation...");

            if (string.IsNullOrWhiteSpace(installPath))
                installPath = Instance.FindInstallPath();

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