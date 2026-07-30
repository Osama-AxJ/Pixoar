using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Resizes supported images.
/// </summary>
public interface IImageResizeService
{
    /// <summary>
    /// Resizes a single image.
    /// </summary>
    /// <param name="request">The resize request.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The operation result.</returns>
    Task<ImageOperationResult> ResizeAsync(
        ImageResizeRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resizes multiple images while collecting per-file failures.
    /// </summary>
    /// <param name="requests">The resize requests.</param>
    /// <param name="progress">An optional progress sink.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The batch operation result.</returns>
    Task<BatchImageOperationResult> ResizeBatchAsync(
        IEnumerable<ImageResizeRequest> requests,
        IProgress<ImageOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
