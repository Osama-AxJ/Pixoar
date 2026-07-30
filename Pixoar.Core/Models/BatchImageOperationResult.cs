namespace Pixoar.Core.Models;

/// <summary>
/// Represents the result of a batch image operation.
/// </summary>
public sealed class BatchImageOperationResult
{
    /// <summary>
    /// Gets operation results for every input file.
    /// </summary>
    public List<ImageOperationResult> Results { get; } = [];

    /// <summary>
    /// Gets successful operation results.
    /// </summary>
    public IEnumerable<ImageOperationResult> SuccessfulResults => Results.Where(result => result.Success);

    /// <summary>
    /// Gets failed operation errors.
    /// </summary>
    public IEnumerable<ImageOperationError> Errors => Results
        .Where(result => result.Error is not null)
        .Select(result => result.Error!);

    /// <summary>
    /// Gets the number of successful items.
    /// </summary>
    public int SuccessCount => Results.Count(result => result.Success);

    /// <summary>
    /// Gets the number of failed items.
    /// </summary>
    public int ErrorCount => Results.Count(result => !result.Success);
}
