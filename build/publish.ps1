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
$installDirectory = Join-Path $distDirectory "Pixoar"
$projectPath = Join-Path $repositoryRoot "Pixoar.App\Pixoar.App.csproj"
$propsPath = Join-Path $repositoryRoot "Directory.Build.props"

[xml]$props = Get-Content -LiteralPath $propsPath
$version = $props.Project.PropertyGroup.Version
if ([string]::IsNullOrWhiteSpace($version)) {
    $version = "0.1.0"
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

dotnet restore (Join-Path $repositoryRoot "Pixoar.sln")

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

Get-ChildItem -LiteralPath $resolvedInstall -Recurse -File |
    Where-Object { $_.Extension -in ".pdb", ".xml" } |
    Remove-Item -Force

$requiredFiles = @(
    "Pixoar.exe",
    "Pixoar.Cli.exe",
    "texconv.exe",
    "LICENSE",
    "licenses\DirectXTex-LICENSE.txt",
    "licenses\Magick.NET-Notice.txt",
    "licenses\Microsoft.Extensions-LICENSE.txt",
    "licenses\Microsoft.Extensions-THIRD-PARTY-NOTICES.txt",
    "Resources\Assets\Branding\pixoar.ico"
)

foreach ($fileName in $requiredFiles) {
    $path = Join-Path $resolvedInstall $fileName
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Publish output is missing $fileName."
    }
}

if (-not $NoZip) {
    $zipPath = Join-Path $distDirectory "Pixoar-$version-$RuntimeIdentifier.zip"
    if (Test-Path -LiteralPath $zipPath) {
        Remove-Item -LiteralPath $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $resolvedInstall "*") -DestinationPath $zipPath -Force
    Write-Host "Created release archive: $zipPath"
}

Write-Host "Pixoar distribution ready: $resolvedInstall"
Write-Host "Run Pixoar.exe, then select Settings > Quick Actions > Enable Context Menu and Apply Changes to register Explorer actions."
