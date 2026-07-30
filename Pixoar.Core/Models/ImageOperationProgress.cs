namespace Pixoar.Core.Models;

/// <summary>
/// Reports progress for batch image operations.
/// </summary>
/// <param name="Completed">The number of completed items.</param>
/// <param name="Total">The total number of items.</param>
/// <param name="CurrentFile">The file currently being processed.</param>
/// <param name="Status">A short progress status.</param>
public sealed record ImageOperationProgress(
    int Completed,
    int Total,
    string CurrentFile,
    string Status)
{
    /// <summary>
    /// Gets the completion percentage.
    /// </summary>
    public double Percent => Total <= 0 ? 0 : Completed * 100d / Total;
}
