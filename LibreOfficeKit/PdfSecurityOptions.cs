//
// PdfSecurityOptions.cs
//
// Author: Kees van Spelde <sicos2002@hotmail.com>
//
// Copyright (c) 2026 Kees van Spelde. (www.magic-sessions.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NON INFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//
using LibreOfficeKit.Enums;

namespace LibreOfficeKit;

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