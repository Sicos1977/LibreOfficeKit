# Optional Features

LibreOfficeKit provides optional features that modify callback behavior and rendering options. These are available since LibreOffice 6.0.

## Overview

Optional features are disabled by default and must be explicitly enabled via `Instance.SetOptionalFeatures()`.

> **Note:** These features are primarily useful for **interactive viewers** and **collaborative editing** scenarios. Most document conversion use cases don't require them.

## Available Features

| Feature | Value | Description |
|---------|-------|-------------|
| `None` | `0x00` | No optional features (default) |
| `DocumentPassword` | `0x01` | Enable password prompts for encrypted documents |
| `DocumentPasswordToModify` | `0x02` | Enable password prompts for write-protected documents |
| `PartInInvalidationCallback` | `0x04` | Include part number in tile invalidation callbacks |
| `NoTiledAnnotations` | `0x08` | Disable annotation rendering in tiles |
| `RangeHeaders` | `0x10` | Enable range-based spreadsheet header queries |
| `ViewIdInVisibleCursorInvalidationCallback` | `0x20` | Include view ID in cursor invalidation callbacks |

## Usage

### Basic Example

```csharp
using LibreOfficeKit;
using LibreOfficeKit.Enums;

var instance = Instance.Create(@"C:\Program Files\LibreOffice\program");

// Enable a single feature
instance.SetOptionalFeatures(OptionalFeatures.NoTiledAnnotations);

var doc = instance.DocumentLoad(Instance.PathToFileUrl("input.docx"));
doc.SaveAs(Instance.PathToFileUrl("output.pdf"), "pdf");
```

### Combining Features

Features can be combined using bitwise OR (`|`):

```csharp
instance.SetOptionalFeatures(
	OptionalFeatures.DocumentPassword | 
	OptionalFeatures.DocumentPasswordToModify |
	OptionalFeatures.NoTiledAnnotations);
```

## Common Scenarios

### 1. Performance Optimization (Conversion)

For server-side document conversion, disable annotation rendering:

```csharp
var instance = Instance.Create(installPath, logger);

// Optimize for conversion
instance.SetOptionalFeatures(OptionalFeatures.NoTiledAnnotations);

var doc = instance.DocumentLoad(inputUrl);
doc.SaveAs(outputUrl, "pdf");
```

**Why:** Annotations slow down rendering and aren't needed for pure conversion.

### 2. Password-Protected Documents

Enable password support and pre-register passwords:

```csharp
var instance = Instance.Create(installPath);

// Step 1: Enable password features
instance.SetOptionalFeatures(
	OptionalFeatures.DocumentPassword | 
	OptionalFeatures.DocumentPasswordToModify);

// Step 2: Pre-register password
var fileUrl = Instance.PathToFileUrl(@"C:\encrypted.docx");
instance.SetDocumentPassword(fileUrl, "myPassword123");

// Step 3: Load (password used automatically)
var doc = instance.DocumentLoad(fileUrl);
doc.SaveAs(outputUrl, "pdf");
```

To clear a password:

```csharp
instance.SetDocumentPassword(fileUrl, null);
```

### 3. Interactive Multi-User Viewer

For collaborative editing platforms:

```csharp
instance.SetOptionalFeatures(
	OptionalFeatures.PartInInvalidationCallback |
	OptionalFeatures.ViewIdInVisibleCursorInvalidationCallback |
	OptionalFeatures.RangeHeaders);
```

This enables:
- **Part number** in tile invalidations → Know which page/sheet changed
- **View ID** in cursor invalidations → Track which user's cursor moved
- **Range headers** → Efficiently query large spreadsheet headers

### 4. Large Spreadsheet Viewer

For spreadsheet viewers with millions of rows:

```csharp
instance.SetOptionalFeatures(
	OptionalFeatures.RangeHeaders |
	OptionalFeatures.PartInInvalidationCallback);
```

## Feature Details

### DocumentPassword (`0x01`)

**Purpose:** Handle encrypted documents requiring a password to open.

**Behavior:** When enabled, LibreOfficeKit emits a callback when encountering an encrypted document.

**Use Case:** Interactive document viewers, web applications with user input.

### DocumentPasswordToModify (`0x02`)

**Purpose:** Handle write-protected documents requiring a password to edit.

**Behavior:** Similar to `DocumentPassword`, but for edit-protection rather than encryption.

**Use Case:** Collaborative editing platforms, document approval workflows.

### PartInInvalidationCallback (`0x04`)

**Purpose:** Include part (page/sheet/slide) number in tile invalidation callbacks.

**Callback Payload:**
```
Without feature: "x, y, width, height"
With feature:    "x, y, width, height, part"
```

**Use Case:** Multi-page viewers that need to know which page changed.

### NoTiledAnnotations (`0x08`)

**Purpose:** Disable rendering of annotations (comments, tracked changes) in tiles.

**Performance Impact:**
- Reduces rendering complexity
- Faster tile generation
- Lower memory usage

**Use Case:** Headless conversion where annotations aren't needed or are exported separately.

**Note:** Annotations may still be exported in the final document (e.g., PDF comments).

### RangeHeaders (`0x10`)

**Purpose:** Enable range-based header queries for spreadsheets.

**Behavior:** Allows querying column/row headers for a specific range rather than the entire sheet.

**Use Case:** Interactive spreadsheet viewers with virtual scrolling.

**Not needed for:** Simple document conversion.

### ViewIdInVisibleCursorInvalidationCallback (`0x20`)

**Purpose:** Include active view's ID in cursor invalidation callbacks.

**Callback Payload:**

**Old format (disabled):**
```
"x, y, width, height"
```

**New format (enabled):**
```json
{
  "viewId": 123,
  "rectangle": "x, y, width, height",
  "misspelledWord": 0
}
```

**Use Case:** Multi-user collaborative editing to track which user's cursor moved.

## Important Notes

### Timing

⚠️ **Call `SetOptionalFeatures()` BEFORE loading any documents.**

```csharp
// Correct
var instance = Instance.Create(installPath);
instance.SetOptionalFeatures(OptionalFeatures.NoTiledAnnotations);
var doc = instance.DocumentLoad(fileUrl);

// Wrong - feature may not apply
var instance = Instance.Create(installPath);
var doc = instance.DocumentLoad(fileUrl);
instance.SetOptionalFeatures(OptionalFeatures.NoTiledAnnotations); // Too late!
```

### Version Requirements

All features require **LibreOffice 6.0 or later**.

If unavailable, an `InvalidOperationException` is thrown:

```csharp
try
{
	instance.SetOptionalFeatures(OptionalFeatures.NoTiledAnnotations);
}
catch (InvalidOperationException ex)
{
	logger.LogWarning("Optional features not supported: {Message}", ex.Message);
}
```

### Clearing Features

To disable all features:

```csharp
instance.SetOptionalFeatures(OptionalFeatures.None);
```

**Note:** This must still be called before loading documents.

## API Reference

### SetOptionalFeatures

```csharp
public void SetOptionalFeatures(OptionalFeatures features)
```

**Parameters:**
- `features`: Bitmask of features to enable (combine with `|`)

**Exceptions:**
- `ObjectDisposedException`: Instance has been disposed
- `InvalidOperationException`: Feature not available (LibreOffice < 6.0)

### SetDocumentPassword

```csharp
public void SetDocumentPassword(string url, string? password)
```

**Parameters:**
- `url`: Document URL in file:// format (use `Instance.PathToFileUrl()`)
- `password`: Password to use, or `null` to clear

**Requirements:**
- Must enable `OptionalFeatures.DocumentPassword` or `OptionalFeatures.DocumentPasswordToModify` first

## See Also

- [Instance API Reference](xref:LibreOfficeKit.Instance)
- [OptionalFeatures Enum](xref:LibreOfficeKit.Enums.OptionalFeatures)
- [Getting Started Guide](getting-started.md)
- [Detailed Optional Features Documentation](https://github.com/Sicos1977/LibreOfficeKit/blob/main/LibreOfficeKit/OPTIONAL_FEATURES.md)