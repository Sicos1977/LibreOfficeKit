# LibreOfficeKit

A .NET 10 library and console application for document-to-PDF conversion using LibreOfficeKit, featuring a multi-process
worker pool for safe concurrent conversions.

## Architecture

LibreOfficeKit (LOK) is **not** thread-safe — only one instance per process is allowed. To enable concurrent
conversions, this project uses a **process pool** architecture:

```
┌─────────────────────────────────────┐
│         Host Process                │
│  ┌───────────────────────────────┐  │
│  │      Converter (pool mgr)    │  │
│  │  - hot standby management    │  │
│  │  - health monitoring         │  │
│  │  - idle timeout              │  │
│  │  - request queuing           │  │
│  └───┬───────┬───────┬──────────┘  │
│      │ pipe  │ pipe  │ pipe        │
└──────┼───────┼───────┼─────────────┘
       │       │       │
  ┌────┴──┐ ┌──┴───┐ ┌─┴─────┐
  │Worker1│ │Worker2│ │Worker3│   (separate OS processes)
  │  LOK  │ │  LOK  │ │  LOK  │
  └───────┘ └──────┘ └───────┘
```

## Solution Structure

| Project                  | Type          | Description                                                      |
|--------------------------|---------------|------------------------------------------------------------------|
| `LibreOfficeKit`         | Class Library | Core library: Converter, WorkerProcess, IPC, LOK bindings, Enums |
| `LibreOfficeKit.Console` | Console App   | Demo/testing console application                                 |

## Key Components

| File                        | Description                                                 |
|-----------------------------|-------------------------------------------------------------|
| `Converter.cs`              | Main pool manager — spawns, monitors, and recycles workers  |
| `WorkerHandle.cs`           | Encapsulates state and IPC for a single worker process      |
| `WorkerProcess.cs`          | Worker mode entry point — runs in spawned processes         |
| `IpcProtocol.cs`            | IPC message types and JSON serialization                    |
| `LibreOfficeInstance.cs`    | Low-level LOK wrapper — initialization and document loading |
| `LoDocument.cs`             | Document wrapper — save/convert and type queries            |
| `LibreOfficeKitBindings.cs` | P/Invoke bindings for the native LOK C API                  |
| `Enums/`                    | SaveFormat, DocumentType, PdfOptions, PdfVersion, etc.      |

## Features

- **Hot standby**: Pre-spawned workers for immediate conversion
- **On-demand scaling**: Scale up to `maxInstances` as needed
- **Idle timeout**: Excess workers shut down after idle period
- **Health monitoring**: Background pings detect crashed/hung workers
- **Request queuing**: Requests queue when all workers are busy
- **Stream support**: Convert from/to streams (uses temp files internally)
- **PDF options**: Full control over PDF export quality, compliance, security
- **Enum-based API**: Type-safe save formats and document types
- **Proper disposal**: `IDisposable` and `IAsyncDisposable` for clean shutdown

## Usage

### As a library

```csharp
using LibreOfficeKit;

await using var converter = new Converter(
    maxInstances: 4,
    minHotStandby: 2,
    idleTimeout: TimeSpan.FromMinutes(5));

await converter.ConvertToPdfAsync("input.docx", "output.pdf");
```

### CLI

```bash
# Pool-based conversion (recommended)
dotnet run --project src/LibreOfficeKit.Console -- input.docx output.pdf

# Direct single-instance conversion
dotnet run --project src/LibreOfficeKit.Console -- --direct input.docx output.pdf
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [LibreOffice](https://www.libreoffice.org/) installed

## Build

```bash
dotnet build LibreOfficeKit.sln
```
