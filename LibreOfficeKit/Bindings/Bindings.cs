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
// ReSharper disable UnusedMember.Global

namespace LibreOfficeKit.Bindings;

/// <summary>
///     Delegate for the <c>libreofficekit_hook</c> entry point exported by the LOK shared library.
/// </summary>
/// <param name="installPath">UTF-8 encoded string containing the LibreOffice installation path.</param>
/// <returns>Pointer to the initialized LibreOfficeKit instance, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#if NETSTANDARD2_0
internal delegate IntPtr LokHookFunction(IntPtr installPath);
#else
internal delegate IntPtr LokHookFunction([MarshalAs(UnmanagedType.LPUTF8Str)] string installPath);
#endif

/// <summary>
///     Delegate for the <c>libreofficekit_callback_2</c> entry point.
/// </summary>
/// <param name="type">The type of the callback event.</param>
/// <param name="payload">UTF-8 encoded payload string.</param>
/// <param name="pData">Pointer to user data.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public delegate void LokCallback2Function(int type, [MarshalAs(UnmanagedType.LPStr)] string payload, IntPtr pData);

/// <summary>
///     Delegate voor LibreOfficeKitClass.registerCallback om globale/document-laad events op te vangen.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="callback">The callback function to register for receiving events.</param>
/// <param name="pData">Pointer to user-defined data that will be passed to the callback.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokRegisterCallbackFunction(IntPtr pOffice, LokCallback2Function callback, IntPtr pData);

/// <summary>
///     Delegate for the <c>libreofficekit_hook_2</c> entry point with user profile support.
/// </summary>
/// <param name="installPath">UTF-8 encoded string containing the LibreOffice installation path.</param>
/// <param name="userProfileUrl">UTF-8 encoded string containing the user profile URL (file:// format), or null.</param>
/// <returns>Pointer to the initialized LibreOfficeKit instance, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#if NETSTANDARD2_0
internal delegate IntPtr LokHook2Function(IntPtr installPath, IntPtr userProfileUrl);
#else
internal delegate IntPtr LokHook2Function(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string installPath,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? userProfileUrl);
#endif

/// <summary>
///     Delegate for the <c>libreofficekit_preinit</c> entry point for pre-initialization.
/// </summary>
/// <param name="installPath">UTF-8 encoded string containing the LibreOffice installation path, or null.</param>
/// <param name="userProfileUrl">UTF-8 encoded string containing the user profile URL (file:// format), or null.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#if NETSTANDARD2_0
internal delegate void LokPreinitFunction(IntPtr installPath, IntPtr userProfileUrl);
#else
internal delegate void LokPreinitFunction(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? installPath,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? userProfileUrl);
#endif

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.destroy</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance to destroy.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokDestroyFunction(IntPtr pOffice);


#if NETSTANDARD2_0
/// <summary>
///     Delegate for <c>LibreOfficeKitClass.documentLoad</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pUrl">Pointer to a UTF-8 encoded string containing the document URL to load.</param>
/// <returns>Pointer to the loaded LibreOfficeKitDocument, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokDocumentLoadFunction(IntPtr pOffice, IntPtr pUrl);
#else
/// <summary>
///     Delegate for <c>LibreOfficeKitClass.documentLoad</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="url">UTF-8 encoded string containing the document URL to load.</param>
/// <returns>Pointer to the loaded LibreOfficeKitDocument, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokDocumentLoadFunction(IntPtr pOffice, [MarshalAs(UnmanagedType.LPUTF8Str)] string url);
#endif

#if NETSTANDARD2_0
/// <summary>
///     Delegate for <c>LibreOfficeKitClass.documentLoadWithOptions</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pUrl">Pointer to a UTF-8 encoded string containing the document URL to load.</param>
/// <param name="pOptions">Pointer to a UTF-8 encoded string containing load options, or null.</param>
/// <returns>Pointer to the loaded LibreOfficeKitDocument, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokDocumentLoadWithOptionsFunction(IntPtr pOffice, IntPtr pUrl, IntPtr pOptions);
#else
/// <summary>
///     Delegate for <c>LibreOfficeKitClass.documentLoadWithOptions</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="url">UTF-8 encoded string containing the document URL to load.</param>
/// <param name="options">UTF-8 encoded string containing load options, or null.</param>
/// <returns>Pointer to the loaded LibreOfficeKitDocument, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokDocumentLoadWithOptionsFunction(
    IntPtr pOffice,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string url,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? options);
#endif

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.getError</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <returns>Pointer to a UTF-8 encoded error string, or IntPtr.Zero if no error. Must be freed with freeError.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokGetErrorFunction(IntPtr pOffice);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.freeError</c>.
/// </summary>
/// <param name="pFree">Pointer to the error string returned by getError that should be freed.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokFreeErrorFunction(IntPtr pFree);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.getVersionInfo</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <returns>UTF-8 encoded JSON string containing version information (does not need to be freed), or null.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#if NETSTANDARD2_0
internal delegate IntPtr LokGetVersionInfoFunction(IntPtr pOffice);
#else
[return: MarshalAs(UnmanagedType.LPUTF8Str)]
internal delegate string? LokGetVersionInfoFunction(IntPtr pOffice);
#endif

/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.destroy</c>.
/// </summary>
/// <param name="pDocument">Pointer to the LibreOfficeKitDocument instance to destroy.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokDocDestroyFunction(IntPtr pDocument);

#if NETSTANDARD2_0
/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.saveAs</c>.
/// </summary>
/// <param name="pDocument">Pointer to the LibreOfficeKitDocument instance.</param>
/// <param name="pUrl">UTF-8 encoded string containing the output file URL.</param>
/// <param name="pFormat">UTF-8 encoded string containing the output format (e.g., "pdf").</param>
/// <param name="pFilterOptions">UTF-8 encoded string containing filter options, or null.</param>
/// <returns>1 on success, 0 on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LokDocSaveAsFunction(IntPtr pDocument, IntPtr pUrl, IntPtr pFormat, IntPtr pFilterOptions);
#else
/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.saveAs</c>.
/// </summary>
/// <param name="pDocument">Pointer to the LibreOfficeKitDocument instance.</param>
/// <param name="url">UTF-8 encoded string containing the output file URL.</param>
/// <param name="format">UTF-8 encoded string containing the output format (e.g., "pdf").</param>
/// <param name="filterOptions">UTF-8 encoded string containing filter options, or null.</param>
/// <returns>1 on success, 0 on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LokDocSaveAsFunction(
    IntPtr pDocument,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string url,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string format,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? filterOptions);
#endif

/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.getDocumentType</c>.
/// </summary>
/// <param name="pDocument">Pointer to the LibreOfficeKitDocument instance.</param>
/// <returns>The document type as an integer (0 = text, 1 = spreadsheet, 2 = presentation, etc.).</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LokDocGetDocumentTypeFunction(IntPtr pDocument);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.setOptionalFeatures</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="nFeatures">Bitmask of features to enable.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokSetOptionalFeaturesFunction(IntPtr pOffice, ulong nFeatures);

#if NETSTANDARD2_0
/// <summary>
///     Delegate for <c>LibreOfficeKitClass.setDocumentPassword</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pUrl">Pointer to a UTF-8 encoded string containing the document URL.</param>
/// <param name="pPassword">Pointer to a UTF-8 encoded string containing the password, or null to clear.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokSetDocumentPasswordFunction(IntPtr pOffice, IntPtr pUrl, IntPtr pPassword);
#else
/// <summary>
///     Delegate for <c>LibreOfficeKitClass.setDocumentPassword</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="url">UTF-8 encoded string containing the document URL.</param>
/// <param name="password">UTF-8 encoded string containing the password, or null to clear.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokSetDocumentPasswordFunction(
    IntPtr pOffice,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string url,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? password);
#endif

