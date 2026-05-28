using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;

namespace LibreOfficeKit;

/// <summary>
///     A logger that sends log messages to the host process via the named pipe.
/// </summary>
internal sealed class PipeLogger(string categoryName, StreamWriter writer) : ILogger
{
    private readonly object _lock = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null)
            return;

        var fullMessage = $"{categoryName}: {message}";
        var exceptionText = exception?.ToString();

        WorkerResponse logResponse = new LogResponse(logLevel, fullMessage, exceptionText);

        // Serialize and send asynchronously but without blocking the logger
        // We use lock to ensure messages are sent in order
        lock (_lock)
        {
            try
            {
                var json = IpcSerializer.Serialize(logResponse);
                writer.WriteLine(json);
                writer.Flush();
            }
            catch
            {
                // Ignore errors when sending log messages to avoid infinite loops
            }
        }
    }
}