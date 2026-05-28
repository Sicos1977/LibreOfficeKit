using System.Diagnostics;

namespace LibreOfficeTest;

[TestClass]
public class DiagnosticTests
{
    [TestMethod]
    public void TestWorkerExeExists()
    {
        var workerPath = @"..\..\..\..\LibreOfficeKit.Console\bin\Debug\net10.0\LibreOfficeKit.Console.exe";
        Console.WriteLine($"Looking for worker at: {Path.GetFullPath(workerPath)}");
        Console.WriteLine($"Current directory: {Environment.CurrentDirectory}");
        Console.WriteLine($"File exists: {File.Exists(workerPath)}");
        Assert.IsTrue(File.Exists(workerPath), $"Worker executable not found at {Path.GetFullPath(workerPath)}");
    }

    [TestMethod]
    public void TestWorkerCanStart()
    {
        var workerPath = @"..\..\..\..\LibreOfficeKit.Console\bin\Debug\net10.0\LibreOfficeKit.Console.exe";
        Console.WriteLine($"Attempting to start worker: {workerPath}");

        var startInfo = new ProcessStartInfo
        {
            FileName = workerPath,
            Arguments = "--help",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);
        Assert.IsNotNull(process, "Failed to start process");

        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();

        process.WaitForExit(5000);

        Console.WriteLine($"Exit code: {process.ExitCode}");
        Console.WriteLine($"Output: {output}");
        Console.WriteLine($"Error: {error}");

        Assert.IsTrue(process.HasExited, "Process did not exit within timeout");
    }

    [TestMethod]
    public void TestLibreOfficeInstalled()
    {
        var installPath = LibreOfficeKit.Instance.FindInstallPath();
        Console.WriteLine($"LibreOffice install path: {installPath ?? "NOT FOUND"}");
        Assert.IsNotNull(installPath, "LibreOffice installation not found");
    }
}
