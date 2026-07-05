using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using GitHubWallpaper.Desktop;

namespace GitHubWallpaper.GitHub;

/// <summary>
/// OAuth Device Authorization Grant — вход через <c>github.com/login/device</c>.
/// </summary>
internal sealed class GitHubOAuthDeviceAuth : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public GitHubOAuthDeviceAuth(HttpClient? httpClient = null)
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

    /// <summary>Запрашивает код устройства и открывает страницу подтверждения в браузере.</summary>
    public async Task<GitHubDeviceAuthorization> RequestDeviceCodeAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GitHubOAuthDefaults.DeviceCodeUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = new
        {
            client_id = clientId,
            scope = GitHubOAuthDefaults.Scope,
        };

        request.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json");

        using var response = await _httpClient
            .SendAsync(request, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubOAuthException(
                $"Не удалось запросить код устройства (HTTP {(int)response.StatusCode}). " +
                "Убедитесь, что в OAuth App включён Device Flow.");
        }

        var authorization = JsonSerializer.Deserialize<GitHubDeviceAuthorization>(body, JsonOptions)
            ?? throw new GitHubOAuthException("Некорректный ответ GitHub при запросе кода устройства.");

        if (string.IsNullOrWhiteSpace(authorization.DeviceCode)
            || string.IsNullOrWhiteSpace(authorization.UserCode))
        {
            throw new GitHubOAuthException("GitHub не вернул device_code или user_code.");
        }

        var verificationUri = string.IsNullOrWhiteSpace(authorization.VerificationUri)
            ? GitHubOAuthDefaults.DeviceVerificationUrl
            : authorization.VerificationUri;

        BrowserLauncher.Open(verificationUri);
        return authorization with { VerificationUri = verificationUri };
    }

    /// <summary>Ожидает подтверждение пользователем на github.com и возвращает access token.</summary>
    public async Task<string> PollForAccessTokenAsync(
        string clientId,
        GitHubDeviceAuthorization authorization,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var interval = Math.Max(authorization.Interval, 5);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(authorization.ExpiresIn, 60));

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);

            using var request = new HttpRequestMessage(HttpMethod.Post, GitHubOAuthDefaults.AccessTokenUrl);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var payload = new
            {
                client_id = clientId,
                device_code = authorization.DeviceCode,
                grant_type = "urn:ietf:params:oauth:grant-type:device_code",
            };

            request.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("access_token", out var tokenElement))
            {
                var token = tokenElement.GetString();
                if (string.IsNullOrWhiteSpace(token))
                {
                    throw new GitHubOAuthException("GitHub вернул пустой access_token.");
                }

                return token.Trim();
            }

            var error = root.TryGetProperty("error", out var errorElement)
                ? errorElement.GetString()
                : null;

            switch (error)
            {
                case "authorization_pending":
                    progress?.Report("Ожидание подтверждения на GitHub…");
                    continue;
                case "slow_down":
                    interval += 5;
                    progress?.Report("GitHub просит замедлить опрос…");
                    continue;
                case "expired_token":
                    throw new GitHubOAuthException(
                        "Код устройства истёк. Нажмите «Войти через GitHub» ещё раз.");
                case "access_denied":
                    throw new GitHubOAuthException("Авторизация отклонена на GitHub.");
                default:
                    var description = root.TryGetProperty("error_description", out var descriptionElement)
                        ? descriptionElement.GetString()
                        : error;

                    throw new GitHubOAuthException(
                        string.IsNullOrWhiteSpace(description)
                            ? "Не удалось получить токен через Device Flow."
                            : description);
            }
        }

        throw new GitHubOAuthException(
            "Время ожидания авторизации истекло. Повторите вход через GitHub.");
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("GitHubWallpaper", GetVersionText()));
        return client;
    }

    private static string GetVersionText()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0" : version.ToString(3);
    }
}

/// <summary>Ответ GitHub на запрос device code.</summary>
internal sealed record GitHubDeviceAuthorization
{
    public string DeviceCode { get; init; } = string.Empty;

    public string UserCode { get; init; } = string.Empty;

    public string VerificationUri { get; init; } = string.Empty;

    public int ExpiresIn { get; init; }

    public int Interval { get; init; }
}
