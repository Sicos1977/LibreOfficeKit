//
// IpcProtocol.cs
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
///     Request to convert a document to PDF.
///     The worker reads from <see cref="InputFile" /> and writes PDF to <see cref="OutputFile" />.
/// </summary>
/// <param name="inputFile">Path to the input document.</param>
/// <param name="outputFile">Path where the PDF will be written.</param>
/// <param name="filterOptions">Optional LibreOffice filter options string passed to <c>saveAs</c>.</param>
internal sealed class ConvertRequest(string inputFile, string outputFile, string? filterOptions = null) : WorkerRequest
{
    #region Properties
    /// <summary>
    ///     Path to the input document that should be converted to PDF. The worker will read from this file.
    /// </summary>
    public string InputFile { get; } = inputFile;

    /// <summary>
    ///     Path where the output PDF will be written. The worker will write to this file.
    /// </summary>
    public string OutputFile { get; } = outputFile;

    /// <summary>
    ///     Optional LibreOffice filter options string passed to the native <c>saveAs</c> function.
    /// </summary>
    public string? FilterOptions { get; } = filterOptions;
    #endregion
}