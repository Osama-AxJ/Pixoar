using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Extracts image metadata.
/// </summary>
public interface IImageInfoService
{
    /// <summary>
    /// Reads image information for a supported image.
    /// </summary>
    /// <param name="path">The image path.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The extracted image information.</returns>
    Task<ImageInformation> GetInformationAsync(
        string path,
        CancellationToken cancellationToken = default);
}
