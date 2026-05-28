# LibreOffice Hang Debug - Mogelijke Oplossingen

## Als documentLoad() hangt (ook zonder filter):

### Oplossing 1: Voeg User Profile toe aan LOK init
LibreOffice heeft soms een temporary user profile nodig.

```csharp
// In Instance.cs Create() methode
// Zoek naar libreofficekit_hook_2 in plaats van libreofficekit_hook

if (NativeLibrary.TryGetExport(libraryHandle, "libreofficekit_hook_2", out var hook2Ptr))
{
	// hook_2 accepteert userProfileUrl parameter
	var hook2 = Marshal.GetDelegateForFunctionPointer<LokHook2Function>(hook2Ptr);

	var tempProfile = Path.Combine(Path.GetTempPath(), $"lok_profile_{Guid.NewGuid():N}");
	Directory.CreateDirectory(tempProfile);

	var pInstallPath = Marshal.StringToHGlobalAnsi(installPath);
	var pUserProfile = Marshal.StringToHGlobalAnsi($"file:///{tempProfile.Replace('\\', '/')}");

	try
	{
		pOffice = hook2(pInstallPath, pUserProfile);
	}
	finally
	{
		Marshal.FreeHGlobal(pInstallPath);
		Marshal.FreeHGlobal(pUserProfile);
	}
}
```

Voeg ook toe aan Bindings.cs:
```csharp
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate IntPtr LokHook2Function(IntPtr pInstallPath, IntPtr pUserProfileUrl);
```

### Oplossing 2: Check LibreOffice versie
Sommige oude LOK versies hebben bugs. Controleer:
```powershell
$loPath = "C:\Program Files\LibreOffice\program"
& "$loPath\soffice.exe" --version
```

Minimaal versie 7.x is aangeraden.

### Oplossing 3: Process Priority
Verhoog de worker process priority:
```csharp
// In Converter.cs SpawnWorkerAsync()
process.PriorityClass = ProcessPriorityClass.AboveNormal;
```

### Oplossing 4: Environment Variables
Voeg toe aan worker process start:
```csharp
processStartInfo.EnvironmentVariables["SAL_NO_MOUSEGRABS"] = "1";
processStartInfo.EnvironmentVariables["SAL_USE_VCLPLUGIN"] = "svp"; // headless plugin
```

### Oplossing 5: File Path Encoding
Controleer of het bestand daadwerkelijk bestaat en leesbaar is:
```csharp
// Voor DocumentLoad()
var actualPath = new Uri(fileUrl).LocalPath;
if (!File.Exists(actualPath))
	throw new FileNotFoundException($"Document not found: {actualPath}");

// Check permissions
using var _ = File.OpenRead(actualPath);
```
