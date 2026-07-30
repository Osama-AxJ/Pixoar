using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ImageMagick;
using Microsoft.Extensions.DependencyInjection;
using Pixoar.Core.Configuration;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

var keepWorkspace = args.Contains("--keep", StringComparer.OrdinalIgnoreCase);
var runRoot = Path.Combine(
    Path.GetTempPath(),
    "PixoarColorRegression",
    Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(runRoot);
Console.WriteLine($"Color regression workspace: {runRoot}");

try
{
    await RunAsync(runRoot);
    Console.WriteLine("PASS: color-management regression suite completed.");
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
        Console.WriteLine($"Kept color regression workspace: {runRoot}");
    }
    else
    {
        DeleteDirectoryWithRetries(runRoot);
    }
}

static async Task RunAsync(string runRoot)
{
    var services = new ServiceCollection();
    services.AddPixoarCore();
    services.AddSingleton<IApplicationPathProvider>(new TestApplicationPathProvider(
        Path.Combine(runRoot, "AppData")));
    using var provider = services.BuildServiceProvider();

    var settings = provider.GetRequiredService<ISettingsService>();
    await settings.LoadAsync();
    await settings.UpdateAsync(current =>
    {
        current.Quality.JpegQuality = 100;
        current.Quality.WebpQuality = 100;
        current.Quality.PngCompressionLevel = 6;
        current.Dds.Compression = DdsCompressionMode.Uncompressed;
        current.Dds.GenerateMipmaps = false;
        current.Dds.PreserveAlpha = true;
        current.Output.PreventOverwrite = true;
        current.Output.RenameDuplicatesAutomatically = true;
    });

    var conversion = provider.GetRequiredService<IImageConversionService>();
    var resize = provider.GetRequiredService<IImageResizeService>();
    var dds = provider.GetRequiredService<IDdsService>();
    var ddsDependency = provider.GetRequiredService<IDdsDependencyService>();
    var preview = provider.GetRequiredService<IImagePreviewService>();
    var fixturesRoot = Path.Combine(runRoot, "Fixtures");
    Directory.CreateDirectory(fixturesRoot);

    var srgbPath = CreateSrgbFixture(Path.Combine(fixturesRoot, "srgb.png"));
    var adobeRgbPath = CreateAdobeRgbFixture(Path.Combine(fixturesRoot, "adobe-rgb.png"));
    var linearPath = CreateLinearFixture(Path.Combine(fixturesRoot, "linear.png"));

    await VerifyProfileAndTransferFixturesAsync(
        runRoot,
        [srgbPath, adobeRgbPath, linearPath],
        conversion,
        dds);
    await VerifyPreviewColorAsync(adobeRgbPath, preview);
    await VerifyResizeColorAsync(runRoot, [adobeRgbPath, linearPath], resize, dds);
    await VerifySupportedFormatMatrixAsync(runRoot, conversion, dds);
    await VerifyDdsPipelineAsync(
        runRoot,
        settings,
        dds,
        ddsDependency.ResolveTexconvPath()
            ?? throw new InvalidOperationException(ddsDependency.MissingTexconvMessage));
    await VerifyDefaultQualityBiasAsync(runRoot, settings, conversion);
}

static async Task VerifyProfileAndTransferFixturesAsync(
    string runRoot,
    IReadOnlyList<string> fixturePaths,
    IImageConversionService conversion,
    IDdsService dds)
{
    foreach (var fixturePath in fixturePaths)
    {
        var source = Inspect(fixturePath);
        Console.WriteLine(
            $"SOURCE {Path.GetFileName(fixturePath),-14} " +
            $"space={source.ColorSpace}, gamma={source.Gamma:F5}, " +
            $"profile={source.Profile ?? "<native/none>"}, sRGB={source.FirstPixel}");

        foreach (var format in TestFormats.Supported)
        {
            var outputPath = await ConvertAsync(
                conversion,
                fixturePath,
                format,
                Path.Combine(
                    runRoot,
                    "ProfileTransfer",
                    Path.GetFileNameWithoutExtension(fixturePath),
                    format.ToString()));
            var output = await InspectOutputAsync(outputPath, format, dds, runRoot);
            var metrics = Compare(source, output.Sample);
            var allowedMae = format == ImageFormat.Jpeg ? 1.0 : 0.01;

            Assert(
                metrics.MeanAbsoluteError <= allowedMae &&
                metrics.MaximumChannelDelta <= (format == ImageFormat.Jpeg ? 2 : 0),
                $"{Path.GetFileName(fixturePath)} to {format} changed sRGB pixels: {metrics}.");
            AssertCanonicalSrgb(format, output);
            if (Path.GetFileName(fixturePath) == "adobe-rgb.png" &&
                format is ImageFormat.Png or ImageFormat.Jpeg or ImageFormat.Webp)
            {
                Assert(
                    output.Sample.ExifColorSpace == 1,
                    $"{format} EXIF color space was {output.Sample.ExifColorSpace?.ToString() ?? "<missing>"}, not sRGB.");
                Assert(
                    string.Equals(
                        output.Sample.ImageDescription,
                        "Pixoar color metadata",
                        StringComparison.Ordinal),
                    $"{format} lost compatible EXIF metadata.");
            }

            Console.WriteLine(
                $"  -> {format,-5} {metrics}, profile={output.Sample.Profile ?? "<native/none>"}" +
                (output.DxgiFormat is null ? string.Empty : $", DXGI={output.DxgiFormat}"));
        }
    }

    Console.WriteLine(
        "PASS profiles/transfers: embedded Adobe RGB and gamma-1.0 input normalize to the same sRGB pixels.");
}

static async Task VerifyPreviewColorAsync(
    string adobeRgbPath,
    IImagePreviewService previewService)
{
    var source = Inspect(adobeRgbPath);
    var result = await previewService.LoadPreviewAsync(adobeRgbPath, 32);
    Assert(!result.IsPlaceholder && result.PngBytes is { Length: > 0 }, "Adobe RGB preview failed.");

    using var previewImage = new MagickImage(result.PngBytes!);
    var preview = InspectImage(previewImage);
    var metrics = Compare(source, preview);
    Assert(metrics.MaximumChannelDelta == 0, $"Preview changed the Adobe RGB color: {metrics}.");
    Assert(
        preview.ColorSpace == ColorSpace.sRGB && IsSrgbGamma(preview.Gamma),
        "Preview PNG was not tagged as sRGB.");
    Console.WriteLine("PASS preview: the app preview is canonical sRGB with unchanged visible pixels.");
}

static async Task VerifyResizeColorAsync(
    string runRoot,
    IReadOnlyList<string> fixturePaths,
    IImageResizeService resizeService,
    IDdsService dds)
{
    foreach (var fixturePath in fixturePaths)
    {
        var source = Inspect(fixturePath);
        foreach (var outputFormat in new[] { ImageFormat.Png, ImageFormat.Dds })
        {
            var outputRoot = Path.Combine(
                runRoot,
                "ResizeColor",
                Path.GetFileNameWithoutExtension(fixturePath),
                outputFormat.ToString());
            Directory.CreateDirectory(outputRoot);
            var result = await resizeService.ResizeAsync(new ImageResizeRequest
            {
                InputPath = fixturePath,
                ResizeMethod = ResizeMethod.Percentage,
                Percentage = 50,
                OutputFormat = outputFormat,
                OutputFolder = outputRoot
            });
            Assert(
                result.Success && result.OutputPath is not null,
                $"{Path.GetFileName(fixturePath)} resize to {outputFormat} failed: {result.Error?.Message}");

            var output = await InspectOutputAsync(result.OutputPath!, outputFormat, dds, runRoot);
            Assert(
                output.Sample.Width == 16 && output.Sample.Height == 16,
                $"Color resize produced {output.Sample.Width}x{output.Sample.Height}, not 16x16.");
            Assert(
                MaximumDeltaFromColor(source.Pixels, output.Sample.Pixels) == 0,
                $"{Path.GetFileName(fixturePath)} resize to {outputFormat} changed the uniform color.");
            AssertCanonicalSrgb(outputFormat, output);
        }
    }

    Console.WriteLine("PASS resize: ICC and linear inputs retain their visible color through PNG and DDS resizing.");
}

static async Task VerifySupportedFormatMatrixAsync(
    string runRoot,
    IImageConversionService conversion,
    IDdsService dds)
{
    var matrixRoot = Path.Combine(runRoot, "FormatMatrix");
    Directory.CreateDirectory(matrixRoot);
    var basePng = CreateColorChart(Path.Combine(matrixRoot, "matrix-source.png"));
    var inputs = new Dictionary<ImageFormat, string>
    {
        [ImageFormat.Png] = basePng
    };

    foreach (var format in TestFormats.Supported.Where(format => format != ImageFormat.Png))
    {
        inputs[format] = await ConvertAsync(
            conversion,
            basePng,
            format,
            Path.Combine(matrixRoot, "Inputs", format.ToString()));
    }

    foreach (var input in inputs)
    {
        var reference = (await InspectOutputAsync(
            input.Value,
            input.Key,
            dds,
            runRoot)).Sample;
        var row = new List<string>();

        foreach (var outputFormat in TestFormats.Supported)
        {
            var outputPath = await ConvertAsync(
                conversion,
                input.Value,
                outputFormat,
                Path.Combine(
                    matrixRoot,
                    "Outputs",
                    input.Key.ToString(),
                    outputFormat.ToString()));
            var output = await InspectOutputAsync(outputPath, outputFormat, dds, runRoot);
            var metrics = Compare(reference, output.Sample);

            var allowedMae = outputFormat switch
            {
                ImageFormat.Jpeg => 2.5,
                ImageFormat.Webp => 1.5,
                _ => 0.05
            };
            var allowedBias = outputFormat == ImageFormat.Jpeg ? 1.0 : 0.35;

            Assert(
                metrics.MeanAbsoluteError <= allowedMae,
                $"{input.Key} to {outputFormat} exceeded the pixel error limit: {metrics}.");
            Assert(
                metrics.MaximumMeanChannelBias <= allowedBias,
                $"{input.Key} to {outputFormat} introduced a global color shift: {metrics}.");
            AssertCanonicalSrgb(outputFormat, output);
            row.Add($"{outputFormat}:{metrics.MeanAbsoluteError:F2}/{metrics.MaximumMeanChannelBias:F2}");
        }

        Console.WriteLine($"MATRIX {input.Key,-5} -> {string.Join("  ", row)}");
    }

    Console.WriteLine(
        "PASS 6x6 matrix: PNG/JPEG/WEBP/BMP/TIFF/DDS conversions stayed within lossless/lossy pixel limits.");
}

static async Task VerifyDdsPipelineAsync(
    string runRoot,
    ISettingsService settings,
    IDdsService dds,
    string texconvPath)
{
    const uint ddsDepthFlag = 0x00800000;
    const uint cubeMapMask = 0x0000FE00;
    const uint volumeFlag = 0x00200000;
    const uint textureCubeFlag = 0x00000004;

    var pipelineRoot = Path.Combine(runRoot, "DdsPipeline");
    Directory.CreateDirectory(pipelineRoot);
    var sourcePath = CreateColorChart(Path.Combine(pipelineRoot, "dds-source.png"));
    var source = Inspect(sourcePath);
    var outputRoot = Path.Combine(runRoot, "DdsPipeline", "Pixoar");
    var directRoot = Path.Combine(runRoot, "DdsPipeline", "DirectTexconv");
    Directory.CreateDirectory(outputRoot);
    Directory.CreateDirectory(directRoot);

    DdsCase[] cases =
    [
        new(DdsCompressionMode.Dxt1, "DXT1", "BC1_UNORM", "DXT1", null, true),
        new(DdsCompressionMode.Dxt3, "DXT3", "BC2_UNORM", "DXT3", null, true),
        new(DdsCompressionMode.Dxt5, "DXT5", "BC3_UNORM", "DXT5", null, true),
        new(DdsCompressionMode.Bc7, "BC7", "BC7_UNORM_SRGB", "DX10", 99, false),
        new(DdsCompressionMode.Uncompressed, "RGBA", "R8G8B8A8_UNORM", "\0\0\0\0", null, false)
    ];

    foreach (var testCase in cases)
    {
        await settings.UpdateAsync(current =>
        {
            current.Dds.Compression = testCase.Compression;
            current.Dds.GenerateMipmaps = false;
            current.Dds.PreserveAlpha = true;
        });

        var pixoarPath = Path.Combine(outputRoot, $"{testCase.Name}.dds");
        await dds.ConvertToDdsAsync(sourcePath, pixoarPath);

        var expectedHeaderArguments = testCase.DxgiFormat is null
            ? "--ignore-srgb -dx9"
            : "-srgb";
        AssertLastEncodingArguments(
            Path.Combine(runRoot, "AppData", "Logs"),
            $"-y -m 1 -sepalpha {expectedHeaderArguments} -ft dds -f {testCase.TexconvFormat} -o ",
            "dds-input.png");

        var pixoarHeader = ReadDdsHeader(pixoarPath);
        Assert(
            pixoarHeader.FourCc == testCase.FourCc &&
            pixoarHeader.DxgiFormat == testCase.DxgiFormat,
            $"{testCase.Name} used FourCC '{DisplayFourCc(pixoarHeader.FourCc)}' / " +
            $"DXGI {pixoarHeader.DxgiFormat?.ToString() ?? "none"}.");
        Assert(
            (pixoarHeader.Flags & ddsDepthFlag) == 0,
            $"{testCase.Name} incorrectly set DDSD_DEPTH.");
        Assert(
            (pixoarHeader.Caps2 & (cubeMapMask | volumeFlag)) == 0,
            $"{testCase.Name} incorrectly set cube/volume caps 0x{pixoarHeader.Caps2:X8}.");

        if (pixoarHeader.HasDx10Header)
        {
            Assert(
                pixoarHeader.ResourceDimension == 3,
                $"{testCase.Name} DX10 resource dimension was {pixoarHeader.ResourceDimension}, not Texture2D.");
            Assert(
                pixoarHeader.ArraySize == 1,
                $"{testCase.Name} DX10 array size was {pixoarHeader.ArraySize}, not 1.");
            Assert(
                (pixoarHeader.MiscFlag.GetValueOrDefault() & textureCubeFlag) == 0,
                $"{testCase.Name} incorrectly set the DX10 texture-cube flag.");
            Assert(
                pixoarHeader.MiscFlags2 == 0,
                $"{testCase.Name} wrote unexpected DX10 miscFlags2 value {pixoarHeader.MiscFlags2}.");
        }

        var directCaseRoot = Path.Combine(directRoot, testCase.Name);
        Directory.CreateDirectory(directCaseRoot);
        var directArguments = new List<string>
        {
            "-y",
            "-m",
            "1",
            "-sepalpha"
        };
        if (testCase.DxgiFormat is null)
        {
            directArguments.Add("--ignore-srgb");
            directArguments.Add("-dx9");
        }
        else
        {
            directArguments.Add("-srgb");
        }

        directArguments.Add("-ft");
        directArguments.Add("dds");
        directArguments.Add("-f");
        directArguments.Add(testCase.TexconvFormat);
        directArguments.Add("-o");
        directArguments.Add(directCaseRoot);
        directArguments.Add(sourcePath);
        await RunTexconvAsync(texconvPath, directArguments);

        var directPath = Path.Combine(
            directCaseRoot,
            $"{Path.GetFileNameWithoutExtension(sourcePath)}.dds");
        var directHeader = ReadDdsHeader(directPath);
        Assert(
            pixoarHeader.HeaderBytes.SequenceEqual(directHeader.HeaderBytes),
            $"{testCase.Name} Pixoar header differs from direct texconv output.");

        var decoded = await InspectOutputAsync(
            pixoarPath,
            ImageFormat.Dds,
            dds,
            runRoot);
        var metrics = Compare(source, decoded.Sample);
        var maximumMae = testCase.Compression == DdsCompressionMode.Uncompressed ? 0.01 : 8.0;
        var maximumBias = testCase.Compression == DdsCompressionMode.Uncompressed ? 0.01 : 3.0;
        Assert(
            metrics.MeanAbsoluteError <= maximumMae &&
            metrics.MaximumMeanChannelBias <= maximumBias,
            $"{testCase.Name} introduced a color shift beyond compression error: {metrics}.");

        var thumbnailResult = ExplorerThumbnail.TryCreate(pixoarPath);
        if (testCase.ExplorerThumbnailExpected)
        {
            Assert(
                thumbnailResult.Success,
                $"Explorer could not create a {testCase.Name} thumbnail: HRESULT 0x{thumbnailResult.HResult:X8}.");
        }

        Console.WriteLine(
            $"DDS {testCase.Name,-4}: 2D, FourCC={DisplayFourCc(pixoarHeader.FourCc)}, " +
            $"DXGI={pixoarHeader.DxgiFormat?.ToString() ?? "none"}, depthField={pixoarHeader.Depth}, " +
            $"pixel={metrics}, Explorer thumbnail=" +
            (thumbnailResult.Success ? "OK" : $"unavailable (0x{thumbnailResult.HResult:X8})"));
    }

    // Exercise the branches that omit both -m 1 and -sepalpha. Their absence
    // must not change the resource into an array, cubemap, or volume texture.
    await settings.UpdateAsync(current =>
    {
        current.Dds.Compression = DdsCompressionMode.Dxt5;
        current.Dds.GenerateMipmaps = true;
        current.Dds.PreserveAlpha = false;
    });
    var mipmappedPath = Path.Combine(outputRoot, "DXT5-mipmapped.dds");
    await dds.ConvertToDdsAsync(sourcePath, mipmappedPath);
    AssertLastEncodingArguments(
        Path.Combine(runRoot, "AppData", "Logs"),
        "-y --ignore-srgb -dx9 -ft dds -f BC3_UNORM -o ",
        "dds-input.png");
    var mipmappedHeader = ReadDdsHeader(mipmappedPath);
    Assert(
        (mipmappedHeader.Flags & ddsDepthFlag) == 0 &&
        (mipmappedHeader.Caps2 & (cubeMapMask | volumeFlag)) == 0,
        "Mipmapped DXT5 output was not an ordinary Texture2D.");
    Assert(
        (mipmappedHeader.Flags & 0x00020000) != 0 && mipmappedHeader.MipmapCount > 1,
        "Generate-mipmaps did not produce a mip chain.");
    await settings.UpdateAsync(current =>
    {
        current.Dds.Compression = DdsCompressionMode.Uncompressed;
        current.Dds.GenerateMipmaps = false;
        current.Dds.PreserveAlpha = true;
    });

    Console.WriteLine(
        "PASS DDS pipeline: exact texconv switches, direct-header parity, Texture2D flags, colors, and legacy DXT Explorer thumbnails verified.");
}

static async Task VerifyDefaultQualityBiasAsync(
    string runRoot,
    ISettingsService settings,
    IImageConversionService conversion)
{
    await settings.UpdateAsync(current =>
    {
        current.Quality.JpegQuality = 90;
        current.Quality.WebpQuality = 90;
    });

    var inputPath = CreateColorChart(Path.Combine(runRoot, "default-quality-source.png"));
    var source = Inspect(inputPath);
    foreach (var outputFormat in new[] { ImageFormat.Jpeg, ImageFormat.Webp })
    {
        var outputPath = await ConvertAsync(
            conversion,
            inputPath,
            outputFormat,
            Path.Combine(runRoot, "DefaultQuality", outputFormat.ToString()));
        var output = Inspect(outputPath);
        var metrics = Compare(source, output);
        Assert(
            metrics.MeanAbsoluteError <= 3.0 &&
            metrics.MaximumMeanChannelBias <= 1.0,
            $"Default-quality {outputFormat} introduced a color shift: {metrics}.");
        Console.WriteLine($"DEFAULT Q90 {outputFormat,-5}: {metrics}");
    }

    Console.WriteLine("PASS default quality: JPEG and WEBP compression add no systematic channel bias.");
}

static void AssertCanonicalSrgb(ImageFormat format, OutputInspection output)
{
    Assert(
        output.Sample.ColorSpace == ColorSpace.sRGB && IsSrgbGamma(output.Sample.Gamma),
        $"{format} output decoded as {output.Sample.ColorSpace} gamma {output.Sample.Gamma:F5}, not sRGB.");

    if (format == ImageFormat.Dds)
    {
        Assert(
            output.DxgiFormat is null,
            $"Uncompressed DDS unnecessarily used a DX10 header (DXGI {output.DxgiFormat}).");
    }
    else if (format != ImageFormat.Png)
    {
        Assert(
            output.Sample.Profile?.Contains("sRGB", StringComparison.OrdinalIgnoreCase) == true,
            $"{format} output did not retain an embedded sRGB ICC profile.");
    }
}

static async Task<string> ConvertAsync(
    IImageConversionService conversion,
    string inputPath,
    ImageFormat outputFormat,
    string outputRoot)
{
    Directory.CreateDirectory(outputRoot);
    var result = await conversion.ConvertAsync(new ImageConversionRequest
    {
        InputPath = inputPath,
        OutputFormat = outputFormat,
        OutputFolder = outputRoot
    });

    Assert(
        result.Success && result.OutputPath is not null,
        $"{Path.GetFileName(inputPath)} to {outputFormat} failed: {result.Error?.Message ?? "unknown error"}");
    return result.OutputPath!;
}

static async Task<OutputInspection> InspectOutputAsync(
    string path,
    ImageFormat format,
    IDdsService dds,
    string runRoot)
{
    if (format != ImageFormat.Dds)
    {
        return new OutputInspection(Inspect(path), null);
    }

    var decodeRoot = Path.Combine(runRoot, "DecodedDds", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(decodeRoot);
    var decodedPath = Path.Combine(decodeRoot, "decoded.png");
    await dds.ConvertFromDdsAsync(path, decodedPath, ImageFormat.Png);
    return new OutputInspection(Inspect(decodedPath), ReadDdsHeader(path).DxgiFormat);
}

static string CreateSrgbFixture(string path)
{
    using var image = new MagickImage(new MagickColor("#D04080"), 32, 32);
    image.Depth = 8;
    image.SetProfile(ColorProfiles.SRGB);
    image.Format = MagickFormat.Png;
    image.Write(path);
    return path;
}

static string CreateAdobeRgbFixture(string path)
{
    using var image = new MagickImage(new MagickColor("#D04080"), 32, 32);
    image.Depth = 8;
    image.SetProfile(ColorProfiles.SRGB);
    Assert(
        image.TransformColorSpace(ColorProfiles.AdobeRGB1998),
        "Could not create the Adobe RGB fixture.");
    var exif = new ExifProfile();
    exif.SetValue(ExifTag.ColorSpace, ushort.MaxValue);
    exif.SetValue(ExifTag.ImageDescription, "Pixoar color metadata");
    image.SetProfile(exif);
    image.Format = MagickFormat.Png;
    image.Write(path);
    return path;
}

static string CreateLinearFixture(string path)
{
    const int width = 32;
    const int height = 32;
    var scanlines = new byte[height * (1 + (width * 4))];
    for (var y = 0; y < height; y++)
    {
        var rowOffset = y * (1 + (width * 4));
        scanlines[rowOffset] = 0;
        for (var x = 0; x < width; x++)
        {
            var pixelOffset = rowOffset + 1 + (x * 4);
            scanlines[pixelOffset] = 161;
            scanlines[pixelOffset + 1] = 13;
            scanlines[pixelOffset + 2] = 55;
            scanlines[pixelOffset + 3] = 255;
        }
    }

    using var compressed = new MemoryStream();
    using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
    {
        zlib.Write(scanlines);
    }

    using var output = File.Create(path);
    output.Write([137, 80, 78, 71, 13, 10, 26, 10]);

    Span<byte> ihdr = stackalloc byte[13];
    BinaryPrimitives.WriteUInt32BigEndian(ihdr, width);
    BinaryPrimitives.WriteUInt32BigEndian(ihdr[4..], height);
    ihdr[8] = 8;
    ihdr[9] = 6;
    WritePngChunk(output, "IHDR", ihdr);

    Span<byte> gamma = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(gamma, 100_000);
    WritePngChunk(output, "gAMA", gamma);
    WritePngChunk(output, "IDAT", compressed.ToArray());
    WritePngChunk(output, "IEND", []);
    return path;
}

static string CreateColorChart(string path)
{
    const int width = 48;
    const int height = 32;
    using var image = new MagickImage(MagickColors.Black, width, height);
    using var pixels = image.GetPixels();

    for (var y = 0; y < height; y++)
    {
        for (var x = 0; x < width; x++)
        {
            pixels.SetPixel(
                x,
                y,
                [
                    (byte)(16 + ((x * 223) / (width - 1))),
                    (byte)(24 + ((y * 207) / (height - 1))),
                    (byte)(32 + (((x + y) * 191) / (width + height - 2))),
                    byte.MaxValue
                ]);
        }
    }

    image.Depth = 8;
    image.SetProfile(ColorProfiles.SRGB);
    image.Format = MagickFormat.Png;
    image.Write(path);
    return path;
}

static ImageSample Inspect(string path)
{
    using var image = new MagickImage(path);
    return InspectImage(image);
}

static ImageSample InspectImage(MagickImage image)
{
    var profile = image.GetColorProfile();
    var exif = image.GetExifProfile();
    var exifColorSpace = exif?.GetValue(ExifTag.ColorSpace)?.Value;
    var imageDescription = exif?.GetValue(ExifTag.ImageDescription)?.Value;
    using var srgb = (MagickImage)image.Clone();
    if (srgb.GetColorProfile() is not null)
    {
        Assert(
            srgb.TransformColorSpace(ColorProfiles.SRGB),
            "Test oracle could not transform an embedded profile to sRGB.");
    }
    else if (srgb.ColorSpace != ColorSpace.sRGB)
    {
        srgb.ColorSpace = ColorSpace.sRGB;
    }

    using var pixels = srgb.GetPixels();
    var bytes = pixels.ToByteArray(PixelMapping.RGBA)
        ?? throw new InvalidOperationException("Could not read image pixels.");
    return new ImageSample(
        image.Width,
        image.Height,
        image.ColorSpace,
        image.Gamma,
        profile?.Description,
        exifColorSpace,
        imageDescription,
        bytes);
}

static PixelMetrics Compare(ImageSample expected, ImageSample actual)
{
    Assert(
        expected.Width == actual.Width && expected.Height == actual.Height,
        $"Image dimensions changed from {expected.Width}x{expected.Height} to {actual.Width}x{actual.Height}.");
    Assert(
        expected.Pixels.Length == actual.Pixels.Length,
        "Decoded pixel buffer lengths did not match.");

    double absoluteError = 0;
    double redBias = 0;
    double greenBias = 0;
    double blueBias = 0;
    var maximumDelta = 0;
    var pixelCount = expected.Pixels.Length / 4;

    for (var index = 0; index < expected.Pixels.Length; index += 4)
    {
        var redDelta = actual.Pixels[index] - expected.Pixels[index];
        var greenDelta = actual.Pixels[index + 1] - expected.Pixels[index + 1];
        var blueDelta = actual.Pixels[index + 2] - expected.Pixels[index + 2];
        redBias += redDelta;
        greenBias += greenDelta;
        blueBias += blueDelta;
        absoluteError += Math.Abs(redDelta) + Math.Abs(greenDelta) + Math.Abs(blueDelta);
        maximumDelta = Math.Max(
            maximumDelta,
            Math.Max(
                Math.Abs(redDelta),
                Math.Max(Math.Abs(greenDelta), Math.Abs(blueDelta))));
    }

    return new PixelMetrics(
        absoluteError / (pixelCount * 3),
        maximumDelta,
        Math.Max(
            Math.Abs(redBias / pixelCount),
            Math.Max(
                Math.Abs(greenBias / pixelCount),
                Math.Abs(blueBias / pixelCount))));
}

static int MaximumDeltaFromColor(byte[] expectedPixels, byte[] actualPixels)
{
    var maximumDelta = 0;
    for (var index = 0; index < actualPixels.Length; index += 4)
    {
        maximumDelta = Math.Max(
            maximumDelta,
            Math.Max(
                Math.Abs(actualPixels[index] - expectedPixels[0]),
                Math.Max(
                    Math.Abs(actualPixels[index + 1] - expectedPixels[1]),
                    Math.Abs(actualPixels[index + 2] - expectedPixels[2]))));
    }

    return maximumDelta;
}

static void WritePngChunk(Stream output, string type, ReadOnlySpan<byte> data)
{
    Span<byte> length = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(length, (uint)data.Length);
    output.Write(length);

    var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
    output.Write(typeBytes);
    output.Write(data);

    var crcInput = new byte[typeBytes.Length + data.Length];
    typeBytes.CopyTo(crcInput, 0);
    data.CopyTo(crcInput.AsSpan(typeBytes.Length));
    Span<byte> crc = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(crc, ComputeCrc32(crcInput));
    output.Write(crc);
}

static uint ComputeCrc32(ReadOnlySpan<byte> data)
{
    var crc = uint.MaxValue;
    foreach (var value in data)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++)
        {
            crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
    }

    return ~crc;
}

static void AssertLastEncodingArguments(
    string logsDirectory,
    string expectedPrefix,
    string expectedInputSuffix)
{
    var logFile = Directory
        .EnumerateFiles(logsDirectory, "*.log", SearchOption.TopDirectoryOnly)
        .OrderBy(File.GetLastWriteTimeUtc)
        .LastOrDefault()
        ?? throw new InvalidOperationException("Pixoar did not create a conversion log.");
    var argumentLine = File
        .ReadLines(logFile)
        .LastOrDefault(line => line.Contains(
            "Generated texconv arguments:",
            StringComparison.Ordinal))
        ?? throw new InvalidOperationException("Pixoar did not log its texconv arguments.");
    var markerIndex = argumentLine.IndexOf(
        "Generated texconv arguments:",
        StringComparison.Ordinal);
    var actual = argumentLine[
        (markerIndex + "Generated texconv arguments:".Length)..].Trim();

    Assert(
        actual.StartsWith(expectedPrefix, StringComparison.Ordinal),
        $"Unexpected texconv arguments: {actual}");
    var pathArguments = actual[expectedPrefix.Length..];
    var exactPathPair = Regex.Match(
        pathArguments,
        "^(?:\"[^\"]+\"|\\S+) (?<input>\"[^\"]+\"|\\S+)$",
        RegexOptions.CultureInvariant);
    Assert(
        exactPathPair.Success &&
        exactPathPair.Groups["input"].Value
            .Trim('"')
            .EndsWith(expectedInputSuffix, StringComparison.OrdinalIgnoreCase),
        $"Unexpected or additional texconv arguments: {actual}");
}

static async Task RunTexconvAsync(string texconvPath, IEnumerable<string> arguments)
{
    var startInfo = new ProcessStartInfo
    {
        FileName = texconvPath,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start direct texconv.");
    var standardOutput = process.StandardOutput.ReadToEndAsync();
    var standardError = process.StandardError.ReadToEndAsync();
    await process.WaitForExitAsync();
    var output = await standardOutput;
    var error = await standardError;
    Assert(
        process.ExitCode == 0,
        $"Direct texconv failed ({process.ExitCode}). stdout: {output} stderr: {error}");
}

static DdsHeader ReadDdsHeader(string path)
{
    var bytes = File.ReadAllBytes(path);
    Assert(bytes.Length >= 128, $"Truncated DDS header: {path}");
    Assert(
        BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 0x20534444,
        $"Invalid DDS magic: {path}");

    var flags = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(8));
    var depth = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(24));
    var mipmapCount = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(28));
    var fourCc = System.Text.Encoding.ASCII.GetString(bytes, 84, 4);
    var caps2 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(112));
    uint? dxgiFormat = null;
    uint? resourceDimension = null;
    uint? miscFlag = null;
    uint? arraySize = null;
    uint? miscFlags2 = null;
    if (fourCc == "DX10")
    {
        Assert(bytes.Length >= 148, $"Truncated DDS DX10 header: {path}");
        dxgiFormat = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(128));
        resourceDimension = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(132));
        miscFlag = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(136));
        arraySize = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(140));
        miscFlags2 = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(144));
    }

    var headerLength = fourCc == "DX10" ? 148 : 128;
    return new DdsHeader(
        fourCc,
        flags,
        depth,
        mipmapCount,
        caps2,
        dxgiFormat,
        resourceDimension,
        miscFlag,
        arraySize,
        miscFlags2,
        bytes[..headerLength]);
}

static string DisplayFourCc(string fourCc)
{
    return fourCc.All(value => value == '\0') ? "none" : fourCc.TrimEnd('\0', ' ');
}

static bool IsSrgbGamma(double gamma)
{
    return Math.Abs(gamma - 0.45455) < 0.0001;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void DeleteDirectoryWithRetries(string path)
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
        catch (IOException) when (attempt < 5)
        {
            Thread.Sleep(75 * attempt);
        }
        catch (UnauthorizedAccessException) when (attempt < 5)
        {
            Thread.Sleep(75 * attempt);
        }
    }
}

readonly record struct DdsHeader(
    string FourCc,
    uint Flags,
    uint Depth,
    uint MipmapCount,
    uint Caps2,
    uint? DxgiFormat,
    uint? ResourceDimension,
    uint? MiscFlag,
    uint? ArraySize,
    uint? MiscFlags2,
    byte[] HeaderBytes)
{
    public bool HasDx10Header => FourCc == "DX10";
}

readonly record struct DdsCase(
    DdsCompressionMode Compression,
    string Name,
    string TexconvFormat,
    string FourCc,
    uint? DxgiFormat,
    bool ExplorerThumbnailExpected);

readonly record struct OutputInspection(ImageSample Sample, uint? DxgiFormat);

readonly record struct PixelMetrics(
    double MeanAbsoluteError,
    int MaximumChannelDelta,
    double MaximumMeanChannelBias)
{
    public override string ToString()
    {
        return $"MAE={MeanAbsoluteError:F3}, max={MaximumChannelDelta}, bias={MaximumMeanChannelBias:F3}";
    }
}

sealed record ImageSample(
    uint Width,
    uint Height,
    ColorSpace ColorSpace,
    double Gamma,
    string? Profile,
    ushort? ExifColorSpace,
    string? ImageDescription,
    byte[] Pixels)
{
    public string FirstPixel => $"{Pixels[0]},{Pixels[1]},{Pixels[2]},{Pixels[3]}";
}

sealed class TestApplicationPathProvider(string appDataDirectory) : IApplicationPathProvider
{
    public string AppDataDirectory { get; } = appDataDirectory;

    public string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");

    public string LogsDirectory => Path.Combine(AppDataDirectory, "Logs");
}

static class TestFormats
{
    public static readonly ImageFormat[] Supported =
    [
        ImageFormat.Png,
        ImageFormat.Jpeg,
        ImageFormat.Webp,
        ImageFormat.Bmp,
        ImageFormat.Tiff,
        ImageFormat.Dds
    ];
}

static class ExplorerThumbnail
{
    private const uint ThumbnailOnly = 0x00000008;

    public static ThumbnailResult TryCreate(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new ThumbnailResult(false, unchecked((int)0x80004001));
        }

        var factoryInterface = typeof(IShellItemImageFactory).GUID;
        var createResult = SHCreateItemFromParsingName(
            Path.GetFullPath(path),
            IntPtr.Zero,
            ref factoryInterface,
            out var factory);
        if (createResult < 0 || factory is null)
        {
            return new ThumbnailResult(false, createResult);
        }

        IntPtr bitmap = IntPtr.Zero;
        try
        {
            var result = factory.GetImage(
                new NativeSize(128, 128),
                ThumbnailOnly,
                out bitmap);
            return new ThumbnailResult(result >= 0 && bitmap != IntPtr.Zero, result);
        }
        finally
        {
            if (bitmap != IntPtr.Zero)
            {
                _ = DeleteObject(bitmap);
            }

            _ = Marshal.ReleaseComObject(factory);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? factory);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr objectHandle);

    [ComImport]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            NativeSize size,
            uint flags,
            out IntPtr bitmapHandle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly record struct NativeSize(int Width, int Height);
}

readonly record struct ThumbnailResult(bool Success, int HResult);
