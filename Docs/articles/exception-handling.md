# Exception Handling

LibreOfficeKit provides specific exception types to distinguish between different failure scenarios during document loading and conversion.

## Exception Types

| Exception | When Thrown |
|-----------|-------------|
| `TimeoutException` | The conversion or worker operation exceeded the specified timeout |
| `FilePasswordProtectedException` | The document is password-protected and cannot be opened |
| `FileTypeNotSupportedException` | The file type is not supported by LibreOffice |
| `ConversionFailedException` | The conversion failed for any other reason |

All custom exceptions are in the `LibreOfficeKit.Exceptions` namespace.

## Basic Usage

```csharp
using LibreOfficeKit;
using LibreOfficeKit.Exceptions;

await using var converter = new Converter(maxInstances: 4);

try
{
	await converter.ConvertToPdfAsync("input.docx", "output.pdf");
}
catch (FilePasswordProtectedException ex)
{
	Console.WriteLine($"File is password-protected: {ex.Message}");
}
catch (FileTypeNotSupportedException ex)
{
	Console.WriteLine($"File type not supported: {ex.Message}");
}
catch (TimeoutException ex)
{
	Console.WriteLine($"Conversion timed out: {ex.Message}");
}
catch (ConversionFailedException ex)
{
	Console.WriteLine($"Conversion failed: {ex.Message}");
}
```

## Exception Flow

Exceptions originate in the worker process and are propagated to the calling code:

```
Worker Process: Document.Load / Document.SaveAs
	 ↓
Worker: Catches specific exception
	 ↓
Worker: Sends ConvertResponse with ExceptionType field
	 ↓
Converter: Reads ExceptionType and re-throws original exception
	 ↓
Your Code: Can catch specific exception types
```

## Detection Logic

The library analyzes error messages from LibreOffice to determine the exception type:

| Exception | Error Message Contains |
|-----------|----------------------|
| `FilePasswordProtectedException` | `"password"`, `"encrypted"`, or `"protected"` |
| `FileTypeNotSupportedException` | `"format"`, `"not supported"`, `"unknown"`, or `"filter"` |
| `TimeoutException` | Operation exceeds timeout parameter |
| `ConversionFailedException` | Any other error |

## Timeout Behavior

When a timeout is specified, the library enforces it at multiple stages:

1. **Worker acquisition** - Waiting for an available worker
2. **Worker spawn** - Starting a new worker process
3. **Document loading** - Loading the input document
4. **PDF conversion** - Converting and saving
5. **IPC communication** - Sending/receiving messages

If any stage exceeds the remaining time budget, a `TimeoutException` is thrown immediately.

## Error Handling Patterns

### Retry Pattern

```csharp
int maxRetries = 3;
int retryCount = 0;

while (retryCount < maxRetries)
{
	try
	{
		await converter.ConvertToPdfAsync("input.docx", "output.pdf");
		break; // Success
	}
	catch (ConversionFailedException ex) when (retryCount < maxRetries - 1)
	{
		retryCount++;
		Console.WriteLine($"Retry {retryCount}/{maxRetries}: {ex.Message}");
		await Task.Delay(TimeSpan.FromSeconds(2));
	}
}
```

### Fallback Pattern

```csharp
try
{
	await converter.ConvertToPdfAsync("input.docx", "output.pdf");
}
catch (FileTypeNotSupportedException)
{
	// Try alternative conversion method
	await AlternativeConversion("input.docx", "output.pdf");
}
```

### Logging Pattern

```csharp
using Microsoft.Extensions.Logging;

try
{
	await converter.ConvertToPdfAsync("input.docx", "output.pdf");
	logger.LogInformation("Conversion successful: {File}", "input.docx");
}
catch (FilePasswordProtectedException ex)
{
	logger.LogWarning(ex, "Password required: {File}", "input.docx");
	throw;
}
catch (TimeoutException ex)
{
	logger.LogError(ex, "Conversion timeout: {File}", "input.docx");
	throw;
}
catch (Exception ex)
{
	logger.LogError(ex, "Conversion failed: {File}", "input.docx");
	throw;
}
```

## Exception Properties

All LibreOfficeKit exceptions inherit from `Exception` and provide:

- `Message`: Human-readable error description
- `InnerException`: Original exception (if any)
- `StackTrace`: Call stack at throw point

### Example

```csharp
try
{
	await converter.ConvertToPdfAsync("input.docx", "output.pdf");
}
catch (ConversionFailedException ex)
{
	Console.WriteLine($"Error: {ex.Message}");
	Console.WriteLine($"Stack: {ex.StackTrace}");

	if (ex.InnerException != null)
	{
		Console.WriteLine($"Inner: {ex.InnerException.Message}");
	}
}
```

## See Also

- [Converter API Reference](xref:LibreOfficeKit.Converter)
- [Exception Types](xref:LibreOfficeKit.Exceptions)
- [Getting Started](getting-started.md)
