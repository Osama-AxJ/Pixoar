namespace Pixoar.Core.Models;

/// <summary>
/// Summarizes cleanup actions performed by Pixoar.
/// </summary>
public sealed class CleanupResult
{
    /// <summary>
    /// Gets cleanup actions that completed successfully.
    /// </summary>
    public List<string> Actions { get; } = [];

    /// <summary>
    /// Gets cleanup errors that were handled safely.
    /// </summary>
    public List<string> Errors { get; } = [];

    /// <summary>
    /// Gets a value indicating whether cleanup completed without handled errors.
    /// </summary>
    public bool Success => Errors.Count == 0;
}
