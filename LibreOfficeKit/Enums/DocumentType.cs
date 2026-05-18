namespace LibreOfficeKit.Enums;

/// <summary>
///     Specifies the type of a loaded LibreOffice document.
///     Maps to the integer returned by <c>getDocumentType()</c> in the LOK API.
/// </summary>
public enum DocumentType
{
    /// <summary>
    ///     Text document (Writer).
    /// </summary>
    Text = 0,

    /// <summary>
    ///     Spreadsheet document (Calc).
    /// </summary>
    Spreadsheet = 1,

    /// <summary>
    ///     Presentation document (Impress).
    /// </summary>
    Presentation = 2,

    /// <summary>
    ///     Drawing document (Draw).
    /// </summary>
    Drawing = 3,

    /// <summary>
    ///     Other/unknown document type.
    /// </summary>
    Other = 4
}