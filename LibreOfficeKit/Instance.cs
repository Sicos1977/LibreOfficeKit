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
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.Text.Json;
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

    #region Constructor
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
#if NETSTANDARD2_0
    /// <summary>
    ///     Converts a C# string to a UTF-8 encoded string in unmanaged memory, null-terminated.
    /// </summary>
    /// <param name="str">The string to convert.</param>
    /// <returns>A pointer to the UTF-8 encoded string in unmanaged memory.</returns>
    internal static IntPtr StringToHGlobalUtf8(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes($"{str}\0");
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return ptr;
    }
#endif
    #endregion

    #region Utf8PtrToString
    /// <summary>
    ///     Converts a UTF-8 encoded unmanaged string pointer to a C# string.
    /// </summary>
    /// <param name="ptr">Pointer to the UTF-8 encoded null-terminated string.</param>
    /// <returns>The managed string, or null if the pointer is IntPtr.Zero.</returns>
    internal static string? Utf8PtrToString(IntPtr ptr)
    {
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (ptr == IntPtr.Zero)
            return null;

#if NET5_0_OR_GREATER
        return Marshal.PtrToStringUTF8(ptr);
#else
        // For .NET Standard 2.0, manually decode UTF-8
        var length = 0;
        while (Marshal.ReadByte(ptr, length) != 0)
            length++;

        if (length == 0)
            return string.Empty;

        var bytes = new byte[length];
        Marshal.Copy(ptr, bytes, 0, length);
        return System.Text.Encoding.UTF8.GetString(bytes);
#endif
    }
    #endregion

    #region Callback Implementation
    /// <summary>
    ///     Managed callback delegate for LibreOfficeKit events.
    /// </summary>
    private static readonly LokCallback2Function CallbackDelegate = OnLibreOfficeEvent;

    /// <summary>
    ///     Callback method for LibreOfficeKit events.
    /// </summary>
    private static void OnLibreOfficeEvent(int type, string? payload, IntPtr pData)
    {
        try
        {
            HandleLibreOfficeEvent(type, payload ?? string.Empty);
        }
        catch (Exception exception)
        {
            _logger?.LogError(exception, "[LOK Callback Error] Callback exception");
        }
    }

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

    #region Create
    /// <summary>
    ///     Creates a new LibreOffice instance from the specified install path.
    ///     Loads the native LOK library, calls <c>libreofficekit_hook</c> to initialize,
    ///     and verifies that no initialization errors occurred.
    /// </summary>
    /// <param name="installPath">Absolute path to the LibreOffice program directory.</param>
    /// <param name="logger">Optional logger for LibreOfficeKit events and diagnostics.</param>
    /// <returns>A new <see cref="Instance" />.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an instance is already active or initialization fails.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the install path does not exist.</exception>
    public static Instance Create(string installPath, ILogger? logger = null)
    {
        _logger = logger;

        _logger?.LogInformation("Initializing LibreOffice...");

        if (_instanceActive)
            throw new InvalidOperationException("Only one LibreOffice instance can be active at a time (LOK is not thread-safe).");

        installPath = Path.GetFullPath(installPath);
        if (!Directory.Exists(installPath))
            throw new DirectoryNotFoundException($"LibreOffice install path not found: {installPath}");

        var libraryHandle = LoadLokLibrary(installPath);

        IntPtr pOffice;

        if (NativeLibrary.TryGetExport(libraryHandle, "libreofficekit_hook_2", out var hook2Ptr))
        {
            var hook2 = Marshal.GetDelegateForFunctionPointer<LokHook2Function>(hook2Ptr);
            var tempProfile = Path.Combine(Path.GetTempPath(), $"lok_profile_{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempProfile);

                var registryPath = Path.Combine(tempProfile, "user", "registrymodifications.xcu");
                Directory.CreateDirectory(Path.GetDirectoryName(registryPath)!);
                File.WriteAllText(registryPath,
                    /*lang=xml*/
                    """
                    <?xml version="1.0" encoding="UTF-8"?>
                    <oor:items xmlns:oor="http://openoffice.org/2001/registry"
                               xmlns:xs="http://www.w3.org/2001/XMLSchema"
                               xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

                      <!-- ===================================================================
                           SECTION 1: GPU / Hardware Acceleration — Prevent GPU driver hangs
                           =================================================================== -->

                      <!-- Disable OpenGL rendering (can hang on GPU init) -->
                      <item oor:path="/org.openoffice.Office.Common/VCL">
                        <prop oor:name="UseOpenGL" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>
                      <item oor:path="/org.openoffice.Office.Common/VCL">
                        <prop oor:name="ForceOpenGL" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable OpenCL (Calc formula acceleration — can hang on GPU init) -->
                      <item oor:path="/org.openoffice.Office.Common/Misc">
                        <prop oor:name="UseOpenCL" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable hardware acceleration globally -->
                      <item oor:path="/org.openoffice.Office.Common/VCL">
                        <prop oor:name="UseHardwareAcceleration" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable Skia rendering (newer LO versions) -->
                      <item oor:path="/org.openoffice.Office.Common/VCL">
                        <prop oor:name="UseSkia" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>
                      <item oor:path="/org.openoffice.Office.Common/VCL">
                        <prop oor:name="ForceSkia" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>
                      <item oor:path="/org.openoffice.Office.Common/VCL">
                        <prop oor:name="ForceSkiaRaster" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable anti-aliasing (reduces rendering complexity) -->
                      <item oor:path="/org.openoffice.Office.Common/View/FontAntiAliasing">
                        <prop oor:name="Enabled" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- ===================================================================
                           SECTION 2: Printer — THE PRIMARY CAUSE of .xlsx/.pptx hangs
                           =================================================================== -->

                      <!-- Suppress printer setup dialog -->
                      <item oor:path="/org.openoffice.Office.Common/Print/Option">
                        <prop oor:name="PrinterSetupOption" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Reduce print warnings -->
                      <item oor:path="/org.openoffice.Office.Common/Print/Warning">
                        <prop oor:name="PaperSize" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>
                      <item oor:path="/org.openoffice.Office.Common/Print/Warning">
                        <prop oor:name="PaperOrientation" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>
                      <item oor:path="/org.openoffice.Office.Common/Print/Warning">
                        <prop oor:name="NotFound" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- ===================================================================
                           SECTION 3: Font / Text — Prevent font enumeration deadlocks
                           =================================================================== -->

                      <!-- Disable font substitution table (triggers full font enumeration) -->
                      <item oor:path="/org.openoffice.Office.Common/Font/Substitution">
                        <prop oor:name="Replacement" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable spell checking (loads hunspell + dictionaries) -->
                      <item oor:path="/org.openoffice.Office.Linguistic/SpellChecking">
                        <prop oor:name="IsSpellAuto" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>
                      <item oor:path="/org.openoffice.Office.Linguistic/SpellChecking">
                        <prop oor:name="IsSpellSpecial" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable grammar checking -->
                      <item oor:path="/org.openoffice.Office.Linguistic/GrammarChecking">
                        <prop oor:name="IsAutoCheck" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- ===================================================================
                           SECTION 4: Macros / Security — Prevent macro execution hangs
                           =================================================================== -->

                      <!-- Macro security: 4 = run without any dialogs -->
                      <item oor:path="/org.openoffice.Office.Common/Security/Scripting">
                        <prop oor:name="MacroSecurityLevel" oor:op="fuse">
                          <value>3</value>
                        </prop>
                      </item>

                      <!-- Disable macro execution entirely for safety -->
                      <item oor:path="/org.openoffice.Office.Common/Security/Scripting">
                        <prop oor:name="DisableMacrosExecution" oor:op="fuse">
                          <value>true</value>
                        </prop>
                      </item>

                      <!-- ===================================================================
                           SECTION 5: Auto-save / Recovery — Prevent background threads
                           =================================================================== -->

                      <item oor:path="/org.openoffice.Office.Recovery/AutoSave">
                        <prop oor:name="Enabled" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>
                      <item oor:path="/org.openoffice.Office.Recovery/AutoSave">
                        <prop oor:name="TimeInterval" oor:op="fuse">
                          <value>0</value>
                        </prop>
                      </item>

                      <!-- ===================================================================
                           SECTION 6: Java — Disable JVM loading entirely
                           =================================================================== -->

                      <item oor:path="/org.openoffice.Office.Common/Java/Environment">
                        <prop oor:name="Enable" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- ===================================================================
                           SECTION 7: Network / Update — Prevent network access
                           =================================================================== -->

                      <item oor:path="/org.openoffice.Office.Common/Misc">
                        <prop oor:name="FirstRun" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable auto-update checking -->
                      <item oor:path="/org.openoffice.Office.ExtensionManager/ExtensionUpdateData">
                        <prop oor:name="TimeLastUpdateCheck" oor:op="fuse">
                          <value>0</value>
                        </prop>
                      </item>

                      <!-- ===================================================================
                           SECTION 8: CALC-SPECIFIC SETTINGS (Critical for .xlsx)
                           These settings prevent Calc from initializing subsystems that
                           can hang during document load.
                           =================================================================== -->

                      <!-- Disable Calc's automatic calculation on load -->
                      <!-- This prevents complex formula chains from triggering OpenCL/threading -->
                      <item oor:path="/org.openoffice.Office.Calc/Calculate">
                        <prop oor:name="AutoCalculate" oor:op="fuse">
                          <value>true</value>
                        </prop>
                      </item>

                      <!-- Disable iterative calculation (can cause infinite loops) -->
                      <item oor:path="/org.openoffice.Office.Calc/Calculate">
                        <prop oor:name="IterationCount" oor:op="fuse">
                          <value>1</value>
                        </prop>
                      </item>

                      <!-- Force single-threaded formula calculation -->
                      <!-- Multi-threaded calc can deadlock in LOK in-process mode -->
                      <item oor:path="/org.openoffice.Office.Calc/Formula/Calculation">
                        <prop oor:name="UseThreadedCalculationForFormulaGroups" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable Calc's OpenCL for formulas (belt-and-suspenders with env var) -->
                      <item oor:path="/org.openoffice.Office.Calc/Formula/Calculation">
                        <prop oor:name="UseOpenCL" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable Calc input line auto-pilot -->
                      <item oor:path="/org.openoffice.Office.Calc/Content/Update">
                        <prop oor:name="Link" oor:op="fuse">
                          <value>0</value>
                        </prop>
                      </item>

                      <!-- Disable external link updating in Calc (prevents network access) -->
                      <item oor:path="/org.openoffice.Office.Calc/Content/Update">
                        <prop oor:name="Link" oor:op="fuse">
                          <value>0</value>
                        </prop>
                      </item>

                      <!-- ===================================================================
                           SECTION 9: IMPRESS-SPECIFIC SETTINGS (Critical for .pptx)
                           These settings prevent Impress from initializing presentation
                           subsystems that are unnecessary for conversion.
                           =================================================================== -->

                      <!-- Disable presentation effects/transitions (triggers rendering init) -->
                      <item oor:path="/org.openoffice.Office.Impress/Misc">
                        <prop oor:name="TransitionEffects" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable Impress start center -->
                      <item oor:path="/org.openoffice.Office.Impress/Misc">
                        <prop oor:name="Start/EnableSdremote" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- ===================================================================
                           SECTION 10: UI / Misc — Disable all UI-related features
                           =================================================================== -->

                      <!-- Disable tips and extended tips -->
                      <item oor:path="/org.openoffice.Office.Common/Help">
                        <prop oor:name="Tip" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>
                      <item oor:path="/org.openoffice.Office.Common/Help">
                        <prop oor:name="ExtendedTip" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable event sounds (triggers audio subsystem) -->
                      <item oor:path="/org.openoffice.Office.Common/Accessibility">
                        <prop oor:name="IsAllowAnimatedGraphics" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>
                      <item oor:path="/org.openoffice.Office.Common/Accessibility">
                        <prop oor:name="IsAllowAnimatedText" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable thumbnails/previews in file dialogs -->
                      <item oor:path="/org.openoffice.Office.Common/Save/Document">
                        <prop oor:name="GenerateThumbnail" oor:op="fuse">
                          <value>false</value>
                        </prop>
                      </item>

                      <!-- Disable recent document list (no file access needed) -->
                      <item oor:path="/org.openoffice.Office.Common/History">
                        <prop oor:name="PickListSize" oor:op="fuse">
                          <value>0</value>
                        </prop>
                      </item>

                      <!-- Disable extension updating -->
                      <item oor:path="/org.openoffice.Office.ExtensionManager/ExtensionSecurity">
                        <prop oor:name="DisableExtensionInstallation" oor:op="fuse">
                          <value>true</value>
                        </prop>
                      </item>

                    </oor:items>
                    """);

                var profileUrl = $"file:///{tempProfile.Replace('\\', '/')}";
#if NETSTANDARD2_0
                var pInstallPath = StringToHGlobalUtf8(installPath);
                var pUserProfile = StringToHGlobalUtf8(profileUrl);

                try
                {
                    pOffice = hook2(pInstallPath, pUserProfile);
                }
                finally
                {
                    Marshal.FreeHGlobal(pInstallPath);
                    Marshal.FreeHGlobal(pUserProfile);
                }
#else
                pOffice = hook2(installPath, profileUrl);
#endif

                var officeStruct = Marshal.PtrToStructure<LibreOfficeKitStruct>(pOffice);
                var vtable = Marshal.PtrToStructure<LibreOfficeKitClass>(officeStruct.pClass);

                // Register callback
                if (vtable.registerCallback != IntPtr.Zero)
                {
                    var registerCallback = Marshal.GetDelegateForFunctionPointer<LokRegisterCallbackFunction>(vtable.registerCallback);
                    registerCallback(pOffice, CallbackDelegate, IntPtr.Zero);
                    _logger?.LogDebug("LibreOffice callback successfully registered via vtable");
                }
        }
        else
        {
            NativeLibrary.Free(libraryHandle);
            throw new InvalidOperationException("Could not find 'libreofficekit_hook_2' export in LibreOffice library.");
        }

        if (pOffice == IntPtr.Zero)
        {
            NativeLibrary.Free(libraryHandle);
            throw new InvalidOperationException("libreofficekit_hook2 returned null. LibreOffice initialization failed.");
        }

        _instanceActive = true;
        var instance = new Instance(pOffice, libraryHandle);
        var initError = instance.GetError();
        if (initError == null)
        {
            // Log version information
            instance.LogVersionInfo();

            _logger?.LogInformation("LibreOffice initialized");
            return instance;
        }

        instance.Dispose();
        throw new InvalidOperationException($"LibreOffice initialization error: {initError}");
    }
    #endregion

    #region LogVersionInfo
    /// <summary>
    ///     Retrieves and logs the LibreOffice version information.
    /// </summary>
    private void LogVersionInfo()
    {
        if (_officeClass.getVersionInfo == IntPtr.Zero)
        {
            _logger?.LogWarning("getVersionInfo function not available");
            return;
        }

        try
        {
            var getVersionInfo = Marshal.GetDelegateForFunctionPointer<LokGetVersionInfoFunction>(_officeClass.getVersionInfo);

#if NETSTANDARD2_0
            var pVersionInfo = getVersionInfo(_pOffice);

            if (pVersionInfo == IntPtr.Zero)
            {
                _logger?.LogWarning("getVersionInfo returned null");
                return;
            }

            var versionInfoJson = Utf8PtrToString(pVersionInfo);
#else
            var versionInfoJson = getVersionInfo(_pOffice);
#endif

            if (string.IsNullOrWhiteSpace(versionInfoJson))
            {
                _logger?.LogWarning("getVersionInfo returned empty string");
                return;
            }

            // Parse JSON to version info object
            var versionInfo = JsonSerializer.Deserialize<LibreOfficeVersionInfo>(versionInfoJson);
            if (versionInfo == null)
            {
                _logger?.LogWarning("Failed to parse version info JSON: '{VersionInfoJson}'", versionInfoJson);
                return;
            }

            // Log structured version information
            _logger?.LogInformation(
                "LibreOffice version: '{ProductName}' '{FullVersion}', Build: '{BuildId}'",
                versionInfo.ProductName,
                versionInfo.FullVersion,
                versionInfo.BuildId);
        }
        catch (JsonException jsonEx)
        {
            _logger?.LogWarning(jsonEx, "Failed to parse LibreOffice version JSON");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to retrieve LibreOffice version information");
        }
    }
    #endregion

    #region SetOptionalFeatures
    /// <summary>
    ///     Enables optional LibreOfficeKit features that modify callback behavior and rendering.
    ///     <para>
    ///         Features are specified as a bitmask and can be combined using the bitwise OR operator.
    ///         This method must be called <strong>before</strong> loading any documents.
    ///     </para>
    /// </summary>
    /// <param name="features">
    ///     The features to enable. Combine multiple features using bitwise OR:
    ///     <code>
    ///         instance.SetOptionalFeatures(
    ///             OptionalFeatures.NoTiledAnnotations | 
    ///             OptionalFeatures.DocumentPassword);
    ///     </code>
    /// </param>
    /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if setOptionalFeatures is not available in this LibreOffice version.</exception>
    /// <remarks>
    ///     <para><strong>Available Features:</strong></para>
    ///     <list type="bullet">
    ///         <item>
    ///             <term><see cref="Enums.OptionalFeatures.DocumentPassword"/></term>
    ///             <description>Enable password prompts for encrypted documents.</description>
    ///         </item>
    ///         <item>
    ///             <term><see cref="Enums.OptionalFeatures.DocumentPasswordToModify"/></term>
    ///             <description>Enable password prompts for write-protected documents.</description>
    ///         </item>
    ///         <item>
    ///             <term><see cref="Enums.OptionalFeatures.PartInInvalidationCallback"/></term>
    ///             <description>Include part number in tile invalidation callbacks.</description>
    ///         </item>
    ///         <item>
    ///             <term><see cref="Enums.OptionalFeatures.NoTiledAnnotations"/></term>
    ///             <description>Disable annotation rendering in tiles (performance optimization).</description>
    ///         </item>
    ///         <item>
    ///             <term><see cref="Enums.OptionalFeatures.RangeHeaders"/></term>
    ///             <description>Enable range-based spreadsheet header queries.</description>
    ///         </item>
    ///         <item>
    ///             <term><see cref="Enums.OptionalFeatures.ViewIdInVisibleCursorInvalidationCallback"/></term>
    ///             <description>Include view ID in cursor invalidation callbacks (multi-user scenarios).</description>
    ///         </item>
    ///     </list>
    ///     <para><strong>Example Usage:</strong></para>
    ///     <code>
    ///         // For headless conversion, disable annotations for performance
    ///         instance.SetOptionalFeatures(OptionalFeatures.NoTiledAnnotations);
    ///         
    ///         // For interactive viewer with password support
    ///         instance.SetOptionalFeatures(
    ///             OptionalFeatures.DocumentPassword | 
    ///             OptionalFeatures.DocumentPasswordToModify |
    ///             OptionalFeatures.PartInInvalidationCallback);
    ///     </code>
    ///     <para>
    ///         This feature is available since LibreOffice 6.0.
    ///     </para>
    /// </remarks>
    public void SetOptionalFeatures(OptionalFeatures features)
    {
        if (_disposed) 
            throw new ObjectDisposedException(GetType().FullName);

        if (_officeClass.setOptionalFeatures == IntPtr.Zero)
            throw new InvalidOperationException(
                "setOptionalFeatures function not available in this LibreOffice version. " +
                "This feature requires LibreOffice 6.0 or later.");

        var setOptionalFeatures = Marshal.GetDelegateForFunctionPointer<LokSetOptionalFeaturesFunction>(_officeClass.setOptionalFeatures);

        var featureMask = (ulong)features;
        setOptionalFeatures(_pOffice, featureMask);

        _logger?.LogDebug("Optional features set: '{Features}' (0x{FeatureMask:X})", features, featureMask);
    }
    #endregion

    #region SetDocumentPassword
    /// <summary>
    ///     Pre-registers a password for a specific document URL.
    ///     <para>
    ///         When LibreOfficeKit encounters a password-protected document at the given URL,
    ///         it will automatically use the provided password instead of triggering a
    ///         <see cref="CallbackType.DocumentPassword"/> or 
    ///         <see cref="CallbackType.DocumentPasswordToModify"/> callback.
    ///     </para>
    /// </summary>
    /// <param name="url">
    ///     The document URL (in file:// format). Must match exactly the URL used in 
    ///     <see cref="DocumentLoad"/>.
    /// </param>
    /// <param name="password">
    ///     The password to use for this document. Pass <c>null</c> to clear a previously set password.
    /// </param>
    /// <exception cref="ObjectDisposedException">Thrown if this instance has been disposed.</exception>
    /// <exception cref="InvalidOperationException">Thrown if setDocumentPassword is not available in this LibreOffice version.</exception>
    /// <remarks>
    ///     <para><strong>Usage Pattern:</strong></para>
    ///     <code>
    ///         var instance = Instance.Create(installPath, logger);
    ///         
    ///         // Enable password callback features
    ///         instance.SetOptionalFeatures(
    ///             OptionalFeatures.DocumentPassword | 
    ///             OptionalFeatures.DocumentPasswordToModify);
    ///         
    ///         // Pre-register password for a specific document
    ///         var fileUrl = Instance.PathToFileUrl(@"C:\docs\encrypted.docx");
    ///         instance.SetDocumentPassword(fileUrl, "mySecret123");
    ///         
    ///         // Load will now use the pre-registered password automatically
    ///         var doc = instance.DocumentLoad(fileUrl);
    ///     </code>
    ///     <para>
    ///         <strong>Note:</strong> The URL format must be exactly as LibreOfficeKit expects it
    ///         (file:/// with forward slashes). Use <see cref="PathToFileUrl"/> to convert paths.
    ///     </para>
    ///     <para>
    ///         This feature is available since LibreOffice 6.0.
    ///     </para>
    /// </remarks>
    /// <seealso cref="SetOptionalFeatures"/>
    /// <seealso cref="Enums.OptionalFeatures.DocumentPassword"/>
    /// <seealso cref="Enums.OptionalFeatures.DocumentPasswordToModify"/>
    public void SetDocumentPassword(string url, string? password)
    {
        if (_disposed) 
            throw new ObjectDisposedException(GetType().FullName);

        if (_officeClass.setDocumentPassword == IntPtr.Zero)
            throw new InvalidOperationException(
                "setDocumentPassword function not available in this LibreOffice version. " +
                "This feature requires LibreOffice 6.0 or later.");

        var setDocumentPassword = Marshal.GetDelegateForFunctionPointer<LokSetDocumentPasswordFunction>(
            _officeClass.setDocumentPassword);

#if NETSTANDARD2_0
        var pUrl = StringToHGlobalUtf8(url);
        var pPassword = password != null ? StringToHGlobalUtf8(password) : IntPtr.Zero;

        try
        {
            setDocumentPassword(_pOffice, pUrl, pPassword);
        }
        finally
        {
            Marshal.FreeHGlobal(pUrl);
            if (pPassword != IntPtr.Zero)
                Marshal.FreeHGlobal(pPassword);
        }
#else
        setDocumentPassword(_pOffice, url, password);
#endif

        _logger?.LogDebug(
            password != null
                ? "Document password registered for URL: '{Url}'"
                : "Document password cleared for URL: '{Url}'",
            url);
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
            "MacroExecutionMode=0",
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

            _logger?.LogDebug("Calling documentLoadWithOptions...");

#if NETSTANDARD2_0
            var pUrl = StringToHGlobalUtf8(fileUrl);
            var pOptions = StringToHGlobalUtf8(options);

            try
            {
                pDoc = loadWithOptions(_pOffice, pUrl, pOptions);
            }
            finally
            {
                Marshal.FreeHGlobal(pUrl);
                Marshal.FreeHGlobal(pOptions);
            }
#else
            pDoc = loadWithOptions(_pOffice, fileUrl, options);
#endif

            _logger?.LogDebug("documentLoadWithOptions returned pointer: {Pointer:X}", (long)pDoc);
        }
        else
        {
            if (_officeClass.documentLoad == IntPtr.Zero)
                throw new InvalidOperationException("documentLoad function not available.");

            var documentLoad = Marshal.GetDelegateForFunctionPointer<LokDocumentLoadFunction>(_officeClass.documentLoad);

            _logger?.LogDebug("Calling documentLoad...");

#if NETSTANDARD2_0
            var pUrl = StringToHGlobalUtf8(fileUrl);

            try
            {
                pDoc = documentLoad(_pOffice, pUrl);
            }
            finally
            {
                Marshal.FreeHGlobal(pUrl);
            }
#else
            pDoc = documentLoad(_pOffice, fileUrl);
#endif

            _logger?.LogDebug("documentLoad returned pointer: {Pointer:X}", (long)pDoc);
        }

        _logger?.LogDebug("Checking for errors...");

        var error = GetError();
        if (error == null)
        {
            _logger?.LogInformation("Document loaded successfully");

            return pDoc == IntPtr.Zero
                ? throw new InvalidOperationException("documentLoad returned null pointer.")
                : new Document(pDoc, _logger);
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

        var errorMessage = Utf8PtrToString(rawError) ?? "Unknown error";

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