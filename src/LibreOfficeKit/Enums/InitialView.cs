namespace LibreOfficeKit.Enums;

/// <summary>
///     Specifies how the PDF viewer panel is displayed when the document is opened.
///     Maps to the LibreOffice <c>InitialView</c> FilterData parameter.
/// </summary>
public enum InitialView
{
    /// <summary>
    ///     Open with the default viewer mode — no panel shown.
    /// </summary>
    Default = 0,

    /// <summary>
    ///     Open with the bookmarks/outlines panel visible.
    /// </summary>
    Outlines = 1,

    /// <summary>
    ///     Open with the page thumbnails panel visible.
    /// </summary>
    Thumbnails = 2
}