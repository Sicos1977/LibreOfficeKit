namespace LibreOfficeKit.Enums;

/// <summary>
///     Legacy PDF/A compliance level enum. Kept for backwards compatibility.
///     Prefer using <see cref="PdfVersion" /> for new code.
/// </summary>
public enum PdfACompliance
{
    /// <summary>
    ///     No PDF/A compliance — uses default PDF 1.7.
    /// </summary>
    None = 0,

    /// <summary>
    ///     PDF/A-1b (ISO 19005-1:2005).
    /// </summary>
    Level1b = 1,

    /// <summary>
    ///     PDF/A-2b (ISO 19005-2:2011).
    /// </summary>
    Level2b = 2,

    /// <summary>
    ///     PDF/A-3b (ISO 19005-3:2012).
    /// </summary>
    Level3b = 3
}