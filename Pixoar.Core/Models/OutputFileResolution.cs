namespace Pixoar.Core.Models;

/// <summary>
/// Represents a resolved output path and the action to take for it.
/// </summary>
public sealed class OutputFileResolution
{
    /// <summary>
    /// Gets or sets the resolved output path.
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the operation should be skipped.
    /// </summary>
    public bool ShouldSkip { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an existing destination may be replaced.
    /// </summary>
    public bool AllowOverwrite { get; set; }
}
