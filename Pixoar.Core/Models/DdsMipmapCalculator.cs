namespace Pixoar.Core.Models;

/// <summary>
/// Calculates texconv DDS mip-level counts from numeric texture dimensions.
/// </summary>
public static class DdsMipmapCalculator
{
    /// <summary>
    /// Calculates the number of mip levels, including the original base level,
    /// needed to reach the requested smallest texture dimension.
    /// </summary>
    /// <param name="width">The source width in pixels.</param>
    /// <param name="height">The source height in pixels.</param>
    /// <param name="smallestMipSize">The target minimum dimension in pixels.</param>
    /// <returns>A valid texconv mip-level count including the base image.</returns>
    public static int CalculateMipCount(int width, int height, int smallestMipSize)
    {
        ValidateDimensions(width, height);
        if (smallestMipSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(smallestMipSize), "The smallest mip size must be at least one pixel.");
        }

        var mipCount = 1;
        while (Math.Min(width, height) > smallestMipSize && (width > 1 || height > 1))
        {
            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
            mipCount++;
        }

        return mipCount;
    }

    /// <summary>
    /// Calculates the maximum full-chain mip-level count, including the base image.
    /// </summary>
    /// <param name="width">The source width in pixels.</param>
    /// <param name="height">The source height in pixels.</param>
    /// <returns>The full mip-chain level count.</returns>
    public static int CalculateFullChainMipCount(int width, int height)
    {
        ValidateDimensions(width, height);

        var mipCount = 1;
        while (width > 1 || height > 1)
        {
            width = Math.Max(1, width / 2);
            height = Math.Max(1, height / 2);
            mipCount++;
        }

        return mipCount;
    }

    private static void ValidateDimensions(int width, int height)
    {
        if (width < 1 || height < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Texture dimensions must be positive.");
        }
    }
}
