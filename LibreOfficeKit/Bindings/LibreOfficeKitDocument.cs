using System.Runtime.InteropServices;

namespace LibreOfficeKit.Bindings;

/// <summary>
///     Represents the raw C struct for a loaded LibreOffice document.
///     Contains a pointer to the <see cref="LibreOfficeKitDocumentClass" /> vtable.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LibreOfficeKitDocument
{
    /// <summary>
    ///     Pointer to the LibreOfficeKitDocumentClass vtable.
    /// </summary>
    public IntPtr pClass;
}