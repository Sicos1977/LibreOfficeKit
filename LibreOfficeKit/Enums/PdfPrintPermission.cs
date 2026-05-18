namespace LibreOfficeKit.Enums;

/// <summary>
///     Specifies the print permission level for a secured PDF document.
///     Maps to the LibreOffice <c>Printing</c> FilterData parameter.
/// </summary>
public enum PdfPrintPermission
{
    /// <summary>
    ///     Printing is not allowed. LibreOffice value: 0.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Only low-resolution (150 DPI) printing is allowed. LibreOffice value: 1.
    /// </summary>
    LowResolution = 1,

    /// <summary>
    ///     High-resolution printing is allowed (default). LibreOffice value: 2.
    /// </summary>
    HighResolution = 2
}