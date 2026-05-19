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
        if (args is ["--worker", _, ..])
            return await WorkerProcess.RunAsync(args[1]);

        System.Console.WriteLine("=== LibreOffice Document to PDF Converter ===");
        System.Console.WriteLine("  Multi-process pool architecture with hot standby");
        System.Console.WriteLine();

        if (args is ["--direct", _, ..])
            return RunDirectConversion(args[1], args.Length > 2 ? args[2] : null);

        if (args.Length >= 1)
        {
            var inputFile = args[0];
            var outputFile = args.Length > 1
                ? args[1]
                : Path.ChangeExtension(inputFile, ".pdf");

            return await RunPoolConversionAsync(inputFile, outputFile);
        }

        // No arguments — show usage
        System.Console.WriteLine("Usage:");
        System.Console.WriteLine("  LibreOfficeKit.Console <input> [output]            Convert using worker pool");
        System.Console.WriteLine("  LibreOfficeKit.Console --direct <input> [output]   Convert directly (single process)");
        System.Console.WriteLine("  LibreOfficeKit.Console --worker <pipeName>         (internal: worker mode)");
        return 0;
    }
    #endregion

    #region RunPoolConversionAsync
    /// <summary>
    ///    Runs the document conversion using the Converter class, which manages a pool of worker processes. This method initializes the converter,
    ///    performs the conversion, and handles any errors that may occur during the process.
    /// </summary>
    /// <param name="inputFile">The path to the input document file.</param>
    /// <param name="outputFile">The path to the output PDF file.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the process exit code: 0 for success, or a non-zero value if an error occurs.</returns>
    private static async Task<int> RunPoolConversionAsync(string inputFile, string outputFile)
    {
        if (!File.Exists(inputFile))
        {
            await System.Console.Error.WriteLineAsync($"ERROR: Input file not found: '{inputFile}'");
            return 1;
        }

        System.Console.WriteLine($"  Input:  '{inputFile}'");
        System.Console.WriteLine($"  Output: '{outputFile}'");
        System.Console.WriteLine();

        try
        {
            await using var converter = new Converter(4, 2, TimeSpan.FromMinutes(5));

            System.Console.Write("Converting to PDF (via worker pool)... ");

            await converter.ConvertToPdfAsync(inputFile, outputFile);

            System.Console.WriteLine("OK");

            if (!File.Exists(outputFile)) return 0;
            var fileInfo = new FileInfo(outputFile);
            System.Console.WriteLine("\n  PDF created successfully!");
            System.Console.WriteLine($"  Output: '{outputFile}'");
            System.Console.WriteLine($"  Size:   {fileInfo.Length:N0} bytes");

            return 0;
        }
        catch (Exception ex)
        {
            await System.Console.Error.WriteLineAsync($"FAILED\n  Error: '{ex.Message}'");
            return 1;
        }
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
        outputFile ??= Path.ChangeExtension(inputFile, ".pdf");

        try
        {
            System.Console.Write("Searching for LibreOffice installation... ");
            var installPath = LibreOfficeInstance.FindInstallPath();
            if (installPath == null)
            {
                System.Console.Error.WriteLine("FAILED — LibreOffice not found.");
                return 1;
            }

            System.Console.WriteLine($"OK '{installPath}'");

            if (!File.Exists(inputFile))
            {
                System.Console.Error.WriteLine($"ERROR: Input file not found: '{inputFile}'");
                return 1;
            }

            var inputUrl = LibreOfficeInstance.PathToFileUrl(inputFile);
            var outputUrl = LibreOfficeInstance.PathToFileUrl(outputFile);

            System.Console.Write("Initializing LibreOffice... ");
            using var office = LibreOfficeInstance.Create(installPath);
            System.Console.WriteLine("OK");

            System.Console.Write("Loading document... ");
            using var document = office.DocumentLoad(inputUrl);
            System.Console.WriteLine("OK");

            System.Console.Write("Converting to PDF... ");
            var success = document.SaveAs(outputUrl, "pdf");

            if (success)
            {
                System.Console.WriteLine("OK");
                if (!File.Exists(outputFile)) return 0;
                var fileInfo = new FileInfo(outputFile);
                System.Console.WriteLine($"\n  Output: '{outputFile}' ({fileInfo.Length:N0} bytes)");
                return 0;
            }

            System.Console.Error.WriteLine("FAILED");
            var error = office.GetError();
            if (error != null)
                System.Console.Error.WriteLine($"  Error: '{error}'");
            return 1;
        }
        catch (Exception exception)
        {
            System.Console.Error.WriteLine($"\nERROR: '{exception.Message}'");
            return 1;
        }
    }
    #endregion
}