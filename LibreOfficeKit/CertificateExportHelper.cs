//
// CertificateExportHelper.cs
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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LibreOfficeKit;

internal static class CertificateExportHelper
{
    internal static byte[] ExportCertificate(X509Certificate2 certificate)
    {
        return certificate.Export(X509ContentType.Cert);
    }

    internal static byte[] ExportPrivateKey(X509Certificate2 certificate)
    {
        if (!certificate.HasPrivateKey)
            throw new InvalidOperationException("The signing certificate must contain a private key.");

#if NET5_0_OR_GREATER
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa != null)
            return rsa.ExportPkcs8PrivateKey();

        using var ecdsa = certificate.GetECDsaPrivateKey();
        if (ecdsa != null)
            return ecdsa.ExportPkcs8PrivateKey();

        using var dsa = certificate.GetDSAPrivateKey();
        if (dsa != null)
            return dsa.ExportPkcs8PrivateKey();

        throw new NotSupportedException("The signing certificate private key algorithm is not supported.");
#else
        using var rsa = certificate.GetRSAPrivateKey();
        if (rsa == null)
            throw new NotSupportedException("Only RSA signing certificates are supported on .NET Standard 2.0.");

        return ExportRsaPrivateKeyPkcs8(rsa.ExportParameters(true));
#endif
    }

#if NETSTANDARD2_0
    private static byte[] ExportRsaPrivateKeyPkcs8(RSAParameters parameters)
    {
        var algorithmIdentifier = EncodeSequence(
            EncodeOid([0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x01]),
            EncodeNull());

        var privateKey = EncodeSequence(
            EncodeInteger([0x00]),
            EncodeInteger(parameters.Modulus),
            EncodeInteger(parameters.Exponent),
            EncodeInteger(parameters.D),
            EncodeInteger(parameters.P),
            EncodeInteger(parameters.Q),
            EncodeInteger(parameters.DP),
            EncodeInteger(parameters.DQ),
            EncodeInteger(parameters.InverseQ));

        return EncodeSequence(
            EncodeInteger([0x00]),
            algorithmIdentifier,
            EncodeOctetString(privateKey));
    }

    private static byte[] EncodeSequence(params byte[][] values)
    {
        return EncodeConstructed(0x30, values);
    }

    private static byte[] EncodeOctetString(byte[] value)
    {
        return EncodePrimitive(0x04, value);
    }

    private static byte[] EncodeNull()
    {
        return [0x05, 0x00];
    }

    private static byte[] EncodeOid(byte[] value)
    {
        return EncodePrimitive(0x06, value);
    }

    private static byte[] EncodeInteger(byte[]? value)
    {
        if (value == null || value.Length == 0)
            value = [0x00];
        else
            value = TrimLeadingZeroes(value);

        if ((value[0] & 0x80) != 0)
        {
            var prefixed = new byte[value.Length + 1];
            Buffer.BlockCopy(value, 0, prefixed, 1, value.Length);
            value = prefixed;
        }

        return EncodePrimitive(0x02, value);
    }

    private static byte[] EncodePrimitive(byte tag, byte[] value)
    {
        var length = EncodeLength(value.Length);
        var result = new byte[1 + length.Length + value.Length];
        result[0] = tag;
        Buffer.BlockCopy(length, 0, result, 1, length.Length);
        Buffer.BlockCopy(value, 0, result, 1 + length.Length, value.Length);
        return result;
    }

    private static byte[] EncodeConstructed(byte tag, params byte[][] values)
    {
        var contentLength = 0;
        foreach (var value in values)
            contentLength += value.Length;

        var length = EncodeLength(contentLength);
        var result = new byte[1 + length.Length + contentLength];
        result[0] = tag;
        Buffer.BlockCopy(length, 0, result, 1, length.Length);

        var offset = 1 + length.Length;
        foreach (var value in values)
        {
            Buffer.BlockCopy(value, 0, result, offset, value.Length);
            offset += value.Length;
        }

        return result;
    }

    private static byte[] EncodeLength(int length)
    {
        if (length < 0x80)
            return [(byte)length];

        var octets = new List<byte>();
        var remaining = length;
        while (remaining > 0)
        {
            octets.Insert(0, (byte)(remaining & 0xFF));
            remaining >>= 8;
        }

        var result = new byte[octets.Count + 1];
        result[0] = (byte)(0x80 | octets.Count);
        octets.CopyTo(result, 1);
        return result;
    }

    private static byte[] TrimLeadingZeroes(byte[] value)
    {
        var index = 0;
        while (index < value.Length - 1 && value[index] == 0x00)
            index++;

        if (index == 0)
            return value;

        var trimmed = new byte[value.Length - index];
        Buffer.BlockCopy(value, index, trimmed, 0, trimmed.Length);
        return trimmed;
    }
#endif
}
