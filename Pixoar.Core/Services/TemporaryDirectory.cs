using Pixoar.Core.Interfaces;

namespace Pixoar.Core.Services;

internal sealed class TemporaryDirectory : IDisposable
{
    private readonly IApplicationLogger? _logger;

    public TemporaryDirectory(IApplicationLogger? logger = null)
    {
        _logger = logger;
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Pixoar", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        const int attempts = 3;
        Exception? cleanupException = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            if (!Directory.Exists(Path))
            {
                return;
            }

            try
            {
                Directory.Delete(Path, recursive: true);
                return;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                cleanupException = exception;
                if (attempt == attempts)
                {
                    break;
                }

                Thread.Sleep(25 * attempt);
            }
        }

        if (cleanupException is null)
        {
            return;
        }

        try
        {
            _logger?.LogErrorAsync(
                $"Temporary directory cleanup failed after {attempts} attempts. Path: {Path}.",
                cleanupException,
                CancellationToken.None).GetAwaiter().GetResult();
        }
        catch
        {
            // Cleanup failures must not replace the image operation's primary exception.
        }
    }
}
