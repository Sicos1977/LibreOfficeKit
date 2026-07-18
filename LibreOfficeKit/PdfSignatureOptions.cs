//
// PdfSignatureOptions.cs
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

using System.Security.Cryptography.X509Certificates;

namespace LibreOfficeKit;

/// <summary>
///     PDF signing options used to digitally sign the generated PDF output.
/// </summary>
public sealed class PdfSignatureOptions
{
    /// <summary>
    ///     Gets or sets the certificate used to sign the generated PDF.
    ///     The certificate must include an accessible private key.
    /// </summary>
    public X509Certificate2? Certificate { get; set; }

    /// <summary>
    ///     Validates the signing options and throws if the configuration is invalid.
    /// </summary>
    internal void Validate()
    {
        if (Certificate == null)
            throw new ArgumentNullException(nameof(Certificate), "A certificate is required to sign the PDF.");

        if (!Certificate.HasPrivateKey)
            throw new InvalidOperationException("The signing certificate must contain a private key.");
    }
}
