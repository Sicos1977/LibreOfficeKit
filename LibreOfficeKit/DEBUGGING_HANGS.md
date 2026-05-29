# LibreOfficeKit Debugging Guide for Document Load Hangs

## Overview

This guide helps diagnose why certain document types (especially .xlsx and .pptx) may hang during loading while others (.docx) work fine.

## Quick Start: Enabling Logging

### Direct Mode (Console App)

When using the console application with `--direct` mode, logging is automatically enabled:

```bash
LibreOfficeKit.Console --direct input.xlsx output.pdf
```

All LibreOfficeKit events will be logged to the console with timestamps.

### Programmatic Usage

#### Option 1: Automatic Console Logging

```csharp
using LibreOfficeKit;
using Microsoft.Extensions.Logging;

// Enable built-in console logging
Instance.EnableConsoleLogging(LogLevel.Debug);

// Now all operations will log to console
using var office = Instance.Create(installPath);
using var document = office.DocumentLoad(fileUrl);
```

#### Option 2: Custom Logger

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder => 
    builder.AddConsole()
           .SetMinimumLevel(LogLevel.Trace));

Instance.SetLogger(loggerFactory.CreateLogger("LibreOfficeKit"));
```

### Log Levels

- **Trace**: Unhandled callback events (catch-all)
- **Debug**: All callback events, internal operations, progress
- **Information**: Major operations (start, finish, size changes)
- **Warning**: Passwords, dialogs, missing fonts
- **Error**: LibreOffice errors

## Enhanced Logging

### Callback Events Now Logged

The following callback events are now logged to help diagnose load hangs:

#### Critical Events (Always Logged)
1. **Error** (Type 22) - LogLevel: Error
   - Any error from LibreOffice
   - Pattern: `[LOK Error] 'error message'`

2. **DocumentPassword** (Type 20) - LogLevel: Warning
   - Document requires a password
   - Pattern: `[LOK Password] Document requires password: 'details'`

3. **DocumentPasswordToModify** (Type 21) - LogLevel: Warning
   - Document requires password to modify
   - Pattern: `[LOK Password] Document requires password: 'details'`

#### Status Indicators
4. **StatusIndicatorStart** (Type 9) - LogLevel: Information
   - Operation started (e.g., "Load document")
   - Pattern: `[LOK Status] Operation started: 'operation'`

5. **StatusIndicatorSetValue** (Type 10) - LogLevel: Debug
   - Progress updates (0-100)
   - Pattern: `[LOK Progress] 'value'`

6. **StatusIndicatorFinish** (Type 11) - LogLevel: Information
   - Operation completed
   - Pattern: `[LOK Status] Operation finished`

#### Document Events
7. **DocumentSizeChanged** (Type 13) - LogLevel: Information
   - Document dimensions changed
   - Pattern: `[LOK Document] Size changed: 'dimensions'`
   - **Important**: This should fire after document is loaded

8. **InvalidateTiles** (Type 0) - LogLevel: Debug
   - Rendering tile invalidation
   - Pattern: `[LOK Render] Tile invalidation: 'region'`
   - **Important**: Frequent during/after load means rendering is working

#### UI/Dialog Events (Potential Hang Causes)
9. **Window** (Type 36) - LogLevel: Information
   - Window/dialog events
   - Pattern: `[LOK Window] Window event: 'details'`
   - **WARNING**: May indicate a blocking dialog

10. **Jsdialog** (Type 46) - LogLevel: Warning
	- JavaScript dialog detected
	- Pattern: `[LOK Dialog] JavaScript dialog detected: 'details'`
	- **WARNING**: Could block execution waiting for user input

11. **ContextMenu** (Type 23) - LogLevel: Debug
	- Context menu appeared
	- Pattern: `[LOK Menu] Context menu: 'details'`

#### State/Command Events
12. **UnoCommandResult** (Type 16) - LogLevel: Debug
	- UNO command execution result
	- Pattern: `[LOK UNO] Command result: 'result'`

13. **StateChanged** (Type 8) - LogLevel: Debug
	- Document state changed (e.g., formatting)
	- Pattern: `[LOK State] State changed: 'state'`

#### Resource Events
14. **FontsMissing** (Type 57) - LogLevel: Warning
	- Missing fonts detected
	- Pattern: `[LOK Fonts] Missing fonts: 'font list'`
	- **Note**: Could cause rendering delays but shouldn't hang

15. **ProfileFrame** (Type 41) - LogLevel: Debug
	- Frame timing information
	- Pattern: `[LOK Profile] Frame timing: 'timing'`

#### Catch-All
16. **All Other Events** - LogLevel: Trace
	- Any unhandled event type
	- Pattern: `[LOK Event] Unhandled type 'TypeName': 'payload'`

## Document Load Logging

Additional logging has been added to `Instance.DocumentLoad()`:

```
Loading document: 'file:///.../document.xlsx'
Resolved filter name: 'Calc MS Excel 2007 XML' for file 'file:///.../document.xlsx'
Load options: 'Hidden=true,MacroExecutionMode=4,TiledRendering=true,ReadOnly=true,UpdateDocMode=0,InteractionHandler=null,FilterName=Calc MS Excel 2007 XML'
Calling documentLoadWithOptions...
[LOK Event] Type: StatusIndicatorStart (9) | Payload: Load document
[LOK Event] Type: StatusIndicatorSetValue (10) | Payload: 2
... (progress updates)
[LOK Event] Type: StatusIndicatorFinish (11) | Payload:
documentLoadWithOptions returned pointer: 0x...
Checking for errors...
Document loaded successfully
Document constructor called with pointer: 0x...
Reading LibreOfficeKitDocument structure...
Reading LibreOfficeKitDocumentClass vtable...
Document object fully initialized
```

## Diagnostic Patterns

### Normal Load Sequence
1. `Loading document`
2. `Resolved filter name`
3. `Load options`
4. `Calling documentLoadWithOptions...`
5. `StatusIndicatorStart` callback
6. Multiple `StatusIndicatorSetValue` callbacks (progress)
7. `StatusIndicatorFinish` callback
8. `documentLoadWithOptions returned pointer`
9. `Checking for errors...`
10. `Document loaded successfully`
11. `Document constructor called`
12. `Document object fully initialized`

### Hang Patterns to Watch For

#### 1. Hang After StatusIndicatorFinish
**Symptoms**: 
- `StatusIndicatorFinish` received
- No `documentLoadWithOptions returned pointer` message

**Likely Cause**: 
- Native code hang in documentLoadWithOptions
- Possible blocking dialog or resource wait

**Look For**:
- `Window` or `Jsdialog` events between finish and return
- Missing `DocumentSizeChanged` event

#### 2. Hang Before StatusIndicatorStart
**Symptoms**:
- `Calling documentLoadWithOptions...` is the last message
- No callback events at all

**Likely Cause**:
- Initialization hang before load starts
- Filter loading issue
- Font/resource discovery hang

#### 3. Hang During Progress
**Symptoms**:
- `StatusIndicatorSetValue` stops at specific percentage
- No `StatusIndicatorFinish`

**Likely Cause**:
- Document parsing issue
- Complex formula/macro processing
- External link resolution

#### 4. Hang After Document Constructor
**Symptoms**:
- All messages complete
- Hang occurs in calling code

**Likely Cause**:
- Not a LibreOffice issue
- Check calling code after DocumentLoad

## Recommended Test Approach

1. **Enable Trace Logging**:
   ```csharp
   var loggerFactory = LoggerFactory.Create(builder => 
	   builder.AddConsole()
			  .SetMinimumLevel(LogLevel.Trace));
   Instance.SetLogger(loggerFactory.CreateLogger("LibreOfficeKit"));
   ```

2. **Run Test with Hanging File**:
   - Note the last log message before hang
   - Check for dialog/window events
   - Look for missing expected events

3. **Compare with Working File** (.docx):
   - Compare event sequences
   - Identify divergence point

4. **Check for Unexpected Events**:
   - Dialog events (Jsdialog, Window)
   - Error events (even before hang)
   - FontsMissing events

## Common Fixes

Based on the logged events, apply these fixes:

- **Dialog Events Detected**: Add more aggressive dialog suppression in load options
- **FontsMissing**: Pre-configure font substitution
- **Hang After Finish**: Add timeout wrapper around documentLoadWithOptions
- **No DocumentSizeChanged**: Document may have loaded but rendering failed

## Configuration Already Applied

The following hang-prevention settings are already in `registrymodifications.xcu`:

- ✅ Printer setup disabled (common Calc hang)
- ✅ Hardware acceleration disabled (Impress hang)
- ✅ Auto-calculate disabled (Calc hang)
- ✅ Spell checking disabled (dictionary loading hang)
- ✅ Font replacement disabled
- ✅ All dialogs disabled
- ✅ Macro execution blocked

If hangs persist after these settings, the logs will reveal the specific event sequence causing the issue.
