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

using LibreOfficeKit;

// ── Worker mode: launched by the Converter class ──────────────────────────────
if (args is ["--worker", _, ..])
{
    var pipeName = args[1];
    return await WorkerProcess.RunAsync(pipeName);
}

// ── Normal mode: demonstrate the Converter ────────────────────────────────────
Console.WriteLine("=== LibreOffice Document to PDF Converter ===");
Console.WriteLine("  Multi-process pool architecture with hot standby");
Console.WriteLine();

// Example: convert using the legacy single-instance approach
if (args is ["--direct", _, ..]) return RunDirectConversion(args[1], args.Length > 2 ? args[2] : null);

// Example: convert using the Converter pool
if (args.Length >= 1 && args[0] != "--worker")
{
    var inputFile = args[0];
    var outputFile = args.Length > 1
        ? args[1]
        : Path.ChangeExtension(inputFile, ".pdf");

    return await RunPoolConversionAsync(inputFile, outputFile);
}

// No arguments — show usage
Console.WriteLine("Usage:");
Console.WriteLine("  LibreOfficeKit.Console <input> [output]            Convert using worker pool");
Console.WriteLine("  LibreOfficeKit.Console --direct <input> [output]   Convert directly (single process)");
Console.WriteLine("  LibreOfficeKit.Console --worker <pipeName>         (internal: worker mode)");
return 0;

// ═══════════════════════════════════════════════════════════════════════════════
// Pool-based conversion (recommended)
// ═══════════════════════════════════════════════════════════════════════════════
static async Task<int> RunPoolConversionAsync(string inputFile, string outputFile)
{
    if (!File.Exists(inputFile))
    {
        Console.Error.WriteLine($"ERROR: Input file not found: {inputFile}");
        return 1;
    }

    Console.WriteLine($"  Input:  {inputFile}");
    Console.WriteLine($"  Output: {outputFile}");
    Console.WriteLine();

    try
    {
        await using var converter = new Converter(
            4,
            2,
            TimeSpan.FromMinutes(5));

        Console.Write("Converting to PDF (via worker pool)... ");

        await converter.ConvertToPdfAsync(inputFile, outputFile);

        Console.WriteLine("OK");

        if (File.Exists(outputFile))
        {
            var fileInfo = new FileInfo(outputFile);
            Console.WriteLine("\n  PDF created successfully!");
            Console.WriteLine($"  Output: {outputFile}");
            Console.WriteLine($"  Size:   {fileInfo.Length:N0} bytes");
        }

        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"FAILED\n  Error: {ex.Message}");
        return 1;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// Direct single-instance conversion (legacy, kept for reference)
// ═══════════════════════════════════════════════════════════════════════════════
static int RunDirectConversion(string inputFile, string? outputFile)
{
    outputFile ??= Path.ChangeExtension(inputFile, ".pdf");

    try
    {
        Console.Write("Searching for LibreOffice installation... ");
        var installPath = LibreOfficeInstance.FindInstallPath();
        if (installPath == null)
        {
            Console.Error.WriteLine("FAILED — LibreOffice not found.");
            return 1;
        }

        Console.WriteLine($"OK ({installPath})");

        if (!File.Exists(inputFile))
        {
            Console.Error.WriteLine($"ERROR: Input file not found: {inputFile}");
            return 1;
        }

        var inputUrl = LibreOfficeInstance.PathToFileUrl(inputFile);
        var outputUrl = LibreOfficeInstance.PathToFileUrl(outputFile);

        Console.Write("Initializing LibreOffice... ");
        using var office = LibreOfficeInstance.Create(installPath);
        Console.WriteLine("OK");

        Console.Write("Loading document... ");
        using var document = office.DocumentLoad(inputUrl);
        Console.WriteLine("OK");

        Console.Write("Converting to PDF... ");
        var success = document.SaveAs(outputUrl, "pdf");

        if (success)
        {
            Console.WriteLine("OK");
            if (!File.Exists(outputFile)) return 0;
            var fi = new FileInfo(outputFile);
            Console.WriteLine($"\n  Output: {outputFile} ({fi.Length:N0} bytes)");

            return 0;
        }

        Console.Error.WriteLine("FAILED");
        var err = office.GetError();
        if (err != null) Console.Error.WriteLine($"  Error: {err}");
        return 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\nERROR: {ex.Message}");
        return 1;
    }
}