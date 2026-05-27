//
// PdfVersion.cs
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
///     Specifies the PDF version or PDF/A compliance level to use during export.
///     Maps to the LibreOffice <c>SelectPdfVersion</c> FilterData parameter.
/// </summary>
public enum PdfVersion
{
    /// <summary>
    ///     PDF 1.7 â€” the default, modern PDF version with full feature support.
    ///     LibreOffice value: 0.
    /// </summary>
    Pdf17 = 0,

    /// <summary>
    ///     PDF/A-1b â€” archival format conforming to ISO 19005-1:2005.
    ///     Ensures long-term preservation; most restrictive PDF/A level.
    ///     LibreOffice value: 1.
    /// </summary>
    PdfA1b = 1,

    /// <summary>
    ///     PDF/A-2b â€” archival format conforming to ISO 19005-2:2011.
    ///     Supports JPEG2000, transparency, and layers. Recommended for most archival needs.
    ///     LibreOffice value: 2.
    /// </summary>
    PdfA2b = 2,

    /// <summary>
    ///     PDF/A-3b â€” archival format conforming to ISO 19005-3:2012.
    ///     Allows arbitrary file attachments (e.g. original source documents).
    ///     LibreOffice value: 3.
    /// </summary>
    PdfA3b = 3,

    /// <summary>
    ///     PDF 1.5 â€” older version for compatibility with legacy readers.
    ///     LibreOffice value: 15.
    /// </summary>
    Pdf15 = 15,

    /// <summary>
    ///     PDF 1.6 â€” slightly older version, supports AES-128 encryption.
    ///     LibreOffice value: 16.
    /// </summary>
    Pdf16 = 16,

    /// <summary>
    ///     PDF/UA-1 â€” accessible PDF conforming to ISO 14289-1.
    ///     Requires tagged PDF structure for screen readers and assistive tools.
    ///     Note: This is mapped by combining <c>SelectPdfVersion=0</c> with <c>PDFUACompliance=true</c>.
    ///     LibreOffice value: 0 (with PDFUACompliance flag).
    /// </summary>
    PdfUA1 = 100
}