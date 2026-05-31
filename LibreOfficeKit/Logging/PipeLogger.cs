using LibreOfficeKit.Protocols;
using Microsoft.Extensions.Logging;

namespace LibreOfficeKit.Logging;

/// <summary>
///     A logger that sends log messages to the host process via the named pipe.
/// </summary>
internal sealed class PipeLogger(string categoryName, StreamWriter writer, LogLevel minLogLevel = LogLevel.Information) : ILogger
{
    #region Fields
#if NETSTANDARD2_0
    /// <summary>
    ///     Lock for thread-safe writing
    /// </summary>
    private readonly object _lock = new();
#else
    /// <summary>
    ///     Lock for thread-safe writing
    /// </summary>
    private readonly Lock _lock = new();
#endif
    #endregion

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
    #endregion
}