using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Pixoar.Core.Configuration;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.ResizeSmoke;

internal static class Program
{
    private const int SourceWidth = 160;
    private const int SourceHeight = 96;
    private static readonly string[] GuidTempSearchPatterns = ["????????????????????????????????"];

    public static async Task<int> Main(string[] args)
    {
        var keepWorkspace = args.Contains("--keep", StringComparer.OrdinalIgnoreCase);
        var runRoot = Path.Combine(
            Path.GetTempPath(),
            "PixoarResizeSmoke",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(runRoot);
        Console.WriteLine($"Resize smoke workspace: {runRoot}");

        try
        {
            if (args.Contains("--output-organization", StringComparer.OrdinalIgnoreCase))
            {
                await RunOutputOrganizationAsync(runRoot);
                Console.WriteLine("PASS: output organization and collision behavior verified.");
            }
            else
            {
                await RunAsync(runRoot);
                Console.WriteLine("PASS: resize smoke matrix and regressions completed.");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL: {exception}");
            return 1;
        }
        finally
        {
            if (keepWorkspace)
            {
                Console.WriteLine($"Kept resize smoke workspace: {runRoot}");
            }
            else
            {
                DeleteDirectoryWithRetries(runRoot);
            }
        }
    }

    private static async Task RunAsync(string runRoot)
    {
        var appDataRoot = Path.Combine(runRoot, "AppData");
        var fixtureRoot = Path.Combine(runRoot, "Fixtures");
        Directory.CreateDirectory(fixtureRoot);

        var pathProvider = new SmokeApplicationPathProvider(appDataRoot);
        using var provider = CreateProvider(pathProvider);
        var settingsService = provider.GetRequiredService<ISettingsService>();
        var resizeService = provider.GetRequiredService<IImageResizeService>();
        var conversionService = provider.GetRequiredService<IImageConversionService>();
        var previewService = provider.GetRequiredService<IImagePreviewService>();
        var infoService = provider.GetRequiredService<IImageInfoService>();
        var ddsService = provider.GetRequiredService<IDdsService>();
        var dependencyService = provider.GetRequiredService<IDdsDependencyService>();

        await settingsService.LoadAsync();
        await ApplyDdsSettingsAsync(
            settingsService,
            DdsCompressionMode.Dxt3,
            generateMipmaps: false,
            preserveAlpha: true);

        var texconvPath = dependencyService.ResolveTexconvPath()
            ?? throw new InvalidOperationException(dependencyService.MissingTexconvMessage);
        var bundledTexconvPath = Path.Combine(FindRepositoryRoot(), "tools", "texconv", "texconv.exe");
        Assert(
            FileSnapshot.Hash(texconvPath) == FileSnapshot.Hash(bundledTexconvPath),
            $"The resolved texconv executable did not match the bundled binary: {texconvPath}");
        Console.WriteLine($"Bundled texconv: {texconvPath}");

        var fixtures = await CreateFixturesAsync(fixtureRoot, ddsService);
        var originalSnapshots = fixtures.ToDictionary(
            fixture => fixture.InputPath,
            fixture => FileSnapshot.Create(fixture.InputPath),
            StringComparer.OrdinalIgnoreCase);

        foreach (var fixture in fixtures)
        {
            await VerifyResizeAsync(
                resizeService,
                infoService,
                fixture,
                CreatePercentageRequest(fixture.InputPath, 50),
                expectedWidth: 80,
                expectedHeight: 48,
                expectedSuffix: "_50pct");
            await VerifyResizeAsync(
                resizeService,
                infoService,
                fixture,
                CreatePercentageRequest(fixture.InputPath, 75),
                expectedWidth: 120,
                expectedHeight: 72,
                expectedSuffix: "_75pct");
            await VerifyResizeAsync(
                resizeService,
                infoService,
                fixture,
                new ImageResizeRequest
                {
                    InputPath = fixture.InputPath,
                    ResizeMethod = ResizeMethod.Dimensions,
                    Width = 80,
                    Height = 80,
                    KeepAspectRatio = true,
                    Mode = ResizeMode.Fit
                },
                expectedWidth: 80,
                expectedHeight: 48,
                expectedSuffix: "_80x80");

            Console.WriteLine(
                $"PASS {fixture.Name,-4}: 50%=80x48, 75%=120x72, Fit 80x80=80x48, output={fixture.PrimaryExtension}");
        }

        await VerifyPercentageRoundingAsync(
            fixtureRoot,
            resizeService,
            infoService,
            ddsService);
        await VerifyBatchResizeAsync(resizeService, infoService, fixtures);
        await VerifyDdsSettingsAsync(
            fixtureRoot,
            pathProvider,
            settingsService,
            resizeService,
            infoService,
            fixtures.Single(fixture => fixture.Format == ImageFormat.Dds));
        await VerifyDdsDimensionModesAsync(
            fixtureRoot,
            resizeService,
            infoService,
            fixtures.Single(fixture => fixture.Format == ImageFormat.Dds));
        await VerifyFailureCleanupAsync(
            resizeService,
            infoService,
            fixtures.Single(fixture => fixture.Format == ImageFormat.Dds),
            fixtures.Single(fixture => fixture.Name == "PNG"));
        await VerifyRegressionsAsync(
            fixtureRoot,
            pathProvider,
            settingsService,
            resizeService,
            conversionService,
            previewService,
            infoService,
            dependencyService,
            fixtures);
        await VerifySettingsRegressionsAsync(fixtureRoot);
        VerifyRequiredLogging(pathProvider.LogsDirectory);

        foreach (var pair in originalSnapshots)
        {
            Assert(
                pair.Value == FileSnapshot.Create(pair.Key),
                $"Original input was modified: {pair.Key}");
        }

    }

    private static ServiceProvider CreateProvider(IApplicationPathProvider pathProvider)
    {
        var services = new ServiceCollection();
        services.AddPixoarCore();
        services.AddSingleton(pathProvider);
        return services.BuildServiceProvider();
    }

    private static async Task<IReadOnlyList<FormatFixture>> CreateFixturesAsync(
        string fixtureRoot,
        IDdsService ddsService)
    {
        var fixtures = new List<FormatFixture>
        {
            new("PNG", ImageFormat.Png, "png", "png"),
            new("JPG", ImageFormat.Jpeg, "jpg", "jpg"),
            new("JPEG", ImageFormat.Jpeg, "jpeg", "jpg"),
            new("WEBP", ImageFormat.Webp, "webp", "webp"),
            new("BMP", ImageFormat.Bmp, "bmp", "bmp"),
            new("TIFF", ImageFormat.Tiff, "tiff", "tiff"),
            new("TIF", ImageFormat.Tiff, "tif", "tiff")
        };

        foreach (var fixture in fixtures)
        {
            fixture.InputPath = Path.Combine(
                fixtureRoot,
                $"matrix_{fixture.Name.ToLowerInvariant()}.{fixture.InputExtension}");
            WriteFixture(fixture.InputPath, fixture.Format);
        }

        var sourcePng = fixtures.Single(fixture => fixture.Format == ImageFormat.Png).InputPath;
        var ddsFixture = new FormatFixture("DDS", ImageFormat.Dds, "dds", "dds")
        {
            InputPath = Path.Combine(fixtureRoot, "matrix_dds.dds")
        };

        await AssertNoGuidTempLeakAsync(
            () => ddsService.ConvertToDdsAsync(sourcePng, ddsFixture.InputPath));
        fixtures.Add(ddsFixture);

        return fixtures;
    }

    private static void WriteFixture(
        string path,
        ImageFormat format,
        int width = SourceWidth,
        int height = SourceHeight)
    {
        using var image = new MagickImage(
            new MagickColor("#802864DC"),
            (uint)width,
            (uint)height);

        image.Format = format switch
        {
            ImageFormat.Png => MagickFormat.Png,
            ImageFormat.Jpeg => MagickFormat.Jpeg,
            ImageFormat.Webp => MagickFormat.WebP,
            ImageFormat.Bmp => MagickFormat.Bmp,
            ImageFormat.Tiff => MagickFormat.Tiff,
            _ => throw new InvalidOperationException($"Unsupported smoke fixture format: {format}")
        };
        image.Write(path);
    }

    private static async Task VerifyResizeAsync(
        IImageResizeService resizeService,
        IImageInfoService infoService,
        FormatFixture fixture,
        ImageResizeRequest request,
        int expectedWidth,
        int expectedHeight,
        string expectedSuffix)
    {
        var result = await AssertNoGuidTempLeakAsync(() => resizeService.ResizeAsync(request));
        Assert(
            result.Success,
            $"{fixture.Name} resize failed: {result.Error?.Message ?? "unknown error"}");
        var outputPath = result.OutputPath
            ?? throw new InvalidOperationException($"{fixture.Name} resize returned no output path.");
        Assert(File.Exists(outputPath), $"{fixture.Name} output does not exist: {outputPath}");
        Assert(
            string.Equals(
                Path.GetExtension(outputPath),
                $".{fixture.InputExtension}",
                StringComparison.OrdinalIgnoreCase),
            $"{fixture.Name} output extension was not preserved: {outputPath}");
        Assert(
            !Path.GetFileNameWithoutExtension(outputPath).EndsWith(
                expectedSuffix,
                StringComparison.OrdinalIgnoreCase),
            $"{fixture.Name} output naming still contained the operation suffix {expectedSuffix}: {outputPath}");

        var information = await infoService.GetInformationAsync(outputPath);
        Assert(information.Format == fixture.Format, $"{fixture.Name} output format could not be reopened.");
        Assert(
            information.Width == expectedWidth && information.Height == expectedHeight,
            $"{fixture.Name} output was {information.Width}x{information.Height}; expected {expectedWidth}x{expectedHeight}.");

        if (fixture.Format != ImageFormat.Dds)
        {
            using var reopened = new MagickImage(outputPath);
            Assert(
                IsExpectedMagickFormat(reopened.Format, fixture.Format),
                $"{fixture.Name} output content decoded as {reopened.Format}, not {fixture.Format}.");
        }
    }

    private static async Task VerifyPercentageRoundingAsync(
        string fixtureRoot,
        IImageResizeService resizeService,
        IImageInfoService infoService,
        IDdsService ddsService)
    {
        var oddPngPath = Path.Combine(fixtureRoot, "rounding_6x5.png");
        var oddDdsPath = Path.Combine(fixtureRoot, "rounding_6x5.dds");
        WriteFixture(oddPngPath, ImageFormat.Png, width: 6, height: 5);
        await AssertNoGuidTempLeakAsync(
            () => ddsService.ConvertToDdsAsync(oddPngPath, oddDdsPath));

        foreach (var inputPath in new[] { oddPngPath, oddDdsPath })
        {
            var result = await AssertNoGuidTempLeakAsync(
                () => resizeService.ResizeAsync(CreatePercentageRequest(inputPath, 50)));
            Assert(result.Success && result.OutputPath is not null, $"Odd-dimension resize failed: {inputPath}");
            var information = await infoService.GetInformationAsync(result.OutputPath!);
            Assert(
                information.Width == 3 && information.Height == 2,
                $"The explicit 6x5 at 50% formula produced {information.Width}x{information.Height}, not 3x2.");
        }

        var tinyPngPath = Path.Combine(fixtureRoot, "rounding_1x1.png");
        WriteFixture(tinyPngPath, ImageFormat.Png, width: 1, height: 1);
        var tinyResult = await resizeService.ResizeAsync(CreatePercentageRequest(tinyPngPath, 50));
        Assert(tinyResult.Success && tinyResult.OutputPath is not null, "1x1 minimum-size resize failed.");
        var tinyInformation = await infoService.GetInformationAsync(tinyResult.OutputPath!);
        Assert(
            tinyInformation.Width == 1 && tinyInformation.Height == 1,
            "Percentage resize did not clamp each dimension to at least one pixel.");

        Console.WriteLine("PASS percentage formula: 6x5 at 50%=3x2 for PNG/DDS; 1x1 clamps to 1x1.");
    }

    private static async Task VerifyBatchResizeAsync(
        IImageResizeService resizeService,
        IImageInfoService infoService,
        IReadOnlyList<FormatFixture> fixtures)
    {
        var progress = new CollectingProgress();
        var requests = fixtures.Select(
            fixture => CreatePercentageRequest(fixture.InputPath, 50));
        var result = await AssertNoGuidTempLeakAsync(
            () => resizeService.ResizeBatchAsync(requests, progress));

        Assert(result.SuccessCount == fixtures.Count, "Batch resize did not process every supported format.");
        Assert(result.ErrorCount == 0, "Batch resize reported one or more errors.");
        Assert(progress.Events.Count == fixtures.Count * 2, "Batch resize progress did not report start and completion for every file.");

        for (var index = 0; index < fixtures.Count; index++)
        {
            var fixture = fixtures[index];
            var outputPath = result.Results[index].OutputPath
                ?? throw new InvalidOperationException($"Batch output path missing for {fixture.Name}.");
            Assert(File.Exists(outputPath), $"Batch output missing for {fixture.Name}.");
            Assert(
                Path.GetFileNameWithoutExtension(outputPath).EndsWith("_4", StringComparison.OrdinalIgnoreCase),
                $"Duplicate output naming failed for {fixture.Name}: {outputPath}");
            var information = await infoService.GetInformationAsync(outputPath);
            Assert(
                information.Width == 80 && information.Height == 48,
                $"Batch output dimensions failed for {fixture.Name}.");
        }

        Console.WriteLine("PASS batch: 8/8 succeeded, numbered duplicates, 16 progress events.");
    }

    private static async Task VerifyDdsSettingsAsync(
        string fixtureRoot,
        SmokeApplicationPathProvider pathProvider,
        ISettingsService settingsService,
        IImageResizeService resizeService,
        IImageInfoService infoService,
        FormatFixture ddsFixture)
    {
        var compressionCases = new[]
        {
            new DdsCompressionCase(DdsCompressionMode.Dxt1, "DXT1", null),
            new DdsCompressionCase(DdsCompressionMode.Dxt3, "DXT3", null),
            new DdsCompressionCase(DdsCompressionMode.Dxt5, "DXT5", null),
            new DdsCompressionCase(DdsCompressionMode.Bc7, "DX10", 99u),
            new DdsCompressionCase(DdsCompressionMode.Uncompressed, "\0\0\0\0", null)
        };

        foreach (var compressionCase in compressionCases)
        {
            await ApplyDdsSettingsAsync(
                settingsService,
                compressionCase.Mode,
                generateMipmaps: false,
                preserveAlpha: true);
            var inputPath = Path.Combine(
                fixtureRoot,
                $"settings_{compressionCase.Mode.ToString().ToLowerInvariant()}.dds");
            File.Copy(ddsFixture.InputPath, inputPath);

            var result = await AssertNoGuidTempLeakAsync(
                () => resizeService.ResizeAsync(CreatePercentageRequest(inputPath, 50)));
            Assert(result.Success && result.OutputPath is not null, $"{compressionCase.Mode} DDS resize failed.");
            var header = ReadDdsHeader(result.OutputPath!);
            Assert(
                CompressionMatches(header, compressionCase),
                $"{compressionCase.Mode} DDS header was FourCC={header.FourCc}, DXGI={header.DxgiFormat?.ToString() ?? "none"}.");
            Assert(header.MipmapCount == 1, $"{compressionCase.Mode} unexpectedly generated mipmaps.");
            var information = await infoService.GetInformationAsync(result.OutputPath!);
            Assert(information.Width == 80 && information.Height == 48, $"{compressionCase.Mode} output could not be reopened.");
            Console.WriteLine($"PASS DDS setting: {compressionCase.Mode}, 80x48, mipmaps=1.");
        }

        await ApplyDdsSettingsAsync(
            settingsService,
            DdsCompressionMode.Dxt5,
            generateMipmaps: true,
            preserveAlpha: false);
        var specialInput = Path.Combine(fixtureRoot, "settings_mipmaps.dds");
        File.Copy(ddsFixture.InputPath, specialInput);
        var logLengthBefore = GetLogLength(pathProvider.LogsDirectory);
        var specialResult = await AssertNoGuidTempLeakAsync(
            () => resizeService.ResizeAsync(CreatePercentageRequest(specialInput, 50)));
        Assert(specialResult.Success && specialResult.OutputPath is not null, "DDS mipmap resize failed.");
        var specialHeader = ReadDdsHeader(specialResult.OutputPath!);
        Assert(specialHeader.MipmapCount > 1, "Generate mipmaps was not applied.");
        var logDelta = ReadLogDelta(pathProvider.LogsDirectory, logLengthBefore);
        Assert(!logDelta.Contains("-sepalpha", StringComparison.Ordinal), "Preserve alpha=false still passed -sepalpha.");
        Assert(
            logDelta.Contains("DDS Mipmaps: True", StringComparison.Ordinal),
            "Generate mipmaps=true was not logged.");
        Assert(
            logDelta.Contains("DDS Preserve Alpha: False", StringComparison.Ordinal),
            "Preserve alpha=false was not logged.");

        await ApplyDdsSettingsAsync(
            settingsService,
            DdsCompressionMode.Dxt5,
            generateMipmaps: false,
            preserveAlpha: true);
        Console.WriteLine("PASS DDS options: mipmaps and alpha arguments verified.");
    }

    private static async Task VerifyDdsDimensionModesAsync(
        string fixtureRoot,
        IImageResizeService resizeService,
        IImageInfoService infoService,
        FormatFixture ddsFixture)
    {
        foreach (var mode in new[] { ResizeMode.Crop, ResizeMode.Stretch })
        {
            var inputPath = Path.Combine(
                fixtureRoot,
                $"mode_{mode.ToString().ToLowerInvariant()}.dds");
            File.Copy(ddsFixture.InputPath, inputPath);
            var result = await AssertNoGuidTempLeakAsync(
                () => resizeService.ResizeAsync(new ImageResizeRequest
                {
                    InputPath = inputPath,
                    ResizeMethod = ResizeMethod.Dimensions,
                    Width = 80,
                    Height = 80,
                    KeepAspectRatio = mode != ResizeMode.Stretch,
                    Mode = mode
                }));
            Assert(result.Success && result.OutputPath is not null, $"DDS {mode} resize failed.");
            var information = await infoService.GetInformationAsync(result.OutputPath!);
            Assert(
                information.Width == 80 && information.Height == 80,
                $"DDS {mode} output was {information.Width}x{information.Height}; expected 80x80.");
        }

        Console.WriteLine("PASS DDS modes: Fit=80x48, Crop=80x80, Stretch=80x80.");
    }

    private static async Task VerifyFailureCleanupAsync(
        IImageResizeService resizeService,
        IImageInfoService infoService,
        FormatFixture ddsFixture,
        FormatFixture pngFixture)
    {
        var failureInput = Path.Combine(
            Path.GetDirectoryName(ddsFixture.InputPath)!,
            "failure_cleanup.dds");
        File.Copy(ddsFixture.InputPath, failureInput);
        var original = FileSnapshot.Create(failureInput);
        var blockedOutputPath = Path.Combine(
            Path.GetDirectoryName(failureInput)!,
            "failure_cleanup_1.dds");
        Directory.CreateDirectory(blockedOutputPath);

        try
        {
            var batch = await AssertNoGuidTempLeakAsync(
                () => resizeService.ResizeBatchAsync(
                [
                    CreatePercentageRequest(failureInput, 25),
                    CreatePercentageRequest(pngFixture.InputPath, 25)
                ]));
            Assert(!batch.Results[0].Success, "The DDS failure-path probe unexpectedly succeeded.");
            Assert(batch.Results[1].Success, "The batch did not continue after a DDS failure.");
            Assert(!File.Exists(blockedOutputPath), "Failure-path probe left a DDS output file.");
            Assert(original == FileSnapshot.Create(failureInput), "Failure-path probe changed its source DDS.");
            var followingOutput = batch.Results[1].OutputPath
                ?? throw new InvalidOperationException("The post-failure PNG resize returned no output path.");
            var followingInfo = await infoService.GetInformationAsync(followingOutput);
            Assert(
                followingInfo.Width == 40 && followingInfo.Height == 24,
                "The post-failure PNG resize had incorrect dimensions.");
        }
        finally
        {
            Directory.Delete(blockedOutputPath);
        }

        Console.WriteLine("PASS DDS failure cleanup/batch continuation: failed DDS left no temp/output; following PNG succeeded.");
    }

    private static async Task VerifyRegressionsAsync(
        string fixtureRoot,
        SmokeApplicationPathProvider pathProvider,
        ISettingsService settingsService,
        IImageResizeService resizeService,
        IImageConversionService conversionService,
        IImagePreviewService previewService,
        IImageInfoService infoService,
        IDdsDependencyService dependencyService,
        IReadOnlyList<FormatFixture> fixtures)
    {
        var png = fixtures.Single(fixture => fixture.Name == "PNG").InputPath;
        var dds = fixtures.Single(fixture => fixture.Name == "DDS").InputPath;
        var conversionRoot = Path.Combine(fixtureRoot, "Conversions");
        Directory.CreateDirectory(conversionRoot);

        var standardConversion = await conversionService.ConvertAsync(new ImageConversionRequest
        {
            InputPath = png,
            OutputFormat = ImageFormat.Jpeg,
            OutputFolder = conversionRoot
        });
        Assert(standardConversion.Success && standardConversion.OutputPath is not null, "Standard conversion regressed.");
        var standardInfo = await infoService.GetInformationAsync(standardConversion.OutputPath!);
        Assert(standardInfo.Format == ImageFormat.Jpeg, "Standard conversion output could not be reopened.");

        var ddsConversion = await AssertNoGuidTempLeakAsync(
            () => conversionService.ConvertAsync(new ImageConversionRequest
            {
                InputPath = png,
                OutputFormat = ImageFormat.Dds,
                OutputFolder = conversionRoot
            }));
        Assert(ddsConversion.Success && ddsConversion.OutputPath is not null, "DDS encoding regression detected.");

        await VerifyDdsSourceConversionMatrixAsync(
            dds,
            conversionRoot,
            conversionService,
            infoService);
        await VerifyOverwritePoliciesAsync(
            fixtureRoot,
            settingsService,
            conversionService,
            resizeService,
            infoService);

        var preview = await AssertNoPixoarTempFileLeakAsync(
            () => previewService.LoadPreviewAsync(dds, 64));
        Assert(!preview.IsPlaceholder && preview.PngBytes is { Length: > 0 }, "DDS preview regression detected.");
        var previewBytes = preview.PngBytes
            ?? throw new InvalidOperationException("DDS preview returned no PNG bytes.");
        using (var previewImage = new MagickImage(previewBytes))
        {
            Assert(previewImage.Width <= 64 && previewImage.Height <= 64, "DDS preview size was invalid.");
        }

        var ddsInfo = await infoService.GetInformationAsync(dds);
        Assert(ddsInfo.Width == SourceWidth && ddsInfo.Height == SourceHeight, "DDS image information regression detected.");
        Assert(dependencyService.IsTexconvAvailable(), "Bundled texconv discovery regressed.");

        using var reloadedProvider = CreateProvider(pathProvider);
        var reloadedSettings = await reloadedProvider.GetRequiredService<ISettingsService>().LoadAsync();
        Assert(reloadedSettings.Dds.Compression == DdsCompressionMode.Dxt5, "DDS compression setting did not persist.");
        Assert(!reloadedSettings.Dds.GenerateMipmaps, "DDS mipmap setting did not persist.");
        Assert(reloadedSettings.Dds.PreserveAlpha, "DDS alpha setting did not persist.");

        Console.WriteLine("PASS regressions: standard/DDS conversion, DDS preview/info, settings, and texconv discovery.");
    }

    private static async Task RunOutputOrganizationAsync(string runRoot)
    {
        var appDataRoot = Path.Combine(runRoot, "AppData");
        var pathProvider = new SmokeApplicationPathProvider(appDataRoot);
        Directory.CreateDirectory(appDataRoot);
        await File.WriteAllTextAsync(
            pathProvider.SettingsFilePath,
            """
            {
              "schemaVersion": 1,
              "output": {
                "saveBesideOriginal": true,
                "preventOverwrite": false,
                "renameDuplicatesAutomatically": false
              }
            }
            """);

        using var provider = CreateProvider(pathProvider);
        var settingsService = provider.GetRequiredService<ISettingsService>();
        var conversionService = provider.GetRequiredService<IImageConversionService>();
        var resizeService = provider.GetRequiredService<IImageResizeService>();
        var settings = await settingsService.LoadAsync();

        Assert(
            settings.Output.ConflictBehavior == OutputConflictBehavior.RenameDuplicatesAutomatically,
            "Legacy output settings did not migrate to automatic duplicate renaming.");
        var migratedJson = await File.ReadAllTextAsync(pathProvider.SettingsFilePath);
        Assert(
            migratedJson.Contains("\"conflictBehavior\": \"renameDuplicatesAutomatically\"", StringComparison.Ordinal) &&
            !migratedJson.Contains("\"preventOverwrite\":", StringComparison.OrdinalIgnoreCase) &&
            !migratedJson.Contains("\"renameDuplicatesAutomatically\":", StringComparison.OrdinalIgnoreCase),
            "Legacy output conflict properties were not replaced in persisted settings.");

        await settingsService.UpdateAsync(current =>
        {
            current.Output.ConflictBehavior = OutputConflictBehavior.RenameDuplicatesAutomatically;
            current.Output.SaveConvertedFilesInConvertedFolder = true;
            current.Output.SaveResizedFilesInResizeFolder = true;
        });

        var sourceA = Path.Combine(runRoot, "Sources", "A");
        var sourceB = Path.Combine(runRoot, "Sources", "B");
        Directory.CreateDirectory(sourceA);
        Directory.CreateDirectory(sourceB);
        var logoA = Path.Combine(sourceA, "logo.png");
        var wallA = Path.Combine(sourceA, "wall.png");
        var logoB = Path.Combine(sourceB, "logo.png");
        WriteFixture(logoA, ImageFormat.Png);
        WriteFixture(wallA, ImageFormat.Png);
        WriteFixture(logoB, ImageFormat.Png);
        var originals = new[] { logoA, wallA, logoB }
            .ToDictionary(path => path, FileSnapshot.Create, StringComparer.OrdinalIgnoreCase);

        var singleConvert = await conversionService.ConvertAsync(new ImageConversionRequest
        {
            InputPath = logoA,
            OutputFormat = ImageFormat.Jpeg
        });
        var expectedSingleConvert = Path.Combine(sourceA, "Converted", "logo.jpg");
        Assert(
            singleConvert.Success && PathsEqual(singleConvert.OutputPath, expectedSingleConvert),
            "Single conversion did not preserve the basename in the Converted folder.");

        var batchConvert = await conversionService.ConvertBatchAsync(
        [
            new ImageConversionRequest { InputPath = wallA, OutputFormat = ImageFormat.Webp },
            new ImageConversionRequest { InputPath = logoB, OutputFormat = ImageFormat.Webp }
        ]);
        Assert(
            batchConvert.SuccessCount == 2 &&
            File.Exists(Path.Combine(sourceA, "Converted", "wall.webp")) &&
            File.Exists(Path.Combine(sourceB, "Converted", "logo.webp")),
            "Batch conversion did not preserve per-source Converted folder grouping.");

        var duplicateOne = await conversionService.ConvertAsync(new ImageConversionRequest
        {
            InputPath = logoA,
            OutputFormat = ImageFormat.Jpeg
        });
        var duplicateTwo = await conversionService.ConvertAsync(new ImageConversionRequest
        {
            InputPath = logoA,
            OutputFormat = ImageFormat.Jpeg
        });
        Assert(
            PathsEqual(duplicateOne.OutputPath, Path.Combine(sourceA, "Converted", "logo_1.jpg")) &&
            PathsEqual(duplicateTwo.OutputPath, Path.Combine(sourceA, "Converted", "logo_2.jpg")),
            "Conversion duplicate numbering did not use _1 and _2.");

        var singleResize = await resizeService.ResizeAsync(CreatePercentageRequest(logoA, 50));
        var expectedSingleResize = Path.Combine(sourceA, "Resize", "logo.png");
        Assert(
            singleResize.Success && PathsEqual(singleResize.OutputPath, expectedSingleResize),
            "Single resize did not preserve the filename in the Resize folder.");

        var batchResize = await resizeService.ResizeBatchAsync(
        [
            CreatePercentageRequest(wallA, 50),
            CreatePercentageRequest(logoB, 50)
        ]);
        Assert(
            batchResize.SuccessCount == 2 &&
            File.Exists(Path.Combine(sourceA, "Resize", "wall.png")) &&
            File.Exists(Path.Combine(sourceB, "Resize", "logo.png")),
            "Batch resize did not preserve per-source Resize folder grouping.");

        var resizeDuplicateOne = await resizeService.ResizeAsync(CreatePercentageRequest(logoA, 50));
        var resizeDuplicateTwo = await resizeService.ResizeAsync(CreatePercentageRequest(logoA, 50));
        Assert(
            PathsEqual(resizeDuplicateOne.OutputPath, Path.Combine(sourceA, "Resize", "logo_1.png")) &&
            PathsEqual(resizeDuplicateTwo.OutputPath, Path.Combine(sourceA, "Resize", "logo_2.png")),
            "Resize duplicate numbering did not use _1 and _2.");

        foreach (var original in originals)
        {
            Assert(original.Value == FileSnapshot.Create(original.Key), $"Original input was modified: {original.Key}");
        }
    }

    private static bool PathsEqual(string? left, string right)
    {
        return left is not null && string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task VerifyDdsSourceConversionMatrixAsync(
        string ddsInputPath,
        string outputRoot,
        IImageConversionService conversionService,
        IImageInfoService infoService)
    {
        var formats = new[]
        {
            ImageFormat.Png,
            ImageFormat.Jpeg,
            ImageFormat.Webp,
            ImageFormat.Bmp,
            ImageFormat.Tiff,
            ImageFormat.Dds
        };

        foreach (var outputFormat in formats)
        {
            var result = await AssertNoStagedOutputLeakAsync(
                outputRoot,
                () => AssertNoGuidTempLeakAsync(
                    () => conversionService.ConvertAsync(new ImageConversionRequest
                    {
                        InputPath = ddsInputPath,
                        OutputFormat = outputFormat,
                        OutputFolder = outputRoot
                    })));
            Assert(
                result.Success && result.OutputPath is not null,
                $"DDS to {outputFormat} conversion failed: {result.Error?.Message ?? "unknown error"}");

            var information = await infoService.GetInformationAsync(result.OutputPath!);
            Assert(
                information.Format == outputFormat,
                $"DDS to {outputFormat} output was detected as {information.Format}.");
            Assert(
                information.Width == SourceWidth && information.Height == SourceHeight,
                $"DDS to {outputFormat} output was {information.Width}x{information.Height}; expected {SourceWidth}x{SourceHeight}.");

            if (outputFormat != ImageFormat.Dds)
            {
                using var reopened = new MagickImage(result.OutputPath!);
                Assert(
                    IsExpectedMagickFormat(reopened.Format, outputFormat),
                    $"DDS to {outputFormat} content decoded as {reopened.Format}.");
            }
        }

        Console.WriteLine("PASS DDS source conversion: PNG/JPEG/WEBP/BMP/TIFF/DDS formats and dimensions verified.");
    }

    private static async Task VerifyOverwritePoliciesAsync(
        string fixtureRoot,
        ISettingsService settingsService,
        IImageConversionService conversionService,
        IImageResizeService resizeService,
        IImageInfoService infoService)
    {
        var outputRoot = Path.Combine(fixtureRoot, "OverwritePolicies");
        Directory.CreateDirectory(outputRoot);

        var conversionInput = Path.Combine(outputRoot, "conversion_input.png");
        WriteFixture(conversionInput, ImageFormat.Png);
        var conversionOutput = Path.Combine(outputRoot, "conversion_input.jpg");
        var protectedBytes = Encoding.UTF8.GetBytes("protected conversion output");
        await File.WriteAllBytesAsync(conversionOutput, protectedBytes);

        await SetOutputPolicyAsync(settingsService, OutputConflictBehavior.SkipExistingFiles);
        var protectedConversion = await AssertNoStagedOutputLeakAsync(
            outputRoot,
            () => conversionService.ConvertAsync(new ImageConversionRequest
            {
                InputPath = conversionInput,
                OutputFormat = ImageFormat.Jpeg,
                OutputFolder = outputRoot
            }));
        Assert(protectedConversion.Skipped, "Skip-existing conversion did not skip an existing output.");
        Assert(
            File.ReadAllBytes(conversionOutput).SequenceEqual(protectedBytes),
            "Protected conversion changed the existing output bytes.");

        await SetOutputPolicyAsync(settingsService, OutputConflictBehavior.OverwriteExistingFiles);
        var overwrittenConversion = await AssertNoStagedOutputLeakAsync(
            outputRoot,
            () => conversionService.ConvertAsync(new ImageConversionRequest
            {
                InputPath = conversionInput,
                OutputFormat = ImageFormat.Jpeg,
                OutputFolder = outputRoot
            }));
        Assert(
            overwrittenConversion.Success &&
            string.Equals(overwrittenConversion.OutputPath, conversionOutput, StringComparison.OrdinalIgnoreCase),
            "Overwrite-enabled conversion did not replace the expected output path.");
        var overwrittenInfo = await infoService.GetInformationAsync(conversionOutput);
        Assert(
            overwrittenInfo.Format == ImageFormat.Jpeg &&
            overwrittenInfo.Width == SourceWidth &&
            overwrittenInfo.Height == SourceHeight,
            "Overwrite-enabled conversion did not produce a valid JPEG.");

        await SetOutputPolicyAsync(settingsService, OutputConflictBehavior.RenameDuplicatesAutomatically);
        var renamedConversion = await AssertNoStagedOutputLeakAsync(
            outputRoot,
            () => conversionService.ConvertAsync(new ImageConversionRequest
            {
                InputPath = conversionInput,
                OutputFormat = ImageFormat.Jpeg,
                OutputFolder = outputRoot
            }));
        var expectedRenamedPath = Path.Combine(outputRoot, "conversion_input_1.jpg");
        Assert(
            renamedConversion.Success &&
            string.Equals(renamedConversion.OutputPath, expectedRenamedPath, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(expectedRenamedPath),
            "Rename-enabled conversion did not select the _1 output path.");

        var resizeInput = Path.Combine(outputRoot, "resize_input.png");
        WriteFixture(resizeInput, ImageFormat.Png);
        var resizeOutputRoot = Path.Combine(outputRoot, "ResizeOutput");
        Directory.CreateDirectory(resizeOutputRoot);
        var resizeOutput = Path.Combine(resizeOutputRoot, "resize_input.png");
        var protectedResizeBytes = Encoding.UTF8.GetBytes("protected resize output");
        await File.WriteAllBytesAsync(resizeOutput, protectedResizeBytes);

        await SetOutputPolicyAsync(settingsService, OutputConflictBehavior.SkipExistingFiles);
        var protectedResizeRequest = CreatePercentageRequest(resizeInput, 50);
        protectedResizeRequest.OutputFolder = resizeOutputRoot;
        var protectedResize = await AssertNoStagedOutputLeakAsync(
            outputRoot,
            () => resizeService.ResizeAsync(protectedResizeRequest));
        Assert(protectedResize.Skipped, "Skip-existing resize did not skip an existing output.");
        Assert(
            File.ReadAllBytes(resizeOutput).SequenceEqual(protectedResizeBytes),
            "Protected resize changed the existing output bytes.");

        await SetOutputPolicyAsync(settingsService, OutputConflictBehavior.OverwriteExistingFiles);
        var overwrittenResizeRequest = CreatePercentageRequest(resizeInput, 50);
        overwrittenResizeRequest.OutputFolder = resizeOutputRoot;
        var overwrittenResize = await AssertNoStagedOutputLeakAsync(
            outputRoot,
            () => resizeService.ResizeAsync(overwrittenResizeRequest));
        Assert(
            overwrittenResize.Success &&
            string.Equals(overwrittenResize.OutputPath, resizeOutput, StringComparison.OrdinalIgnoreCase),
            "Overwrite-enabled resize did not replace the expected output path.");
        var resizedInfo = await infoService.GetInformationAsync(resizeOutput);
        Assert(
            resizedInfo.Format == ImageFormat.Png &&
            resizedInfo.Width == 80 &&
            resizedInfo.Height == 48,
            "Overwrite-enabled resize did not produce a valid 80x48 PNG.");

        Assert(
            GetStagedOutputFiles(outputRoot).Count == 0,
            "Overwrite policy tests left one or more staged output files.");
        await SetOutputPolicyAsync(settingsService, OutputConflictBehavior.RenameDuplicatesAutomatically);
        Console.WriteLine("PASS output conflict policy: skip, overwrite, rename, conversion/resize, and staged cleanup verified.");
    }

    private static Task SetOutputPolicyAsync(
        ISettingsService settingsService,
        OutputConflictBehavior behavior)
    {
        return settingsService.UpdateAsync(settings =>
        {
            settings.Output.SaveBesideOriginal = true;
            settings.Output.ConflictBehavior = behavior;
        });
    }

    private static async Task VerifySettingsRegressionsAsync(string fixtureRoot)
    {
        var settingsRoot = Path.Combine(fixtureRoot, "SettingsRegressions");
        Directory.CreateDirectory(settingsRoot);

        var missingPropertiesPathProvider = new SmokeApplicationPathProvider(
            Path.Combine(settingsRoot, "MissingProperties"));
        Directory.CreateDirectory(missingPropertiesPathProvider.AppDataDirectory);
        await File.WriteAllTextAsync(
            missingPropertiesPathProvider.SettingsFilePath,
            """{ "schemaVersion": 1 }""");
        using (var provider = CreateProvider(missingPropertiesPathProvider))
        {
            var settings = await provider.GetRequiredService<ISettingsService>().LoadAsync();
            Assert(
                settings.ResizePresets.Select(preset => preset.Percentage).SequenceEqual(
                    new int?[] { 50, 75 }),
                "A legacy settings file with no resizePresets property lost the default presets.");
            Assert(
                settings.ConvertPresets.Select(preset => preset.Format).SequenceEqual(
                    new[] { "png", "jpg", "webp", "dds" },
                    StringComparer.Ordinal),
                "A legacy settings file with no convertPresets property lost the default presets.");
        }

        var persistencePathProvider = new SmokeApplicationPathProvider(
            Path.Combine(settingsRoot, "Persistence"));
        using (var provider = CreateProvider(persistencePathProvider))
        {
            var service = provider.GetRequiredService<ISettingsService>();
            await service.LoadAsync();
            await service.UpdateAsync(settings =>
            {
                settings.ResizePresets =
                [
                    new ResizePreset { Name = "75%", Percentage = 75, IsEnabled = false },
                    new ResizePreset { Name = "25%", Percentage = 25 }
                ];
                settings.ConvertPresets =
                [
                    new ConvertPreset { Name = "WEBP", Format = "webp", IsEnabled = false },
                    new ConvertPreset { Name = "PNG", Format = "png" }
                ];
            });
        }

        using (var provider = CreateProvider(persistencePathProvider))
        {
            var service = provider.GetRequiredService<ISettingsService>();
            var settings = await service.LoadAsync();
            Assert(
                settings.ResizePresets.Select(preset => preset.Percentage).SequenceEqual(
                    new int?[] { 75, 25 }),
                "Resize preset order/removal did not persist.");
            Assert(
                !settings.ResizePresets[0].IsEnabled,
                "Resize preset enabled state did not persist.");
            Assert(
                settings.ConvertPresets.Select(preset => preset.Format).SequenceEqual(
                    new[] { "webp", "png" },
                    StringComparer.Ordinal),
                "Convert preset order/removal did not persist.");
            Assert(
                !settings.ConvertPresets[0].IsEnabled,
                "Convert preset enabled state did not persist.");

            await service.UpdateAsync(current =>
            {
                current.ResizePresets.Clear();
                current.ConvertPresets.Clear();
            });
        }

        using (var provider = CreateProvider(persistencePathProvider))
        {
            var settings = await provider.GetRequiredService<ISettingsService>().LoadAsync();
            Assert(
                settings.ResizePresets.Count == 0 && settings.ConvertPresets.Count == 0,
                "Explicitly empty preset lists were repopulated after reload.");
        }

        var legacyPathProvider = new SmokeApplicationPathProvider(
            Path.Combine(settingsRoot, "Legacy"));
        Directory.CreateDirectory(legacyPathProvider.AppDataDirectory);
        await File.WriteAllTextAsync(
            legacyPathProvider.SettingsFilePath,
            """
            {
              "schemaVersion": 1,
              "resizePresets": [
                null,
                {
                  "name": "Legacy 75",
                  "percentage": 75,
                  "width": 1200,
                  "height": 800,
                  "mode": "crop",
                  "isEnabled": false
                },
                {
                  "name": "Duplicate 75",
                  "percentage": 75,
                  "width": 640,
                  "height": 480,
                  "mode": "stretch",
                  "isEnabled": true
                },
                {
                  "name": "25%",
                  "percentage": null,
                  "width": 320,
                  "height": 200,
                  "mode": "fit",
                  "isEnabled": true
                },
                {
                  "name": "Invalid",
                  "percentage": 0,
                  "isEnabled": true
                }
              ],
              "convertPresets": [
                null,
                {
                  "name": "Portable Network Graphics",
                  "format": "png",
                  "isEnabled": false
                },
                {
                  "name": "Duplicate PNG",
                  "format": ".PNG",
                  "isEnabled": true
                },
                {
                  "name": "Unsupported GIF",
                  "format": "gif",
                  "isEnabled": true
                },
                {
                  "name": "Legacy TIF",
                  "format": "tif",
                  "isEnabled": true
                }
              ]
            }
            """);

        using (var provider = CreateProvider(legacyPathProvider))
        {
            var settings = await provider.GetRequiredService<ISettingsService>().LoadAsync();
            Assert(
                settings.ResizePresets.Select(preset => preset.Percentage).SequenceEqual(
                    new int?[] { 75, 25 }),
                "Legacy resize presets were not safely filtered, deduplicated, and ordered.");
            Assert(
                !settings.ResizePresets[0].IsEnabled,
                "Legacy resize duplicate normalization did not keep the first entry.");
            Assert(
                settings.ConvertPresets.Select(preset => preset.Format).SequenceEqual(
                    new[] { "png", "tiff" },
                    StringComparer.Ordinal),
                "Legacy convert presets were not safely filtered, aliased, and deduplicated.");
            Assert(
                !settings.ConvertPresets[0].IsEnabled,
                "Legacy convert duplicate normalization did not keep the first entry.");
        }

        var rewrittenLegacyJson = await File.ReadAllTextAsync(legacyPathProvider.SettingsFilePath);
        foreach (var obsoleteProperty in new[] { "\"width\"", "\"height\"", "\"mode\"" })
        {
            Assert(
                !rewrittenLegacyJson.Contains(obsoleteProperty, StringComparison.OrdinalIgnoreCase),
                $"Obsolete resize property was rewritten: {obsoleteProperty}");
        }

        var corruptPathProvider = new SmokeApplicationPathProvider(
            Path.Combine(settingsRoot, "Corrupt"));
        Directory.CreateDirectory(corruptPathProvider.AppDataDirectory);
        const string malformedJson = "{ this is not valid JSON";
        await File.WriteAllTextAsync(corruptPathProvider.SettingsFilePath, malformedJson);

        using (var provider = CreateProvider(corruptPathProvider))
        {
            var recovered = await provider.GetRequiredService<ISettingsService>().LoadAsync();
            Assert(
                recovered.ResizePresets.Select(preset => preset.Percentage).SequenceEqual(
                    new int?[] { 50, 75 }),
                "Corrupt settings recovery did not restore default resize presets.");
            Assert(
                recovered.ConvertPresets.Select(preset => preset.Format).SequenceEqual(
                    new[] { "png", "jpg", "webp", "dds" },
                    StringComparer.Ordinal),
                "Corrupt settings recovery did not restore default convert presets.");
        }

        var brokenPath = Path.Combine(corruptPathProvider.AppDataDirectory, "settings.broken.json");
        Assert(File.Exists(brokenPath), "Malformed settings were not backed up.");
        Assert(
            string.Equals(await File.ReadAllTextAsync(brokenPath), malformedJson, StringComparison.Ordinal),
            "Malformed settings backup did not preserve the original content.");
        using (JsonDocument.Parse(await File.ReadAllTextAsync(corruptPathProvider.SettingsFilePath)))
        {
        }

        Assert(
            !Directory.EnumerateFiles(settingsRoot, "*.tmp", SearchOption.AllDirectories).Any(),
            "Settings regression tests left a temporary settings file.");
        Console.WriteLine("PASS settings: missing properties, order/removal/empty, legacy cleanup, and corrupt-file recovery verified.");
    }

    private static async Task ApplyDdsSettingsAsync(
        ISettingsService settingsService,
        DdsCompressionMode compression,
        bool generateMipmaps,
        bool preserveAlpha)
    {
        await settingsService.UpdateAsync(settings =>
        {
            settings.Output.SaveBesideOriginal = true;
            settings.Output.ConflictBehavior = OutputConflictBehavior.RenameDuplicatesAutomatically;
            settings.Dds.Compression = compression;
            settings.Dds.GenerateMipmaps = generateMipmaps;
            settings.Dds.PreserveAlpha = preserveAlpha;
        });
    }

    private static ImageResizeRequest CreatePercentageRequest(string inputPath, int percentage)
    {
        return new ImageResizeRequest
        {
            InputPath = inputPath,
            ResizeMethod = ResizeMethod.Percentage,
            Percentage = percentage,
            KeepAspectRatio = true,
            Mode = ResizeMode.Fit
        };
    }

    private static bool CompressionMatches(DdsHeader header, DdsCompressionCase compressionCase)
    {
        return string.Equals(header.FourCc, compressionCase.ExpectedFourCc, StringComparison.Ordinal) &&
            header.DxgiFormat == compressionCase.ExpectedDxgiFormat;
    }

    private static DdsHeader ReadDdsHeader(string path)
    {
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII);
        Assert(reader.ReadUInt32() == 0x20534444, $"Invalid DDS magic: {path}");
        Assert(reader.ReadUInt32() == 124, $"Invalid DDS header size: {path}");
        stream.Position = 28;
        var mipmapCount = reader.ReadUInt32();
        stream.Position = 84;
        var fourCcBytes = reader.ReadBytes(4);
        var fourCc = Encoding.ASCII.GetString(fourCcBytes);
        uint? dxgiFormat = null;
        if (fourCc == "DX10")
        {
            stream.Position = 128;
            dxgiFormat = reader.ReadUInt32();
        }

        return new DdsHeader(fourCc, dxgiFormat, mipmapCount == 0 ? 1 : mipmapCount);
    }

    private static async Task<T> AssertNoGuidTempLeakAsync<T>(Func<Task<T>> action)
    {
        var before = GetGuidTempDirectories();
        try
        {
            return await action();
        }
        finally
        {
            AssertSetsEqual(
                before,
                GetGuidTempDirectories(),
                "Pixoar GUID temporary directories changed after the operation.");
        }
    }

    private static async Task<T> AssertNoStagedOutputLeakAsync<T>(
        string searchRoot,
        Func<Task<T>> action)
    {
        var before = GetStagedOutputFiles(searchRoot);
        try
        {
            return await action();
        }
        finally
        {
            AssertSetsEqual(
                before,
                GetStagedOutputFiles(searchRoot),
                $"Staged output files changed after the operation in {searchRoot}.");
        }
    }

    private static HashSet<string> GetStagedOutputFiles(string searchRoot)
    {
        if (!Directory.Exists(searchRoot))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return Directory.EnumerateFiles(searchRoot, "*", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path).Contains(".pixoar-", StringComparison.OrdinalIgnoreCase))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsExpectedMagickFormat(MagickFormat actual, ImageFormat expected)
    {
        var name = actual.ToString();
        return expected switch
        {
            ImageFormat.Png => name.StartsWith("Png", StringComparison.OrdinalIgnoreCase),
            ImageFormat.Jpeg => name.StartsWith("Jpeg", StringComparison.OrdinalIgnoreCase),
            ImageFormat.Webp => name.StartsWith("WebP", StringComparison.OrdinalIgnoreCase),
            ImageFormat.Bmp => name.StartsWith("Bmp", StringComparison.OrdinalIgnoreCase),
            ImageFormat.Tiff => name.StartsWith("Tiff", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static async Task AssertNoGuidTempLeakAsync(Func<Task> action)
    {
        var before = GetGuidTempDirectories();
        try
        {
            await action();
        }
        finally
        {
            AssertSetsEqual(
                before,
                GetGuidTempDirectories(),
                "Pixoar GUID temporary directories changed after the operation.");
        }
    }

    private static HashSet<string> GetGuidTempDirectories()
    {
        var pixoarTemp = Path.Combine(Path.GetTempPath(), "Pixoar");
        if (!Directory.Exists(pixoarTemp))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return GuidTempSearchPatterns
            .SelectMany(pattern => Directory.EnumerateDirectories(pixoarTemp, pattern, SearchOption.TopDirectoryOnly))
            .Where(path => Guid.TryParseExact(Path.GetFileName(path), "N", out _))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<T> AssertNoPixoarTempFileLeakAsync<T>(Func<Task<T>> action)
    {
        var before = GetPixoarTempFiles();
        try
        {
            return await action();
        }
        finally
        {
            AssertSetsEqual(
                before,
                GetPixoarTempFiles(),
                "Pixoar temporary files changed after the operation.");
        }
    }

    private static HashSet<string> GetPixoarTempFiles()
    {
        var pixoarTemp = Path.Combine(Path.GetTempPath(), "Pixoar");
        return Directory.Exists(pixoarTemp)
            ? Directory.EnumerateFiles(pixoarTemp, "*", SearchOption.AllDirectories)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertSetsEqual(
        HashSet<string> expected,
        HashSet<string> actual,
        string message)
    {
        Assert(expected.SetEquals(actual), message);
    }

    private static long GetLogLength(string logsDirectory)
    {
        return Directory.Exists(logsDirectory)
            ? Directory.EnumerateFiles(logsDirectory, "*.log").Sum(path => new FileInfo(path).Length)
            : 0;
    }

    private static string ReadLogDelta(string logsDirectory, long previousLength)
    {
        var text = string.Concat(
            Directory.EnumerateFiles(logsDirectory, "*.log")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
        Assert(text.Length >= previousLength, "Log length unexpectedly decreased.");
        return text[(int)previousLength..];
    }

    private static void VerifyRequiredLogging(string logsDirectory)
    {
        var log = string.Concat(
            Directory.EnumerateFiles(logsDirectory, "*.log")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .Select(File.ReadAllText));
        var requiredFragments = new[]
        {
            "input path:",
            "Original format:",
            "Original dimensions:",
            "requested resize:",
            "Mode:",
            "calculated output dimensions:",
            "temporary decoded DDS intermediate path:",
            "temporary resized lossless intermediate path:",
            "DDS settings:",
            "Using texconv.exe:",
            "Generated texconv arguments:",
            "texconv stdout:",
            "texconv stderr:",
            "texconv exit code:",
            "Final output path:"
        };

        foreach (var fragment in requiredFragments)
        {
            Assert(
                log.Contains(fragment, StringComparison.Ordinal),
                $"Required resize log field was missing: {fragment}");
        }

        Console.WriteLine("PASS logging: resize plan, intermediates, DDS settings, texconv diagnostics, and final output.");
    }

    private static void DeleteDirectoryWithRetries(string path)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (Exception exception) when (
                attempt < 5 &&
                exception is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(50 * attempt);
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var starts = new[] { Environment.CurrentDirectory, AppContext.BaseDirectory };
        foreach (var start in starts)
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Pixoar.sln")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the Pixoar repository root.");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SmokeApplicationPathProvider(string appDataDirectory) : IApplicationPathProvider
    {
        public string AppDataDirectory { get; } = appDataDirectory;

        public string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");

        public string LogsDirectory => Path.Combine(AppDataDirectory, "Logs");
    }

    private sealed class CollectingProgress : IProgress<ImageOperationProgress>
    {
        public List<ImageOperationProgress> Events { get; } = [];

        public void Report(ImageOperationProgress value)
        {
            Events.Add(value);
        }
    }

    private sealed record FormatFixture(
        string Name,
        ImageFormat Format,
        string InputExtension,
        string PrimaryExtension)
    {
        public string InputPath { get; set; } = string.Empty;
    }

    private readonly record struct DdsCompressionCase(
        DdsCompressionMode Mode,
        string? ExpectedFourCc,
        uint? ExpectedDxgiFormat);

    private readonly record struct DdsHeader(
        string FourCc,
        uint? DxgiFormat,
        uint MipmapCount);

    private readonly record struct FileSnapshot(
        string Sha256,
        long Length,
        DateTime LastWriteTimeUtc)
    {
        public static FileSnapshot Create(string path)
        {
            using var stream = File.OpenRead(path);
            return new FileSnapshot(
                Convert.ToHexString(SHA256.HashData(stream)),
                stream.Length,
                File.GetLastWriteTimeUtc(path));
        }

        public static string Hash(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
