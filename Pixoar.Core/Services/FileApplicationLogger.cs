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
}
