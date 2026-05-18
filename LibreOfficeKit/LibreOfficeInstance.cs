// =============================================================================
// LibreOfficeInstance.cs
//
// Manages the lifecycle of a LibreOfficeKit instance. Handles dynamic loading
// of the native LOK library, initialization via libreofficekit_hook, and
// document loading. A global lock ensures only one instance is active at a
// time since LOK is NOT thread-safe.
// =============================================================================

using System.Runtime.InteropServices;

namespace LibreOfficeKit;

/// <summary>
///     Main LibreOffice instance managing the LOK lifecycle.
///     Only one instance may be active at a time within a single process.
/// </summary>
public sealed class LibreOfficeInstance : IDisposable
{
    #region Fields
    /// <summary>Global lock ensuring only one LOK instance is active at a time.</summary>
    private static readonly object s_globalLock = new();

    /// <summary>Tracks whether an instance is currently active.</summary>
    private static bool s_instanceActive;

    /// <summary>Pointer to the native LibreOfficeKit instance.</summary>
    private IntPtr _pOffice;

    /// <summary>The office class vtable containing native function pointers.</summary>
    private readonly LibreOfficeKitClass _officeClass;

    /// <summary>Handle to the dynamically loaded LOK native library.</summary>
    private IntPtr _libraryHandle;

    /// <summary>Indicates whether this instance has been disposed.</summary>
    private bool _disposed;

    /// <summary>Library names to search for on Windows.</summary>
    private static readonly string[] s_windowsLibs = ["sofficeapp.dll", "mergedlo.dll"];

    /// <summary>Library names to search for on Linux.</summary>
    private static readonly string[] s_linuxLibs = ["libsofficeapp.so", "libmergedlo.so"];

    /// <summary>Library names to search for on macOS.</summary>
    private static readonly string[] s_macosLibs = ["libsofficeapp.dylib", "libmergedlo.dylib"];
    #endregion

    #region LibreOfficeInstance
    /// <summary>
    ///     Initializes a new instance of <see cref="LibreOfficeInstance" /> from native pointers.
    /// </summary>
    /// <param name="pOffice">Pointer to the native LibreOfficeKit instance.</param>
    /// <param name="libraryHandle">Handle to the loaded native library.</param>
    private LibreOfficeInstance(IntPtr pOffice, IntPtr libraryHandle)
    {
        _pOffice = pOffice;
        _libraryHandle = libraryHandle;

        var lok = Marshal.PtrToStructure<LibreOfficeKitStruct>(_pOffice);
        _officeClass = Marshal.PtrToStructure<LibreOfficeKitClass>(lok.pClass);
    }
    #endregion

    #region Create
    /// <summary>
    ///     Creates a new LibreOffice instance from the specified install path.
    ///     Loads the native LOK library, calls <c>libreofficekit_hook</c> to initialize,
    ///     and verifies that no initialization errors occurred.
    /// </summary>
    /// <param name="installPath">Absolute path to the LibreOffice program directory.</param>
    /// <returns>A new <see cref="LibreOfficeInstance" />.</returns>
    /// <exception cref="InvalidOperationException">Thrown when an instance is already active or initialization fails.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the install path does not exist.</exception>
    public static LibreOfficeInstance Create(string installPath)
    {
        lock (s_globalLock)
        {
            if (s_instanceActive)
                throw new InvalidOperationException(
                    "Only one LibreOffice instance can be active at a time (LOK is not thread-safe).");

            installPath = Path.GetFullPath(installPath);

            if (!Directory.Exists(installPath))
                throw new DirectoryNotFoundException($"LibreOffice install path not found: {installPath}");

            if (OperatingSystem.IsWindows())
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
                if (!currentPath.Contains(installPath, StringComparison.OrdinalIgnoreCase))
                    Environment.SetEnvironmentVariable("PATH", $"{installPath};{currentPath}");
            }

            var libraryHandle = LoadLokLibrary(installPath);

            if (!NativeLibrary.TryGetExport(libraryHandle, "libreofficekit_hook", out var hookPtr))
            {
                NativeLibrary.Free(libraryHandle);
                throw new InvalidOperationException(
                    "Could not find 'libreofficekit_hook' export in LibreOffice library.");
            }

            var hook = Marshal.GetDelegateForFunctionPointer<LokHookFunction>(hookPtr);

            var pInstallPath = Marshal.StringToHGlobalAnsi(installPath);
            IntPtr pOffice;
            try
            {
                pOffice = hook(pInstallPath);
            }
            finally
            {
                Marshal.FreeHGlobal(pInstallPath);
            }

            if (pOffice == IntPtr.Zero)
            {
                NativeLibrary.Free(libraryHandle);
                throw new InvalidOperationException(
                    "libreofficekit_hook returned null. LibreOffice initialization failed.");
            }

            s_instanceActive = true;

            var instance = new LibreOfficeInstance(pOffice, libraryHandle);

            var initError = instance.GetError();
            if (initError != null)
            {
                instance.Dispose();
                throw new InvalidOperationException($"LibreOffice initialization error: {initError}");
            }

            return instance;
        }
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

        throw new FileNotFoundException(
            $"Could not find or load LibreOffice library in: {installPath}. " +
            $"Searched for: {string.Join(", ", GetLibraryNames())}.");
    }
    #endregion

    #region GetLibraryNames
    /// <summary>
    ///     Returns the platform-specific LOK library file names to search for.
    /// </summary>
    /// <returns>An array of library file names.</returns>
    private static string[] GetLibraryNames()
    {
        if (OperatingSystem.IsWindows()) return s_windowsLibs;
        if (OperatingSystem.IsMacOS()) return s_macosLibs;
        return s_linuxLibs;
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

        if (OperatingSystem.IsLinux())
            knownPaths =
            [
                "/usr/lib64/libreoffice/program",
                "/usr/lib/libreoffice/program"
            ];
        else if (OperatingSystem.IsMacOS())
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

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            var optPath = FindOptLatest();
            if (optPath != null) return optPath;
        }

        return null;
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
                ? dirName["libreoffice".Length..]
                : null;

            if (versionStr == null || !Version.TryParse(versionStr, out var version))
                continue;

            var programPath = Path.Combine(dir, "program");
            if (Directory.Exists(programPath))
                installs.Add((version, programPath));
        }

        installs.Sort((a, b) => a.version.CompareTo(b.version));
        return installs.Count > 0 ? installs[^1].path : null;
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
    ///     Loads a document from the given file URL.
    /// </summary>
    /// <param name="fileUrl">The <c>file://</c> URL of the document to load.</param>
    /// <returns>A <see cref="LoDocument" /> representing the loaded document.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the document cannot be loaded.</exception>
    public LoDocument DocumentLoad(string fileUrl)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_officeClass.documentLoad == IntPtr.Zero)
            throw new InvalidOperationException("documentLoad function not available.");

        var documentLoad = Marshal.GetDelegateForFunctionPointer<LokDocumentLoadFunction>(_officeClass.documentLoad);

        var pUrl = Marshal.StringToHGlobalAnsi(fileUrl);
        IntPtr pDoc;
        try
        {
            pDoc = documentLoad(_pOffice, pUrl);
        }
        finally
        {
            Marshal.FreeHGlobal(pUrl);
        }

        var error = GetError();
        if (error != null)
            throw new InvalidOperationException($"Failed to load document: {error}");

        if (pDoc == IntPtr.Zero)
            throw new InvalidOperationException("documentLoad returned null pointer.");

        return new LoDocument(pDoc);
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

        if (_officeClass.freeError != IntPtr.Zero)
        {
            var freeError = Marshal.GetDelegateForFunctionPointer<LokFreeErrorFunction>(_officeClass.freeError);
            freeError(rawError);
        }

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

        lock (s_globalLock)
        {
            s_instanceActive = false;
        }
    }
    #endregion
}