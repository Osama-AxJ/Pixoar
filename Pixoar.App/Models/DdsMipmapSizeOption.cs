namespace Pixoar.App.Models;

/// <summary>
/// Represents a user-selectable smallest mip dimension.
/// </summary>
public sealed class DdsMipmapSizeOption
{
    private static readonly int[] SensiblePresetSizes = [64, 32, 16, 8, 4, 2, 1];

    /// <summary>
    /// Initializes a new instance of the <see cref="DdsMipmapSizeOption"/> class.
    /// </summary>
    /// <param name="size">The smallest mip dimension in pixels.</param>
    public DdsMipmapSizeOption(int size)
    {
        Size = size;
    }

    /// <summary>
    /// Gets the smallest mip dimension in pixels.
    /// </summary>
    public int Size { get; }

    /// <summary>
    /// Gets the text shown in the settings dropdown.
    /// </summary>
    public string DisplayName => $"{Size} px";

    /// <inheritdoc />
    public override string ToString() => DisplayName;

    /// <summary>
    /// Creates sensible endpoint presets that generate at least one downscaled mip level.
    /// </summary>
    /// <param name="smallestSourceDimension">The smallest width or height among the source images.</param>
    /// <returns>The valid endpoint presets in user-friendly order.</returns>
    public static IReadOnlyList<DdsMipmapSizeOption> CreateSensiblePresetOptions(int smallestSourceDimension)
    {
        return SensiblePresetSizes
            .Where(size => size < smallestSourceDimension)
            .Select(size => new DdsMipmapSizeOption(size))
            .ToArray();
    }

    /// <summary>
    /// Resolves an old or unavailable stored size to the nearest useful preset.
    /// </summary>
    /// <param name="requestedSize">The stored target endpoint.</param>
    /// <param name="options">The valid presets for the active sources.</param>
    /// <returns>The requested value when valid; otherwise the largest valid preset, or 1 when no downscaled preset exists.</returns>
    public static int ResolveToSensiblePreset(int requestedSize, IReadOnlyList<DdsMipmapSizeOption> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options.FirstOrDefault(option => option.Size == requestedSize)?.Size ??
            options.FirstOrDefault()?.Size ??
            1;
    }
}
