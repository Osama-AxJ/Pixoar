using Pixoar.Core.Models;

namespace Pixoar.Core.Interfaces;

/// <summary>
/// Describes format-specific image codec behavior.
/// </summary>
public interface IImageCodec
{
    /// <summary>
    /// Gets the format handled by the codec.
    /// </summary>
    ImageFormat Format { get; }

    /// <summary>
    /// Gets the supported file extensions without leading dots.
    /// </summary>
    IReadOnlySet<string> Extensions { get; }

    /// <summary>
    /// Gets the primary file extension without a leading dot.
    /// </summary>
    string PrimaryExtension { get; }

    /// <summary>
    /// Gets the user-visible format name.
    /// </summary>
    string DisplayName { get; }
}
