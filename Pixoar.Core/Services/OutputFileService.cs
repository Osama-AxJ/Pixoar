using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class OutputFileService(
    ISettingsService settingsService,
    IImageFormatDetector formatDetector) : IOutputFileService
{
    public string CreateOutputPath(OutputFileRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        var settings = settingsService.Current.Output;
        var source = new FileInfo(request.SourcePath);
        var outputDirectory = ResolveOutputDirectory(request, settings, source);
        Directory.CreateDirectory(outputDirectory);

        var extension = formatDetector.GetPrimaryExtension(request.OutputFormat);
        var suffix = string.IsNullOrWhiteSpace(request.OperationSuffix)
            ? GetDefaultSuffix(request.OperationKind)
            : request.OperationSuffix;
        var baseName = $"{Path.GetFileNameWithoutExtension(source.Name)}_{suffix}";
        var candidate = Path.Combine(outputDirectory, $"{baseName}.{extension}");

        if (IsSamePath(candidate, source.FullName))
        {
            throw new IOException("Output cannot overwrite the source image.");
        }

        if (!File.Exists(candidate))
        {
            return candidate;
        }

        if (!settings.RenameDuplicatesAutomatically)
        {
            if (settings.PreventOverwrite)
            {
                throw new IOException($"Output file already exists: {candidate}");
            }

            return candidate;
        }

        var index = 2;
        while (true)
        {
            var renamed = Path.Combine(outputDirectory, $"{baseName}_{index}.{extension}");
            if (IsSamePath(renamed, source.FullName))
            {
                index++;
                continue;
            }

            if (!File.Exists(renamed))
            {
                return renamed;
            }

            index++;
        }
    }

    private static string ResolveOutputDirectory(
        OutputFileRequest request,
        OutputSettings settings,
        FileSystemInfo source)
    {
        if (!string.IsNullOrWhiteSpace(request.OutputFolder))
        {
            return request.OutputFolder;
        }

        if (!settings.SaveBesideOriginal && !string.IsNullOrWhiteSpace(settings.CustomOutputFolder))
        {
            return settings.CustomOutputFolder;
        }

        return source is FileInfo file && file.DirectoryName is not null
            ? file.DirectoryName
            : Environment.CurrentDirectory;
    }

    private static string GetDefaultSuffix(OutputOperationKind operationKind)
    {
        return operationKind == OutputOperationKind.Resize ? "resized" : "converted";
    }

    private static bool IsSamePath(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
