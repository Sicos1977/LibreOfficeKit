//
// SaveFormat.cs
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