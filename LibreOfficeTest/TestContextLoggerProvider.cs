using Microsoft.Extensions.Logging;

namespace LibreOfficeTest;

/// <summary>
///     An <see cref="ILoggerProvider" /> that writes log messages to <see cref="Console.WriteLine(string)" />.
///     MSTest captures console output per test and shows it in the test result, making this reliable even when
///     a single logger instance is shared across multiple tests (e.g. via a static <c>Converter</c> field).
/// </summary>
internal sealed class TestContextLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestContextLogger(categoryName);

    public void Dispose() { }
}