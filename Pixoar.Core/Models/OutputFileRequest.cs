namespace Pixoar.Core.Models;

/// <summary>
/// Describes an output file name request.
/// </summary>
public sealed class OutputFileRequest
{
    /// <summary>
    /// Gets or sets the source file path.
    /// </summary>
    public string SourcePath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the output format.
    /// </summary>
    public ImageFormat OutputFormat { get; set; }

    /// <summary>
    /// Gets or sets the operation kind.
    /// </summary>
    public OutputOperationKind OperationKind { get; set; }

    /// <summary>
    /// Gets or sets an optional output folder override.
    /// </summary>
    public string? OutputFolder { get; set; }
}
