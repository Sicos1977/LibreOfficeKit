//
// LibreOfficeKitDocumentClass.cs
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
using System.Runtime.InteropServices;

namespace LibreOfficeKit.Bindings;

/// <summary>
///     Represents the LibreOfficeKitDocument vtable containing function pointers for document-level operations.
///     Field order must match the native C header exactly (LibreOfficeKit.h, master branch).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LibreOfficeKitDocumentClass
{
    /// <summary>Size of this struct in bytes.</summary>
    public nuint nSize;

    /// <summary>
    ///     Function pointer: <c>void (*destroy)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr destroy;

    /// <summary>
    ///     Function pointer: <c>int (*saveAs)(LibreOfficeKitDocument* pThis, const char* pUrl, const char* pFormat, const char* pFilterOptions)</c>.
    /// </summary>
    public IntPtr saveAs;

    /// <summary>
    ///     Function pointer: <c>int (*getDocumentType)(LibreOfficeKitDocument* pThis)</c>.
    ///     Returns 0 = text, 1 = spreadsheet, 2 = presentation, 3 = drawing, 4 = other.
    ///     @since LibreOffice 6.0
    /// </summary>
    public IntPtr getDocumentType;

    // --- LOK_USE_UNSTABLE_API ---

    /// <summary>
    ///     Function pointer: <c>int (*getParts)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getParts;

    /// <summary>
    ///     Function pointer: <c>char* (*getPartPageRectangles)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getPartPageRectangles;

    /// <summary>
    ///     Function pointer: <c>int (*getPart)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getPart;

    /// <summary>
    ///     Function pointer: <c>void (*setPart)(LibreOfficeKitDocument* pThis, int nPart)</c>.
    /// </summary>
    public IntPtr setPart;

    /// <summary>
    ///     Function pointer: <c>char* (*getPartName)(LibreOfficeKitDocument* pThis, int nPart)</c>.
    /// </summary>
    public IntPtr getPartName;

    /// <summary>
    ///     Function pointer: <c>void (*setPartMode)(LibreOfficeKitDocument* pThis, int nMode)</c>.
    /// </summary>
    public IntPtr setPartMode;

    /// <summary>
    ///     Function pointer: <c>void (*paintTile)(LibreOfficeKitDocument* pThis, unsigned char* pBuffer,
    ///     const int nCanvasWidth, const int nCanvasHeight,
    ///     const int nTilePosX, const int nTilePosY,
    ///     const int nTileWidth, const int nTileHeight)</c>.
    /// </summary>
    public IntPtr paintTile;

    /// <summary>
    ///     Function pointer: <c>int (*getTileMode)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getTileMode;

    /// <summary>
    ///     Function pointer: <c>void (*getDocumentSize)(LibreOfficeKitDocument* pThis, long* pWidth, long* pHeight)</c>.
    /// </summary>
    public IntPtr getDocumentSize;

    /// <summary>
    ///     Function pointer: <c>void (*initializeForRendering)(LibreOfficeKitDocument* pThis, const char* pArguments)</c>.
    /// </summary>
    public IntPtr initializeForRendering;

    /// <summary>
    ///     Function pointer: <c>void (*registerCallback)(LibreOfficeKitDocument* pThis, LibreOfficeKitCallback pCallback, void* pData)</c>.
    /// </summary>
    public IntPtr registerCallback;

    /// <summary>
    ///     Function pointer: <c>void (*postKeyEvent)(LibreOfficeKitDocument* pThis, int nType, int nCharCode, int nKeyCode)</c>.
    /// </summary>
    public IntPtr postKeyEvent;

    /// <summary>
    ///     Function pointer: <c>void (*postMouseEvent)(LibreOfficeKitDocument* pThis, int nType, int nX, int nY, int nCount, int nButtons, int nModifier)</c>.
    /// </summary>
    public IntPtr postMouseEvent;

    /// <summary>
    ///     Function pointer: <c>void (*postUnoCommand)(LibreOfficeKitDocument* pThis, const char* pCommand, const char* pArguments, bool bNotifyWhenFinished)</c>.
    /// </summary>
    public IntPtr postUnoCommand;

    /// <summary>
    ///     Function pointer: <c>void (*setTextSelection)(LibreOfficeKitDocument* pThis, int nType, int nX, int nY)</c>.
    /// </summary>
    public IntPtr setTextSelection;

    /// <summary>
    ///     Function pointer: <c>char* (*getTextSelection)(LibreOfficeKitDocument* pThis, const char* pMimeType, char** pUsedMimeType)</c>.
    /// </summary>
    public IntPtr getTextSelection;

    /// <summary>
    ///     Function pointer: <c>bool (*paste)(LibreOfficeKitDocument* pThis, const char* pMimeType, const char* pData, size_t nSize)</c>.
    /// </summary>
    public IntPtr paste;

    /// <summary>
    ///     Function pointer: <c>void (*setGraphicSelection)(LibreOfficeKitDocument* pThis, int nType, int nX, int nY)</c>.
    /// </summary>
    public IntPtr setGraphicSelection;

    /// <summary>
    ///     Function pointer: <c>void (*resetSelection)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr resetSelection;

    /// <summary>
    ///     Function pointer: <c>char* (*getCommandValues)(LibreOfficeKitDocument* pThis, const char* pCommand)</c>.
    /// </summary>
    public IntPtr getCommandValues;

    /// <summary>
    ///     Function pointer: <c>void (*setClientZoom)(LibreOfficeKitDocument* pThis, int nTilePixelWidth, int nTilePixelHeight, int nTileTwipWidth, int nTileTwipHeight)</c>.
    /// </summary>
    public IntPtr setClientZoom;

    /// <summary>
    ///     Function pointer: <c>void (*setClientVisibleArea)(LibreOfficeKitDocument* pThis, int nX, int nY, int nWidth, int nHeight)</c>.
    /// </summary>
    public IntPtr setClientVisibleArea;

    /// <summary>
    ///     Function pointer: <c>int (*createView)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr createView;

    /// <summary>
    ///     Function pointer: <c>void (*destroyView)(LibreOfficeKitDocument* pThis, int nId)</c>.
    /// </summary>
    public IntPtr destroyView;

    /// <summary>
    ///     Function pointer: <c>void (*setView)(LibreOfficeKitDocument* pThis, int nId)</c>.
    /// </summary>
    public IntPtr setView;

    /// <summary>
    ///     Function pointer: <c>int (*getView)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getView;

    /// <summary>
    ///     Function pointer: <c>int (*getViewsCount)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getViewsCount;

    /// <summary>
    ///     Function pointer: <c>unsigned char* (*renderFont)(LibreOfficeKitDocument* pThis, const char* pFontName,
    ///     const char* pChar, int* pFontWidth, int* pFontHeight)</c>.
    /// </summary>
    public IntPtr renderFont;

    /// <summary>
    ///     Function pointer: <c>char* (*getPartHash)(LibreOfficeKitDocument* pThis, int nPart)</c>.
    /// </summary>
    public IntPtr getPartHash;

    /// <summary>
    ///     Function pointer: <c>void (*paintPartTile)(LibreOfficeKitDocument* pThis, unsigned char* pBuffer,
    ///     const int nPart, const int nMode, const int nCanvasWidth, const int nCanvasHeight,
    ///     const int nTilePosX, const int nTilePosY, const int nTileWidth, const int nTileHeight)</c>.
    /// </summary>
    public IntPtr paintPartTile;

    /// <summary>
    ///     Function pointer: <c>bool (*getViewIds)(LibreOfficeKitDocument* pThis, int* pArray, size_t nSize)</c>.
    /// </summary>
    public IntPtr getViewIds;

    /// <summary>
    ///     Function pointer: <c>void (*setOutlineState)(LibreOfficeKitDocument* pThis, bool bColumn, int nLevel, int nIndex, bool bHidden)</c>.
    /// </summary>
    public IntPtr setOutlineState;

    /// <summary>
    ///     Function pointer: <c>void (*paintWindow)(LibreOfficeKitDocument* pThis, unsigned nWindowId,
    ///     unsigned char* pBuffer, const int x, const int y, const int width, const int height)</c>.
    /// </summary>
    public IntPtr paintWindow;

    /// <summary>
    ///     Function pointer: <c>void (*postWindow)(LibreOfficeKitDocument* pThis, unsigned nWindowId, int nAction, const char* pData)</c>.
    /// </summary>
    public IntPtr postWindow;

    /// <summary>
    ///     Function pointer: <c>void (*postWindowKeyEvent)(LibreOfficeKitDocument* pThis, unsigned nWindowId,
    ///     int nType, int nCharCode, int nKeyCode)</c>.
    /// </summary>
    public IntPtr postWindowKeyEvent;

    /// <summary>
    ///     Function pointer: <c>void (*postWindowMouseEvent)(LibreOfficeKitDocument* pThis, unsigned nWindowId,
    ///     int nType, int nX, int nY, int nCount, int nButtons, int nModifier)</c>.
    /// </summary>
    public IntPtr postWindowMouseEvent;

    /// <summary>
    ///     Function pointer: <c>void (*setViewLanguage)(LibreOfficeKitDocument* pThis, int nId, const char* language)</c>.
    /// </summary>
    public IntPtr setViewLanguage;

    /// <summary>
    ///     Function pointer: <c>void (*postWindowExtTextInputEvent)(LibreOfficeKitDocument* pThis,
    ///     unsigned nWindowId, int nType, const char* pText)</c>.
    /// </summary>
    public IntPtr postWindowExtTextInputEvent;

    /// <summary>
    ///     Function pointer: <c>char* (*getPartInfo)(LibreOfficeKitDocument* pThis, int nPart)</c>.
    /// </summary>
    public IntPtr getPartInfo;

    /// <summary>
    ///     Function pointer: <c>void (*paintWindowDPI)(LibreOfficeKitDocument* pThis, unsigned nWindowId,
    ///     unsigned char* pBuffer, const int x, const int y, const int width, const int height, const double dpi scale)</c>.
    /// </summary>
    public IntPtr paintWindowDPI;

    /// <summary>
    ///     Function pointer: <c>bool (*insertCertificate)(LibreOfficeKitDocument* pThis,
    ///     const unsigned char* pCertificateBinary, const int nCertificateBinarySize,
    ///     const unsigned char* pPrivateKeyBinary, const int nPrivateKeyBinarySize)</c>.
    /// </summary>
    public IntPtr insertCertificate;

    /// <summary>
    ///     Function pointer: <c>bool (*addCertificate)(LibreOfficeKitDocument* pThis,
    ///     const unsigned char* pCertificateBinary, const int nCertificateBinarySize)</c>.
    /// </summary>
    public IntPtr addCertificate;

    /// <summary>
    ///     Function pointer: <c>int (*getSignatureState)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getSignatureState;

    /// <summary>
    ///     Function pointer: <c>size_t (*renderShapeSelection)(LibreOfficeKitDocument* pThis, char** pOutput)</c>.
    /// </summary>
    public IntPtr renderShapeSelection;

    /// <summary>
    ///     Function pointer: <c>void (*postWindowGestureEvent)(LibreOfficeKitDocument* pThis, unsigned nWindowId,
    ///     const char* pType, int nX, int nY, int nOffset)</c>.
    /// </summary>
    public IntPtr postWindowGestureEvent;

    /// <summary>
    ///     Function pointer: <c>int (*createViewWithOptions)(LibreOfficeKitDocument* pThis, const char* pOptions)</c>.
    /// </summary>
    public IntPtr createViewWithOptions;

    /// <summary>
    ///     Function pointer: <c>void (*selectPart)(LibreOfficeKitDocument* pThis, int nPart, int nSelect)</c>.
    /// </summary>
    public IntPtr selectPart;

    /// <summary>
    ///     Function pointer: <c>void (*moveSelectedParts)(LibreOfficeKitDocument* pThis, int nPosition, bool bDuplicate)</c>.
    /// </summary>
    public IntPtr moveSelectedParts;

    /// <summary>
    ///     Function pointer: <c>void (*resizeWindow)(LibreOfficeKitDocument* pThis, unsigned nWindowId, const int width, const int height)</c>.
    /// </summary>
    public IntPtr resizeWindow;

    /// <summary>
    ///     Function pointer: <c>int (*getClipboard)(LibreOfficeKitDocument* pThis, const char** pMimeTypes,
    ///     size_t* pOutCount, char*** pOutMimeTypes, size_t** pOutSizes, char*** pOutStreams)</c>.
    /// </summary>
    public IntPtr getClipboard;

    /// <summary>
    ///     Function pointer: <c>int (*setClipboard)(LibreOfficeKitDocument* pThis, const size_t nInCount,
    ///     const char** pInMimeTypes, const size_t* pInSizes, const char** pInStreams)</c>.
    /// </summary>
    public IntPtr setClipboard;

    /// <summary>
    ///     Function pointer: <c>int (*getSelectionType)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getSelectionType;

    /// <summary>
    ///     Function pointer: <c>void (*removeTextContext)(LibreOfficeKitDocument* pThis, unsigned nWindowId, int nBefore, int nAfter)</c>.
    /// </summary>
    public IntPtr removeTextContext;

    /// <summary>
    ///     Function pointer: <c>void (*sendDialogEvent)(LibreOfficeKitDocument* pThis, unsigned long long int nLOKWindowId, const char* pArguments)</c>.
    /// </summary>
    public IntPtr sendDialogEvent;

    /// <summary>
    ///     Function pointer: <c>unsigned char* (*renderFontOrientation)(LibreOfficeKitDocument* pThis,
    ///     const char* pFontName, const char* pChar, int* pFontWidth, int* pFontHeight, int pOrientation)</c>.
    /// </summary>
    public IntPtr renderFontOrientation;

    /// <summary>
    ///     Function pointer: <c>void (*paintWindowForView)(LibreOfficeKitDocument* pThis, unsigned nWindowId,
    ///     unsigned char* pBuffer, const int x, const int y, const int width, const int height,
    ///     const double dpi scale, int viewId)</c>.
    /// </summary>
    public IntPtr paintWindowForView;

    /// <summary>
    ///     Function pointer: <c>void (*completeFunction)(LibreOfficeKitDocument* pThis, const char* pFunctionName)</c>.
    /// </summary>
    public IntPtr completeFunction;

    /// <summary>
    ///     Function pointer: <c>void (*setWindowTextSelection)(LibreOfficeKitDocument* pThis, unsigned nWindowId,
    ///     bool bSwap, int nX, int nY)</c>.
    /// </summary>
    public IntPtr setWindowTextSelection;

    /// <summary>
    ///     Function pointer: <c>void (*sendFormFieldEvent)(LibreOfficeKitDocument* pThis, const char* pArguments)</c>.
    /// </summary>
    public IntPtr sendFormFieldEvent;

    /// <summary>
    ///     Function pointer: <c>void (*setBlockedCommandList)(LibreOfficeKitDocument* pThis, int nViewId, const char* blockedCommandList)</c>.
    /// </summary>
    public IntPtr setBlockedCommandList;

    /// <summary>
    ///     Function pointer: <c>bool (*renderSearchResult)(LibreOfficeKitDocument* pThis, const char* pSearchResult,
    ///     unsigned char** pBitmapBuffer, int* pWidth, int* pHeight, size_t* pByteSize)</c>.
    /// </summary>
    public IntPtr renderSearchResult;

    /// <summary>
    ///     Function pointer: <c>void (*sendContentControlEvent)(LibreOfficeKitDocument* pThis, const char* pArguments)</c>.
    /// </summary>
    public IntPtr sendContentControlEvent;

    /// <summary>
    ///     Function pointer: <c>int (*getSelectionTypeAndText)(LibreOfficeKitDocument* pThis,
    ///     const char* pMimeType, char** pText, char** pUsedMimeType)</c>.
    ///     @since LibreOffice 7.4
    /// </summary>
    public IntPtr getSelectionTypeAndText;

    /// <summary>
    ///     Function pointer: <c>void (*getDataArea)(LibreOfficeKitDocument* pThis, long nPart, long* pCol, long* pRow)</c>.
    /// </summary>
    public IntPtr getDataArea;

    /// <summary>
    ///     Function pointer: <c>int (*getEditMode)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getEditMode;

    /// <summary>
    ///     Function pointer: <c>void (*setViewTimezone)(LibreOfficeKitDocument* pThis, int nId, const char* timezone)</c>.
    /// </summary>
    public IntPtr setViewTimezone;

    /// <summary>
    ///     Function pointer: <c>void (*setAccessibilityState)(LibreOfficeKitDocument* pThis, int nId, bool nEnabled)</c>.
    /// </summary>
    public IntPtr setAccessibilityState;

    /// <summary>
    ///     Function pointer: <c>char* (*getA11yFocusedParagraph)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getA11yFocusedParagraph;

    /// <summary>
    ///     Function pointer: <c>int (*getA11yCaretPosition)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getA11yCaretPosition;

    /// <summary>
    ///     Function pointer: <c>void (*setViewReadOnly)(LibreOfficeKitDocument* pThis, int nId, const bool readOnly)</c>.
    /// </summary>
    public IntPtr setViewReadOnly;

    /// <summary>
    ///     Function pointer: <c>void (*setAllowChangeComments)(LibreOfficeKitDocument* pThis, int nId, const bool allow)</c>.
    /// </summary>
    public IntPtr setAllowChangeComments;

    /// <summary>
    ///     Function pointer: <c>char* (*getPresentationInfo)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getPresentationInfo;

    /// <summary>
    ///     Function pointer: <c>bool (*createSlideRenderer)(LibreOfficeKitDocument* pThis, const char* pSlideHash,
    ///     int nSlideNumber, unsigned* nViewWidth, unsigned* nViewHeight,
    ///     bool bRenderBackground, bool bRenderMasterPage)</c>.
    /// </summary>
    public IntPtr createSlideRenderer;

    /// <summary>
    ///     Function pointer: <c>void (*postSlideshowCleanup)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr postSlideshowCleanup;

    /// <summary>
    ///     Function pointer: <c>bool (*renderNextSlideLayer)(LibreOfficeKitDocument* pThis, unsigned char* pBuffer,
    ///     bool* bIsBitmapLayer, double* pScale, char** pJsonMessage)</c>.
    /// </summary>
    public IntPtr renderNextSlideLayer;

    /// <summary>
    ///     Function pointer: <c>void (*setViewOption)(LibreOfficeKitDocument* pThis, const char* pOption, const char* pValue)</c>.
    /// </summary>
    public IntPtr setViewOption;

    /// <summary>
    ///     Function pointer: <c>void (*setColorPreviewState)(LibreOfficeKitDocument* pThis, int nId, bool nEnabled)</c>.
    /// </summary>
    public IntPtr setColorPreviewState;

    /// <summary>
    ///     Function pointer: <c>void (*setAllowManageRedlines)(LibreOfficeKitDocument* pThis, int nId, bool allow)</c>.
    /// </summary>
    public IntPtr setAllowManageRedlines;
}