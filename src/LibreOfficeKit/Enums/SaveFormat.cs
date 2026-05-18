namespace LibreOfficeKit.Enums;

/// <summary>
///     Specifies the output format for document conversion via LibreOfficeKit.
///     These values map to the format string passed to the LOK <c>saveAs</c> function.
/// </summary>
public enum SaveFormat
{
    /// <summary>
    ///     Portable Document Format (PDF).
    /// </summary>
    Pdf,

    /// <summary>
    ///     Microsoft Word Open XML Document (.docx).
    /// </summary>
    Docx,

    /// <summary>
    ///     Microsoft Excel Open XML Spreadsheet (.xlsx).
    /// </summary>
    Xlsx,

    /// <summary>
    ///     Microsoft PowerPoint Open XML Presentation (.pptx).
    /// </summary>
    Pptx,

    /// <summary>
    ///     Open Document Text (.odt).
    /// </summary>
    Odt,

    /// <summary>
    ///     Open Document Spreadsheet (.ods).
    /// </summary>
    Ods,

    /// <summary>
    ///     Open Document Presentation (.odp).
    /// </summary>
    Odp,

    /// <summary>
    ///     Rich Text Format (.rtf).
    /// </summary>
    Rtf,

    /// <summary>
    ///     Plain Text (.txt).
    /// </summary>
    Txt,

    /// <summary>
    ///     HTML (.html).
    /// </summary>
    Html,

    /// <summary>
    ///     Portable Network Graphics (.png).
    /// </summary>
    Png,

    /// <summary>
    ///     JPEG image (.jpg).
    /// </summary>
    Jpg,

    /// <summary>
    ///     Scalable Vector Graphics (.svg).
    /// </summary>
    Svg,

    /// <summary>
    ///     Enhanced Postscript (.eps).
    /// </summary>
    Eps
}

/// <summary>
///     Extension methods for <see cref="SaveFormat" />.
/// </summary>
public static class SaveFormatExtensions
{
    #region ToFormatString
    /// <summary>
    ///     Converts the <see cref="SaveFormat" /> enum value to the format string expected by the LOK <c>saveAs</c> API.
    /// </summary>
    /// <param name="format">The save format.</param>
    /// <returns>The LOK format string (e.g. <c>"pdf"</c>, <c>"docx"</c>).</returns>
    public static string ToFormatString(this SaveFormat format)
    {
        return format switch
        {
            SaveFormat.Pdf => "pdf",
            SaveFormat.Docx => "docx",
            SaveFormat.Xlsx => "xlsx",
            SaveFormat.Pptx => "pptx",
            SaveFormat.Odt => "odt",
            SaveFormat.Ods => "ods",
            SaveFormat.Odp => "odp",
            SaveFormat.Rtf => "rtf",
            SaveFormat.Txt => "txt",
            SaveFormat.Html => "html",
            SaveFormat.Png => "png",
            SaveFormat.Jpg => "jpg",
            SaveFormat.Svg => "svg",
            SaveFormat.Eps => "eps",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported save format.")
        };
    }
    #endregion

    #region ToFileExtension
    /// <summary>
    ///     Converts the <see cref="SaveFormat" /> enum value to the corresponding file extension (including the dot).
    /// </summary>
    /// <param name="format">The save format.</param>
    /// <returns>The file extension (e.g. <c>".pdf"</c>, <c>".docx"</c>).</returns>
    public static string ToFileExtension(this SaveFormat format)
    {
        return format switch
        {
            SaveFormat.Pdf => ".pdf",
            SaveFormat.Docx => ".docx",
            SaveFormat.Xlsx => ".xlsx",
            SaveFormat.Pptx => ".pptx",
            SaveFormat.Odt => ".odt",
            SaveFormat.Ods => ".ods",
            SaveFormat.Odp => ".odp",
            SaveFormat.Rtf => ".rtf",
            SaveFormat.Txt => ".txt",
            SaveFormat.Html => ".html",
            SaveFormat.Png => ".png",
            SaveFormat.Jpg => ".jpg",
            SaveFormat.Svg => ".svg",
            SaveFormat.Eps => ".eps",
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported save format.")
        };
    }
    #endregion
}