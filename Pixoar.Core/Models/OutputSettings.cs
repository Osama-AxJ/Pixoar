using System.Text.Json.Serialization;

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
    /// Gets or sets the behavior used when an output file already exists.
    /// </summary>
    public OutputConflictBehavior ConflictBehavior { get; set; } =
        OutputConflictBehavior.RenameDuplicatesAutomatically;

    /// <summary>
    /// Gets or sets a value indicating whether conversions should be stored in a Converted folder.
    /// </summary>
    public bool SaveConvertedFilesInConvertedFolder { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether resizes should be stored in a Resize folder.
    /// </summary>
    public bool SaveResizedFilesInResizeFolder { get; set; }

    /// <summary>
    /// Gets or sets the legacy overwrite setting while older settings are migrated.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? PreventOverwrite { get; set; }

    /// <summary>
    /// Gets or sets the legacy duplicate-name setting while older settings are migrated.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? RenameDuplicatesAutomatically { get; set; }
}
