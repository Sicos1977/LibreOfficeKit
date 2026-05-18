namespace LibreOfficeKit;

/// <summary>
///     Result of a conversion request.
/// </summary>
public sealed class ConvertResponse : WorkerResponse
{
    #region Properties
    /// <summary>
    ///     Gets a value indicating whether the conversion succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    ///     Gets the error message if the conversion failed; otherwise <c>null</c>.
    /// </summary>
    public string? Error { get; init; }
    #endregion
}