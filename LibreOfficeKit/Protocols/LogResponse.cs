//
// LogResponse.cs
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

using Microsoft.Extensions.Logging;

namespace LibreOfficeKit.Protocols;

/// <summary>
///     Sent by the worker to transmit log messages to the host process.
/// </summary>
/// <param name="logLevel">The log level of the message.</param>
/// <param name="message">The log message.</param>
/// <param name="exception">Optional exception details.</param>
internal sealed class LogResponse(LogLevel logLevel, string message, string? exception = null) : WorkerResponse
{
    #region Properties
    /// <summary>
    ///     The log level of the message.
    /// </summary>
    public LogLevel LogLevel { get; } = logLevel;

    /// <summary>
    ///     The log message.
    /// </summary>
    public string Message { get; } = message;

    /// <summary>
    ///     Optional exception details.
    /// </summary>
    public string? Exception { get; } = exception;
    #endregion
}
