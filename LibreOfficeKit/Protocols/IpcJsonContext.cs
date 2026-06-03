//
// IpcJsonContext.cs
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
using System.Text.Json.Serialization;

namespace LibreOfficeKit.Protocols;

/// <summary>
///     JSON serialization context for IPC protocol types.
///     This enables source-generated JSON serialization for better performance and trimming support.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = false)]
[JsonSerializable(typeof(WorkerRequest))]
[JsonSerializable(typeof(WorkerResponse))]
[JsonSerializable(typeof(ConvertRequest))]
[JsonSerializable(typeof(ReadyRequest))]
[JsonSerializable(typeof(PingRequest))]
[JsonSerializable(typeof(ShutdownRequest))]
[JsonSerializable(typeof(ConvertResponse))]
[JsonSerializable(typeof(ReadyResponse))]
[JsonSerializable(typeof(PongResponse))]
[JsonSerializable(typeof(ShutdownResponse))]
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(LogResponse))]
[JsonSerializable(typeof(LogLevel))]
internal partial class IpcJsonContext : JsonSerializerContext;