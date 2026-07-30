namespace Pixoar.Core.Interfaces;

using Pixoar.Core.Models;

/// <summary>
/// Manages Pixoar Windows Explorer context menu registration.
/// </summary>
public interface IContextMenuService
{
    /// <summary>
    /// Applies the current quick action settings to the per-user Explorer context menu.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The verified context menu installation status after applying the settings.</returns>
    Task<ContextMenuInstallationStatus> ApplyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all Pixoar-owned Explorer context menu entries.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when registration has been removed.</returns>
    Task UninstallAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Recreates Explorer context menu entries and returns a full diagnostic report.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that returns the diagnostic report.</returns>
    Task<ContextMenuDiagnosticReport> RepairAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads installed context menu commands and returns a full diagnostic report.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that returns the diagnostic report.</returns>
    Task<ContextMenuDiagnosticReport> DiagnoseAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies Pixoar's complete Explorer context menu registry tree.
    /// </summary>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The current context menu installation status.</returns>
    Task<ContextMenuInstallationStatus> GetInstallationStatusAsync(CancellationToken cancellationToken = default);
}
