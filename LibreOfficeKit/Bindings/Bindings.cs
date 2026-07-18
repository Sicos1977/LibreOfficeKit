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
///     Delegate for the <c>libreofficekit_callback_2</c> entry point.
///     This delegate is platform-independent and is used on all targets.
/// </summary>
/// <param name="type">The type of the callback event.</param>
/// <param name="payload">UTF-8 encoded payload string.</param>
/// <param name="pData">Pointer to user data.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
public delegate void LokCallback2Function(int type, [MarshalAs(UnmanagedType.LPStr)] string payload, IntPtr pData);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.registerCallback</c> to capture global/document-load events.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="callback">The callback function to register for receiving events.</param>
/// <param name="pData">Pointer to user-defined data that will be passed to the callback.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokRegisterCallbackFunction(IntPtr pOffice, LokCallback2Function callback, IntPtr pData);

/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.getDocumentType</c>.
/// </summary>
/// <param name="pDocument">Pointer to the LibreOfficeKitDocument instance.</param>
/// <returns>The document type as an integer (0 = text, 1 = spreadsheet, 2 = presentation, 3 = drawing, 4 = other).</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LokDocGetDocumentTypeFunction(IntPtr pDocument);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.setOptionalFeatures</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="nFeatures">Bitmask of features to enable.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokSetOptionalFeaturesFunction(IntPtr pOffice, ulong nFeatures);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.destroy</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance to destroy.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokDestroyFunction(IntPtr pOffice);

/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.destroy</c>.
/// </summary>
/// <param name="pDocument">Pointer to the LibreOfficeKitDocument instance to destroy.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokDocDestroyFunction(IntPtr pDocument);

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
///     Joins all threads if possible to get down to a single process which can be forked from safely.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <returns>Non-zero for successful join, 0 for failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LokJoinThreadsFunction(IntPtr pOffice);

/// <summary>
///     Starts all threads that are necessary to continue working after a <c>joinThreads()</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokStartThreadsFunction(IntPtr pOffice);

/// <summary>
///     Gets the number of open documents in this LibreOfficeKit instance.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <returns>The number of open documents.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LokGetDocsCountFunction(IntPtr pOffice);

/// <summary>
///     Frees the memory pointed to by <paramref name="pFree"/>.
///     Use on dynamically allocated data returned by LibreOfficeKit functions.
///     This is a wrapper for <c>freeError()</c> with a more descriptive name.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pFree">The pointer to the unmanaged memory block to free.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokFreeMemoryFunction(IntPtr pOffice, IntPtr pFree);

/// <summary>
///     Delegate for the poll callback that LibreOffice calls to wait for events. Should return 1 if events were processed, 0 if timed out with no events, or a negative value on error.
/// </summary>
/// <param name="data">Pointer to user-defined data.</param>
/// <param name="timeoutUs">Timeout in microseconds.</param>
/// <returns>1 if events were processed, 0 if timed out with no events, or a negative value on error.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LibreOfficeKitPollCallback(IntPtr data, int timeoutUs);

/// <summary>
///    Delegate for the wake callback that LibreOffice calls to wake up the event loop when new events are available.
/// </summary>
/// <param name="data">Pointer to user-defined data.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LibreOfficeKitWakeCallback(IntPtr data);

/// <summary>
///    Delegate for the <c>libreofficekit_run_loop</c> entry point, which runs the event loop with the provided poll and wake callbacks.
/// </summary>
/// <param name="kit">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pollCallback">Pointer to the poll callback function.</param>
/// <param name="wakeCallback">Pointer to the wake callback function.</param>
/// <param name="data">Pointer to user-defined data.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void RunLoopDelegate(
    IntPtr kit, 
    LibreOfficeKitPollCallback pollCallback, 
    LibreOfficeKitWakeCallback wakeCallback, 
    IntPtr data
);

/// <summary>
///     Delegate for the callback that LibreOffice calls in forked child processes after a fork. This allows the application to perform any necessary reinitialization in the child process.
/// </summary>
/// <param name="data">Pointer to user-defined data.</param>
[UnmanagedFunctionPointer( CallingConvention.Cdecl)]
public delegate void LibreOfficeKitForkedChildCallback(IntPtr data);

/// <summary>
///    Delegate for the <c>libreofficekit_set_forked_child_callback</c> entry point, which registers a callback to be called in forked child processes after a fork.
/// </summary>
/// <param name="kit">Pointer to the LibreOfficeKit instance.</param>
/// <param name="callback">Pointer to the callback function to be called in forked child processes.</param>
/// <param name="data">Pointer to user-defined data.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void SetForkedChildDelegate(
    IntPtr kit, 
    LibreOfficeKitForkedChildCallback callback, 
    IntPtr data
);

/// <summary>
///   Delegate for the <c>libreofficekit_trim_memory</c> entry point, which allows the application to request that LibreOffice trim its memory usage when the system is under memory pressure. The nTarget parameter indicates the severity of memory pressure, with higher values indicating more severe pressure.
/// </summary>
/// <param name="kit">Pointer to the LibreOfficeKit instance.</param>
/// <param name="nTarget">The severity of memory pressure, with higher values indicating more severe pressure.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void TrimMemoryDelegate(IntPtr kit, int nTarget);

/// <summary>
///     Delegate for the <c>libreofficekit_initialize</c> entry point, which initializes a LibreOfficeKit instance with the specified user and profile paths. This is an alternative to the hook functions and allows for more direct control over initialization parameters.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pUserPath">Pointer to a UTF-8 encoded string containing the user path.</param>
/// <param name="pProfilePath">Pointer to a UTF-8 encoded string containing the profile path.</param>
/// <returns>0 on success, or a negative value on error.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate int LokInitializeFunction(
    IntPtr pOffice,
    [MarshalAs(UnmanagedType.LPStr)] string pUserPath,
    [MarshalAs(UnmanagedType.LPStr)] string pProfilePath
);

#if NETSTANDARD2_0

/// <summary>
///     Delegate for the <c>libreofficekit_hook</c> entry point exported by the LOK shared library.
/// </summary>
/// <param name="installPath">Pointer to a UTF-8 encoded string containing the LibreOffice installation path.</param>
/// <returns>Pointer to the initialized LibreOfficeKit instance, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokHookFunction(IntPtr installPath);

/// <summary>
///     Delegate for the <c>libreofficekit_hook_2</c> entry point with user profile support.
/// </summary>
/// <param name="installPath">Pointer to a UTF-8 encoded string containing the LibreOffice installation path.</param>
/// <param name="userProfileUrl">Pointer to a UTF-8 encoded string containing the user profile URL (file:// format), or null.</param>
/// <returns>Pointer to the initialized LibreOfficeKit instance, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokHook2Function(IntPtr installPath, IntPtr userProfileUrl);

/// <summary>
///     Delegate for the <c>libreofficekit_preinit</c> entry point for pre-initialization.
/// </summary>
/// <param name="installPath">Pointer to a UTF-8 encoded string containing the LibreOffice installation path, or null.</param>
/// <param name="userProfileUrl">Pointer to a UTF-8 encoded string containing the user profile URL (file:// format), or null.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokPreinitFunction(IntPtr installPath, IntPtr userProfileUrl);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.documentLoad</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pUrl">Pointer to a UTF-8 encoded string containing the document URL to load.</param>
/// <returns>Pointer to the loaded LibreOfficeKitDocument, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokDocumentLoadFunction(IntPtr pOffice, IntPtr pUrl);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.documentLoadWithOptions</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pUrl">Pointer to a UTF-8 encoded string containing the document URL to load.</param>
/// <param name="pOptions">Pointer to a UTF-8 encoded string containing load options, or null.</param>
/// <returns>Pointer to the loaded LibreOfficeKitDocument, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokDocumentLoadWithOptionsFunction(IntPtr pOffice, IntPtr pUrl, IntPtr pOptions);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.getVersionInfo</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <returns>Pointer to a UTF-8 encoded JSON string containing version information (does not need to be freed), or IntPtr.Zero.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokGetVersionInfoFunction(IntPtr pOffice);

/// <summary>
///     Delegate for <c>LibreOfficeKitDocumentClass.saveAs</c>.
/// </summary>
/// <param name="pDocument">Pointer to the LibreOfficeKitDocument instance.</param>
/// <param name="pUrl">Pointer to a UTF-8 encoded string containing the output file URL.</param>
/// <param name="pFormat">Pointer to a UTF-8 encoded string containing the output format (e.g., "pdf").</param>
/// <param name="pFilterOptions">Pointer to a UTF-8 encoded string containing filter options, or null.</param>
/// <returns>1 on success, 0 on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int LokDocSaveAsFunction(IntPtr pDocument, IntPtr pUrl, IntPtr pFormat, IntPtr pFilterOptions);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.setDocumentPassword</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pUrl">Pointer to a UTF-8 encoded string containing the document URL.</param>
/// <param name="pPassword">Pointer to a UTF-8 encoded string containing the password, or null to clear.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokSetDocumentPasswordFunction(IntPtr pOffice, IntPtr pUrl, IntPtr pPassword);

/// <summary>
///     Callback signature that can display an interactive file save dialog.
/// </summary>
/// <param name="pData">User data pointer passed to the registration function.</param>
/// <param name="pUrl">Pointer to a UTF-8 encoded string containing the target file URL.</param>
/// <param name="pFormat">Pointer to a UTF-8 encoded string containing the target file format.</param>
/// <param name="pFilterOptions">Pointer to a UTF-8 encoded string containing the filter options for the file save operation.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LibreOfficeKitFileSaveDialogCallback(
    IntPtr pData,
    IntPtr pUrl,
    IntPtr pFormat,
    IntPtr pFilterOptions);

/// <summary>
///     Registers a callback that can display an interactive file save dialog.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pCallback">Function pointer to the interactive file save dialog callback.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokRegisterFileSaveDialogCallbackFunction(IntPtr pOffice, IntPtr pCallback);

#else

/// <summary>
///     Delegate for the <c>libreofficekit_hook</c> entry point exported by the LOK shared library.
/// </summary>
/// <param name="installPath">UTF-8 encoded string containing the LibreOffice installation path.</param>
/// <returns>Pointer to the initialized LibreOfficeKit instance, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokHookFunction([MarshalAs(UnmanagedType.LPUTF8Str)] string installPath);

/// <summary>
///     Delegate for the <c>libreofficekit_hook_2</c> entry point with user profile support.
/// </summary>
/// <param name="installPath">UTF-8 encoded string containing the LibreOffice installation path.</param>
/// <param name="userProfileUrl">UTF-8 encoded string containing the user profile URL (file:// format), or null.</param>
/// <returns>Pointer to the initialized LibreOfficeKit instance, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokHook2Function(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string installPath,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? userProfileUrl);

/// <summary>
///     Delegate for the <c>libreofficekit_preinit</c> entry point for pre-initialization.
/// </summary>
/// <param name="installPath">UTF-8 encoded string containing the LibreOffice installation path, or null.</param>
/// <param name="userProfileUrl">UTF-8 encoded string containing the user profile URL (file:// format), or null.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokPreinitFunction(
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? installPath,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string? userProfileUrl);

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.documentLoad</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="url">UTF-8 encoded string containing the document URL to load.</param>
/// <returns>Pointer to the loaded LibreOfficeKitDocument, or IntPtr.Zero on failure.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokDocumentLoadFunction(IntPtr pOffice, [MarshalAs(UnmanagedType.LPUTF8Str)] string url);

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

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.getVersionInfo</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <returns>UTF-8 encoded JSON string containing version information (does not need to be freed), or null.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.LPUTF8Str)]
internal delegate string? LokGetVersionInfoFunction(IntPtr pOffice);

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

/// <summary>
///     Delegate for <c>LibreOfficeKitClass.signDocument</c>.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="url">Pointer to a UTF-8 encoded string containing the document URL.</param>
/// <param name="certificateBinary">Pointer to the certificate binary data.</param>
/// <param name="certificateBinarySize">Size of the certificate binary data in bytes.</param>
/// <param name="privateKeyBinary">Pointer to the private key binary data.</param>
/// <param name="privateKeyBinarySize">Size of the private key binary data in bytes.</param>
/// <returns><c>true</c> when the document is signed successfully; otherwise <c>false</c>.</returns>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
[return: MarshalAs(UnmanagedType.I1)]
internal delegate bool LokSignDocumentFunction(
    IntPtr pOffice,
    IntPtr url,
    IntPtr certificateBinary,
    int certificateBinarySize,
    IntPtr privateKeyBinary,
    int privateKeyBinarySize);

/// <summary>
///     Callback signature that can display an interactive file save dialog.
/// </summary>
/// <param name="pData">User data pointer passed to the registration function.</param>
/// <param name="url">The target file URL.</param>
/// <param name="format">The target file format.</param>
/// <param name="filterOptions">The filter options for the file save operation.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LibreOfficeKitFileSaveDialogCallback(
    IntPtr pData,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string url,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string format,
    [MarshalAs(UnmanagedType.LPUTF8Str)] string filterOptions);

/// <summary>
///     Registers a callback that can display an interactive file save dialog.
/// </summary>
/// <param name="pOffice">Pointer to the LibreOfficeKit instance.</param>
/// <param name="pCallback">The interactive file save dialog callback.</param>
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void LokRegisterFileSaveDialogCallbackFunction(
    IntPtr pOffice,
    LibreOfficeKitFileSaveDialogCallback pCallback);

#endif