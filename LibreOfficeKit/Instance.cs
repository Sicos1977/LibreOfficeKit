//
// Instance.cs
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
// =============================================================================
//
// Manages the lifecycle of a LibreOfficeKit instance. Handles dynamic loading
// of the native LOK library, initialization via libreofficekit_hook, and
// document loading. A global lock ensures only one instance is active at a
// time since LOK is NOT thread-safe.
// =============================================================================

using LibreOfficeKit.Bindings;
using LibreOfficeKit.Enums;
using LibreOfficeKit.Logging;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
// ReSharper disable NotAccessedField.Local

#if !NETSTANDARD2_0
using NativeLibrary = System.Runtime.InteropServices.NativeLibrary;
#endif

namespace LibreOfficeKit;

/// <summary>
///     Main LibreOffice instance managing the LOK lifecycle.
///     Only one instance may be active at a time within a single process.
/// </summary>
public sealed class Instance : IDisposable
{
    #region Fields
#if NET10_0_OR_GREATER
    /// <summary>
    ///     Global lock ensuring only one LOK instance is active at a time.
    /// </summary>
    private static readonly Lock GlobalLock = new();
#else
    /// <summary>
    ///     Global lock ensuring only one LOK instance is active at a time.
    /// </summary>
    private static readonly object GlobalLock = new();
#endif
    /// <summary>
    ///     Tracks whether an instance is currently active.
    /// </summary>
    private static bool _instanceActive;

    /// <summary>
    ///     Pointer to the native LibreOfficeKit instance.
    /// </summary>
    private IntPtr _pOffice;

    /// <summary>
    ///     The office class vtable containing native function pointers.
    /// </summary>
    private readonly LibreOfficeKitClass _officeClass;

    /// <summary>
    ///     Handle to the dynamically loaded LOK native library.
    /// </summary>
    private IntPtr _libraryHandle;

    /// <summary>
    ///     Indicates whether this instance has been disposed.
    /// </summary>
    private bool _disposed;

    /// <summary>
    ///     Optional logger for callback events.
    /// </summary>
    private static ILogger? _logger;

    /// <summary>
    ///     Gets the current logger instance.
    /// </summary>
    internal static ILogger? Logger => _logger;

    /// <summary>
    ///     Library names to search for on Windows.
    /// </summary>
    private static readonly string[] WindowsLibs = ["sofficeapp.dll", "mergedlo.dll"];

    /// <summary>
    ///     Library names to search for on Linux.
    /// </summary>
    private static readonly string[] LinuxLibs = ["libsofficeapp.so", "libmergedlo.so"];

    /// <summary>
    ///     Library names to search for on macOS.
    /// </summary>
    private static readonly string[] MacosLibs = ["libsofficeapp.dylib", "libmergedlo.dylib"];
    #endregion

    #region LibreOfficeInstance
    /// <summary>
    ///     Initializes a new instance of <see cref="Instance" /> from native pointers.
    /// </summary>
    /// <param name="pOffice">Pointer to the native LibreOfficeKit instance.</param>
    /// <param name="libraryHandle">Handle to the loaded native library.</param>
    private Instance(IntPtr pOffice, IntPtr libraryHandle)
    {
        _pOffice = pOffice;
        _libraryHandle = libraryHandle;

        var lok = Marshal.PtrToStructure<LibreOfficeKitStruct>(_pOffice);
        _officeClass = Marshal.PtrToStructure<LibreOfficeKitClass>(lok.pClass);
    }
    #endregion

    #region StringToHGlobalUtf8
    /// <summary>
    ///     Converts a C# string to a UTF-8 encoded string in unmanaged memory, null-terminated.
    /// </summary>
    /// <param name="str">The string to convert.</param>
    /// <returns>A pointer to the UTF-8 encoded string in unmanaged memory.</returns>
    internal static IntPtr StringToHGlobalUtf8(string str)
    {
        var bytes = Encoding.UTF8.GetBytes($"{str}\0");
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return ptr;
    }
    #endregion

    #region Callback Implementation
#if NET5_0_OR_GREATER
    /// <summary>
    ///     Unmanaged callback method for LibreOfficeKit events (.NET 5+).
    ///     This method is called from native code and must be marked with [UnmanagedCallersOnly].
    /// </summary>
    /// <param name="type">The callback event type (see <see cref="CallbackType"/>).</param>
    /// <param name="pPayload">Pointer to a UTF-8 encoded payload string.</param>
    /// <param name="pData">User data pointer (currently unused).</param>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static unsafe void OnLibreOfficeEventUnmanaged(int type, byte* pPayload, IntPtr pData)
    {
        try
        {
            // Convert the UTF-8 payload to a managed string
            var payload = pPayload != null ? Marshal.PtrToStringUTF8((IntPtr)pPayload) ?? string.Empty : string.Empty;

            HandleLibreOfficeEvent(type, payload);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "[LOK Callback Error] Unmanaged callback exception");
        }
    }
#else
    /// <summary>
    ///     Managed callback delegate for LibreOfficeKit events (.NET Standard 2.0).
    /// </summary>
    private static readonly LokCallback2Function CallbackDelegate = OnLibreOfficeEventManaged;

    /// <summary>
    ///     Managed callback method for LibreOfficeKit events (.NET Standard 2.0).
    /// </summary>
    private static void OnLibreOfficeEventManaged(int type, string payload, IntPtr pData)
    {
        try
        {
            HandleLibreOfficeEvent(type, payload ?? string.Empty);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "[LOK Callback Error] Managed callback exception");
        }
    }
#endif

    /// <summary>
    ///     Handles a LibreOfficeKit callback event.
    /// </summary>
    /// <param name="type">The callback event type.</param>
    /// <param name="payload">The event payload.</param>
    private static void HandleLibreOfficeEvent(int type, string payload)
    {
        var callbackType = (CallbackType)type;

        // Log the event
       // _logger?.LogDebug("[LOK Event] Type: '{Type}' ({TypeId}) | Payload: '{Payload}'", callbackType, type, payload);

        switch (callbackType)
        {
            case CallbackType.Error:
                _logger?.LogError("[LOK Error] '{Payload}'", payload);
                break;

            case CallbackType.StatusIndicatorStart:
                _logger?.LogInformation("[LOK Status] Operation started: '{Payload}'", payload);
                break;

            case CallbackType.StatusIndicatorSetValue:
                _logger?.LogDebug("[LOK Progress] '{Payload}'", payload);
                break;

            case CallbackType.StatusIndicatorFinish:
                _logger?.LogInformation("[LOK Status] Operation finished");
                break;

            case CallbackType.DocumentPassword:
            case CallbackType.DocumentPasswordToModify:
                _logger?.LogWarning("[LOK Password] Document requires password: '{Payload}'", payload);
                break;

            case CallbackType.DocumentSizeChanged:
                _logger?.LogInformation("[LOK Document] Size changed: '{Payload}'", payload);
                break;

            case CallbackType.InvalidateTiles:
                _logger?.LogDebug("[LOK Render] Tile invalidation: '{Payload}'", payload);
                break;

            case CallbackType.Window:
                _logger?.LogInformation("[LOK Window] Window event: '{Payload}'", payload);
                break;

            case CallbackType.Jsdialog:
                _logger?.LogWarning("[LOK Dialog] JavaScript dialog detected: '{Payload}'", payload);
                break;

            case CallbackType.UnoCommandResult:
                _logger?.LogDebug("[LOK UNO] Command result: '{Payload}'", payload);
                break;

            case CallbackType.StateChanged:
                _logger?.LogDebug("[LOK State] State changed: '{Payload}'", payload);
                break;

            case CallbackType.ContextMenu:
                _logger?.LogDebug("[LOK Menu] Context menu: '{Payload}'", payload);
                break;

            case CallbackType.FontsMissing:
                _logger?.LogWarning("[LOK Fonts] Missing fonts: '{Payload}'", payload);
                break;

            case CallbackType.ProfileFrame:
                _logger?.LogDebug("[LOK Profile] Frame timing: '{Payload}'", payload);
                break;

            default:
                // Log all other events at trace level to catch unexpected behavior
                _logger?.LogTrace("[LOK Event] Unhandled type '{Type}': '{Payload}'", callbackType, payload);
                break;
        }
    }
    #endregion

    #region SetLogger
    /// <summary>
    ///     Sets the logger for LibreOfficeKit callback events.
    /// </summary>
    /// <param name="logger">The logger to use for callback events.</param>
    public static void SetLogger(ILogger? logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Enables console logging for direct mode.
    /// </summary>
    /// <param name="minLevel">The minimum log level to output. Defaults to Information.</param>
    public static void EnableConsoleLogging(LogLevel minLevel = LogLevel.Information)
    {
        _logger = new ConsoleLogger("LibreOfficeKit", minLevel);
    }
    #endregion

    #region Create
    /// <summary>
    ///     Creates a new LibreOffice instance from the specified install path.
    ///     Loads the native LOK library, calls <c>libreofficekit_hook</c> to initialize,
    ///     and verifies that no initialization errors occurred.
    /// </summary>
    /// <param name="installPath">Absolute path to the LibreOffice program directory.</param>
    /// <returns>A new <see cref="Instance" />.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an instance is already active or initialization fails.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the install path does not exist.</exception>
    public static Instance Create(string installPath)
    {
        if (_instanceActive)
            throw new InvalidOperationException("Only one LibreOffice instance can be active at a time (LOK is not thread-safe).");

        installPath = Path.GetFullPath(installPath);
        if (!Directory.Exists(installPath))
            throw new DirectoryNotFoundException($"LibreOffice install path not found: {installPath}");

        var libraryHandle = LoadLokLibrary(installPath);

        IntPtr pOffice;
        var pInstallPath = IntPtr.Zero;
        var pUserProfile = IntPtr.Zero;

        try
        {
            if (NativeLibrary.TryGetExport(libraryHandle, "libreofficekit_hook_2", out var hook2Ptr))
            {
                var hook2 = Marshal.GetDelegateForFunctionPointer<LokHook2Function>(hook2Ptr);
                var tempProfile = Path.Combine(Path.GetTempPath(), $"lok_profile_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempProfile);

                var registryPath = Path.Combine(tempProfile, "user", "registrymodifications.xcu");
                Directory.CreateDirectory(Path.GetDirectoryName(registryPath)!);
                File.WriteAllText(registryPath, """
                                                <?xml version="1.0" encoding="UTF-8"?>
                                                <oor:items xmlns:oor="http://openoffice.org" xmlns:xs="http://w3.org" xmlns:xsi="http://w3.org-instance">
                                                
                                                    <!-- Disable all GUI dialogs and first-run wizards -->
                                                    <item oor:path="/org.openoffice.Office.Common/Misc"><prop oor:name="UseSystemFileDialog" oor:op="fuse"><value>false</value></prop></item>
                                                    <item oor:path="/org.openoffice.Office.Common/Misc"><prop oor:name="FirstRun" oor:op="fuse"><value>false</value></prop></item>
                                                    <item oor:path="/org.openoffice.Office.Common/Misc"><prop oor:name="ShowTipOfTheDay" oor:op="fuse"><value>false</value></prop></item>
                                                    <item oor:path="/org.openoffice.Setup/Office"><prop oor:name="ooSetupShowIntro" oor:op="fuse"><value>false</value></prop></item>
                                                
                                                    <!-- Block all macro execution -->
                                                    <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="MacroSecurityLevel" oor:op="fuse"><value>3</value></prop></item>
                                                    <item oor:path="/org.openoffice.Office.Common/Security/Scripting"><prop oor:name="RemovePersonalInfoOnSaving" oor:op="fuse"><value>false</value></prop></item>
                                                    <item oor:path="/org.openoffice.Office.BasicIDE/EditorSettings"><prop oor:name="MacroRecorderMode" oor:op="fuse"><value>false</value></prop></item>
                                                
                                                    <!-- Disable auto-save and crash recovery (not needed in headless/LOK) -->
                                                    <item oor:path="/org.openoffice.Office.Common/Save/Document"><prop oor:name="AutoSave" oor:op="fuse"><value>false</value></prop></item>
                                                    <item oor:path="/org.openoffice.Office.Recovery/AutoSave"><prop oor:name="Enabled" oor:op="fuse"><value>false</value></prop></item>
                                                
                                                    <!-- Fix locale to prevent font and date formatting inconsistencies across machines -->
                                                    <item oor:path="/org.openoffice.Setup/L10N"><prop oor:name="ooSetupSystemLocale" oor:op="fuse"><value>en-US</value></prop></item>
                                                
                                                    <!-- Disable printer setup entirely (fixes the Calc hang) -->
                                                    <item oor:path="/org.openoffice.Office.Common/Print/Option"><prop oor:name="PrinterSetupOption" oor:op="fuse"><value>false</value></prop></item>
                                                
                                                    <!-- Disable hardware acceleration (causes hangs in Calc/Impress on headless Windows) -->
                                                    <item oor:path="/org.openoffice.Office.Common/VCL"><prop oor:name="UseOpenGL" oor:op="fuse"><value>false</value></prop></item>
                                                    <item oor:path="/org.openoffice.Office.Common/VCL"><prop oor:name="ForceOpenGL" oor:op="fuse"><value>false</value></prop></item>
                                                    <item oor:path="/org.openoffice.Office.Calc/Calculate"><prop oor:name="UseOpenCL" oor:op="fuse"><value>false</value></prop></item>
                                                    <item oor:path="/org.openoffice.Office.Calc/Calculate"><prop oor:name="AutoCalculate" oor:op="fuse"><value>false</value></prop></item>
                                                
                                                    <!-- Disable font replacement/checking -->
                                                    <item oor:path="/org.openoffice.Office.Common/Font/Substitution"><prop oor:name="Replacement" oor:op="fuse"><value>false</value></prop></item>
                                                
                                                    <!-- Disable spell checking and dictionary loading (prevents threading hang during filter init) -->
                                                    <item oor:path="/org.openoffice.Office.Linguistic/General"><prop oor:name="ActiveDictionaryList" oor:op="fuse"><value></value></prop></item>
                                                    <item oor:path="/org.openoffice.Office.Linguistic/SpellChecking"><prop oor:name="IsSpellAuto" oor:op="fuse"><value>false</value></prop></item>
                                                
                                                </oor:items>
                                                """);

                var profileUrl = $"file:///{tempProfile.Replace('\\', '/')}";
                pInstallPath = StringToHGlobalUtf8(installPath);
                pUserProfile = StringToHGlobalUtf8(profileUrl);
                pOffice = hook2(pInstallPath, pUserProfile);

                var officeStruct = Marshal.PtrToStructure<LibreOfficeKitStruct>(pOffice);
                var vtable = Marshal.PtrToStructure<LibreOfficeKitClass>(officeStruct.pClass);

                // Register callback
                if (vtable.registerCallback != IntPtr.Zero)
                {
#if NET5_0_OR_GREATER
                    unsafe
                    {
                        var registerCallback = (delegate* unmanaged[Cdecl]<IntPtr, delegate* unmanaged[Cdecl]<int, byte*, IntPtr, void>, IntPtr, void>)vtable.registerCallback;

                        // Register the unmanaged callback using a function pointer
                        registerCallback(pOffice, &OnLibreOfficeEventUnmanaged, IntPtr.Zero);

                        _logger?.LogDebug("LibreOffice callback successfully registered via vtable (unmanaged)");
                    }
#else
                    // For .NET Standard 2.0, use the managed delegate approach
                    var registerCallback = Marshal.GetDelegateForFunctionPointer<LokRegisterCallbackFunction>(vtable.registerCallback);
                    registerCallback(pOffice, CallbackDelegate, IntPtr.Zero);
                    _logger?.LogDebug("LibreOffice callback successfully registered via vtable (unmanaged)");
#endif
                }

              

            }
            else
            {
                NativeLibrary.Free(libraryHandle);
                throw new InvalidOperationException("Could not find 'libreofficekit_hook_2' export in LibreOffice library.");
            }
        }
        finally
        {
            if (pInstallPath != IntPtr.Zero)
                Marshal.FreeHGlobal(pInstallPath);
            if (pUserProfile != IntPtr.Zero)
                Marshal.FreeHGlobal(pUserProfile);
        }

        if (pOffice == IntPtr.Zero)
        {
            NativeLibrary.Free(libraryHandle);
            throw new InvalidOperationException("libreofficekit_hook2 returned null. LibreOffice initialization failed.");
        }

        _instanceActive = true;
        var instance = new Instance(pOffice, libraryHandle);
        var initError = instance.GetError();
        if (initError == null) return instance;

        instance.Dispose();
        throw new InvalidOperationException($"LibreOffice initialization error: {initError}");
    }
    #endregion

    #region LoadLokLibrary
    /// <summary>
    ///     Loads the LOK shared library from the install path.
    ///     Tries the primary library first, then the merged library as fallback.
    /// </summary>
    /// <param name="installPath">Path to the LibreOffice program directory.</param>
    /// <returns>A native library handle.</returns>
    /// <exception cref="FileNotFoundException">Thrown when no LOK library can be found or loaded.</exception>
    private static IntPtr LoadLokLibrary(string installPath)
    {
        var libNames = GetLibraryNames();

        foreach (var libName in libNames)
        {
            var libPath = Path.Combine(installPath, libName);
            if (!File.Exists(libPath)) continue;

            if (NativeLibrary.TryLoad(libPath, out var handle))
                return handle;
        }

        throw new FileNotFoundException($"Could not find or load LibreOffice library in: {installPath}. Searched for: {string.Join(", ", GetLibraryNames())}.");
    }
    #endregion

    #region GetLibraryNames
    /// <summary>
    ///     Returns the platform-specific LOK library file names to search for.
    /// </summary>
    /// <returns>An array of library file names.</returns>
    private static string[] GetLibraryNames()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return WindowsLibs;
        return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? MacosLibs : LinuxLibs;
    }
    #endregion

    #region FindInstallPath
    /// <summary>
    ///     Finds the LibreOffice install path from known locations.
    ///     Checks the <c>LOK_PROGRAM_PATH</c> environment variable first,
    ///     then platform-specific known paths, and finally <c>/opt</c> for versioned installs.
    /// </summary>
    /// <returns>The path to the LibreOffice program directory, or <c>null</c> if not found.</returns>
    public static string? FindInstallPath()
    {
        var envPath = Environment.GetEnvironmentVariable("LOK_PROGRAM_PATH");
        if (!string.IsNullOrEmpty(envPath) && Directory.Exists(envPath))
            return envPath;

        string[] knownPaths;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            knownPaths =
            [
                "/usr/lib64/libreoffice/program",
                "/usr/lib/libreoffice/program"
            ];
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            knownPaths =
            [
                "/usr/lib64/libreoffice/program",
                "/usr/lib/libreoffice/program",
                "/Applications/LibreOffice.app/Contents/Frameworks"
            ];
        else
            knownPaths =
            [
                @"C:\Program Files\LibreOffice\program",
                @"C:\Program Files (x86)\LibreOffice\program"
            ];

        foreach (var path in knownPaths)
            if (Directory.Exists(path))
                return path;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return null;
        var optPath = FindOptLatest();
        return optPath ?? null;
    }
    #endregion

    #region FindOptLatest
    /// <summary>
    ///     Finds the latest LibreOffice install from the <c>/opt</c> directory by version number.
    /// </summary>
    /// <returns>The program path of the latest install, or <c>null</c> if none found.</returns>
    private static string? FindOptLatest()
    {
        const string optDir = "/opt";
        if (!Directory.Exists(optDir)) return null;

        var installs = new List<(Version version, string path)>();

        foreach (var dir in Directory.GetDirectories(optDir, "libreoffice*"))
        {
            var dirName = Path.GetFileName(dir);
            var versionStr = dirName.StartsWith("libreoffice")
                ? dirName.Substring("libreoffice".Length)
                : null;

            if (versionStr == null || !Version.TryParse(versionStr, out var version))
                continue;

            var programPath = Path.Combine(dir, "program");
            if (Directory.Exists(programPath))
                installs.Add((version, programPath));
        }

        installs.Sort((a, b) => a.version.CompareTo(b.version));
#pragma warning disable IDE0056
        return installs.Count > 0 ? installs[installs.Count - 1].path : null;
#pragma warning restore IDE0056
    }
    #endregion

    #region PathToFileUrl
    /// <summary>
    ///     Converts a file system path to a <c>file://</c> URL that LibreOffice expects.
    /// </summary>
    /// <param name="filePath">The file system path to convert.</param>
    /// <returns>A <c>file://</c> URI string.</returns>
    public static string PathToFileUrl(string filePath)
    {
        filePath = Path.GetFullPath(filePath);
        var uri = new Uri(filePath);
        return uri.AbsoluteUri;
    }
    #endregion

    #region DocumentLoad
    /// <summary>
    ///     Loads a document from the given file URL, automatically selecting the correct
    ///     LibreOffice import filter based on the file extension.
    ///     Uses <c>documentLoadWithOptions</c> when available, falling back to <c>documentLoad</c>.
    /// </summary>
    /// <param name="fileUrl">The <c>file://</c> URL of the document to load.</param>
    /// <returns>A <see cref="Document" /> representing the loaded document.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the document cannot be loaded.</exception>
    /// <exception cref="LibreOfficeKit.Exceptions.FilePasswordProtectedException">Thrown when the document is password-protected.</exception>
    /// <exception cref="LibreOfficeKit.Exceptions.FileTypeNotSupportedException">Thrown when the file type is not supported.</exception>
    public Document DocumentLoad(string fileUrl)
    {
        if (_disposed) throw new ObjectDisposedException(GetType().FullName);

        _logger?.LogInformation("Loading document: '{FileUrl}'", fileUrl);

        // Derive a filter name from the file extension so LOK does not have to guess.
        var filterName = GetFilterName(fileUrl);
        _logger?.LogDebug("Resolved filter name: '{FilterName}' for file '{FileUrl}'", filterName ?? "(auto-detect)", fileUrl);

        var optionsList = new List<string> 
        { 
            "Hidden=true", 
            "MacroExecutionMode=4",
            "TiledRendering=true",
            "ReadOnly=true",
            "UpdateDocMode=0",
            "InteractionHandler=null"
        };

        if (!string.IsNullOrEmpty(filterName)) optionsList.Add($"FilterName={filterName}");

        var options = string.Join(",", optionsList);

        _logger?.LogDebug("Load options: '{Options}'", options);

        IntPtr pDoc;
        if (_officeClass.documentLoadWithOptions != IntPtr.Zero)
        {
            var loadWithOptions = Marshal.GetDelegateForFunctionPointer<LokDocumentLoadWithOptionsFunction>(_officeClass.documentLoadWithOptions);

            var pUrl = StringToHGlobalUtf8(fileUrl);
            var pOptions = StringToHGlobalUtf8(options);

            try
            {
                _logger?.LogDebug("Calling documentLoadWithOptions...");

                pDoc = loadWithOptions(_pOffice, pUrl, pOptions);

                _logger?.LogDebug("documentLoadWithOptions returned pointer: {Pointer:X}", (long)pDoc);
            }
            finally
            {
                Marshal.FreeHGlobal(pUrl);
                Marshal.FreeHGlobal(pOptions);
            }
        }
        else
        {
            if (_officeClass.documentLoad == IntPtr.Zero)
                throw new InvalidOperationException("documentLoad function not available.");

            var documentLoad = Marshal.GetDelegateForFunctionPointer<LokDocumentLoadFunction>(_officeClass.documentLoad);
            var pUrl = StringToHGlobalUtf8(fileUrl);

            try
            {
                _logger?.LogDebug("Calling documentLoad...");

                pDoc = documentLoad(_pOffice, pUrl);

                _logger?.LogDebug("documentLoad returned pointer: {Pointer:X}", (long)pDoc);
            }
            finally
            {
                Marshal.FreeHGlobal(pUrl);
            }
        }

        _logger?.LogDebug("Checking for errors...");

        var error = GetError();
        if (error == null)
        {
            _logger?.LogInformation("Document loaded successfully");

            return pDoc == IntPtr.Zero
                ? throw new InvalidOperationException("documentLoad returned null pointer.")
                : new Document(pDoc);
        }

        // Check if the error indicates a password-protected document
        if (error.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("protected", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exceptions.FilePasswordProtectedException($"Document is password-protected: '{error}'");
        }

        // Check if the error indicates an unsupported file type
        if (error.Contains("format", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("filter", StringComparison.OrdinalIgnoreCase))
        {
            throw new Exceptions.FileTypeNotSupportedException($"Failed to load document: '{error}'");
        }

        throw new InvalidOperationException($"Failed to load document: '{error}'");
    }
    #endregion

    #region GetFilterName
    /// <summary>
    ///     Returns the LibreOffice import filter name for a given file URL or path,
    ///     based on the file extension. Returns <c>null</c> when the extension is unknown
    ///     so LOK can fall back to auto-detection.
    /// </summary>
    private static string? GetFilterName(string fileUrl)
    {
        var ext = Path.GetExtension(fileUrl).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            // ── Writer ──────────────────────────────────────────────────────
            "odt"    => "writer8",
            "ott"    => "writer8_template",
            "odm"    => "writerglobal8",
            "oth"    => "writerweb8_writer_template",
            "doc"    => "MS Word 97",
            "dot"    => "MS Word 97 Vorlage",
            "docx"   => "MS Word 2007 XML",
            "docm"   => "MS Word 2007 XML",
            "dotx"   => "MS Word 2007 XML Template",
            "dotm"   => "MS Word 2007 XML Template",
            "rtf"    => "Rich Text Format",
            "txt"    => "Text",
            "fodt"   => "OpenDocument Text Flat XML",
            "uot"    => "UOF Text",
            "wpd"    => "WordPerfect",
            "wps"    => "MS Works",
            "lwp"    => "Lotus WordPro",
            "pages"  => "writer_pages",
            "fb2"    => "FictionBook 2",
            "epub"   => "EPUB",
            "html"   => "HTML (StarWriter)",
            "htm"    => "HTML (StarWriter)",
            "xhtml"  => "XHTML Writer File",
            "sxw"    => "StarWriter 5.5",
            "sdw"    => "StarWriter 3.0",
            "vor"    => "StarWriter 5.5 Vorlage",
            "pdb"    => "PalmDoc",
            "abw"    => "AbiWord",
            "zabw"   => "AbiWord",
            "hwp"    => "writer_HWP",
            "hwpx"   => "writer_HWPX",
            "602"    => "T602Document",

            // ── Calc ────────────────────────────────────────────────────────
            "ods"    => "calc8",
            "ots"    => "calc8_template",
            "xls"    => "MS Excel 97",
            "xlt"    => "MS Excel 97 Vorlage",
            "xlsx"   => "Calc MS Excel 2007 XML",
            "xlsm"   => "Calc MS Excel 2007 XML",
            "xltx"   => "Calc MS Excel 2007 XML Template",
            "xltm"   => "Calc MS Excel 2007 XML Template",
            "xlsb"   => "MS Excel 2007 Binary",
            "csv"    => "Text - txt - csv (StarCalc)",
            "tsv"    => "Text - txt - csv (StarCalc)",
            "dif"    => "DIF",
            "slk"    => "SYLK",
            "fods"   => "OpenDocument Spreadsheet Flat XML",
            "uos"    => "UOF Spreadsheet",
            "sxc"    => "StarCalc 5.5",
            "sdc"    => "StarCalc 3.0",
            "numbers" => "calc_numbers",
            "wk1"    => "Lotus",
            "wk3"    => "Lotus",
            "wk4"    => "Lotus",
            "wb2"    => "Quattro Pro 6.0",

            // ── Impress ─────────────────────────────────────────────────────
            "odp"    => "impress8",
            "otp"    => "impress8_template",
            "ppt"    => "MS PowerPoint 97",
            "pot"    => "MS PowerPoint 97 Vorlage",
            "pps"    => "MS PowerPoint 97",
            "pptx"   => "Impress MS PowerPoint 2007 XML",
            "pptm"   => "Impress MS PowerPoint 2007 XML",
            "potx"   => "Impress MS PowerPoint 2007 XML Template",
            "potm"   => "Impress MS PowerPoint 2007 XML Template",
            "ppsx"   => "Impress MS PowerPoint 2007 XML",
            "fodp"   => "OpenDocument Presentation Flat XML",
            "uop"    => "UOF Presentation",
            "sxi"    => "StarImpress 5.5",
            "sdd"    => "StarDraw 3.0",
            "key"    => "impress_key",
            "cgm"    => "CGM - Computer Graphics Metafile",

            // ── Draw ─────────────────────────────────────────────────────────
            "odg"    => "draw8",
            "otg"    => "draw8_template",
            "fodg"   => "OpenDocument Drawing Flat XML",
            "sxd"    => "StarDraw 5.5",
            "std"    => "StarDraw 5.5 Vorlage",
            "svg"    => "draw_svg_Export",
            "svgz"   => "draw_svg_Export",
            "wmf"    => "WMF",
            "emf"    => "EMF",
            "vsd"    => "Visio",
            "vsdx"   => "Visio 2013",
            "vsdm"   => "Visio 2013",
            "vss"    => "Visio",
            "vst"    => "Visio",
            "pub"    => "Publisher",
            "cdr"    => "Corel Draw",
            "fh"     => "FreeHand",
            "fh4"    => "FreeHand",
            "fh5"    => "FreeHand",
            "fh8"    => "FreeHand",
            "fh9"    => "FreeHand",
            "fh10"   => "FreeHand",
            "fh11"   => "FreeHand",

            // ── Math ─────────────────────────────────────────────────────────
            "odf"    => "math8",
            "mml"    => "MathML XML (Math)",
            "sxm"    => "StarMath 5.5",

            // ── Raster images (Draw opens these) ─────────────────────────────
            "bmp"    => "BMP",
            "gif"    => "GIF",
            "jpg"    => "JPEG",
            "jpeg"   => "JPEG",
            "jpe"    => "JPEG",
            "jfif"   => "JPEG",
            "png"    => "PNG",
            "tif"    => "TIFF",
            "tiff"   => "TIFF",
            "webp"   => "WEBP",
            "pbm"    => "PBM",
            "pgm"    => "PGM",
            "ppm"    => "PPM",
            "pnm"    => "PNM",
            "xpm"    => "XPM",
            "pcx"    => "PCX",
            "psd"    => "PSD",
            "tga"    => "TGA",
            "ico"    => "ICO",
            "cur"    => "ICO",
            "eps"    => "EPS",
            "met"    => "MET",
            "svm"    => "SVM",
            "xbm"    => "XBM",
            "dxf"    => "DXF",

            // ── PDF (import via Draw/Writer) ──────────────────────────────────
            "pdf"    => "draw_pdf_import",

            // ── Database ─────────────────────────────────────────────────────
            "odb"    => "StarBase",

            _        => null
        };
    }
    #endregion

    #region GetError
    /// <summary>
    ///     Retrieves the last error message from the LibreOffice instance.
    ///     Returns <c>null</c> if there is no error.
    /// </summary>
    /// <returns>The error message string, or <c>null</c> if no error occurred.</returns>
    public string? GetError()
    {
        if (_officeClass.getError == IntPtr.Zero) return null;

        var getError = Marshal.GetDelegateForFunctionPointer<LokGetErrorFunction>(_officeClass.getError);
        var rawError = getError(_pOffice);

        if (rawError == IntPtr.Zero) return null;

        var firstByte = Marshal.ReadByte(rawError);
        if (firstByte == 0) return null;

        var errorMessage = Marshal.PtrToStringAnsi(rawError) ?? "Unknown error";

        if (_officeClass.freeError == IntPtr.Zero) return errorMessage;
        var freeError = Marshal.GetDelegateForFunctionPointer<LokFreeErrorFunction>(_officeClass.freeError);
        freeError(rawError);

        return errorMessage;
    }
    #endregion

    #region Dispose
    /// <summary>
    ///     Releases native resources and the global LOK instance lock.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_pOffice != IntPtr.Zero && _officeClass.destroy != IntPtr.Zero)
        {
            var destroy = Marshal.GetDelegateForFunctionPointer<LokDestroyFunction>(_officeClass.destroy);
            destroy(_pOffice);
        }

        _pOffice = IntPtr.Zero;
        _libraryHandle = IntPtr.Zero;

        lock (GlobalLock)
        {
            _instanceActive = false;
        }
    }
    #endregion
}