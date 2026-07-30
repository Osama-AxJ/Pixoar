namespace Pixoar.Core.Models;

/// <summary>
/// Stores output behavior shared by desktop and CLI operations.
/// </summary>
public sealed class OutputSettings
{
    /// <summary>
    /// Gets or sets a value indicating whether output files should be saved beside their source files.
    /// </summary>
    public bool SaveBesideOriginal { get; set; } = true;

    /// <summary>
    /// Gets or sets the custom output folder used when SaveBesideOriginal is disabled.
    /// </summary>
    public string? CustomOutputFolder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether existing files should be protected from overwrites.
    /// </summary>
    public bool PreventOverwrite { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether duplicate output names should be renamed automatically.
    /// </summary>
    public bool RenameDuplicatesAutomatically { get; set; } = true;
}
