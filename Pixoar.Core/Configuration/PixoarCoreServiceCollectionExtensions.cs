using Microsoft.Extensions.DependencyInjection;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Services;
using Pixoar.Core.Services.ImageCodecs;

namespace Pixoar.Core.Configuration;

/// <summary>
/// Registers reusable Pixoar core services with a dependency injection container.
/// </summary>
public static class PixoarCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds path, settings, and logging services shared by the desktop app and CLI.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="configure">An optional callback used to override default core options.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPixoarCore(
        this IServiceCollection services,
        Action<PixoarCoreOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new PixoarCoreOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(_ => new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        });
        services.AddSingleton<IApplicationPathProvider, EnvironmentApplicationPathProvider>();
        services.AddSingleton<ISettingsFactory, DefaultSettingsFactory>();
        services.AddSingleton<IApplicationLogger, FileApplicationLogger>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IImageCodec, PngCodec>();
        services.AddSingleton<IImageCodec, JpegCodec>();
        services.AddSingleton<IImageCodec, WebpCodec>();
        services.AddSingleton<IImageCodec, BmpCodec>();
        services.AddSingleton<IImageCodec, TiffCodec>();
        services.AddSingleton<IImageCodec, DdsCodec>();
        services.AddSingleton<IImageFormatDetector, ImageFormatDetector>();
        services.AddSingleton<IOutputFileService, OutputFileService>();
        services.AddSingleton<IDdsDependencyService, DdsDependencyService>();
        services.AddSingleton<IDdsEncoder, TexconvDdsEncoder>();
        services.AddSingleton<IDdsService, DdsService>();
        services.AddSingleton<IImageConversionService, ImageConversionService>();
        services.AddSingleton<IImageResizeService, ImageResizeService>();
        services.AddSingleton<IImagePreviewService, ImagePreviewService>();
        services.AddSingleton<IImageInfoService, ImageInfoService>();
        services.AddSingleton<IContextMenuService, ContextMenuService>();
        services.AddSingleton<IPixoarCleanupService, PixoarCleanupService>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();

        return services;
    }
}
