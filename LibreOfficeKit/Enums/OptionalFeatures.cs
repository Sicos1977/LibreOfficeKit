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
///     These features modify callback behavior and rendering options.
///     Use <see cref="Instance.SetOptionalFeatures"/> to enable features.
/// </summary>
/// <remarks>
///     Features can be combined using bitwise OR operations.
///     Example: <c>OptionalFeatures.DocumentPassword | OptionalFeatures.NoTiledAnnotations</c>
/// </remarks>
[Flags]
public enum OptionalFeatures : ulong
{
    /// <summary>
    ///     No optional features enabled (default).
    /// </summary>
    None = 0,

    /// <summary>
    ///     Handle <see cref="CallbackType.DocumentPassword"/> by prompting the user
    ///     for a password.
    ///     <para>
    ///         When enabled, LibreOfficeKit will emit a callback when a password-protected
    ///         document is encountered, allowing the client to provide credentials.
    ///     </para>
    /// </summary>
    /// <seealso cref="Instance.SetDocumentPassword"/>
    DocumentPassword = 1UL << 0,

    /// <summary>
    ///     Handle <see cref="CallbackType.DocumentPasswordToModify"/> by prompting
    ///     the user for a password to modify a write-protected document.
    ///     <para>
    ///         Similar to <see cref="DocumentPassword"/>, but for documents that require
    ///         a password to enable editing (not just opening).
    ///     </para>
    /// </summary>
    /// <seealso cref="Instance.SetDocumentPassword"/>
    DocumentPasswordToModify = 1UL << 1,

    /// <summary>
    ///     Request to have the part number as a 5th value in the
    ///     <see cref="CallbackType.InvalidateTiles"/> payload.
    ///     <para>
    ///         When enabled, tile invalidation callbacks will include the part (page/sheet/slide)
    ///         number that was invalidated. Useful for multi-part documents.
    ///     </para>
    ///     <para>
    ///         Format: "x, y, width, height, part"
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     Without this feature, the callback payload is: "x, y, width, height"
    /// </remarks>
    PartInInvalidationCallback = 1UL << 2,

    /// <summary>
    ///     Turn off tile rendering for annotations (comments, tracked changes, etc.).
    ///     <para>
    ///         When enabled, annotations will not be rendered in tiles, improving performance
    ///         for document conversion scenarios where annotations are not needed.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     Useful for headless conversion where annotations are not displayed or are
    ///     exported separately (e.g., PDF comments).
    /// </remarks>
    NoTiledAnnotations = 1UL << 3,

    /// <summary>
    ///     Enable range-based header data for spreadsheets.
    ///     <para>
    ///         When enabled, spreadsheet column/row headers can be queried for specific
    ///         ranges rather than the entire sheet, improving performance for large sheets.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     Primarily useful for interactive spreadsheet viewers with virtual scrolling.
    ///     Not typically needed for document conversion.
    /// </remarks>
    RangeHeaders = 1UL << 4,

    /// <summary>
    ///     Request to have the active view's ID as the 1st value in the
    ///     <see cref="CallbackType.InvalidateVisibleCursor"/> payload.
    ///     <para>
    ///         When enabled, visible cursor invalidation callbacks will include the view ID,
    ///         allowing multi-user collaborative editing scenarios to track which user's
    ///         cursor moved.
    ///     </para>
    ///     <para>
    ///         Format: JSON with viewId, rectangle, and misspelledWord fields.
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para><strong>Old format (feature disabled):</strong></para>
    ///     <code>"x, y, width, height"</code>
    ///     <para><strong>New format (feature enabled):</strong></para>
    ///     <code>{"viewId": 123, "rectangle": "x, y, width, height", "misspelledWord": 0}</code>
    /// </remarks>
    ViewIdInVisibleCursorInvalidationCallback = 1UL << 5
}
