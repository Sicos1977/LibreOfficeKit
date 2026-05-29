//
// ConsoleLogger.cs
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
// Simple console logger implementation for direct mode debugging.
// =============================================================================

using Microsoft.Extensions.Logging;

namespace LibreOfficeKit.Logging;

/// <summary>
///     Simple console logger for LibreOfficeKit direct mode.
/// </summary>
internal sealed class ConsoleLogger : ILogger
{
    #region Fields
    /// <summary>
    ///  The category name
    /// </summary>
    private readonly string _categoryName;

    /// <summary>
    ///     Minimal log level
    /// </summary>
    private readonly LogLevel _minLevel;
    #endregion

    #region Constructor
    /// <summary>
    ///     Initializes a new instance of the <see cref="ConsoleLogger"/> class.
    /// </summary>
    /// <param name="categoryName">The category name for the logger.</param>
    /// <param name="minLevel">The minimum log level to output.</param>
    internal ConsoleLogger(string categoryName, LogLevel minLevel = LogLevel.Information)
    {
        _categoryName = categoryName;
        _minLevel = minLevel;
    }
    #endregion

    #region BeginScope
    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    #endregion

    #region IsEnabled
    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minLevel;
    #endregion

    #region Log
    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var level = logLevel switch
        {
            LogLevel.Trace       => "TRC",
            LogLevel.Debug       => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning     => "WRN",
            LogLevel.Error       => "ERR",
            LogLevel.Critical    => "CRT",
            _                    => "   "
        };

        Console.WriteLine($"{timestamp} [{level}]: {message}");

        if (exception != null)
            Console.WriteLine($"Exception:\n{exception}");
    }
    #endregion
}