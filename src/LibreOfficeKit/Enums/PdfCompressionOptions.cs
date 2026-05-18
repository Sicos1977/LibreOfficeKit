namespace LibreOfficeKit.Enums;

/// <summary>
///     Image quality and compression settings for PDF export.
///     Controls how images embedded in the document are compressed in the output PDF.
/// </summary>
/// <remarks>
///     <para>
///         When <see cref="UseLosslessCompression" /> is <c>true</c>, images are stored using
///         PNG (lossless) compression and the <see cref="Quality" /> setting is ignored.
///     </para>
///     <para>
///         When <see cref="UseLosslessCompression" /> is <c>false</c> (default), images are
///         compressed using JPEG with the specified <see cref="Quality" /> level.
///     </para>
/// </remarks>
public class PdfCompressionOptions
{
    #region Properties
    /// <summary>
    ///     Gets or sets whether to use lossless compression (PNG) for images.
    ///     When <c>true</c>, the <see cref="Quality" /> setting is ignored.
    /// </summary>
    public bool? UseLosslessCompression { get; set; }

    /// <summary>
    ///     Gets or sets the JPEG compression quality (1–100).
    ///     Only effective when <see cref="UseLosslessCompression" /> is <c>false</c>.
    /// </summary>
    public int? Quality { get; set; }

    /// <summary>
    ///     Gets or sets whether to reduce image resolution to <see cref="MaxImageResolution" />.
    /// </summary>
    public bool? ReduceImageResolution { get; set; }

    /// <summary>
    ///     Gets or sets the maximum image resolution in DPI.
    ///     Only effective when <see cref="ReduceImageResolution" /> is <c>true</c>.
    /// </summary>
    public int? MaxImageResolution { get; set; }
    #endregion

    #region Validate
    /// <summary>
    ///     Validates the compression options and throws if the configuration is invalid.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when Quality is outside the valid range of 1–100,
    ///     or when MaxImageResolution is not a positive value.
    /// </exception>
    internal void Validate()
    {
        if (Quality.HasValue && (Quality.Value < 1 || Quality.Value > 100))
            throw new ArgumentOutOfRangeException(
                nameof(Quality), Quality.Value,
                "Quality must be between 1 and 100.");

        if (MaxImageResolution.HasValue && MaxImageResolution.Value <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(MaxImageResolution), MaxImageResolution.Value,
                "MaxImageResolution must be a positive value in DPI.");
    }
    #endregion
}