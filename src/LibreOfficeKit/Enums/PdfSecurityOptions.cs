namespace LibreOfficeKit.Enums;

/// <summary>
///     Security and permission settings for PDF export.
///     Controls document open passwords, permission passwords, and fine-grained
///     access restrictions such as printing, editing, and content copying.
/// </summary>
public class PdfSecurityOptions
{
    #region Properties
    /// <summary>
    ///     Gets or sets the password required to open the PDF document.
    ///     Only effective when <see cref="EncryptFile" /> is <c>true</c>.
    /// </summary>
    public string? DocumentOpenPassword { get; set; }

    /// <summary>
    ///     Gets or sets whether to encrypt the PDF file.
    ///     When <c>true</c>, the PDF can only be opened with the <see cref="DocumentOpenPassword" />.
    /// </summary>
    public bool EncryptFile { get; set; }

    /// <summary>
    ///     Gets or sets the password required to change document permissions.
    ///     Only effective when <see cref="RestrictPermissions" /> is <c>true</c>.
    /// </summary>
    public string? PermissionPassword { get; set; }

    /// <summary>
    ///     Gets or sets whether PDF permissions should be restricted.
    /// </summary>
    public bool RestrictPermissions { get; set; }

    /// <summary>
    ///     Gets or sets the printing permission level for the PDF document.
    ///     Only effective when <see cref="RestrictPermissions" /> is <c>true</c>.
    /// </summary>
    public PdfPrintPermission? Printing { get; set; }

    /// <summary>
    ///     Gets or sets what types of changes are allowed to the document.
    ///     Only effective when <see cref="RestrictPermissions" /> is <c>true</c>.
    /// </summary>
    public PdfChangePermission? Changes { get; set; }

    /// <summary>
    ///     Gets or sets whether content can be copied from the PDF document.
    ///     Only effective when <see cref="RestrictPermissions" /> is <c>true</c>.
    /// </summary>
    public bool? EnableCopyingOfContent { get; set; }

    /// <summary>
    ///     Gets or sets whether text can be extracted for accessibility tools.
    ///     Only effective when <see cref="RestrictPermissions" /> is <c>true</c>.
    /// </summary>
    public bool? EnableTextAccessForAccessibilityTools { get; set; }
    #endregion

    #region Validate
    /// <summary>
    ///     Validates the security options and throws if the configuration is invalid.
    /// </summary>
    /// <exception cref="ArgumentException">
    ///     Thrown when EncryptFile is true but no DocumentOpenPassword is provided,
    ///     or when RestrictPermissions is true but no PermissionPassword is provided.
    /// </exception>
    internal void Validate()
    {
        if (EncryptFile && string.IsNullOrEmpty(DocumentOpenPassword))
            throw new ArgumentException(
                "DocumentOpenPassword is required when EncryptFile is true.",
                nameof(DocumentOpenPassword));

        if (RestrictPermissions && string.IsNullOrEmpty(PermissionPassword))
            throw new ArgumentException(
                "PermissionPassword is required when RestrictPermissions is true.",
                nameof(PermissionPassword));
    }
    #endregion
}