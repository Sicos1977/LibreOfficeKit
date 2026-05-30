using System.Text.Json.Serialization;

namespace LibreOfficeKit;

/// <summary>
///     Represents version information returned by LibreOfficeKit.
/// </summary>
public sealed class VersionInfo
{
    /// <summary>
    ///     Gets or sets the product name (typically "LibreOffice").
    /// </summary>
    [JsonPropertyName("ProductName")]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the product version (e.g., "24.8.0.3").
    /// </summary>
    [JsonPropertyName("ProductVersion")]
    public string ProductVersion { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the product extension (e.g., ".0.alpha0").
    /// </summary>
    [JsonPropertyName("ProductExtension")]
    public string ProductExtension { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the build ID (Git commit hash).
    /// </summary>
    [JsonPropertyName("BuildId")]
    public string BuildId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets the full version string combining ProductVersion and ProductExtension.
    /// </summary>
    [JsonIgnore]
    public string FullVersion => ProductVersion + ProductExtension;

    /// <summary>
    ///     Returns a formatted string representation of the version information.
    /// </summary>
    public override string ToString()
    {
        return $"{ProductName} {FullVersion} (Build: {BuildId})";
    }
}
