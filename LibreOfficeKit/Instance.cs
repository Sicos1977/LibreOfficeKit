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
using System.Runtime.InteropServices;

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
        lock (GlobalLock)
        {
            if (_instanceActive)
                throw new InvalidOperationException(
                    "Only one LibreOffice instance can be active at a time (LOK is not thread-safe).");

            installPath = Path.GetFullPath(installPath);

            if (!Directory.Exists(installPath))
                throw new DirectoryNotFoundException($"LibreOffice install path not found: {installPath}");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                if (currentPath.IndexOf(installPath, StringComparison.OrdinalIgnoreCase) < 0)
                    Environment.SetEnvironmentVariable("PATH", $"{installPath};{currentPath}");
            }

            var libraryHandle = LoadLokLibrary(installPath);

            if (!NativeLibrary.TryGetExport(libraryHandle, "libreofficekit_hook", out var hookPtr))
            {
                NativeLibrary.Free(libraryHandle);
                throw new InvalidOperationException("Could not find 'libreofficekit_hook' export in LibreOffice library.");
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
                throw new InvalidOperationException("libreofficekit_hook returned null. LibreOffice initialization failed.");
            }

            _instanceActive = true;

            var instance = new Instance(pOffice, libraryHandle);

            var initError = instance.GetError();
            if (initError == null) return instance;
            instance.Dispose();
            throw new InvalidOperationException($"LibreOffice initialization error: {initError}");

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
        return installs.Count > 0 ? installs[installs.Count - 1].path : null;
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

        // Derive a filter name from the file extension so LOK does not have to guess.
        var filterName = GetFilterName(fileUrl);
        var options = filterName != null ? $"FilterName={filterName}" : null;

        IntPtr pDoc;

        if (_officeClass.documentLoadWithOptions != IntPtr.Zero && options != null)
        {
            var loadWithOptions = Marshal.GetDelegateForFunctionPointer<LokDocumentLoadWithOptionsFunction>(_officeClass.documentLoadWithOptions);

            var pUrl = Marshal.StringToHGlobalAnsi(fileUrl);
            var pOptions = Marshal.StringToHGlobalAnsi(options);
            try
            {
                pDoc = loadWithOptions(_pOffice, pUrl, pOptions);
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
            var pUrl = Marshal.StringToHGlobalAnsi(fileUrl);
            try
            {
                pDoc = documentLoad(_pOffice, pUrl);
            }
            finally
            {
                Marshal.FreeHGlobal(pUrl);
            }
        }

        var error = GetError();
        if (error != null)
        {
            // Check if the error indicates a password-protected document
            if (error.Contains("password", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("encrypted", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("protected", StringComparison.OrdinalIgnoreCase))
            {
                throw new LibreOfficeKit.Exceptions.FilePasswordProtectedException($"Document is password-protected: '{error}'");
            }

            // Check if the error indicates an unsupported file type
            if (error.Contains("format", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("not supported", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
                error.Contains("filter", StringComparison.OrdinalIgnoreCase))
            {
                throw new LibreOfficeKit.Exceptions.FileTypeNotSupportedException($"Failed to load document: '{error}'");
            }

            throw new InvalidOperationException($"Failed to load document: '{error}'");
        }

        return pDoc == IntPtr.Zero ? throw new InvalidOperationException("documentLoad returned null pointer.") : new Document(pDoc);
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