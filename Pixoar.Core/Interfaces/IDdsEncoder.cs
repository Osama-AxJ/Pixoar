using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Encapsulates DDS conversion through an external encoder or decoder.
/// </summary>
public interface IDdsEncoder
{
    /// <summary>
    /// Gets a value indicating whether DDS conversion is available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Converts a non-DDS image to DDS.
    /// </summary>
    /// <param name="inputPath">The input image path.</param>
    /// <param name="outputPath">The DDS output path.</param>
    /// <param name="settings">DDS settings to apply.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when conversion succeeds.</returns>
    Task ConvertToDdsAsync(
        string inputPath,
        string outputPath,
        DdsSettings settings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Decodes a DDS image to a lossless PNG intermediate.
    /// </summary>
    /// <param name="inputPath">The DDS input path.</param>
    /// <param name="outputPngPath">The PNG output path.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when decoding succeeds.</returns>
    Task DecodeToPngAsync(
        string inputPath,
        string outputPngPath,
        CancellationToken cancellationToken = default);
}
