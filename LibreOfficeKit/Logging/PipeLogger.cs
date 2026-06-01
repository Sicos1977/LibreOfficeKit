using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;

namespace LibreOfficeKit.Logging;

/// <summary>
///     A logger that sends log messages to the host process via the named pipe.
/// </summary>
internal sealed class PipeLogger(string categoryName, StreamWriter writer, LogLevel minLogLevel = LogLevel.Information) : ILogger
{
    #region ILogger Implementation
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None && logLevel >= minLogLevel;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception == null)
            return;

        var fullMessage = $"{categoryName}: {message}";
        var exceptionText = exception?.ToString();

        WorkerResponse logResponse = new LogResponse(logLevel, fullMessage, exceptionText);

        try
        {
            var json = IpcSerializer.Serialize(logResponse);
            writer.WriteLine(json);
            writer.Flush();
        }
        catch 
        {
            // Ignore
        }
    }
    #endregion
}