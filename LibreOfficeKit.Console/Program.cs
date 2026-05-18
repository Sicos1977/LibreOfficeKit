// =============================================================================
// Program.cs
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
if (args.Length >= 2 && args[0] == "--worker")
{
    var pipeName = args[1];
    return await WorkerProcess.RunAsync(pipeName);
}

// ── Normal mode: demonstrate the Converter ────────────────────────────────────
Console.WriteLine("=== LibreOffice Document to PDF Converter ===");
Console.WriteLine("  Multi-process pool architecture with hot standby");
Console.WriteLine();

// Example: convert using the legacy single-instance approach
if (args.Length >= 2 && args[0] == "--direct") return RunDirectConversion(args[1], args.Length > 2 ? args[2] : null);

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
            if (File.Exists(outputFile))
            {
                var fi = new FileInfo(outputFile);
                Console.WriteLine($"\n  Output: {outputFile} ({fi.Length:N0} bytes)");
            }

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