using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Removes Pixoar-owned registry entries and optional user data.
/// </summary>
public interface IPixoarCleanupService
{
    /// <summary>
    /// Removes Pixoar-owned registry entries and optionally removes AppData.
    /// </summary>
    /// <param name="removeUserData">True to remove `%AppData%\Pixoar`; otherwise false.</param>
    /// <param name="cancellationToken">A token used to cancel cleanup.</param>
    /// <returns>A cleanup summary.</returns>
    Task<CleanupResult> CleanupAsync(bool removeUserData, CancellationToken cancellationToken = default);
}
