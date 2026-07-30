using ImageMagick;

namespace Pixoar.Core.Services;

internal static class ImageColorManagement
{
    public static void NormalizeToSrgb(MagickImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        var sourceProfile = image.GetColorProfile();
        var pixelsTransformed = false;
        if (sourceProfile is not null)
        {
            if (!image.TransformColorSpace(ColorProfiles.SRGB))
            {
                throw new InvalidOperationException(
                    $"The embedded color profile '{sourceProfile.Description}' could not be converted to sRGB.");
            }

            pixelsTransformed = true;
        }
        else if (image.ColorSpace != ColorSpace.sRGB)
        {
            // Setting ColorSpace performs a pixel conversion. This is required for
            // gamma-1.0 PNG/TIFF data; merely tagging those samples as sRGB makes
            // the result visibly darker.
            image.ColorSpace = ColorSpace.sRGB;
            pixelsTransformed = true;
        }

        if (pixelsTransformed && image.Depth < 8)
        {
            // ICC conversion promotes palette entries to direct RGB samples.
            // TIFF honors the original PNG palette depth unless it is updated,
            // which can quantize the converted samples to black.
            image.Depth = 8;
        }

        if (image.GetColorProfile() is null)
        {
            // Some encoders serialize this as native sRGB/gAMA chunks instead of
            // an iCCP block. Either representation unambiguously tags the pixels.
            image.SetProfile(ColorProfiles.SRGB);
        }

        UpdateExifColorSpace(image);
    }

    public static void TagSrgbSamplesWithoutTransform(MagickImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        // texconv decodes legacy color DDS samples exactly as stored, but legacy
        // headers cannot declare a transfer function. Magick therefore sees
        // some decoded files as linear RGB. Preserve the sample bytes while
        // correcting only their interpretation and metadata.
        using var pixels = image.GetPixels();
        var samples = pixels.ToByteArray(PixelMapping.RGBA)
            ?? throw new InvalidOperationException("Could not read decoded DDS pixels.");
        image.ColorSpace = ColorSpace.sRGB;
        image.ImportPixels(
            samples,
            new PixelImportSettings(
                image.Width,
                image.Height,
                StorageType.Char,
                PixelMapping.RGBA));
        image.SetProfile(ColorProfiles.SRGB);
        UpdateExifColorSpace(image);
    }

    private static void UpdateExifColorSpace(MagickImage image)
    {
        var exifProfile = image.GetExifProfile();
        if (exifProfile is null)
        {
            return;
        }

        // ICC is authoritative, but some decoders also inspect this tag.
        // Leaving an Adobe RGB/uncalibrated value after converting the
        // samples to sRGB creates contradictory color metadata.
        exifProfile.SetValue(ExifTag.ColorSpace, (ushort)1);
        image.SetProfile(exifProfile);
    }
}
