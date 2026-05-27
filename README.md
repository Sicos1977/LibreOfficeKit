# LibreOfficeKit

A .NET library and console application for document-to-PDF conversion using LibreOfficeKit, featuring a
multi-process worker pool for safe concurrent conversions.

## Architecture

LibreOfficeKit (LOK) is **not** thread-safe — only one instance per process is allowed. To enable concurrent
conversions, this project uses a **process pool** architecture:

- The host process (`Converter`) manages a pool of isolated worker processes.
- Each worker connects via a **named pipe**, initializes its own LOK instance, and processes one request at a time.
- The host dispatches conversion requests to free workers and queues requests when all workers are busy.
- Workers are health-monitored via periodic pings and recycled if they crash or hang.

## Solution Structure

| Project                  | Type          | Target          | Description                                                        |
|--------------------------|---------------|-----------------|--------------------------------------------------------------------|
| `LibreOfficeKit`         | Class Library | .NET Standard 2.0 | Core library: Converter, WorkerProcess, IPC, LOK bindings, Enums |
| `LibreOfficeKit.Console` | Console App   | .NET 10         | Worker entry point and optional standalone demo                    |
| `LibreOfficeTest`        | Test Project  | .NET 10         | MSTest integration tests                                           |

## Key Components

### `LibreOfficeKit` (Class Library)

| File                      | Description                                                        |
|---------------------------|--------------------------------------------------------------------|
| `Converter.cs`            | Main pool manager — spawns, monitors, and recycles workers         |
| `WorkerHandle.cs`         | Encapsulates state and IPC for a single worker process             |
| `WorkerProcess.cs`        | Worker mode entry point — runs inside spawned processes            |
| `Instance.cs`             | Low-level LOK wrapper — initialization and document loading        |
| `Document.cs`             | Document wrapper — save/convert and type queries                   |
| `NativeLibraryCompat.cs`  | Cross-platform native library loading helper                       |

#### `Bindings/`

| File                          | Description                                      |
|-------------------------------|--------------------------------------------------|
| `Bindings.cs`                 | P/Invoke entry points for the native LOK C API   |
| `LibreOfficeKitClass.cs`      | LOK instance struct and function pointer table   |
| `LibreOfficeKitDocument.cs`   | LOK document struct definition                   |
| `LibreOfficeKitDocumentClass.cs` | LOK document function pointer table           |
| `LibreOfficeKitStruct.cs`     | Shared native struct definitions                 |

#### `Protocols/`

| File                  | Description                                                  |
|-----------------------|--------------------------------------------------------------|
| `IpcProtocol.cs`      | IPC message type definitions (including `ConvertRequest`)    |
| `IpcSerializer.cs`    | JSON serialization/deserialization for IPC messages          |
| `WorkerRequest.cs`    | Abstract base for all host-to-worker request messages        |
| `WorkerResponse.cs`   | Abstract base for all worker-to-host response messages       |
| `ConvertResponse.cs`  | Conversion result response (success/failure + error text)    |
| `ReadyResponse.cs`    | Worker ready/idle status response                            |
| `ErrorResponse.cs`    | Error response from a worker                                 |
| `PingRequest.cs`      | Health-check ping request                                    |
| `PongResponse.cs`     | Health-check pong response                                   |
| `ShutdownRequest.cs`  | Graceful shutdown request                                    |

#### `Enums/`

| File                       | Description                                            |
|----------------------------|--------------------------------------------------------|
| `DocumentType.cs`          | Document type classification (Writer, Calc, Impress …) |
| `InitialView.cs`           | PDF initial viewer panel state                         |
| `PdfACompliance.cs`        | PDF/A compliance level (None, 1b, 2b, 3b …)           |
| `PdfChangePermission.cs`   | PDF change permission flags                            |
| `PdfCompressionOptions.cs` | Per-image-type PDF compression overrides               |
| `PdfOptions.cs`            | Aggregate PDF export options (see section below)       |
| `PdfPrintPermission.cs`    | PDF print permission flags                             |
| `PdfSecurityOptions.cs`    | PDF encryption and permission settings                 |
| `PdfVersion.cs`            | PDF version/standard selection (1.4, 1.5, PDF/A, …)   |
| `SaveFormat.cs`            | Output save format enumeration                         |
| `SaveFormatExtensions.cs`  | Extension methods for `SaveFormat`                     |

#### `Exceptions/`

| File                          | Description                                                          |
|-------------------------------|----------------------------------------------------------------------|
| `TimeoutException.cs`         | Thrown when a conversion or worker operation exceeds the time limit  |
| `ConversionFailedException.cs`| Thrown when LibreOffice reports a conversion failure                 |

### `LibreOfficeKit.Console` (Console App)

| File         | Description                                                    |
|--------------|----------------------------------------------------------------|
| `Program.cs` | Worker entry point; also supports a standalone demo/test mode  |

## Features

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

## Usage

### Basic conversion (file paths)

```csharp
using LibreOfficeKit;

await using var converter = new Converter(
    maxInstances: 4,
    minHotStandby: 2,
    idleTimeout: TimeSpan.FromMinutes(5));

await converter.ConvertToPdfAsync("input.docx", "output.pdf");
```

### With a timeout

```csharp
await converter.ConvertToPdfAsync(
    "input.docx",
    "output.pdf",
    timeout: TimeSpan.FromSeconds(30));
```

### With PDF options

```csharp
using LibreOfficeKit.Enums;

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

### Using a preset

```csharp
await converter.ConvertToPdfAsync(
    "input.docx",
    "output.pdf",
    pdfOptions: PdfOptions.Archive);   // PDF/A-2b archival preset
```

### Stream conversion

```csharp
await using var input  = File.OpenRead("input.docx");
await using var output = File.Create("output.pdf");

await converter.ConvertToPdfAsync(input, output, pdfOptions: PdfOptions.Screen);
```

## PdfOptions Reference

`PdfOptions` controls how LibreOffice exports the PDF. Pass an instance as the `pdfOptions` parameter of
`ConvertToPdfAsync`.

### Image quality

| Property                 | Type   | Default | Description                                                                    |
|--------------------------|--------|---------|--------------------------------------------------------------------------------|
| `UseLosslessCompression` | `bool` | `false` | Use lossless (PNG) compression for images instead of JPEG                      |
| `Quality`                | `int`  | `90`    | JPEG compression quality (1–100). Higher = better quality, larger file         |
| `ReduceImageResolution`  | `bool` | `true`  | Down-sample images that exceed `MaxImageResolutionDpi`                         |
| `MaxImageResolutionDpi`  | `int`  | `300`   | Maximum DPI for embedded images when `ReduceImageResolution` is `true`         |

### Content & accessibility

| Property            | Type   | Default | Description                                                       |
|---------------------|--------|---------|-------------------------------------------------------------------|
| `ExportBookmarks`   | `bool` | `true`  | Include document headings as PDF bookmarks/outlines               |
| `ExportNotes`       | `bool` | `false` | Include document comments/annotations                             |
| `ExportFormFields`  | `bool` | `true`  | Export form fields as interactive PDF widgets                     |
| `UseTaggedPdf`      | `bool` | `false` | Create a tagged PDF (required for PDF/UA accessibility)           |
| `SinglePageSheets`  | `bool` | `false` | Fit each spreadsheet sheet onto a single PDF page                 |

### Compliance & version

| Property          | Type             | Default              | Description                                              |
|-------------------|------------------|----------------------|----------------------------------------------------------|
| `PdfACompliance`  | `PdfACompliance` | `None`               | PDF/A archival standard (None, Level1b, Level2b, Level3b) |
| `PdfVersion`      | `PdfVersion?`    | `null` (LOK default) | Target PDF version (PDF14, PDF15, PDF16, PDF17, PdfA1b, PdfA2b, PdfUA1) |

> When `PdfVersion` is set it takes precedence over `PdfACompliance`.

### Layout

| Property    | Type          | Default           | Description                                     |
|-------------|---------------|-------------------|-------------------------------------------------|
| `PageRange` | `string?`     | `null` (all)      | Page range to export, e.g. `"1-5"` or `"2,4"` |
| `Watermark` | `string?`     | `null`            | Watermark text printed on every page            |
| `InitialView` | `InitialView` | `Default`       | Which panel is shown when the PDF is opened (Default, Bookmarks, Thumbnails) |

### Security

| Property             | Type                 | Default | Description                                                  |
|----------------------|----------------------|---------|--------------------------------------------------------------|
| `EncryptionPassword` | `string?`            | `null`  | Password required to open the PDF (simple encryption)        |
| `Security`           | `PdfSecurityOptions?`| `null`  | Full security settings: passwords, printing and change permissions |

**`PdfSecurityOptions` properties:**

| Property                              | Type                      | Description                                           |
|---------------------------------------|---------------------------|-------------------------------------------------------|
| `EncryptFile`                         | `bool`                    | Enable encryption                                     |
| `DocumentOpenPassword`                | `string?`                 | Password to open the document                         |
| `RestrictPermissions`                 | `bool`                    | Enable permission restrictions                        |
| `PermissionPassword`                  | `string?`                 | Password required to change permissions               |
| `Printing`                            | `PdfPrintPermission?`     | Print permission level (NotPermitted, LowResolution, HighResolution) |
| `Changes`                             | `PdfChangePermission?`    | Change permission level (NotPermitted, OnlyComments, OnlyFormFields, …) |
| `EnableCopyingOfContent`              | `bool?`                   | Allow copying text/images                             |
| `EnableTextAccessForAccessibilityTools` | `bool?`                 | Allow screen readers                                  |

### Advanced compression overrides

Set `Compression` (`PdfCompressionOptions`) to override image compression settings per image type rather than
using the top-level `UseLosslessCompression`/`Quality` properties.

| Property                   | Type    | Description                                          |
|----------------------------|---------|------------------------------------------------------|
| `UseLosslessCompression`   | `bool?` | Override lossless flag                               |
| `Quality`                  | `int?`  | Override JPEG quality (1–100)                        |
| `ReduceImageResolution`    | `bool?` | Override resolution reduction flag                   |
| `MaxImageResolution`       | `int?`  | Override maximum DPI                                 |

### Built-in presets

| Preset                  | Description                                                        |
|-------------------------|--------------------------------------------------------------------|
| `PdfOptions.HighQuality`| Lossless compression, full resolution, tagged PDF — for archiving  |
| `PdfOptions.Screen`     | JPEG 85%, 150 DPI — small file size optimised for on-screen use    |
| `PdfOptions.Print`      | JPEG 90%, 300 DPI — balanced quality for printing                  |
| `PdfOptions.Archive`    | JPEG 90%, 300 DPI, tagged, PDF/A-2b — long-term archival           |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [LibreOffice](https://www.libreoffice.org/) installed

## Documentation

The full API reference is published automatically to GitHub Pages on every push to `main`:

**[https://sicos1977.github.io/LibreOfficeKit](https://sicos1977.github.io/LibreOfficeKit)**

## Build

```bash
dotnet build LibreOfficeKit.sln
```

## NuGet Package

### How it works

The `LibreOfficeKit` NuGet package bundles the `LibreOfficeKit.Console` worker executable for all supported
platforms. The worker is a self-contained, trimmed single-file binary — no .NET installation is required on the
target machine.

When the package is installed in a consuming project, the MSBuild targets file (`build/LibreOfficeKit.targets`)
that ships inside the package automatically copies the correct worker executable for the current platform to the
project's output directory at build time. No manual steps are needed.

### Package contents

```
build/
  LibreOfficeKit.targets          ← MSBuild targets, auto-copies the right worker exe
runtimes/
  win-x64/native/
    LibreOfficeKit.Console.exe    ← Self-contained worker for Windows x64
  linux-x64/native/
    LibreOfficeKit.Console        ← Self-contained worker for Linux x64
  osx-x64/native/
    LibreOfficeKit.Console        ← Self-contained worker for macOS Intel
  osx-arm64/native/
    LibreOfficeKit.Console        ← Self-contained worker for macOS Apple Silicon
lib/
  net10.0/LibreOfficeKit.dll
  netstandard2.0/LibreOfficeKit.dll
```

### Building the package locally

Building the package requires the cross-platform publish tools included with .NET 10.

```bash
dotnet pack LibreOfficeKit/LibreOfficeKit.csproj -c Release
```

During packing, MSBuild automatically publishes `LibreOfficeKit.Console` for all four runtime identifiers
(`win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`) before assembling the package. The resulting files are staged
in `LibreOfficeKit/_WorkerPublish/` (excluded from source control via `.gitignore`) and then embedded in the
`.nupkg`.

The output is written to:

```
LibreOfficeKit/bin/Release/LibreOfficeKit.<version>.nupkg
LibreOfficeKit/bin/Release/LibreOfficeKit.<version>.snupkg   ← debug symbols
```

### Publishing to NuGet.org

1. Create an API key at [https://www.nuget.org/account/apikeys](https://www.nuget.org/account/apikeys).
2. Push the package:

```bash
dotnet nuget push LibreOfficeKit/bin/Release/LibreOfficeKit.<version>.nupkg \
  --api-key <your-api-key> \
  --source https://api.nuget.org/v3/index.json
```

The `.snupkg` symbol package is pushed automatically alongside the main package when using the NuGet.org source.

### Versioning

The package version is set via the `<Version>` property in `LibreOfficeKit/LibreOfficeKit.csproj`.
Update it before packing a new release:

```xml
<Version>1.2.0</Version>
```