//
// PdfACompliance.cs
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
///     Legacy PDF/A compliance level enum. Kept for backwards compatibility.
///     Prefer using <see cref="PdfVersion" /> for new code.
/// </summary>
public enum PdfACompliance
{
    /// <summary>
    ///     No PDF/A compliance â€” uses default PDF 1.7.
    /// </summary>
    None = 0,

    /// <summary>
    ///     PDF/A-1b (ISO 19005-1:2005).
    /// </summary>
    Level1B = 1,

    /// <summary>
    ///     PDF/A-2b (ISO 19005-2:2011).
    /// </summary>
    Level2B = 2,

    /// <summary>
    ///     PDF/A-3b (ISO 19005-3:2012).
    /// </summary>
    Level3B = 3
}