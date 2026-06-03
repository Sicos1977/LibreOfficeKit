//
// OptionalFeatures.cs
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
///     Optional features that can be enabled in LibreOfficeKit.
///     Use <see cref="Instance.SetOptionalFeatures" /> to enable one or more flags.
/// </summary>
/// <remarks>
///     Verified against LibreOffice 26.2:
///     <see
///         href="https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26-2/include/LibreOfficeKit/LibreOfficeKitEnums.h">
///         LibreOfficeKitEnums.h
///     </see>
///     and
///     <see
///         href="https://raw.githubusercontent.com/LibreOffice/core/libreoffice-26-2/include/LibreOfficeKit/LibreOfficeKit.h">
///         LibreOfficeKit.h
///     </see>
///     .
/// </remarks>
[Flags]
public enum OptionalFeatures : ulong
{
    /// <summary>
    ///     No optional features enabled.
    /// </summary>
    None = 0,

    /// <summary>
    ///     Enables handling of password-protected documents.
    /// </summary>
    /// <remarks>
    ///     Corresponds to <c>LOK_FEATURE_DOCUMENT_PASSWORD</c>.
    ///     When enabled, LibreOfficeKit can emit a
    ///     <see cref="CallbackType.DocumentPassword" /> callback.
    /// </remarks>
    /// <seealso cref="Instance.SetDocumentPassword" />
    DocumentPassword = 1UL << 0,

    /// <summary>
    ///     Enables handling of passwords required to modify a write-protected document.
    /// </summary>
    /// <remarks>
    ///     Corresponds to <c>LOK_FEATURE_DOCUMENT_PASSWORD_TO_MODIFY</c>.
    ///     When enabled, LibreOfficeKit can emit a
    ///     <see cref="CallbackType.DocumentPasswordToModify" /> callback.
    /// </remarks>
    /// <seealso cref="Instance.SetDocumentPassword" />
    DocumentPasswordToModify = 1UL << 1,

    /// <summary>
    ///     Adds the part number as the 5th value in the tile invalidation callback payload.
    /// </summary>
    /// <remarks>
    ///     Corresponds to <c>LOK_FEATURE_PART_IN_INVALIDATION_CALLBACK</c>.
    ///     <para>Disabled format: <c>"x, y, width, height"</c></para>
    ///     <para>Enabled format: <c>"x, y, width, height, part"</c></para>
    /// </remarks>
    PartInInvalidationCallback = 1UL << 2,

    /// <summary>
    ///     Disables rendering of annotations in tiles.
    /// </summary>
    /// <remarks>
    ///     Corresponds to <c>LOK_FEATURE_NO_TILED_ANNOTATIONS</c>.
    ///     Useful for conversion or headless rendering scenarios.
    /// </remarks>
    NoTiledAnnotations = 1UL << 3,

    /// <summary>
    ///     Enables range-based spreadsheet row and column header retrieval.
    /// </summary>
    /// <remarks>
    ///     Corresponds to <c>LOK_FEATURE_RANGE_HEADERS</c>.
    ///     Useful for large spreadsheets and virtualized viewers.
    /// </remarks>
    RangeHeaders = 1UL << 4,

    /// <summary>
    ///     Adds the active view ID to visible cursor invalidation callback payloads.
    /// </summary>
    /// <remarks>
    ///     Corresponds to <c>LOK_FEATURE_VIEWID_IN_VISCURSOR_INVALIDATION_CALLBACK</c>.
    ///     <para>Disabled format: <c>"x, y, width, height"</c></para>
    ///     <para>
    ///         Enabled format:
    ///         <c>{"viewId": 123, "rectangle": "x, y, width, height", "misspelledWord": 0}</c>
    ///     </para>
    /// </remarks>
    ViewIdInVisibleCursorInvalidationCallback = 1UL << 5
}