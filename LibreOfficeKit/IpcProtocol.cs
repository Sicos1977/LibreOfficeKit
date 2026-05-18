// =============================================================================
// IpcProtocol.cs
//
// Defines the IPC protocol messages exchanged between the Converter (host)
// and worker processes via named pipes. Uses a simple JSON-line protocol:
// each message is a single JSON line terminated by newline.
// =============================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace LibreOfficeKit;

/// <summary>
///     Base class for all IPC messages sent from host to worker.
/// </summary>
[JsonDerivedType(typeof(ConvertRequest), "convert")]
[JsonDerivedType(typeof(PingRequest), "ping")]
[JsonDerivedType(typeof(ShutdownRequest), "shutdown")]
public abstract class WorkerRequest
{
}

/// <summary>
///     Request to convert a document to PDF.
///     The worker reads from <see cref="InputPath" /> and writes PDF to <see cref="OutputPath" />.
/// </summary>
public sealed class ConvertRequest : WorkerRequest
{
    #region Properties
    /// <summary>
    ///     Gets the absolute path to the input document.
    /// </summary>
    public required string InputPath { get; init; }

    /// <summary>
    ///     Gets the absolute path where the output PDF will be written.
    /// </summary>
    public required string OutputPath { get; init; }
    #endregion
}

/// <summary>
///     Graceful shutdown request — worker should clean up and exit.
/// </summary>
public sealed class ShutdownRequest : WorkerRequest
{
}

/// <summary>
///     Base class for all IPC messages sent from worker to host.
/// </summary>
[JsonDerivedType(typeof(ConvertResponse), "convertResult")]
[JsonDerivedType(typeof(PongResponse), "pong")]
[JsonDerivedType(typeof(ReadyResponse), "ready")]
[JsonDerivedType(typeof(ErrorResponse), "error")]
public abstract class WorkerResponse
{
}

/// <summary>
///     Response to a <see cref="PingRequest" />.
/// </summary>
public sealed class PongResponse : WorkerResponse
{
}

/// <summary>
///     Sent by the worker after successful initialization (LibreOfficeKit loaded).
/// </summary>
public sealed class ReadyResponse : WorkerResponse
{
}

/// <summary>
///     Sent when the worker encounters a fatal error during initialization.
/// </summary>
public sealed class ErrorResponse : WorkerResponse
{
    #region Properties
    /// <summary>
    ///     Gets the error message describing what went wrong.
    /// </summary>
    public required string Message { get; init; }
    #endregion
}

/// <summary>
///     Helper for serializing and deserializing IPC messages as JSON lines.
/// </summary>
internal static class IpcSerializer
{
    #region Fields
    /// <summary>Shared JSON serializer options for IPC messages.</summary>
    private static readonly JsonSerializerOptions s_options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    #endregion

    #region Serialize
    /// <summary>
    ///     Serializes an IPC message to a JSON string.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="message">The message to serialize.</param>
    /// <returns>A JSON string representation of the message.</returns>
    public static string Serialize<T>(T message)
    {
        return JsonSerializer.Serialize(message, s_options);
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
        return JsonSerializer.Deserialize<T>(json, s_options);
    }
    #endregion
}