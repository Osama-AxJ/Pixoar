using System.Net;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pixoar.Core.Interfaces;
using Pixoar.Core.Models;

namespace Pixoar.Core.Services;

internal sealed class GitHubUpdateService(
    HttpClient httpClient,
    IApplicationLogger logger) : IUpdateService
{
    private const string LatestReleaseUrl = "https://api.github.com/repos/Osama-AxJ/Pixoar/releases/latest";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var currentVersion = GetCurrentVersionText();

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.ParseAdd($"Pixoar/{currentVersion}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                await LogHttpFailureAsync(response, cancellationToken).ConfigureAwait(false);
                return Failed(currentVersion);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubReleaseResponse>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            var tagName = release?.TagName;
            var releaseUrl = release?.HtmlUrl;
            if (release is null ||
                release.Draft ||
                release.Prerelease ||
                string.IsNullOrWhiteSpace(tagName) ||
                !IsValidReleaseUrl(releaseUrl))
            {
                await logger.LogWarningAsync("GitHub latest release response could not be used for update checking.", cancellationToken).ConfigureAwait(false);
                return Failed(currentVersion);
            }

            var validatedReleaseUrl = releaseUrl!;
            if (!SemanticVersion.TryParse(currentVersion, out var installedVersion) ||
                !SemanticVersion.TryParse(tagName, out var latestVersion))
            {
                await logger.LogWarningAsync("GitHub release version could not be compared during update checking.", cancellationToken).ConfigureAwait(false);
                return Failed(currentVersion);
            }

            var status = latestVersion.CompareTo(installedVersion) > 0
                ? UpdateStatus.UpdateAvailable
                : UpdateStatus.UpToDate;

            return new UpdateCheckResult
            {
                Status = status,
                CurrentVersion = installedVersion.ToString(),
                LatestVersion = latestVersion.ToString(),
                ReleaseName = release.Name ?? tagName,
                ReleaseUrl = validatedReleaseUrl,
                PublishedAt = release.PublishedAt
            };
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await logger.LogWarningAsync("Update check timed out.", cancellationToken).ConfigureAwait(false);
            return Failed(currentVersion);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await logger.LogErrorAsync("Update check failed.", ex, cancellationToken).ConfigureAwait(false);
            return Failed(currentVersion);
        }
    }

    private static UpdateCheckResult Failed(string currentVersion)
    {
        return new UpdateCheckResult
        {
            Status = UpdateStatus.CheckFailed,
            CurrentVersion = currentVersion
        };
    }

    private static bool IsValidReleaseUrl(string? releaseUrl)
    {
        if (!Uri.TryCreate(releaseUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase) &&
            uri.AbsolutePath.StartsWith("/Osama-AxJ/Pixoar/releases/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task LogHttpFailureAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var rateLimitRemaining = response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
            ? values.FirstOrDefault()
            : null;

        var message = response.StatusCode == HttpStatusCode.Forbidden && rateLimitRemaining == "0"
            ? "GitHub update check failed because the unauthenticated rate limit was reached."
            : $"GitHub update check failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";

        await logger.LogWarningAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private static string GetCurrentVersionText()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(GitHubUpdateService).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+')[0];
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private sealed class GitHubReleaseResponse
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }
    }

    private readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
    {
        public int CompareTo(SemanticVersion other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0)
            {
                return major;
            }

            var minor = Minor.CompareTo(other.Minor);
            return minor != 0 ? minor : Patch.CompareTo(other.Patch);
        }

        public override string ToString()
        {
            return $"{Major}.{Minor}.{Patch}";
        }

        public static bool TryParse(string? value, out SemanticVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value.Trim();
            if (normalized.StartsWith('v') || normalized.StartsWith('V'))
            {
                normalized = normalized[1..];
            }

            normalized = normalized.Split('-', 2)[0];
            var parts = normalized.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length is < 2 or > 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var major) ||
                !int.TryParse(parts[1], out var minor))
            {
                return false;
            }

            var patch = 0;
            if (parts.Length == 3 && !int.TryParse(parts[2], out patch))
            {
                return false;
            }

            version = new SemanticVersion(major, minor, patch);
            return true;
        }
    }
}
