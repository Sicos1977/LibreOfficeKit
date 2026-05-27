//
// PdfChangePermission.cs
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