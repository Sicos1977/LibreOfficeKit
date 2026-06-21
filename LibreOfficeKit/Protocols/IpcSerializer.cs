//
// IpcSerializer.cs
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

using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace LibreOfficeKit.Protocols;

/// <summary>
///     Helper for serializing and deserializing IPC messages as JSON lines.
/// </summary>
internal static class IpcSerializer
{
    #region Serialize
    /// <summary>
    ///     Serializes an IPC message to a JSON string.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="message">The message to serialize.</param>
    /// <returns>A JSON string representation of the message.</returns>
    public static string Serialize<T>(T message)
    {
        var typeInfo = (JsonTypeInfo<T>)IpcJsonContext.Default.GetTypeInfo(typeof(T))!;
        return JsonSerializer.Serialize(message, typeInfo);
    }
    #endregion

    #region Deserialize
    /// <summary>
    ///     Deserializes a JSON string to an IPC message.
    /// </summary>
    /// <typeparam name="T">The expected message type.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized message, or <c>null</c> if deserialization fails.</returns>
    public static T? Deserialize<T>(string json)
    {
        var typeInfo = (JsonTypeInfo<T>)IpcJsonContext.Default.GetTypeInfo(typeof(T))!;
        return JsonSerializer.Deserialize(json, typeInfo);
    }
    #endregion
}
