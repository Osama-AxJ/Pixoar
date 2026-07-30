using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class DefaultSettingsFactory : ISettingsFactory
{
    private readonly IReadOnlyDictionary<string, ImageFormatDescriptor> _convertFormatsByAlias;

    public DefaultSettingsFactory(IImageFormatDetector formatDetector)
    {
        _convertFormatsByAlias = CreateConvertFormatAliases(formatDetector.SupportedFormats);
    }

    public PixoarSettings CreateDefault()
    {
        return new PixoarSettings();
    }

    public PixoarSettings Normalize(PixoarSettings? settings)
    {
        if (settings is null)
        {
            return CreateDefault();
        }

        var defaults = CreateDefault();
        settings.SchemaVersion = settings.SchemaVersion <= 0 ? defaults.SchemaVersion : settings.SchemaVersion;
        settings.General ??= defaults.General;
        settings.Dds ??= defaults.Dds;
        settings.Quality ??= defaults.Quality;
        settings.Output ??= defaults.Output;
        settings.ContextMenu ??= defaults.ContextMenu;
        settings.ResizePresets ??= defaults.ResizePresets;
        settings.ConvertPresets ??= defaults.ConvertPresets;
        settings.ResizePresets = NormalizeResizePresets(settings.ResizePresets);
        settings.ConvertPresets = NormalizeConvertPresets(settings.ConvertPresets);

        return settings;
    }

    private static List<ResizePreset> NormalizeResizePresets(IEnumerable<ResizePreset?> presets)
    {
        var seenPercentages = new HashSet<int>();
        return presets
            .Select(NormalizeResizePreset)
            .Where(preset => preset is not null)
            .Cast<ResizePreset>()
            .Where(preset => seenPercentages.Add(preset.Percentage!.Value))
            .ToList();
    }

    private List<ConvertPreset> NormalizeConvertPresets(IEnumerable<ConvertPreset?> presets)
    {
        var normalized = new List<ConvertPreset>();
        var seenFormats = new HashSet<ImageFormat>();

        foreach (var preset in presets)
        {
            if (preset is null)
            {
                continue;
            }

            var value = string.IsNullOrWhiteSpace(preset.Format) ? preset.Name : preset.Format;
            var alias = NormalizeFormatAlias(value);
            if (alias.Length == 0 ||
                !_convertFormatsByAlias.TryGetValue(alias, out var descriptor) ||
                !seenFormats.Add(descriptor.Format))
            {
                continue;
            }

            normalized.Add(new ConvertPreset
            {
                Name = descriptor.PrimaryExtension.ToUpperInvariant(),
                Format = descriptor.PrimaryExtension.ToLowerInvariant(),
                IsEnabled = preset.IsEnabled
            });
        }

        return normalized;
    }

    private static ResizePreset? NormalizeResizePreset(ResizePreset? preset)
    {
        if (preset is null)
        {
            return null;
        }

        var percentage = preset.Percentage;
        if (percentage is null && TryParsePercentage(preset.Name, out var parsedPercentage))
        {
            percentage = parsedPercentage;
        }

        if (percentage is not > 0)
        {
            return null;
        }

        return new ResizePreset
        {
            Name = $"{percentage.Value}%",
            Percentage = percentage,
            IsEnabled = preset.IsEnabled
        };
    }

    private static IReadOnlyDictionary<string, ImageFormatDescriptor> CreateConvertFormatAliases(
        IEnumerable<ImageFormatDescriptor> supportedFormats)
    {
        var aliases = new Dictionary<string, ImageFormatDescriptor>(StringComparer.OrdinalIgnoreCase);
        foreach (var descriptor in supportedFormats)
        {
            aliases[NormalizeFormatAlias(descriptor.PrimaryExtension)] = descriptor;
            aliases[NormalizeFormatAlias(descriptor.DisplayName)] = descriptor;

            foreach (var extension in descriptor.Extensions)
            {
                aliases[NormalizeFormatAlias(extension)] = descriptor;
            }
        }

        return aliases;
    }

    private static string NormalizeFormatAlias(string? value)
    {
        return value?.Trim().TrimStart('.') ?? string.Empty;
    }

    private static bool TryParsePercentage(string? value, out int percentage)
    {
        percentage = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.EndsWith('%')
            && int.TryParse(trimmed.TrimEnd('%'), out percentage)
            && percentage > 0;
    }
}
