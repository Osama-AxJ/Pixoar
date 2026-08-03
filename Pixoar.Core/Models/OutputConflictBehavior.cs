namespace Pixoar.Core.Models;

/// <summary>
/// Defines how output operations handle an existing destination file.
/// </summary>
public enum OutputConflictBehavior
{
    /// <summary>
    /// Creates a new numbered filename when the destination already exists.
    /// </summary>
    RenameDuplicatesAutomatically,

    /// <summary>
    /// Skips an input when its destination already exists.
    /// </summary>
    SkipExistingFiles,

    /// <summary>
    /// Replaces an existing destination file.
    /// </summary>
    OverwriteExistingFiles
}
