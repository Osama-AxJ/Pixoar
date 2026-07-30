using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Checks whether a newer stable Pixoar release is available.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Checks the latest stable GitHub release.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the request.</param>
    /// <returns>The update check result.</returns>
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
}
