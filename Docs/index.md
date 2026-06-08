# LibreOfficeKit

A .NET library and console application for document-to-PDF conversion using LibreOfficeKit,
featuring a multi-process worker pool for safe concurrent conversions.

[![NuGet](https://img.shields.io/nuget/v/LibreOfficeKit.svg?style=flat-square)](https://www.nuget.org/packages/LibreOfficeKit)

## Quick Start

```csharp
using LibreOfficeKit;

await using var converter = new Converter(
    maxInstances: 4,
    minHotStandby: 2,
    idleTimeout: TimeSpan.FromMinutes(5));

await converter.ConvertToPdfAsync("input.docx", "output.pdf");
```

## Key Features

- **Hot standby**: Pre-spawned workers for immediate conversion
- **On-demand scaling**: Scale up to `maxInstances` as needed
- **Idle timeout**: Excess workers shut down automatically after an idle period
- **Health monitoring**: Background pings detect crashed or hung workers
- **Request queuing**: Requests queue when all workers are busy
- **Conversion timeout**: Optional per-call timeout with `TimeoutException`
- **Stream support**: Convert from/to `Stream` (uses temp files internally)
- **PDF options**: Full control over PDF export quality, compliance, security and layout via `PdfOptions`
- **Enum-based API**: Type-safe save formats and document types
- **Proper disposal**: `IDisposable` and `IAsyncDisposable` for clean shutdown

## Architecture

LibreOfficeKit (LOK) is **not** thread-safe — only one instance per process is allowed. To enable concurrent
conversions, this project uses a **process pool** architecture:

- The host process (`Converter`) manages a pool of isolated worker processes.
- Each worker connects via a **named pipe**, initializes its own LOK instance, and processes one request at a time.
- The host dispatches conversion requests to free workers and queues requests when all workers are busy.
- Workers are health-monitored via periodic pings and recycled if they crash or hang.

## Usage Examples

### With PDF Options

```csharp
using LibreOfficeKit;

var options = new PdfOptions
{
    Quality            = 95,
    ReduceImageResolution = true,
    MaxImageResolutionDpi = 150,
    UseTaggedPdf       = true,
    ExportBookmarks    = true,
    PageRange          = "1-10"
};

await converter.ConvertToPdfAsync("input.docx", "output.pdf", pdfOptions: options);
```

### Using Presets

```csharp
// High quality for archiving
await converter.ConvertToPdfAsync("input.docx", "output.pdf", 
    pdfOptions: PdfOptions.Archive);

// Screen optimized (smaller file)
await converter.ConvertToPdfAsync("input.docx", "output.pdf", 
    pdfOptions: PdfOptions.Screen);

// Print quality
await converter.ConvertToPdfAsync("input.docx", "output.pdf", 
    pdfOptions: PdfOptions.Print);
```

### Stream Conversion

```csharp
await using var input  = File.OpenRead("input.docx");
await using var output = File.Create("output.pdf");

await converter.ConvertToPdfAsync(input, output, pdfOptions: PdfOptions.Screen);
```

### With Timeout

```csharp
try
{
    await converter.ConvertToPdfAsync(
        "input.docx",
        "output.pdf",
        timeout: TimeSpan.FromSeconds(30));
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Conversion timed out: {ex.Message}");
}
```

## Exception Handling

The library provides specific exception types for different failure scenarios:

| Exception | When thrown |
|-----------|-------------|
| `TimeoutException` | Conversion exceeded the specified timeout |
| `FilePasswordProtectedException` | Document is password-protected |
| `FileTypeNotSupportedException` | File type not supported by LibreOffice |
| `ConversionFailedException` | Other conversion failure |

### Example

```csharp
using LibreOfficeKit;
using LibreOfficeKit.Exceptions;

try
{
    await converter.ConvertToPdfAsync("input.docx", "output.pdf");
}
catch (FilePasswordProtectedException ex)
{
    Console.WriteLine($"Password required: {ex.Message}");
}
catch (FileTypeNotSupportedException ex)
{
    Console.WriteLine($"Unsupported file type: {ex.Message}");
}
catch (TimeoutException ex)
{
    Console.WriteLine($"Timed out: {ex.Message}");
}
catch (ConversionFailedException ex)
{
    Console.WriteLine($"Conversion failed: {ex.Message}");
}
```

## Advanced Features

### Optional Features (LibreOffice 6.0+)

Enable optional LibreOfficeKit features for advanced scenarios:

```csharp
using LibreOfficeKit;
using LibreOfficeKit.Enums;

var instance = Instance.Create(@"C:\Program Files\LibreOffice\program");

// Optimize for conversion
instance.SetOptionalFeatures(OptionalFeatures.NoTiledAnnotations);

// Password support
instance.SetOptionalFeatures(
    OptionalFeatures.DocumentPassword | 
    OptionalFeatures.DocumentPasswordToModify);

var doc = instance.DocumentLoad(Instance.PathToFileUrl(@"C:\input.docx"));
doc.SaveAs(Instance.PathToFileUrl(@"C:\output.pdf"), "pdf");
```

Available features:
- `DocumentPassword` - Enable password prompts for encrypted documents
- `DocumentPasswordToModify` - Enable password prompts for write-protected documents
- `PartInInvalidationCallback` - Include part number in tile invalidation callbacks
- `NoTiledAnnotations` - Disable annotation rendering (performance optimization)
- `RangeHeaders` - Enable range-based spreadsheet header queries
- `ViewIdInVisibleCursorInvalidationCallback` - Include view ID in cursor callbacks (multi-user)

### Password-Protected Documents

```csharp
var instance = Instance.Create(installPath);

instance.SetOptionalFeatures(OptionalFeatures.DocumentPassword);

var fileUrl = Instance.PathToFileUrl(@"C:\encrypted.docx");
instance.SetDocumentPassword(fileUrl, "myPassword123");

var doc = instance.DocumentLoad(fileUrl); // Password used automatically
doc.SaveAs(outputUrl, "pdf");
```

### Direct LibreOfficeKit Instance

For advanced scenarios requiring direct LOK control (without the worker pool):

```csharp
using LibreOfficeKit;

var installPath = Instance.FindInstallPath() 
    ?? @"C:\Program Files\LibreOffice\program";

using var instance = Instance.Create(installPath);

var inputUrl = Instance.PathToFileUrl(@"C:\input.docx");
var outputUrl = Instance.PathToFileUrl(@"C:\output.pdf");

using var doc = instance.DocumentLoad(inputUrl);
doc.SaveAs(outputUrl, "pdf");
```

⚠️ **Note:** Only one `Instance` can be active per process. Use `Converter` for concurrent conversions.

## PdfOptions Presets

| Preset | Description |
|--------|-------------|
| `PdfOptions.HighQuality` | Lossless compression, full resolution, tagged PDF — for archiving |
| `PdfOptions.Screen` | JPEG 85%, 150 DPI — small file size optimized for on-screen use |
| `PdfOptions.Print` | JPEG 90%, 300 DPI — balanced quality for printing |
| `PdfOptions.Archive` | JPEG 90%, 300 DPI, tagged, PDF/A-2b — long-term archival |

## API Reference

Browse the complete API documentation:

- [Converter](xref:LibreOfficeKit.Converter) - Main worker pool manager
- [Instance](xref:LibreOfficeKit.Instance) - Direct LibreOfficeKit wrapper
- [Document](xref:LibreOfficeKit.Document) - Document operations
- [PdfOptions](xref:LibreOfficeKit.PdfOptions) - PDF export options
- [OptionalFeatures](xref:LibreOfficeKit.Enums.OptionalFeatures) - Optional feature flags
- [Exceptions](xref:LibreOfficeKit.Exceptions) - Exception types

## Additional Resources

For detailed documentation, advanced usage patterns, and source code, visit the [GitHub repository](https://github.com/Sicos1977/LibreOfficeKit).

### Specialized Documentation

- [OPTIONAL_FEATURES.md](https://github.com/Sicos1977/LibreOfficeKit/blob/main/LibreOfficeKit/OPTIONAL_FEATURES.md) - Detailed optional features guide
- [STRING_MARSHALLING.md](https://github.com/Sicos1977/LibreOfficeKit/blob/main/LibreOfficeKit/STRING_MARSHALLING.md) - String marshalling implementation details
- [LOGGING_REFACTORING.md](https://github.com/Sicos1977/LibreOfficeKit/blob/main/LibreOfficeKit/LOGGING_REFACTORING.md) - Logging architecture
- [DEBUGGING_HANGS.md](https://github.com/Sicos1977/LibreOfficeKit/blob/main/LibreOfficeKit/DEBUGGING_HANGS.md) - Hang debugging guide

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [LibreOffice](https://www.libreoffice.org/) installed

## License

MIT License - see [LICENSE](https://github.com/Sicos1977/LibreOfficeKit/blob/main/LICENSE) for details.

