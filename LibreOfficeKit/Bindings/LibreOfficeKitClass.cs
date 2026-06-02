using System.Runtime.InteropServices;

namespace LibreOfficeKit.Bindings;

/// <summary>
///     Represents the LibreOfficeKit vtable containing function pointers for office-level operations.
///     Field order must match the native C header exactly (LibreOfficeKit.h, master branch).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct LibreOfficeKitClass
{
    /// <summary>Size of this struct in bytes.</summary>
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
    ///     Function pointer: <c>LibreOfficeKitDocument* (*documentLoadWithOptions)(LibreOfficeKit* pThis, const char* pURL, const char* pOptions)</c>.
    ///     @since LibreOffice 5.0
    /// </summary>
    public IntPtr documentLoadWithOptions;

    /// <summary>
    ///     Function pointer: <c>void (*freeError)(char* pFree)</c>.
    ///     Generic deallocation function for dynamically allocated memory returned by LibreOfficeKit functions.
    ///     @since LibreOffice 5.2
    /// </summary>
    public IntPtr freeError;

    /// <summary>
    ///     Function pointer: <c>void (*registerCallback)(LibreOfficeKit* pThis, LibreOfficeKitCallback pCallback, void* pData)</c>.
    ///     @since LibreOffice 6.0
    /// </summary>
    public IntPtr registerCallback;

    /// <summary>
    ///     Function pointer: <c>char* (*getFilterTypes)(LibreOfficeKit* pThis)</c>.
    ///     @since LibreOffice 6.0
    /// </summary>
    public IntPtr getFilterTypes;

    /// <summary>
    ///     Function pointer: <c>void (*setOptionalFeatures)(LibreOfficeKit* pThis, unsigned long long features)</c>.
    ///     @since LibreOffice 6.0
    /// </summary>
    public IntPtr setOptionalFeatures;

    /// <summary>
    ///     Function pointer: <c>void (*setDocumentPassword)(LibreOfficeKit* pThis, char const* pURL, char const* pPassword)</c>.
    ///     @since LibreOffice 6.0
    /// </summary>
    public IntPtr setDocumentPassword;

    /// <summary>
    ///     Function pointer: <c>char* (*getVersionInfo)(LibreOfficeKit* pThis)</c>.
    ///     @since LibreOffice 6.0
    /// </summary>
    public IntPtr getVersionInfo;

    /// <summary>
    ///     Function pointer: <c>int (*runMacro)(LibreOfficeKit* pThis, const char* pURL)</c>.
    ///     @since LibreOffice 6.0
    /// </summary>
    public IntPtr runMacro;

    /// <summary>
    ///     Function pointer: <c>bool (*signDocument)(LibreOfficeKit* pThis, const char* pUrl,
    ///     const unsigned char* pCertificateBinary, const int nCertificateBinarySize,
    ///     const unsigned char* pPrivateKeyBinary, const int nPrivateKeyBinarySize)</c>.
    ///     @since LibreOffice 6.2
    /// </summary>
    public IntPtr signDocument;

    /// <summary>
    ///     Function pointer: <c>void (*runLoop)(LibreOfficeKit* pThis, LibreOfficeKitPollCallback pPollCallback,
    ///     LibreOfficeKitWakeCallback pWakeCallback, void* pData)</c>.
    /// </summary>
    public IntPtr runLoop;

    /// <summary>
    ///     Function pointer: <c>void (*sendDialogEvent)(LibreOfficeKit* pThis, unsigned long long int nLOKWindowId,
    ///     const char* pArguments)</c>.
    /// </summary>
    public IntPtr sendDialogEvent;

    /// <summary>
    ///     Function pointer: <c>void (*setOption)(LibreOfficeKit* pThis, const char* pOption, const char* pValue)</c>.
    /// </summary>
    public IntPtr setOption;

    /// <summary>
    ///     Function pointer: <c>void (*dumpState)(LibreOfficeKit* pThis, const char* pOptions, char** pState)</c>.
    ///     @since LibreOffice 7.5
    /// </summary>
    public IntPtr dumpState;

    /// <summary>
    ///     Function pointer: <c>char* (*extractRequest)(LibreOfficeKit* pThis, const char* pFilePath)</c>.
    /// </summary>
    public IntPtr extractRequest;

    /// <summary>
    ///     Function pointer: <c>void (*trimMemory)(LibreOfficeKit* pThis, int nTarget)</c>.
    ///     @since LibreOffice 7.6
    /// </summary>
    public IntPtr trimMemory;

    /// <summary>
    ///     Function pointer: <c>void* (*startURP)(LibreOfficeKit* pThis, void* pReceiveURPFromLOContext,
    ///     void* pSendURPToLOContext, int (*fnReceiveURPFromLO)(...), int (*fnSendURPToLO)(...))</c>.
    /// </summary>
    public IntPtr startURP;

    /// <summary>
    ///     Function pointer: <c>void (*stopURP)(LibreOfficeKit* pThis, void* pSendURPToLOContext)</c>.
    /// </summary>
    public IntPtr stopURP;

    /// <summary>
    ///     Function pointer: <c>int (*joinThreads)(LibreOfficeKit* pThis)</c>.
    /// </summary>
    public IntPtr joinThreads;

    /// <summary>
    ///     Function pointer: <c>void (*startThreads)(LibreOfficeKit* pThis)</c>.
    /// </summary>
    public IntPtr startThreads;

    /// <summary>
    ///     Function pointer: <c>void (*setForkedChild)(LibreOfficeKit* pThis, bool bIsChild)</c>.
    /// </summary>
    public IntPtr setForkedChild;

    /// <summary>
    ///     Function pointer: <c>char* (*extractDocumentStructureRequest)(LibreOfficeKit* pThis,
    ///     const char* pFilePath, const char* pFilter)</c>.
    /// </summary>
    public IntPtr extractDocumentStructureRequest;

    /// <summary>
    ///     Function pointer: <c>void (*registerAnyInputCallback)(LibreOfficeKit* pThis,
    ///     LibreOfficeKitAnyInputCallback pCallback, void* pData)</c>.
    /// </summary>
    public IntPtr registerAnyInputCallback;

    /// <summary>
    ///     Function pointer: <c>int (*getDocsCount)(LibreOfficeKit* pThis)</c>.
    /// </summary>
    public IntPtr getDocsCount;

    /// <summary>
    ///     Function pointer: <c>void (*registerFileSaveDialogCallback)(LibreOfficeKit* pThis,
    ///     LibreOfficeKitFileSaveDialogCallback pCallback)</c>.
    /// </summary>
    public IntPtr registerFileSaveDialogCallback;
}