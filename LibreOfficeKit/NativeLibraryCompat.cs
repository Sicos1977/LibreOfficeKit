//
// NativeLibraryCompat.cs
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

#if NETSTANDARD2_0
using System.Runtime.InteropServices;
// ReSharper disable MemberCanBePrivate.Local

namespace LibreOfficeKit;

/// <summary>
///     Polyfill for <c>System.Runtime.InteropServices.NativeLibrary</c> on netstandard2.0.
///     Delegates to <c>kernel32</c> on Windows and <c>libdl</c> on Linux/macOS.
/// </summary>
internal static class NativeLibrary
{
    private const int RtldNow = 2;

    #region Windows P/Invoke
    private static class Windows
    {
        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi, BestFitMapping = false)]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
    #endregion

    #region Linux P/Invoke
    private static class Linux
    {
        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        public static extern IntPtr DlOpen(string filename, int flags);

        [DllImport("libdl.so.2", EntryPoint = "dlsym")]
        public static extern IntPtr DlSym(IntPtr handle, string symbol);

        [DllImport("libdl.so.2", EntryPoint = "dlclose")]
        public static extern int DlClose(IntPtr handle);

        public static IntPtr Open(string filename) => DlOpen(filename, RtldNow);
    }
    #endregion

    #region macOS P/Invoke
    private static class MacOs
    {
        [DllImport("libdl.dylib", EntryPoint = "dlopen")]
        public static extern IntPtr DlOpen(string filename, int flags);

        [DllImport("libdl.dylib", EntryPoint = "dlsym")]
        public static extern IntPtr DlSym(IntPtr handle, string symbol);

        [DllImport("libdl.dylib", EntryPoint = "dlclose")]
        public static extern int DlClose(IntPtr handle);

        public static IntPtr Open(string filename) => DlOpen(filename, RtldNow);
    }
    #endregion

    #region TryLoad
    /// <summary>
    ///     Attempts to load a native library from the specified path.
    /// </summary>
    public static bool TryLoad(string libraryPath, out IntPtr handle)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                handle = Windows.LoadLibraryEx(libraryPath, IntPtr.Zero, 0);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                handle = MacOs.Open(libraryPath);
            else
                handle = Linux.Open(libraryPath);

            return handle != IntPtr.Zero;
        }
        catch
        {
            handle = IntPtr.Zero;
            return false;
        }
    }
    #endregion

    #region TryGetExport
    /// <summary>
    ///     Attempts to get a function pointer export from a loaded native library.
    /// </summary>
    public static bool TryGetExport(IntPtr handle, string name, out IntPtr address)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                address = Windows.GetProcAddress(handle, name);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                address = MacOs.DlSym(handle, name);
            else
                address = Linux.DlSym(handle, name);

            return address != IntPtr.Zero;
        }
        catch
        {
            address = IntPtr.Zero;
            return false;
        }
    }
    #endregion

    #region Free
    /// <summary>
    ///     Frees a loaded native library.
    /// </summary>
    public static void Free(IntPtr handle)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Windows.FreeLibrary(handle);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                MacOs.DlClose(handle);
            else
                Linux.DlClose(handle);
        }
        catch
        {
            // ignored
        }
    }
    #endregion
}
#endif
