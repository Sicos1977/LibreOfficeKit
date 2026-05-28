//
// Bindings.cs
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
///     Delegate for the <c>libreofficekit_hook</c> entry point exported by the LOK shared library.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokHookFunction(IntPtr installPath);

/// <summary>
///     Delegate for the <c>libreofficekit_hook_2</c> entry point with user profile support.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokHook2Function(IntPtr installPath, IntPtr userProfileUrl);

/// <summary>
///     Delegate for the <c>libreofficekit_preinit</c> entry point for pre-initialization.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokPreinitFunction(IntPtr installPath, IntPtr userProfileUrl);

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
///     Delegate for <c>LibreOfficeKitClass.documentLoadWithOptions</c>.
/// </summary>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokDocumentLoadWithOptionsFunction(IntPtr pThis, IntPtr pUrl, IntPtr pOptions);

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