using System.Runtime.InteropServices;

namespace LibreOfficeKit.Bindings;

/// <summary>
///     Represents the LibreOfficeKit document vtable containing function pointers for document-level operations.
///     Field order must match the native C header exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LibreOfficeKitDocumentClass
{
    /// <summary>
    ///     Size of this struct in bytes.
    /// </summary>
    public nuint nSize;

    /// <summary>
    ///     Function pointer: <c>void (*destroy)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr destroy;

    /// <summary>
    ///     Function pointer:
    ///     <c>int (*saveAs)(LibreOfficeKitDocument* pThis, const char* pUrl, const char* pFormat, const char* pFilterOptions)</c>
    ///     .
    /// </summary>
    public IntPtr saveAs;

    /// <summary>
    ///     Function pointer: <c>int (*getDocumentType)(LibreOfficeKitDocument* pThis)</c>.
    /// </summary>
    public IntPtr getDocumentType;
}