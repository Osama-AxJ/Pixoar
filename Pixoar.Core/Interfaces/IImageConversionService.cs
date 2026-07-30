using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Converts images between supported formats.
/// </summary>
public interface IImageConversionService
{
    /// <summary>
    /// Converts a single image.
    /// </summary>
    /// <param name="request">The conversion request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<ImageOperationResult> ConvertAsync(
        ImageConversionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts multiple images while collecting per-file failures.
    /// </summary>
    /// <param name="requests">The conversion requests.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The batch operation result.</returns>
    Task<BatchImageOperationResult> ConvertBatchAsync(
        IEnumerable<ImageConversionRequest> requests,
        IProgress<ImageOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
