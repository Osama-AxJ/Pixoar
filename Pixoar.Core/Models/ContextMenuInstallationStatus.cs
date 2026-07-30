namespace Pixoar.Core.Models;

/// <summary>
/// Describes the state of Pixoar's Windows Explorer context menu registration.
/// </summary>
public enum ContextMenuInstallationStatus
{
    /// <summary>
    /// No Pixoar context menu registry entries are installed.
    /// </summary>
    NotInstalled,

    /// <summary>
    /// All expected context menu registry entries are installed and current.
    /// </summary>
    Installed,

    /// <summary>
    /// Pixoar context menu registry entries exist but are incomplete, stale, or malformed.
    /// </summary>
    NeedsRepair
}
