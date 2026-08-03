using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class OutputFileService(
    ISettingsService settingsService,
    IImageFormatDetector formatDetector) : IOutputFileService
{
    public OutputFileResolution CreateOutputPath(OutputFileRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        var settings = settingsService.Current.Output;
        var source = new FileInfo(request.SourcePath);
        var outputDirectory = ResolveOutputDirectory(request, settings, source);
        Directory.CreateDirectory(outputDirectory);

        var fileName = request.OperationKind == OutputOperationKind.Resize &&
            formatDetector.Detect(source.FullName) == request.OutputFormat
                ? source.Name
                : $"{Path.GetFileNameWithoutExtension(source.Name)}.{formatDetector.GetPrimaryExtension(request.OutputFormat)}";
        var candidate = Path.Combine(outputDirectory, fileName);

        if (IsSamePath(candidate, source.FullName))
        {
            return settings.ConflictBehavior switch
            {
                OutputConflictBehavior.RenameDuplicatesAutomatically => CreateRenamedResolution(candidate, source.FullName),
                OutputConflictBehavior.SkipExistingFiles => CreateResolution(candidate, shouldSkip: true),
                _ => throw new IOException("Output cannot overwrite the source image.")
            };
        }

        if (!File.Exists(candidate))
        {
            return CreateResolution(candidate);
        }

        return settings.ConflictBehavior switch
        {
            OutputConflictBehavior.RenameDuplicatesAutomatically => CreateRenamedResolution(candidate, source.FullName),
            OutputConflictBehavior.SkipExistingFiles => CreateResolution(candidate, shouldSkip: true),
            OutputConflictBehavior.OverwriteExistingFiles => CreateResolution(candidate, allowOverwrite: true),
            _ => CreateRenamedResolution(candidate, source.FullName)
        };
    }

    private static OutputFileResolution CreateRenamedResolution(string candidate, string sourcePath)
    {
        var outputDirectory = Path.GetDirectoryName(candidate)
            ?? throw new IOException("The output path does not have a valid directory.");
        var baseName = Path.GetFileNameWithoutExtension(candidate);
        var extension = Path.GetExtension(candidate);
        var index = 1;
        while (true)
        {
            var renamed = Path.Combine(outputDirectory, $"{baseName}_{index}{extension}");
            if (IsSamePath(renamed, sourcePath))
            {
                index++;
                continue;
            }

            if (!File.Exists(renamed))
            {
                return CreateResolution(renamed);
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

        var sourceDirectory = source is FileInfo file && file.DirectoryName is not null
            ? file.DirectoryName
            : Environment.CurrentDirectory;

        if (request.OperationKind == OutputOperationKind.Convert &&
            settings.SaveConvertedFilesInConvertedFolder)
        {
            return Path.Combine(sourceDirectory, "Converted");
        }

        if (request.OperationKind == OutputOperationKind.Resize &&
            settings.SaveResizedFilesInResizeFolder)
        {
            return Path.Combine(sourceDirectory, "Resize");
        }

        if (!settings.SaveBesideOriginal && !string.IsNullOrWhiteSpace(settings.CustomOutputFolder))
        {
            return settings.CustomOutputFolder;
        }

        return sourceDirectory;
    }

    private static OutputFileResolution CreateResolution(
        string path,
        bool shouldSkip = false,
        bool allowOverwrite = false)
    {
        return new OutputFileResolution
        {
            Path = path,
            ShouldSkip = shouldSkip,
            AllowOverwrite = allowOverwrite
        };
    }

    private static bool IsSamePath(string left, string right)
    {
        return string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }
}
