using LibreOfficeKit;
using Microsoft.Extensions.Logging;

namespace LibreOfficeTest;

public sealed class TestConverter : Converter
{
    public TestConverter(int maxInstances, int minHotStandby, TimeSpan idleTimeout, string workerExePath, ILogger<Converter>? logger = null)
        : base(maxInstances, minHotStandby, idleTimeout, logger)
    {
        WorkerExePath = workerExePath;
    }
}