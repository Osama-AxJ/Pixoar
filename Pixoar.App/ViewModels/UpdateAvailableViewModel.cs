using System.Diagnostics;
using Pixoar.App.Commands;
using Pixoar.Core.Models;

namespace Pixoar.App.ViewModels;

/// <summary>
/// Provides state for the update available notification.
/// </summary>
public sealed class UpdateAvailableViewModel : ViewModelBase
{
    private readonly Action _close;
    private readonly string _releaseUrl;
    private string _statusText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateAvailableViewModel"/> class.
    /// </summary>
    /// <param name="result">The update check result.</param>
    /// <param name="closeButtonText">The secondary button text.</param>
    /// <param name="close">The action used to close the window.</param>
    public UpdateAvailableViewModel(
        UpdateCheckResult result,
        string closeButtonText,
        Action close)
    {
        _close = close;
        _releaseUrl = result.ReleaseUrl;
        CurrentVersion = result.CurrentVersion;
        LatestVersion = result.LatestVersion;
        ReleaseName = string.IsNullOrWhiteSpace(result.ReleaseName)
            ? $"Pixoar v{result.LatestVersion}"
            : result.ReleaseName;
        PublishedAt = result.PublishedAt?.ToString("g") ?? "Unknown";
        CloseButtonText = closeButtonText;
        ViewReleaseCommand = new RelayCommand(_ => ViewRelease(), _ => IsValidReleaseUrl(_releaseUrl));
        CloseCommand = new RelayCommand(_ => _close());
    }

    /// <summary>
    /// Gets the installed version.
    /// </summary>
    public string CurrentVersion { get; }

    /// <summary>
    /// Gets the latest release version.
    /// </summary>
    public string LatestVersion { get; }

    /// <summary>
    /// Gets the release name.
    /// </summary>
    public string ReleaseName { get; }

    /// <summary>
    /// Gets the release publication date.
    /// </summary>
    public string PublishedAt { get; }

    /// <summary>
    /// Gets the close button text.
    /// </summary>
    public string CloseButtonText { get; }

    /// <summary>
    /// Gets the command that opens the GitHub release page.
    /// </summary>
    public RelayCommand ViewReleaseCommand { get; }

    /// <summary>
    /// Gets the command that closes the notification.
    /// </summary>
    public RelayCommand CloseCommand { get; }

    /// <summary>
    /// Gets or sets status text for link-opening failures.
    /// </summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private void ViewRelease()
    {
        if (!IsValidReleaseUrl(_releaseUrl))
        {
            StatusText = "Pixoar couldn't open the release page.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _releaseUrl,
                UseShellExecute = true
            });
            _close();
        }
        catch
        {
            StatusText = "Pixoar couldn't open the release page.";
        }
    }

    private static bool IsValidReleaseUrl(string? releaseUrl)
    {
        if (!Uri.TryCreate(releaseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith("/Osama-AxJ/Pixoar/releases/", StringComparison.OrdinalIgnoreCase);
    }
}
