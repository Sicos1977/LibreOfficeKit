namespace LibreOfficeKit.Enums;

/// <summary>
///     Specifies what types of changes are allowed in a secured PDF document.
///     Maps to the LibreOffice <c>Changes</c> FilterData parameter.
/// </summary>
public enum PdfChangePermission
{
    /// <summary>
    ///     No changes are allowed to the document. LibreOffice value: 0.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Inserting, deleting, and rotating pages is allowed. LibreOffice value: 1.
    /// </summary>
    InsertDeletePages = 1,

    /// <summary>
    ///     Only filling in form fields is allowed. LibreOffice value: 2.
    /// </summary>
    FillFormFields = 2,

    /// <summary>
    ///     Filling in form fields and adding comments is allowed. LibreOffice value: 3.
    /// </summary>
    FillFormFieldsAndComment = 3,

    /// <summary>
    ///     All changes are allowed except page extraction (copying). LibreOffice value: 4.
    /// </summary>
    AllExceptExtraction = 4
}