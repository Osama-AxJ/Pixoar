using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Loads and saves Pixoar user settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the currently loaded settings document.
    /// </summary>
    PixoarSettings Current { get; }

    /// <summary>
    /// Loads settings from disk, creating the settings file with defaults when needed.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The loaded settings document.</returns>
    Task<PixoarSettings> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Applies a settings mutation and persists the updated document.
    /// </summary>
    /// <param name="update">The mutation to apply to the current settings document.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The updated settings document.</returns>
    Task<PixoarSettings> UpdateAsync(Action<PixoarSettings> update, CancellationToken cancellationToken = default);
}
