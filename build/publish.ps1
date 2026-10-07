param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [switch]$SelfContained,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$repositoryRoot = Resolve-Path (Join-Path $scriptDirectory "..")
$artifactsDirectory = Join-Path $repositoryRoot "artifacts"
$distDirectory = Join-Path $artifactsDirectory "dist"
$publishDirectory = Join-Path $artifactsDirectory "publish"
$installDirectory = Join-Path $publishDirectory "Pixoar"
$projectPath = Join-Path $repositoryRoot "Pixoar.App\Pixoar.App.csproj"
$propsPath = Join-Path $repositoryRoot "Directory.Build.props"

[xml]$props = Get-Content -LiteralPath $propsPath
$version = $props.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.2.0"
}

$resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsDirectory)
$resolvedInstall = [System.IO.Path]::GetFullPath($installDirectory)
if (-not $resolvedInstall.StartsWith($resolvedArtifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean unexpected publish path: $resolvedInstall"
}

if (Test-Path -LiteralPath $resolvedInstall) {
    Remove-Item -LiteralPath $resolvedInstall -Recurse -Force
}

New-Item -ItemType Directory -Path $resolvedInstall | Out-Null
New-Item -ItemType Directory -Path $distDirectory -Force | Out-Null

dotnet restore (Join-Path $repositoryRoot "Pixoar.sln")
if ($LASTEXITCODE -ne 0) {
    throw "Restore failed with exit code $LASTEXITCODE."
}

$publishArguments = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", $RuntimeIdentifier,
    "--self-contained", $SelfContained.IsPresent.ToString().ToLowerInvariant(),
    "-o", $resolvedInstall,
    "/p:PublishSingleFile=false"
)

dotnet @publishArguments
if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE."
}

Get-ChildItem -LiteralPath $resolvedInstall -Recurse -File |
    Where-Object { $_.Extension -in ".pdb", ".xml" } |
    Remove-Item -Force

$requiredFiles = @(
    "Pixoar.exe",
    "Pixoar.Cli.exe",
    "tools\texconv\texconv.exe",
    "tools\realesrgan\realesrgan-ncnn-vulkan.exe",
    "tools\realesrgan\vcomp140.dll",
    "tools\realesrgan\models\realesrgan-x4plus.param",
    "tools\realesrgan\models\realesrgan-x4plus.bin",
    "LICENSE",
    "licenses\DirectXTex-LICENSE.txt",
    "licenses\Magick.NET-Notice.txt",
    "licenses\Microsoft.Extensions-LICENSE.txt",
    "licenses\Microsoft.Extensions-THIRD-PARTY-NOTICES.txt",
    "licenses\Real-ESRGAN-ncnn-vulkan-MIT.txt",
    "licenses\NCNN-BSD-3-Clause.txt",
    "licenses\Real-ESRGAN-model-BSD-3-Clause.txt",
    "Resources\Assets\Branding\pixoar.ico"
)

foreach ($fileName in $requiredFiles) {
    $path = Join-Path $resolvedInstall $fileName
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Publish output is missing $fileName."
    }
}

$texconvFiles = @(Get-ChildItem -LiteralPath $resolvedInstall -Recurse -File -Filter "texconv.exe")
if ($texconvFiles.Count -ne 1) {
    throw "Publish output must contain exactly one bundled texconv.exe in tools\texconv. Found $($texconvFiles.Count)."
}

if (-not $NoZip) {
    $zipPath = Join-Path $distDirectory "Pixoar-$version-$RuntimeIdentifier.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $resolvedInstall "*") -DestinationPath $zipPath -Force
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zipPath)
    try {
        $texconvEntries = @($archive.Entries | Where-Object { $_.Name -ieq "texconv.exe" })
        if ($texconvEntries.Count -ne 1 -or $texconvEntries[0].FullName.Replace('\', '/') -cne "tools/texconv/texconv.exe") {
            throw "Release archive must contain exactly one bundled tools/texconv/texconv.exe."
        }
    }
    finally {
        $archive.Dispose()
    }
    Write-Host "Created release archive: $zipPath"
}

Write-Host "Pixoar distribution ready: $resolvedInstall"
Write-Host "Run Pixoar.exe, then select Settings > Quick Actions > Enable Context Menu and Apply Changes to register Explorer actions."
