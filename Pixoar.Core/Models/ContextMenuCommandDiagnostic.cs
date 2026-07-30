namespace Pixoar.Core.Models;

/// <summary>
/// Describes an installed Explorer context menu command.
/// </summary>
public sealed record ContextMenuCommandDiagnostic
{
    /// <summary>
    /// Gets the file extension that owns the context menu command.
    /// </summary>
    public string Extension { get; init; } = string.Empty;

    /// <summary>
    /// Gets the readable menu path, such as Resize > 50%.
    /// </summary>
    public string ActionPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the registry path that stores the command value.
    /// </summary>
    public string RegistryKeyPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the menu display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the full command value written to the registry.
    /// </summary>
    public string Command { get; init; } = string.Empty;

    /// <summary>
    /// Gets the executable path parsed from the command.
    /// </summary>
    public string ExecutablePath { get; init; } = string.Empty;

    /// <summary>
    /// Gets the command arguments before Explorer's selected file placeholder.
    /// </summary>
    public string Arguments { get; init; } = string.Empty;

    /// <summary>
    /// Gets Explorer's selected file placeholder.
    /// </summary>
    public string SelectedFilePlaceholder { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the command executable exists.
    /// </summary>
    public bool ExecutableExists { get; init; }

    /// <summary>
    /// Gets a value indicating whether the command value is quoted correctly.
    /// </summary>
    public bool ExecutableWasQuoted { get; init; }

    /// <summary>
    /// Gets the icon path assigned to the command.
    /// </summary>
    public string IconPath { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the assigned icon file exists.
    /// </summary>
    public bool IconExists { get; init; }

    /// <summary>
    /// Gets a value indicating whether the command matches the expected Pixoar action.
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets a readable validation issue when invalid.
    /// </summary>
    public string Issue { get; init; } = string.Empty;
}
