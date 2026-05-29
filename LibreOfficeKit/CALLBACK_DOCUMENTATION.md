# LibreOfficeKit Callback Implementation

## Overview

The LibreOfficeKit callback mechanism enables receiving real-time events from LibreOffice during document operations such as loading, converting, and rendering.

## Architecture

### Callback Registration

The callback is registered during LibreOffice instance initialization in `Instance.Create()`:

```csharp
// .NET 5.0 and higher: use UnmanagedCallersOnly for better performance
var registerCallback = (delegate* unmanaged[Cdecl]<IntPtr, delegate* unmanaged[Cdecl]<int, byte*, IntPtr, void>, IntPtr, void>)vtable.registerCallback;
registerCallback(pOffice, &OnLibreOfficeEventUnmanaged, IntPtr.Zero);

// .NET Standard 2.0: use managed delegate
var registerCallback = Marshal.GetDelegateForFunctionPointer<LokRegisterCallbackFunction>(vtable.registerCallback);
registerCallback(pOffice, CallbackDelegate, IntPtr.Zero);
```

### Callback Types

The callback events are defined in the `CallbackType` enum with 64 different event types:

- **InvalidateTiles** (0): Tile or region invalidation
- **StatusIndicatorSetValue** (10): Progress updates during conversion
- **DocumentPassword** (20): Document requires password
- **Error** (22): Error message from LibreOffice
- And many more...

### Event Handling

Events are handled in the `HandleLibreOfficeEvent` method:

```csharp
private static void HandleLibreOfficeEvent(int type, string payload)
{
	var callbackType = (CallbackType)type;

	switch (callbackType)
	{
		case CallbackType.Error:
			_logger?.LogError("[LOK Error] '{Payload}'", payload);
			break;

		case CallbackType.StatusIndicatorSetValue:
			_logger?.LogDebug("[LOK Progress] '{Payload}'", payload);
			break;

		case CallbackType.DocumentPassword:
			_logger?.LogWarning("[LOK Password] Document requires password: '{Payload}'", payload);
			break;

		// ... other cases
	}
}
```

## Logger Configuration

To log callback events, use the `SetLogger` method:

```csharp
var loggerFactory = LoggerFactory.Create(builder => 
	builder.AddConsole().AddFilter(null, LogLevel.Debug));
var logger = loggerFactory.CreateLogger("LibreOfficeKit.Callbacks");

Instance.SetLogger(logger);
```

### Test Setup

In test setup:

```csharp
[ClassInitialize]
public static void TestInitialize(TestContext context)
{
	var loggerFactory = LoggerFactory.Create(builder => 
		builder.AddProvider(new TestContextLoggerProvider())
			   .AddFilter(null, LogLevel.Trace));

	var lokLogger = loggerFactory.CreateLogger("LibreOfficeKit.Callbacks");
	Instance.SetLogger(lokLogger);
}
```

## Platform Differences

### .NET 5.0 and Higher

Uses `[UnmanagedCallersOnly]` attribute for direct function pointer interop:

```csharp
[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
private static unsafe void OnLibreOfficeEventUnmanaged(int type, byte* pPayload, IntPtr pData)
{
	var payload = pPayload != null ? Marshal.PtrToStringUTF8((IntPtr)pPayload) ?? string.Empty : string.Empty;
	HandleLibreOfficeEvent(type, payload);
}
```

**Advantages:**
- Better performance (no delegate allocation)
- Direct function pointer
- Native interop without marshaling overhead

### .NET Standard 2.0

Uses a managed delegate:

```csharp
private static readonly LokCallback2Function CallbackDelegate = OnLibreOfficeEventManaged;

private static void OnLibreOfficeEventManaged(int type, string payload, IntPtr pData)
{
	HandleLibreOfficeEvent(type, payload ?? string.Empty);
}
```

**Advantages:**
- Works on all .NET platforms
- Automatic string marshaling
- Simpler debugging

## Common Events

### During Document Conversion

1. **StatusIndicatorStart**: Conversion begins
2. **StatusIndicatorSetValue**: Progress updates (multiple times)
3. **StatusIndicatorFinish**: Conversion completed

### With Password-Protected Documents

1. **DocumentPassword** or **DocumentPasswordToModify**: Document requires password
2. Use `Instance.SetDocumentPassword()` to provide password

### On Errors

1. **Error**: Contains error message in payload
2. Log level: Error
3. Example: "Error: Cannot open file"

## Logging Format

All callback logs follow the format with single quotes around placeholders:

```
[LOK Event] Type: 'StatusIndicatorSetValue' (10) | Payload: '50'
[LOK Progress] '50'
[LOK Error] 'Cannot load document'
```

This aligns with the Copilot Instructions for structured logging.

## Best Practices

1. **Always set a logger** for production debugging
2. **Use LogLevel.Debug** for detailed callback tracking
3. **Filter StatusIndicatorSetValue** events if they create too much noise
4. **Always monitor Error and DocumentPassword** events
5. **Test with and without logger** to validate fallback to Console.WriteLine

## Error Handling

Exceptions in the callback are caught and logged:

```csharp
try
{
	HandleLibreOfficeEvent(type, payload);
}
catch (Exception ex)
{
	Console.WriteLine($"[LOK Callback Error] {ex.Message}");
}
```

This prevents native code crashes from managed exceptions.

## Performance Considerations

- **.NET 5.0+**: `UnmanagedCallersOnly` has minimal overhead
- **.NET Standard 2.0**: Delegate has small marshaling overhead
- **Logging**: Debug level can generate significant output during conversions
- **String Marshaling**: UTF-8 conversion is optimized in both implementations
