using System.Runtime.InteropServices;

namespace LibreOfficeKit.Bindings;

/// <summary>
///     Represents the LibreOfficeKit vtable containing function pointers for office-level operations.
///     Field order must match the native C header exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LibreOfficeKitClass
{
    /// <summary>
    ///     Size of this struct in bytes.
    /// </summary>
    public nuint nSize;

    /// <summary>
    ///     Function pointer: <c>void (*destroy)(LibreOfficeKit* pThis)</c>.
    /// </summary>
    public IntPtr destroy;

    /// <summary>
    ///     Function pointer: <c>LibreOfficeKitDocument* (*documentLoad)(LibreOfficeKit* pThis, const char* pURL)</c>.
    /// </summary>
    public IntPtr documentLoad;

    /// <summary>
    ///     Function pointer: <c>char* (*getError)(LibreOfficeKit* pThis)</c>.
    /// </summary>
    public IntPtr getError;

    /// <summary>
    ///     Function pointer: <c>documentLoadWithOptions</c> — not used but required for correct layout.
    /// </summary>
    public IntPtr documentLoadWithOptions;

    /// <summary>
    ///     Function pointer: <c>void (*freeError)(char* pFree)</c>.
    /// </summary>
    public IntPtr freeError;
}