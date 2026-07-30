namespace Pixoar.Core.Models;

/// <summary>
/// Represents the result of checking for a newer Pixoar release.
/// </summary>
public sealed class UpdateCheckResult
{
    /// <summary>
    /// Gets or sets the update check status.
    /// </summary>
    public UpdateStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the currently installed version.
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latest release version when available.
    /// </summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latest release name when available.
    /// </summary>
    public string ReleaseName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latest release URL when available.
    /// </summary>
    public string ReleaseUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the latest release publication date when available.
    /// </summary>
    public DateTimeOffset? PublishedAt { get; set; }
}
