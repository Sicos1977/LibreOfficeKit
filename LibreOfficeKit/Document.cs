//
// Document.cs
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
using LibreOfficeKit.Bindings;
using LibreOfficeKit.Enums;
// ReSharper disable UnusedMember.Global

namespace LibreOfficeKit;

/// <summary>
///     Represents a loaded LibreOffice document.
///     Provides methods for saving/converting the document and querying its type.
/// </summary>
public sealed class Document : IDisposable
{
    #region Fields
    /// <summary>Pointer to the native LibreOfficeKitDocument instance.</summary>
    private IntPtr _pDocument;

    /// <summary>The document class vtable containing native function pointers.</summary>
    private readonly LibreOfficeKitDocumentClass _docClass;

    /// <summary>Indicates whether this instance has been disposed.</summary>
    private bool _disposed;
    #endregion

    #region Constructor
    /// <summary>
    ///     Initializes a new instance of <see cref="Document" /> wrapping the given native document pointer.
    /// </summary>
    /// <param name="pDocument">Pointer to the native LibreOfficeKitDocument.</param>
    internal Document(IntPtr pDocument)
    {
        _pDocument = pDocument;

        var doc = Marshal.PtrToStructure<LibreOfficeKitDocument>(_pDocument);
        _docClass = Marshal.PtrToStructure<LibreOfficeKitDocumentClass>(doc.pClass);
    }
    #endregion

    #region SaveAs
    /// <summary>
    ///     Saves the document in another format using the native LOK <c>saveAs</c> function.
    /// </summary>
    /// <param name="outputUrl">The output file URL (file:// format).</param>
    /// <param name="format">The target format string (e.g. <c>"pdf"</c>).</param>
    /// <param name="filterOptions">Optional filter options string.</param>
    /// <returns><c>true</c> if the save operation succeeded; otherwise <c>false</c>.</returns>
    public bool SaveAs(string outputUrl, string format, string? filterOptions = null)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        if (_docClass.saveAs == IntPtr.Zero)
            throw new InvalidOperationException("saveAs function not available in this LibreOffice version.");

        var saveAs = Marshal.GetDelegateForFunctionPointer<LokDocSaveAsFunction>(_docClass.saveAs);
        var pUrl = Marshal.StringToHGlobalAnsi(outputUrl);
        var pFormat = Marshal.StringToHGlobalAnsi(format);
        var pFilter = filterOptions != null ? Marshal.StringToHGlobalAnsi(filterOptions) : IntPtr.Zero;

        try
        {
            var result = saveAs(_pDocument, pUrl, pFormat, pFilter);
            return result != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(pUrl);
            Marshal.FreeHGlobal(pFormat);
            if (pFilter != IntPtr.Zero) Marshal.FreeHGlobal(pFilter);
        }
    }
    
    /// <summary>
    ///     Saves the document using a <see cref="SaveFormat" /> enum value.
    /// </summary>
    /// <param name="outputUrl">The output file URL (file:// format).</param>
    /// <param name="format">The target <see cref="SaveFormat" />.</param>
    /// <param name="filterOptions">Optional filter options string.</param>
    /// <returns><c>true</c> if the save operation succeeded; otherwise <c>false</c>.</returns>
    public bool SaveAs(string outputUrl, SaveFormat format, string? filterOptions = null)
    {
        return SaveAs(outputUrl, format.ToFormatString().ToLowerInvariant(), filterOptions);
    }

    /// <summary>
    ///     Saves the document as PDF using detailed <see cref="PdfOptions" />.
    /// </summary>
    /// <param name="outputUrl">The output file URL (file:// format).</param>
    /// <param name="options">The PDF export options.</param>
    /// <returns><c>true</c> if the save operation succeeded; otherwise <c>false</c>.</returns>
    public bool SaveAs(string outputUrl, PdfOptions options)
    {
        var filterOptions = options.ToFilterOptions();
        return SaveAs(outputUrl, "pdf", filterOptions);
    }
    #endregion

    #region GetDocumentType
    /// <summary>
    ///     Gets the document type (text, spreadsheet, presentation, drawing, or other).
    /// </summary>
    /// <returns>The <see cref="DocumentType" /> of this document.</returns>
    public DocumentType GetDocumentType()
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        if (_docClass.getDocumentType == IntPtr.Zero)
            throw new InvalidOperationException("getDocumentType function not available.");

        var getDocType =
            Marshal.GetDelegateForFunctionPointer<LokDocGetDocumentTypeFunction>(_docClass.getDocumentType);
        var result = getDocType(_pDocument);

        if (Enum.IsDefined(typeof(DocumentType), result))
            return (DocumentType)result;

        return DocumentType.Other;
    }
    #endregion

    #region Dispose
    /// <summary>
    ///     Releases native resources held by this document instance.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_pDocument != IntPtr.Zero && _docClass.destroy != IntPtr.Zero)
        {
            var destroy = Marshal.GetDelegateForFunctionPointer<LokDocDestroyFunction>(_docClass.destroy);
            destroy(_pDocument);
        }

        _pDocument = IntPtr.Zero;
    }
    #endregion
}