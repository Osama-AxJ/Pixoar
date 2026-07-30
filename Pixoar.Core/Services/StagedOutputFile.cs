using Pixoar.Core.Interfaces;

namespace Pixoar.Core.Services;

internal sealed class StagedOutputFile : IDisposable
{
    private const int CleanupAttempts = 3;
    private readonly IApplicationLogger? _logger;
    private bool _committed;
    private bool _ownsPath;

    public StagedOutputFile(string destinationPath, IApplicationLogger? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        DestinationPath = System.IO.Path.GetFullPath(destinationPath);
        var directory = System.IO.Path.GetDirectoryName(DestinationPath)
            ?? throw new IOException("The output path does not have a valid directory.");
        var extension = System.IO.Path.GetExtension(DestinationPath);
        var baseName = System.IO.Path.GetFileNameWithoutExtension(DestinationPath);

        Directory.CreateDirectory(directory);
        _logger = logger;

        string stagedPath;
        do
        {
            stagedPath = System.IO.Path.Combine(
                directory,
                $".{baseName}.pixoar-{Guid.NewGuid():N}{extension}");
        }
        while (File.Exists(stagedPath));

        Path = stagedPath;
    }

    public string DestinationPath { get; }

    public string Path { get; }

    public void Validate()
    {
        if (!File.Exists(Path))
        {
            throw new InvalidDataException("The image operation did not create a valid output file.");
        }

        _ownsPath = true;
        if (new FileInfo(Path).Length == 0)
        {
            throw new InvalidDataException("The image operation did not create a valid output file.");
        }
    }

    public void Commit(bool overwrite)
    {
        if (_committed)
        {
            throw new InvalidOperationException("The staged output has already been committed.");
        }

        Validate();
        File.Move(Path, DestinationPath, overwrite);
        _committed = true;
    }

    public void Dispose()
    {
        if (_committed || !_ownsPath || !File.Exists(Path))
        {
            return;
        }

        Exception? cleanupException = null;
        for (var attempt = 1; attempt <= CleanupAttempts; attempt++)
        {
            try
            {
                File.Delete(Path);
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                cleanupException = exception;
                if (attempt < CleanupAttempts)
                {
                    Thread.Sleep(25 * attempt);
                }
            }
        }

        try
        {
            _logger?.LogErrorAsync(
                $"Staged output cleanup failed after {CleanupAttempts} attempts. Path: {Path}.",
                cleanupException!,
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // Cleanup failures must not replace the image operation's primary exception.
        }
    }
}
