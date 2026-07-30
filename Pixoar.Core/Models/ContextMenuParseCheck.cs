namespace Pixoar.Core.Models;

/// <summary>
/// Describes a CLI argument parser check used by context menu diagnostics.
/// </summary>
public sealed record ContextMenuParseCheck
{
    /// <summary>
    /// Gets the check name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the check passed.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Gets additional check details.
    /// </summary>
    public string Details { get; init; } = string.Empty;
}
