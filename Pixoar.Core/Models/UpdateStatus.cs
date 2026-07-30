namespace Pixoar.Core.Models;

/// <summary>
/// Describes the outcome of an update check.
/// </summary>
public enum UpdateStatus
{
    /// <summary>
    /// A newer stable release is available.
    /// </summary>
    UpdateAvailable,

    /// <summary>
    /// The installed version is current.
    /// </summary>
    UpToDate,

    /// <summary>
    /// The update check could not be completed.
    /// </summary>
    CheckFailed
}
