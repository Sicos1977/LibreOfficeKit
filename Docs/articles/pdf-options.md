# PDF Options Guide

The `PdfOptions` class provides comprehensive control over PDF export settings. This guide covers all available options and best practices.

## Quick Start

### Using Presets

LibreOfficeKit includes built-in presets for common scenarios:

```csharp
// High quality archiving
await converter.ConvertToPdfAsync("input.docx", "output.pdf", 
	pdfOptions: PdfOptions.Archive);

// Screen viewing (smaller files)
await converter.ConvertToPdfAsync("input.docx", "output.pdf", 
	pdfOptions: PdfOptions.Screen);

// Print quality
await converter.ConvertToPdfAsync("input.docx", "output.pdf", 
	pdfOptions: PdfOptions.Print);

// Maximum quality
await converter.ConvertToPdfAsync("input.docx", "output.pdf", 
	pdfOptions: PdfOptions.HighQuality);
```

### Preset Specifications

| Preset | Compression | DPI | Tagged | PDF/A | Use Case |
|--------|-------------|-----|--------|-------|----------|
| `HighQuality` | Lossless | Full | Yes | No | Archiving, editing |
| `Screen` | JPEG 85% | 150 | No | No | Web viewing, email |
| `Print` | JPEG 90% | 300 | No | No | Printing |
| `Archive` | JPEG 90% | 300 | Yes | 2b | Long-term storage |

## Custom Options

### Image Quality

Control how images are compressed in the PDF:

```csharp
var options = new PdfOptions
{
	UseLosslessCompression = false,     // Use JPEG instead of PNG
	Quality = 95,                       // JPEG quality (1-100)
	ReduceImageResolution = true,       // Down-sample high-res images
	MaxImageResolutionDpi = 300         // Max DPI for images
};
```

**Recommendations:**
- **Print**: Quality 90-95, DPI 300
- **Screen**: Quality 75-85, DPI 150
- **Archive**: Lossless or Quality 95, Full DPI

### Content & Accessibility

```csharp
var options = new PdfOptions
{
	ExportBookmarks = true,      // Include table of contents
	ExportNotes = true,          // Include comments/annotations
	ExportFormFields = true,     // Interactive forms
	UseTaggedPdf = true,         // Accessibility (required for PDF/UA)
	SinglePageSheets = false     // Spreadsheet: one sheet per page
};
```

**Tagged PDF** is required for:
- PDF/UA accessibility compliance
- Screen reader compatibility
- Advanced text extraction

### Compliance & Version

```csharp
var options = new PdfOptions
{
	PdfACompliance = PdfACompliance.Level2b,  // PDF/A-2b archival
	PdfVersion = PdfVersion.PdfA2b            // Alternative: explicit version
};
```

**Available PDF/A Levels:**
- `None` - No PDF/A compliance
- `Level1b` - PDF/A-1b (ISO 19005-1:2005)
- `Level2b` - PDF/A-2b (ISO 19005-2:2011)
- `Level3b` - PDF/A-3b (ISO 19005-3:2012)

**Available PDF Versions:**
- `PDF14` - PDF 1.4 (Acrobat 5)
- `PDF15` - PDF 1.5 (Acrobat 6)
- `PDF16` - PDF 1.6 (Acrobat 7)
- `PDF17` - PDF 1.7 (Acrobat 8)
- `PdfA1b` - PDF/A-1b
- `PdfA2b` - PDF/A-2b
- `PdfUA1` - PDF/UA-1 (accessibility)

> **Note:** When `PdfVersion` is set, it takes precedence over `PdfACompliance`.

### Layout & Export Range

```csharp
var options = new PdfOptions
{
	PageRange = "1-10",                          // Export pages 1-10
	Watermark = "CONFIDENTIAL",                  // Watermark text
	InitialView = InitialView.Bookmarks          // Show bookmarks panel
};
```

**Page Range Syntax:**
- `"1-10"` - Pages 1 through 10
- `"2,4,6"` - Specific pages
- `"1-5,10-15"` - Multiple ranges
- `null` - All pages (default)

**Initial View Options:**
- `Default` - No side panel
- `Bookmarks` - Show bookmarks/outline
- `Thumbnails` - Show page thumbnails

## Security

### Simple Password Protection

```csharp
var options = new PdfOptions
{
	EncryptionPassword = "mySecretPassword"
};
```

This enables simple encryption and requires the password to open the PDF.

### Advanced Security

For full control over permissions and encryption:

```csharp
var options = new PdfOptions
{
	Security = new PdfSecurityOptions
	{
		EncryptFile = true,
		DocumentOpenPassword = "openPassword",
		RestrictPermissions = true,
		PermissionPassword = "permPassword",
		Printing = PdfPrintPermission.HighResolution,
		Changes = PdfChangePermission.OnlyComments,
		EnableCopyingOfContent = false,
		EnableTextAccessForAccessibilityTools = true
	}
};
```

**Print Permissions:**
- `NotPermitted` - No printing allowed
- `LowResolution` - Low-quality printing only
- `HighResolution` - Full-quality printing

**Change Permissions:**
- `NotPermitted` - No changes allowed
- `OnlyComments` - Only annotations
- `OnlyFormFields` - Only form data
- `OnlyPageInsertion` - Only insert pages
- `AnyExceptExtraction` - Any except extracting pages
- `Any` - All changes allowed

### Example: Watermarked Review Copy

```csharp
var options = new PdfOptions
{
	Watermark = "DRAFT - REVIEW ONLY",
	Security = new PdfSecurityOptions
	{
		EncryptFile = true,
		DocumentOpenPassword = "review2024",
		RestrictPermissions = true,
		PermissionPassword = "admin2024",
		Printing = PdfPrintPermission.LowResolution,
		Changes = PdfChangePermission.OnlyComments,
		EnableCopyingOfContent = false
	}
};
```

## Advanced Compression

Override compression per image type:

```csharp
var options = new PdfOptions
{
	Compression = new PdfCompressionOptions
	{
		UseLosslessCompression = false,
		Quality = 85,
		ReduceImageResolution = true,
		MaxImageResolution = 200
	}
};
```

This overrides the top-level compression settings.

## Complete Example

```csharp
using LibreOfficeKit;
using LibreOfficeKit.Enums;

await using var converter = new Converter(maxInstances: 4);

var options = new PdfOptions
{
	// Image quality
	Quality = 90,
	ReduceImageResolution = true,
	MaxImageResolutionDpi = 300,

	// Content
	ExportBookmarks = true,
	ExportNotes = false,
	UseTaggedPdf = true,

	// Compliance
	PdfVersion = PdfVersion.PdfA2b,

	// Layout
	PageRange = "1-100",
	Watermark = "© 2026 Company Name",
	InitialView = InitialView.Bookmarks,

	// Security
	Security = new PdfSecurityOptions
	{
		EncryptFile = true,
		DocumentOpenPassword = "viewer",
		RestrictPermissions = true,
		PermissionPassword = "editor",
		Printing = PdfPrintPermission.HighResolution,
		Changes = PdfChangePermission.OnlyComments,
		EnableCopyingOfContent = true,
		EnableTextAccessForAccessibilityTools = true
	}
};

await converter.ConvertToPdfAsync("document.docx", "document.pdf", pdfOptions: options);
```

## Spreadsheet-Specific Options

For spreadsheet conversions:

```csharp
var options = new PdfOptions
{
	SinglePageSheets = true   // Fit each sheet to one page
};
```

When `false` (default), sheets may span multiple PDF pages.

## Best Practices

### For Archiving

```csharp
var options = PdfOptions.Archive;
// Or customize:
options.PdfACompliance = PdfACompliance.Level3b;
options.UseTaggedPdf = true;
options.Quality = 95;
```

### For Web Distribution

```csharp
var options = PdfOptions.Screen;
// Or customize:
options.Quality = 80;
options.MaxImageResolutionDpi = 150;
options.ReduceImageResolution = true;
```

### For Printing

```csharp
var options = PdfOptions.Print;
// Or customize:
options.Quality = 92;
options.MaxImageResolutionDpi = 300;
```

### For Accessibility

```csharp
var options = new PdfOptions
{
	UseTaggedPdf = true,
	PdfVersion = PdfVersion.PdfUA1,  // PDF/UA accessibility standard
	ExportBookmarks = true,
	Security = new PdfSecurityOptions
	{
		EnableTextAccessForAccessibilityTools = true
	}
};
```

## See Also

- [PdfOptions API Reference](xref:LibreOfficeKit.Enums.PdfOptions)
- [PdfSecurityOptions API Reference](xref:LibreOfficeKit.Enums.PdfSecurityOptions)
- [Getting Started](getting-started.md)
- [Converter API Reference](xref:LibreOfficeKit.Converter)
