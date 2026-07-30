using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Loads lightweight image previews.
/// </summary>
public interface IImagePreviewService
{
    /// <summary>
    /// Loads a PNG-encoded preview for a supported image.
    /// </summary>
    /// <param name="path">The image path.</param>
    /// <param name="maxPixelSize">The maximum preview width or height.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The preview result.</returns>
    Task<ImagePreviewResult> LoadPreviewAsync(
        string path,
        int maxPixelSize,
        CancellationToken cancellationToken = default);
}
