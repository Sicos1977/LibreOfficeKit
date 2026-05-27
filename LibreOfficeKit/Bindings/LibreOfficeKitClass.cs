//
// LibreOfficeKitClass.cs
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
    ///     Function pointer: <c>documentLoadWithOptions</c> â€” not used but required for correct layout.
    /// </summary>
    public IntPtr documentLoadWithOptions;

    /// <summary>
    ///     Function pointer: <c>void (*freeError)(char* pFree)</c>.
    /// </summary>
    public IntPtr freeError;
}