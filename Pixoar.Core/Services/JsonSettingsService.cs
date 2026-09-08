using System.Text.Json;
using System.Text.Json.Serialization;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class JsonSettingsService(
    IApplicationPathProvider pathProvider,
    ISettingsFactory settingsFactory,
    IApplicationLogger logger) : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly SemaphoreSlim _settingsLock = new(1, 1);
    private PixoarSettings? _current;

    public PixoarSettings Current => _current ??= settingsFactory.CreateDefault();

    public async Task<PixoarSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(pathProvider.AppDataDirectory);
            await using var crossProcessLock = await AcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);

            if (!File.Exists(pathProvider.SettingsFilePath))
            {
                _current = settingsFactory.CreateDefault();
                await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
                await logger.LogInformationAsync("Created default settings file.", cancellationToken).ConfigureAwait(false);
                return _current;
            }

            try
            {
                PixoarSettings? settings;
                var migrateLegacyDdsMipmapFilter = false;

                await using (var stream = await OpenSettingsReadStreamAsync(cancellationToken).ConfigureAwait(false))
                {
                    using var reader = new StreamReader(stream);
                    var settingsJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                    settings = JsonSerializer.Deserialize<PixoarSettings>(settingsJson, SerializerOptions);
                    migrateLegacyDdsMipmapFilter = HasLegacyDdsMipmapFilter(settingsJson);
                }

                var beforeNormalization = JsonSerializer.Serialize(settings, SerializerOptions);
                _current = settingsFactory.Normalize(settings);
                var afterNormalization = JsonSerializer.Serialize(_current, SerializerOptions);
                if (migrateLegacyDdsMipmapFilter ||
                    !string.Equals(beforeNormalization, afterNormalization, StringComparison.Ordinal))
                {
                    await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
                    await logger.LogInformationAsync(
                        "Saved normalized settings because the loaded data changed.",
                        cancellationToken).ConfigureAwait(false);
                }

                return _current;
            }
            catch (JsonException ex)
            {
                await PreserveInvalidSettingsAsync(cancellationToken).ConfigureAwait(false);
                await logger.LogErrorAsync("Settings file was corrupt and has been backed up as settings.broken.json.", ex, cancellationToken).ConfigureAwait(false);

                _current = settingsFactory.CreateDefault();
                await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
                return _current;
            }
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<PixoarSettings> UpdateAsync(
        Action<PixoarSettings> update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _settingsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(pathProvider.AppDataDirectory);
            await using var crossProcessLock = await AcquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);
            update(Current);
            _current = settingsFactory.Normalize(Current);
            await SaveUnlockedAsync(cancellationToken).ConfigureAwait(false);
            return _current;
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    private async Task SaveUnlockedAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(pathProvider.AppDataDirectory);

        var tempFilePath = Path.Combine(
            pathProvider.AppDataDirectory,
            $"{Path.GetFileName(pathProvider.SettingsFilePath)}.{Guid.NewGuid():N}.tmp");

        await RetryFileOperationAsync(async () =>
        {
            try
            {
                await using (var stream = new FileStream(
                    tempFilePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    await JsonSerializer.SerializeAsync(
                        stream,
                        Current,
                        SerializerOptions,
                        cancellationToken).ConfigureAwait(false);
                }

                File.Move(tempFilePath, pathProvider.SettingsFilePath, overwrite: true);
            }
            finally
            {
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }, cancellationToken).ConfigureAwait(false);
    }

    private async Task<FileStream> AcquireCrossProcessLockAsync(CancellationToken cancellationToken)
    {
        var lockFilePath = $"{pathProvider.SettingsFilePath}.lock";
        const int maxAttempts = 100;

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
                await Task.Delay(50, cancellationToken).ConfigureAwait(false);
            }
        }

        throw new IOException("Pixoar could not acquire the cross-process settings lock.");
    }

    private async Task<FileStream> OpenSettingsReadStreamAsync(CancellationToken cancellationToken)
    {
        FileStream? stream = null;
        await RetryFileOperationAsync(() =>
        {
            stream = new FileStream(
                pathProvider.SettingsFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite);
            return Task.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);

        return stream!;
    }

    private static async Task RetryFileOperationAsync(
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await operation().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (
                (ex is IOException or UnauthorizedAccessException) &&
                attempt < maxAttempts)
            {
                await Task.Delay(75 * attempt, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private Task PreserveInvalidSettingsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(pathProvider.SettingsFilePath))
        {
            return Task.CompletedTask;
        }

        var brokenFilePath = Path.Combine(pathProvider.AppDataDirectory, "settings.broken.json");
        File.Move(pathProvider.SettingsFilePath, brokenFilePath, overwrite: true);
        return Task.CompletedTask;
    }

    private static bool HasLegacyDdsMipmapFilter(string settingsJson)
    {
        using var document = JsonDocument.Parse(settingsJson);
        if (!TryGetPropertyIgnoreCase(document.RootElement, "dds", out var dds) ||
            !TryGetPropertyIgnoreCase(dds, "mipmapFilter", out var filter))
        {
            return false;
        }

        if (filter.ValueKind == JsonValueKind.String)
        {
            return filter.GetString()?.Trim().ToLowerInvariant() is "default" or "texconvdefault" or "default (texconv)";
        }

        return filter.ValueKind == JsonValueKind.Number &&
            filter.TryGetInt32(out var value) &&
            value == 0;
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string name,
        out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }
}
