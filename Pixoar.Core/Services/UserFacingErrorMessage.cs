using ImageMagick;

namespace Pixoar.Core.Services;

internal static class UserFacingErrorMessage
{
    public static string ForImageLoad(Exception exception)
    {
        if (IsTexconvMissing(exception))
        {
            return "DDS support requires bundled texconv.exe. Build Pixoar again or place texconv.exe beside the app.";
        }

        return exception switch
        {
            FileNotFoundException => "The selected file could not be found.",
            DirectoryNotFoundException => "The selected folder could not be found.",
            UnauthorizedAccessException => "Pixoar does not have permission to access this file.",
            InvalidDataException => "The selected file is not a valid image.",
            MagickException => "The selected image could not be loaded.",
            ArgumentException => "The selected image path is not valid.",
            _ => "The selected image could not be loaded."
        };
    }

    public static string ForImageOperation(Exception exception)
    {
        if (IsTexconvMissing(exception))
        {
            return "DDS support requires bundled texconv.exe. Build Pixoar again or place texconv.exe beside the app.";
        }

        if (exception is InvalidOperationException invalidOperationException &&
            invalidOperationException.Message.StartsWith("DDS ", StringComparison.OrdinalIgnoreCase))
        {
            return invalidOperationException.Message;
        }

        if (exception is IOException ioException &&
            ioException.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            return "The output file already exists. Enable duplicate naming or choose another folder.";
        }

        return exception switch
        {
            FileNotFoundException => "The selected file could not be found.",
            DirectoryNotFoundException => "The output folder could not be found.",
            UnauthorizedAccessException => "Pixoar does not have permission to write the output file.",
            MagickException => "The selected image could not be processed.",
            ArgumentException => "The operation settings are not valid.",
            _ => "The image could not be processed."
        };
    }

    private static bool IsTexconvMissing(Exception exception)
    {
        return exception is FileNotFoundException &&
            exception.Message.Contains("texconv.exe", StringComparison.OrdinalIgnoreCase);
    }
}
