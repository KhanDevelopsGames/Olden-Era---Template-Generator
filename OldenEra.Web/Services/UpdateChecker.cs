using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace OldenEra.Web.Services;

/// <summary>
/// Hits the GitHub releases API to detect newer versions, mirroring the WPF
/// app's startup update check. Fire-and-forget, never blocks the UI; any
/// failure (CORS, network, parse) is silently swallowed.
/// </summary>
public sealed class UpdateChecker
{
    private const string LatestReleaseUrl =
        "https://api.github.com/repos/KhanDevelopsGames/Olden-Era---Template-Generator/releases/latest";

    public const string ReleasesPageUrl =
        "https://github.com/KhanDevelopsGames/Olden-Era---Template-Generator/releases";

    private readonly HttpClient _http;

    public UpdateChecker(HttpClient http) => _http = http;

    /// <summary>
    /// Returns the latest version if it is strictly greater than
    /// <paramref name="currentVersion"/>; otherwise null.
    /// </summary>
    public async Task<Version?> GetNewerVersionAsync(Version? currentVersion)
    {
        if (currentVersion is null) return null;
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            // Browser sets User-Agent automatically; GitHub accepts that for unauthenticated GET.
            req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var release = await resp.Content.ReadFromJsonAsync<GitHubRelease>();
            if (release?.TagName is null) return null;

            string tag = release.TagName.TrimStart('v', 'V');
            if (!Version.TryParse(tag, out var latest)) return null;
            return latest > currentVersion ? latest : null;
        }
        catch
        {
            return null;
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
    }
}
