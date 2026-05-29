//
// CallbackType.cs
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

// ReSharper disable UnusedMember.Global
namespace LibreOfficeKit.Enums;

/// <summary>
///     LibreOfficeKit callback event types.
///     These are the event types that can be received via the LOK callback mechanism.
/// </summary>
public enum CallbackType
{
    /// <summary>
    ///     Invalidation of a tile or region.
    /// </summary>
    InvalidateTiles = 0,

    /// <summary>
    ///     Invalidation of visible cursor.
    /// </summary>
    InvalidateVisibleCursor = 1,

    /// <summary>
    ///     Text selection changed.
    /// </summary>
    TextSelection = 2,

    /// <summary>
    ///     Text selection start position.
    /// </summary>
    TextSelectionStart = 3,

    /// <summary>
    ///     Text selection end position.
    /// </summary>
    TextSelectionEnd = 4,

    /// <summary>
    ///     Cursor position changed.
    /// </summary>
    CursorVisible = 5,

    /// <summary>
    ///     Graphic selection.
    /// </summary>
    GraphicSelection = 6,

    /// <summary>
    ///     Hyperlink clicked.
    /// </summary>
    HyperlinkClicked = 7,

    /// <summary>
    ///     State changed (e.g., bold, italic).
    /// </summary>
    StateChanged = 8,

    /// <summary>
    ///     Status indicator start.
    /// </summary>
    StatusIndicatorStart = 9,

    /// <summary>
    ///     Status indicator set value.
    /// </summary>
    StatusIndicatorSetValue = 10,

    /// <summary>
    ///     Status indicator finish.
    /// </summary>
    StatusIndicatorFinish = 11,

    /// <summary>
    ///     Search not found.
    /// </summary>
    SearchNotFound = 12,

    /// <summary>
    ///     Document size changed.
    /// </summary>
    DocumentSizeChanged = 13,

    /// <summary>
    ///     Set part.
    /// </summary>
    SetPart = 14,

    /// <summary>
    ///     Search result selection.
    /// </summary>
    SearchResultSelection = 15,

    /// <summary>
    ///     UNO command result.
    /// </summary>
    UnoCommandResult = 16,

    /// <summary>
    ///     Cell cursor.
    /// </summary>
    CellCursor = 17,

    /// <summary>
    ///     Mouse pointer.
    /// </summary>
    MousePointer = 18,

    /// <summary>
    ///     Cell formula.
    /// </summary>
    CellFormula = 19,

    /// <summary>
    ///     Document password required.
    /// </summary>
    DocumentPassword = 20,

    /// <summary>
    ///     Document password to modify.
    /// </summary>
    DocumentPasswordToModify = 21,

    /// <summary>
    ///     Error occurred.
    /// </summary>
    Error = 22,

    /// <summary>
    ///     Context menu.
    /// </summary>
    ContextMenu = 23,

    /// <summary>
    ///     Invalidate view cursor.
    /// </summary>
    InvalidateViewCursor = 24,

    /// <summary>
    ///     Text view selection.
    /// </summary>
    TextViewSelection = 25,

    /// <summary>
    ///     Cell view cursor.
    /// </summary>
    CellViewCursor = 26,

    /// <summary>
    ///     Graphic view selection.
    /// </summary>
    GraphicViewSelection = 27,

    /// <summary>
    ///     View cursor visible.
    /// </summary>
    ViewCursorVisible = 28,

    /// <summary>
    ///     View lock.
    /// </summary>
    ViewLock = 29,

    /// <summary>
    ///     Redline table size changed.
    /// </summary>
    RedlineTableSizeChanged = 30,

    /// <summary>
    ///     Redline table entry modified.
    /// </summary>
    RedlineTableEntryModified = 31,

    /// <summary>
    ///     Comment.
    /// </summary>
    Comment = 32,

    /// <summary>
    ///     Invalidate header.
    /// </summary>
    InvalidateHeader = 33,

    /// <summary>
    ///     Cell address.
    /// </summary>
    CellAddress = 34,

    /// <summary>
    ///     Ruler update.
    /// </summary>
    RulerUpdate = 35,

    /// <summary>
    ///     Window.
    /// </summary>
    Window = 36,

    /// <summary>
    ///     Validity list button.
    /// </summary>
    ValidityListButton = 37,

    /// <summary>
    ///     Clipboard changed.
    /// </summary>
    ClipboardChanged = 38,

    /// <summary>
    ///     Context changed.
    /// </summary>
    ContextChanged = 39,

    /// <summary>
    ///     Signature status.
    /// </summary>
    SignatureStatus = 40,

    /// <summary>
    ///     Profile frame.
    /// </summary>
    ProfileFrame = 41,

    /// <summary>
    ///     Cell selection area.
    /// </summary>
    CellSelectionArea = 42,

    /// <summary>
    ///     Cell auto fill area.
    /// </summary>
    CellAutoFillArea = 43,

    /// <summary>
    ///     Table selected.
    /// </summary>
    TableSelected = 44,

    /// <summary>
    ///     Reference marks.
    /// </summary>
    ReferenceMarks = 45,

    /// <summary>
    ///     Jsdialog.
    /// </summary>
    Jsdialog = 46,

    /// <summary>
    ///     Calc function list.
    /// </summary>
    CalcFunctionList = 47,

    /// <summary>
    ///     Tab stop list update.
    /// </summary>
    TabStopListUpdate = 48,

    /// <summary>
    ///     Form field button.
    /// </summary>
    FormFieldButton = 49,

    /// <summary>
    ///     Invalidate sheet geometry.
    /// </summary>
    InvalidateSheetGeometry = 50,

    /// <summary>
    ///     Document background color.
    /// </summary>
    DocumentBackgroundColor = 51,

    /// <summary>
    ///     A11y focus changed.
    /// </summary>
    A11YFocusChanged = 52,

    /// <summary>
    ///     A11y caret changed.
    /// </summary>
    A11YCaretChanged = 53,

    /// <summary>
    ///     A11y text selection changed.
    /// </summary>
    A11YTextSelectionChanged = 54,

    /// <summary>
    ///     A11y editing in selection state.
    /// </summary>
    A11YEditingInSelectionState = 55,

    /// <summary>
    ///     A11y selection changed.
    /// </summary>
    A11YSelectionChanged = 56,

    /// <summary>
    ///     Fonts missing.
    /// </summary>
    FontsMissing = 57,

    /// <summary>
    ///     Media shape.
    /// </summary>
    MediaShape = 58,

    /// <summary>
    ///     Content control.
    /// </summary>
    ContentControl = 59,

    /// <summary>
    ///     Export file.
    /// </summary>
    ExportFile = 60,

    /// <summary>
    ///     View render state.
    /// </summary>
    ViewRenderState = 61,

    /// <summary>
    ///     Application background color.
    /// </summary>
    ApplicationBackgroundColor = 62,

    /// <summary>
    ///     Print ranges.
    /// </summary>
    PrintRanges = 63
}