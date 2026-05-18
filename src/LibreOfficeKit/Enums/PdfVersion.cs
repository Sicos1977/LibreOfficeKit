namespace LibreOfficeKit.Enums;

/// <summary>
///     Specifies the PDF version or PDF/A compliance level to use during export.
///     Maps to the LibreOffice <c>SelectPdfVersion</c> FilterData parameter.
/// </summary>
public enum PdfVersion
{
    /// <summary>
    ///     PDF 1.7 — the default, modern PDF version with full feature support.
    ///     LibreOffice value: 0.
    /// </summary>
    Pdf17 = 0,

    /// <summary>
    ///     PDF/A-1b — archival format conforming to ISO 19005-1:2005.
    ///     Ensures long-term preservation; most restrictive PDF/A level.
    ///     LibreOffice value: 1.
    /// </summary>
    PdfA1b = 1,

    /// <summary>
    ///     PDF/A-2b — archival format conforming to ISO 19005-2:2011.
    ///     Supports JPEG2000, transparency, and layers. Recommended for most archival needs.
    ///     LibreOffice value: 2.
    /// </summary>
    PdfA2b = 2,

    /// <summary>
    ///     PDF/A-3b — archival format conforming to ISO 19005-3:2012.
    ///     Allows arbitrary file attachments (e.g. original source documents).
    ///     LibreOffice value: 3.
    /// </summary>
    PdfA3b = 3,

    /// <summary>
    ///     PDF 1.5 — older version for compatibility with legacy readers.
    ///     LibreOffice value: 15.
    /// </summary>
    Pdf15 = 15,

    /// <summary>
    ///     PDF 1.6 — slightly older version, supports AES-128 encryption.
    ///     LibreOffice value: 16.
    /// </summary>
    Pdf16 = 16,

    /// <summary>
    ///     PDF/UA-1 — accessible PDF conforming to ISO 14289-1.
    ///     Requires tagged PDF structure for screen readers and assistive tools.
    ///     Note: This is mapped by combining <c>SelectPdfVersion=0</c> with <c>PDFUACompliance=true</c>.
    ///     LibreOffice value: 0 (with PDFUACompliance flag).
    /// </summary>
    PdfUA1 = 100
}