using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>Upscales images locally with the bundled Real-ESRGAN NCNN/Vulkan backend.</summary>
public interface IImageUpscaleService
{
    /// <summary>Upscales one image locally.</summary>
    Task<ImageOperationResult> UpscaleAsync(UpscaleRequest request, CancellationToken cancellationToken = default);

    /// <summary>Upscales a batch while preserving per-file failures.</summary>
    Task<BatchImageOperationResult> UpscaleBatchAsync(
        IEnumerable<UpscaleRequest> requests,
        IProgress<ImageOperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
