// =============================================================================
// LibreOfficeKitBindings.cs
//
// Low-level P/Invoke bindings for LibreOfficeKit (LOK).
// Uses NativeLibrary + Marshal for dynamic loading and vtable-based
// function pointer invocation of the native LOK C API.
// =============================================================================

using System.Runtime.InteropServices;

namespace LibreOfficeKit;

/// <summary>
///     Represents the raw C struct returned by <c>libreofficekit_hook</c>.
///     Contains a pointer to the <see cref="LibreOfficeKitClass" /> vtable.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LibreOfficeKitStruct
{
    /// <summary>Pointer to the LibreOfficeKitClass vtable.</summary>
    public IntPtr pClass;
}

/// <summary>
///     Represents the LibreOfficeKit vtable containing function pointers for office-level operations.
///     Field order must match the native C header exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LibreOfficeKitClass
{
    #region Fields
    /// <summary>Size of this struct in bytes.</summary>
    public nuint nSize;

    /// <summary>Function pointer: <c>void (*destroy)(LibreOfficeKit* pThis)</c>.</summary>
    public IntPtr destroy;

    /// <summary>Function pointer: <c>LibreOfficeKitDocument* (*documentLoad)(LibreOfficeKit* pThis, const char* pURL)</c>.</summary>
    public IntPtr documentLoad;

    /// <summary>Function pointer: <c>char* (*getError)(LibreOfficeKit* pThis)</c>.</summary>
    public IntPtr getError;

    /// <summary>Function pointer: <c>documentLoadWithOptions</c> — not used but required for correct layout.</summary>
    public IntPtr documentLoadWithOptions;

    /// <summary>Function pointer: <c>void (*freeError)(char* pFree)</c>.</summary>
    public IntPtr freeError;
    #endregion
}

/// <summary>
///     Represents the raw C struct for a loaded LibreOffice document.
///     Contains a pointer to the <see cref="LibreOfficeKitDocumentClass" /> vtable.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LibreOfficeKitDocument
{
    /// <summary>Pointer to the LibreOfficeKitDocumentClass vtable.</summary>
    public IntPtr pClass;
}

/// <summary>
///     Represents the LibreOfficeKit document vtable containing function pointers for document-level operations.
///     Field order must match the native C header exactly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LibreOfficeKitDocumentClass
{
    #region Fields
    /// <summary>Size of this struct in bytes.</summary>
    public nuint nSize;

    /// <summary>Function pointer: <c>void (*destroy)(LibreOfficeKitDocument* pThis)</c>.</summary>
    public IntPtr destroy;

    /// <summary>
    ///     Function pointer:
    ///     <c>int (*saveAs)(LibreOfficeKitDocument* pThis, const char* pUrl, const char* pFormat, const char* pFilterOptions)</c>
    ///     .
    /// </summary>
    public IntPtr saveAs;

    /// <summary>Function pointer: <c>int (*getDocumentType)(LibreOfficeKitDocument* pThis)</c>.</summary>
    public IntPtr getDocumentType;
    #endregion
}

// =============================================================================
// Delegate types for the function pointers in the vtables.
// =============================================================================

/// <summary>
///     Delegate for the <c>libreofficekit_hook</c> entry point exported by the LOK shared library.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokHookFunction(IntPtr installPath);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.destroy</c>.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokDestroyFunction(IntPtr pThis);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.documentLoad</c>.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokDocumentLoadFunction(IntPtr pThis, IntPtr pUrl);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.getError</c>.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokGetErrorFunction(IntPtr pThis);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.freeError</c>.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokFreeErrorFunction(IntPtr pFree);

/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.destroy</c>.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokDocDestroyFunction(IntPtr pThis);

/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.saveAs</c>.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LokDocSaveAsFunction(IntPtr pThis, IntPtr pUrl, IntPtr pFormat, IntPtr pFilterOptions);

/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.getDocumentType</c>.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LokDocGetDocumentTypeFunction(IntPtr pThis);