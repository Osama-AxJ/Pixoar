namespace Pixoar.Core.Models;

/// <summary>
/// Describes a non-fatal image operation failure.
/// </summary>
public sealed class ImageOperationError
{
    /// <summary>
    /// Gets or sets the operation type.
    /// </summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the input file path.
    /// </summary>
    public string InputPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the output file path when one was selected.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Gets or sets the user-facing error message.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the time when the error happened.
    /// </summary>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.Now;
}
