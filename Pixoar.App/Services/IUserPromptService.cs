using Pixoar.Core.Models;

namespace Pixoar.App.Services;

/// <summary>
/// Shows small user prompts needed by view models.
/// </summary>
public interface IUserPromptService
{
    /// <summary>
    /// Confirms an operation that may overwrite existing output files.
    /// </summary>
    /// <returns><see langword="true" /> when the operation should continue; otherwise <see langword="false" />.</returns>
    bool ConfirmOverwriteRisk();

    /// <summary>
    /// Confirms removal of Pixoar user settings and logs.
    /// </summary>
    /// <returns><see langword="true" /> when user data should be removed; otherwise <see langword="false" />.</returns>
    bool ConfirmRemoveUserData();

    /// <summary>
    /// Shows a non-blocking update notification after startup.
    /// </summary>
    /// <param name="result">The update check result.</param>
    void ShowAutomaticUpdateAvailable(UpdateCheckResult result);

    /// <summary>
    /// Shows the result of a manual update check.
    /// </summary>
    /// <param name="result">The update check result.</param>
    void ShowManualUpdateCheckResult(UpdateCheckResult result);
}
