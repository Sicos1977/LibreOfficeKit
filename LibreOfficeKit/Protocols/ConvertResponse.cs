//
// ConvertResponse.cs
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

namespace LibreOfficeKit.Protocols;

/// <summary>
///     Result of a conversion request.
/// </summary>
/// <paramref name="succes">Indicates whether the conversion succeeded.</paramref>
/// <paramref name="error">The error message if the conversion failed; otherwise <c>null</c>.</paramref>
internal sealed class ConvertResponse(bool succes, string? error = null) : WorkerResponse
{
    #region Properties
    /// <summary>
    ///     Gets a value indicating whether the conversion succeeded.
    /// </summary>
    public bool Success { get; } = succes;

    /// <summary>
    ///     Gets the error message if the conversion failed; otherwise <c>null</c>.
    /// </summary>
    public string? Error { get; } = error;
    #endregion
}