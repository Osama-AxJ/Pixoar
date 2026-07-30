using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Detects supported image formats from file paths.
/// </summary>
public interface IImageFormatDetector
{
    /// <summary>
    /// Gets every supported image format descriptor.
    /// </summary>
    IReadOnlyList<ImageFormatDescriptor> SupportedFormats { get; }

    /// <summary>
    /// Determines whether a file path has a supported image extension.
    /// </summary>
    /// <param name="path">The path to test.</param>
    /// <returns>True when the path is supported; otherwise false.</returns>
    bool IsSupported(string path);

    /// <summary>
    /// Detects the image format for a path.
    /// </summary>
    /// <param name="path">The path to inspect.</param>
    /// <returns>The detected image format.</returns>
    ImageFormat Detect(string path);

    /// <summary>
    /// Attempts to detect the image format for a path.
    /// </summary>
    /// <param name="path">The path to inspect.</param>
    /// <param name="format">The detected format when successful.</param>
    /// <returns>True when a supported format was detected; otherwise false.</returns>
    bool TryDetect(string path, out ImageFormat format);

    /// <summary>
    /// Gets the preferred file extension for a format.
    /// </summary>
    /// <param name="format">The image format.</param>
    /// <returns>The extension without a leading dot.</returns>
    string GetPrimaryExtension(ImageFormat format);

    /// <summary>
    /// Gets the display name for a format.
    /// </summary>
    /// <param name="format">The image format.</param>
    /// <returns>The display name.</returns>
    string GetDisplayName(ImageFormat format);
}
