using Pixoar.Core.Interfaces;

namespace Pixoar.Core.Services;

internal sealed class FileApplicationLogger(IApplicationPathProvider pathProvider) : IApplicationLogger
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public Task LogInformationAsync(string message, CancellationToken cancellationToken = default)
    {
        return WriteAsync("INFO", message, null, cancellationToken);
    }

    public Task LogWarningAsync(string message, CancellationToken cancellationToken = default)
    {
        return WriteAsync("WARN", message, null, cancellationToken);
    }

    public Task LogErrorAsync(string message, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        return WriteAsync("ERROR", message, exception, cancellationToken);
    }

    private async Task WriteAsync(
        string level,
        string message,
        Exception? exception,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(pathProvider.LogsDirectory);

        var timestamp = DateTimeOffset.Now;
        var logFile = Path.Combine(pathProvider.LogsDirectory, $"pixoar-{timestamp:yyyyMMdd}.log");
        var line = $"[{timestamp:O}] [{level}] {message}";

        if (exception is not null)
        {
            line = $"{line}{Environment.NewLine}{exception}";
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using var crossProcessLock = await AcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);
            await File.AppendAllTextAsync(
                logFile,
                line + Environment.NewLine,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task<FileStream> AcquireCrossProcessLockAsync(CancellationToken cancellationToken)
    {
        var lockFilePath = Path.Combine(pathProvider.LogsDirectory, ".write.lock");
        const int maxAttempts = 200;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return new FileStream(
                    lockFilePath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (Exception ex) when (
                (ex is IOException or UnauthorizedAccessException) &&
                attempt < maxAttempts)
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new IOException("Pixoar could not acquire the cross-process log lock.");
    }
}
