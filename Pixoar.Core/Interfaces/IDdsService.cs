using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Provides DDS-specific conversion, preview, and metadata operations.
/// </summary>
public interface IDdsService
{
    /// <summary>
    /// Gets a value indicating whether DDS conversion is available.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Converts an image to DDS using saved settings.
    /// </summary>
    /// <param name="inputPath">The input image path.</param>
    /// <param name="outputPath">The output DDS path.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when conversion succeeds.</returns>
    Task ConvertToDdsAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts an image to DDS with an optional per-operation compression override.
    /// </summary>
    /// <param name="inputPath">The input image path.</param>
    /// <param name="outputPath">The output DDS path.</param>
    /// <param name="compressionOverride">The compression used for this operation, or <see langword="null" /> for the saved default.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when conversion succeeds.</returns>
    Task ConvertToDdsAsync(
        string inputPath,
        string outputPath,
        DdsCompressionMode? compressionOverride,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a DDS image to a non-DDS image.
    /// </summary>
    /// <param name="inputPath">The DDS input path.</param>
    /// <param name="outputPath">The output image path.</param>
    /// <param name="format">The target format.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when conversion succeeds.</returns>
    Task ConvertFromDdsAsync(
        string inputPath,
        string outputPath,
        ImageFormat format,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a PNG preview for a DDS image.
    /// </summary>
    /// <param name="inputPath">The DDS input path.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The generated preview file path.</returns>
    Task<string> CreatePreviewPngAsync(
        string inputPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads DDS metadata when possible.
    /// </summary>
    /// <param name="inputPath">The DDS input path.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>DDS metadata.</returns>
    Task<DdsImageInformation> GetInformationAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
}
