//
// SaveFormatExtensions.cs
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
///     Extension methods for <see cref="SaveFormat" />.
/// </summary>
internal static class SaveFormatExtensions
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