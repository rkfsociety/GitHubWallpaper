using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GitHubWallpaper.Update;

/// <summary>Проверяет GitHub Release <c>latest</c> на наличие новой версии.</summary>
internal sealed class AppUpdateChecker : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly Regex VersionInNotesRegex = new(
        @"\*\*(?<version>\d+\.\d+\.\d+(?:[-\w.]*)?)\*\*",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public AppUpdateChecker(HttpClient? httpClient = null)
    {
        if (httpClient is null)
        {
            _httpClient = CreateHttpClient();
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
    }

    public async Task<AppUpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!AppVersion.CanSelfUpdate)
        {
            return new AppUpdateCheckResult.Skipped(
                "Автообновление доступно только в portable GitHubWallpaper.exe.");
        }

        if (AppVersion.IsDevelopmentBuild)
        {
            return new AppUpdateCheckResult.Skipped(
                $"Сборка для разработки ({AppVersion.CurrentVersion}) не обновляется автоматически.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AppUpdateDefaults.ReleaseApiUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new AppUpdateCheckResult.Failed(
                    $"GitHub API вернул HTTP {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (!root.TryGetProperty("assets", out var assetsElement)
                || assetsElement.ValueKind != JsonValueKind.Array)
            {
                return new AppUpdateCheckResult.Failed("В релизе нет файлов для скачивания.");
            }

            string? exeUrl = null;
            long? exeSize = null;
            string? versionJsonUrl = null;

            foreach (var asset in assetsElement.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var name = nameElement.GetString();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!asset.TryGetProperty("browser_download_url", out var urlElement))
                {
                    continue;
                }

                var url = urlElement.GetString();
                if (string.IsNullOrWhiteSpace(url))
                {
                    continue;
                }

                if (name.Equals(AppUpdateDefaults.ExeAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    exeUrl = url;
                    if (asset.TryGetProperty("size", out var sizeElement) && sizeElement.TryGetInt64(out var size))
                    {
                        exeSize = size;
                    }
                }
                else if (name.Equals(AppUpdateDefaults.VersionAssetName, StringComparison.OrdinalIgnoreCase))
                {
                    versionJsonUrl = url;
                }
            }

            if (string.IsNullOrWhiteSpace(exeUrl))
            {
                return new AppUpdateCheckResult.Failed("В релизе не найден GitHubWallpaper.exe.");
            }

            var remoteVersion = await ResolveRemoteVersionAsync(versionJsonUrl, root, cancellationToken)
                .ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(remoteVersion))
            {
                return new AppUpdateCheckResult.Failed("Не удалось определить версию релиза.");
            }

            var currentVersion = AppVersion.CurrentVersion;
            if (!AppVersion.IsRemoteNewer(remoteVersion))
            {
                return new AppUpdateCheckResult.UpToDate(currentVersion);
            }

            var releasePageUrl = root.TryGetProperty("html_url", out var htmlUrlElement)
                ? htmlUrlElement.GetString() ?? AppUpdateDefaults.ReleasePageUrl
                : AppUpdateDefaults.ReleasePageUrl;

            return new AppUpdateCheckResult.UpdateAvailable(
                new AppUpdateInfo(remoteVersion, exeUrl, releasePageUrl, exeSize),
                currentVersion);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AppUpdateCheckResult.Failed($"Не удалось проверить обновления: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<string?> ResolveRemoteVersionAsync(
        string? versionJsonUrl,
        JsonElement releaseRoot,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(versionJsonUrl))
        {
            try
            {
                using var response = await _httpClient
                    .GetAsync(versionJsonUrl, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    await using var stream = await response.Content
                        .ReadAsStreamAsync(cancellationToken)
                        .ConfigureAwait(false);

                    var metadata = await JsonSerializer
                        .DeserializeAsync<ReleaseVersionMetadata>(stream, JsonOptions, cancellationToken)
                        .ConfigureAwait(false);

                    if (!string.IsNullOrWhiteSpace(metadata?.Version))
                    {
                        return metadata.Version.Trim();
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (JsonException)
            {
            }
        }

        if (releaseRoot.TryGetProperty("body", out var bodyElement))
        {
            var body = bodyElement.GetString();
            var match = string.IsNullOrWhiteSpace(body) ? null : VersionInNotesRegex.Match(body);
            if (match is { Success: true })
            {
                return match.Groups["version"].Value;
            }
        }

        return null;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("GitHubWallpaper", GetVersionText()));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        return client;
    }

    private static string GetVersionText()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0" : version.ToString(3);
    }

    private sealed class ReleaseVersionMetadata
    {
        public string Version { get; set; } = string.Empty;
    }
}
