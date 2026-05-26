using System.Runtime.InteropServices;

namespace LibreOfficeKit.Bindings;

/// <summary>
///     Represents the raw C struct returned by <c>libreofficekit_hook</c>.
///     Contains a pointer to the <see cref="LibreOfficeKitClass" /> vtable.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal struct LibreOfficeKitStruct
{
    /// <summary>
    ///     Pointer to the LibreOfficeKitClass vtable.
    /// </summary>
    public IntPtr pClass;
}