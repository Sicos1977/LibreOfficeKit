using System.Text.Json.Serialization;

namespace LibreOfficeKit;

/// <summary>
///     JSON serialization context for LibreOfficeKit types.
///     This enables source-generated JSON serialization for better performance and trimming support.
/// </summary>
[JsonSerializable(typeof(LibreOfficeVersionInfo))]
internal partial class LibreOfficeKitJsonContext : JsonSerializerContext
{
}
