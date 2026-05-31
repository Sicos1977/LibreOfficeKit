# Getting Started

This guide will help you get started with LibreOfficeKit for .NET.

## Installation

### NuGet Package

Install the LibreOfficeKit package via NuGet:

```bash
dotnet add package LibreOfficeKit
```

Or via the Package Manager Console:

```powershell
Install-Package LibreOfficeKit
```

### Prerequisites

LibreOfficeKit requires LibreOffice to be installed on the system:

- **Windows**: Download from [LibreOffice.org](https://www.libreoffice.org/download/download/)
- **Linux**: `sudo apt-get install libreoffice` (Debian/Ubuntu) or `sudo yum install libreoffice` (RHEL/CentOS)
- **macOS**: `brew install --cask libreoffice`

## Basic Usage

### Simple Conversion

```csharp
using LibreOfficeKit;

await using var converter = new Converter(
	maxInstances: 4,
	minHotStandby: 2);

await converter.ConvertToPdfAsync("input.docx", "output.pdf");
```

### With Configuration

```csharp
using LibreOfficeKit;

await using var converter = new Converter(
	maxInstances: 8,              // Maximum worker processes
	minHotStandby: 2,              // Pre-spawned workers
	idleTimeout: TimeSpan.FromMinutes(5),  // Idle worker shutdown
	healthCheckInterval: TimeSpan.FromSeconds(10));

try
{
	await converter.ConvertToPdfAsync(
		"presentation.pptx",
		"presentation.pdf",
		timeout: TimeSpan.FromSeconds(30));

	Console.WriteLine("Conversion successful!");
}
catch (Exception ex)
{
	Console.WriteLine($"Conversion failed: {ex.Message}");
}
```

### Stream-Based Conversion

```csharp
await using var inputStream = File.OpenRead("document.docx");
await using var outputStream = File.Create("document.pdf");

await converter.ConvertToPdfAsync(inputStream, outputStream);
```

## Configuration Options

### Converter Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `maxInstances` | `int` | `4` | Maximum number of worker processes |
| `minHotStandby` | `int` | `1` | Minimum number of pre-spawned workers |
| `idleTimeout` | `TimeSpan` | `5 min` | Time before idle workers shut down |
| `healthCheckInterval` | `TimeSpan` | `10 sec` | Interval for health check pings |

### Performance Tuning

**For high throughput:**
```csharp
var converter = new Converter(
	maxInstances: Environment.ProcessorCount,
	minHotStandby: Environment.ProcessorCount / 2);
```

**For low memory usage:**
```csharp
var converter = new Converter(
	maxInstances: 2,
	minHotStandby: 0,
	idleTimeout: TimeSpan.FromMinutes(1));
```

**For instant conversion (startup cost):**
```csharp
var converter = new Converter(
	maxInstances: 4,
	minHotStandby: 4);  // All workers pre-spawned
```

## Finding LibreOffice

By default, the library searches for LibreOffice in standard installation locations. You can also specify a custom path:

### Auto-Detection

```csharp
var installPath = Instance.FindInstallPath();
if (installPath == null)
{
	Console.WriteLine("LibreOffice not found!");
	return;
}

Console.WriteLine($"Found LibreOffice at: {installPath}");
```

### Custom Path

Set the `LOK_PROGRAM_PATH` environment variable:

**Windows:**
```powershell
$env:LOK_PROGRAM_PATH = "C:\Program Files\LibreOffice\program"
```

**Linux/macOS:**
```bash
export LOK_PROGRAM_PATH=/opt/libreoffice24.2/program
```

## Next Steps

- [PDF Options Guide](pdf-options.md) - Customize PDF export settings
- [Exception Handling](exception-handling.md) - Handle conversion errors
- [Optional Features](optional-features.md) - Advanced LibreOfficeKit features

## See Also

- [Converter Class API Reference](xref:LibreOfficeKit.Converter)
- [Instance Class API Reference](xref:LibreOfficeKit.Instance)
- [GitHub Repository](https://github.com/Sicos1977/LibreOfficeKit)
