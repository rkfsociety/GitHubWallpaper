using System.Net;
using System.Net.Http.Headers;
using System.Reflection;

namespace GitHubWallpaper.GitHub;

/// <summary>
/// HTTP-клиент для GitHub REST API с опциональным заголовком <c>Authorization</c>.
/// </summary>
internal sealed class GitHubApiClient : IDisposable
{
    public const string ApiBaseUrl = "https://api.github.com";
    private const string GitHubApiVersion = "2022-11-28";

    private static readonly string UserAgent = BuildUserAgent();

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly RateLimitGuard _rateLimitGuard;
    private string? _token;

    public GitHubApiClient(
        string? token = null,
        HttpClient? httpClient = null,
        RateLimitGuard? rateLimitGuard = null)
    {
        _token = NormalizeToken(token);
        _rateLimitGuard = rateLimitGuard ?? new RateLimitGuard();

        if (httpClient is null)
        {
            _httpClient = CreateDefaultHttpClient();
            _ownsHttpClient = true;
        }
        else
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }

        ApplyDefaultHeaders();
    }

    /// <summary>Токен задан и будет отправляться в заголовке Authorization.</summary>
    public bool HasToken => _token is not null;

    /// <summary>Отслеживание лимитов GitHub API и backoff между запросами.</summary>
    public RateLimitGuard RateLimit => _rateLimitGuard;

    /// <summary>Обновляет PAT; пустая строка снимает авторизацию.</summary>
    public void SetToken(string? token) => _token = NormalizeToken(token);

    /// <summary>
    /// Выполняет GET-запрос к GitHub REST API.
    /// </summary>
    /// <param name="path">Относительный путь, например <c>/repos/{owner}/{repo}</c>.</param>
    /// <param name="ifNoneMatch">Значение ETag для заголовка <c>If-None-Match</c>.</param>
    public Task<GitHubApiResult> GetAsync(
        string path,
        CancellationToken cancellationToken = default) =>
        GetAsync(path, ifNoneMatch: null, cancellationToken);

    /// <summary>
    /// Выполняет GET-запрос с опциональным <c>If-None-Match</c> (ответ 304 не считается ошибкой).
    /// </summary>
    public async Task<GitHubApiResult> GetAsync(
        string path,
        string? ifNoneMatch,
        CancellationToken cancellationToken = default)
    {
        await _rateLimitGuard.WaitIfNeededAsync(cancellationToken).ConfigureAwait(false);

        using var request = CreateRequest(HttpMethod.Get, path, ifNoneMatch);
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var rateLimit = GitHubRateLimit.TryParse(response);
        var etag = response.Headers.ETag?.Tag ?? ifNoneMatch;

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            _rateLimitGuard.Observe(rateLimit);

            return new GitHubApiResult(
                response.StatusCode,
                string.Empty,
                rateLimit,
                etag,
                IsNotModified: true);
        }

        if (!response.IsSuccessStatusCode)
        {
            _rateLimitGuard.HandleErrorResponse(
                response.StatusCode,
                rateLimit,
                body,
                TryParseRetryAfter(response));

            throw new GitHubApiException(response.StatusCode, path, body, rateLimit);
        }

        _rateLimitGuard.Observe(rateLimit);

        return new GitHubApiResult(
            response.StatusCode,
            body,
            rateLimit,
            etag);
    }

    /// <summary>
    /// GET к <c>stats/*</c> с повтором при HTTP 202 (данные ещё не готовы).
    /// </summary>
    public async Task<GitHubApiResult> GetStatsAsync(
        string path,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 5;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var result = await GetAsync(path, ifNoneMatch, cancellationToken).ConfigureAwait(false);

            if (result.IsNotModified || result.StatusCode != HttpStatusCode.Accepted)
            {
                return result;
            }

            var delay = TimeSpan.FromSeconds(2 + attempt);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }

        throw new GitHubApiException(
            HttpStatusCode.Accepted,
            path,
            "GitHub stats не готовы после нескольких попыток.",
            _rateLimitGuard.Current);
    }

    /// <summary>Проверяет PAT через <c>GET /user</c>.</summary>
    public Task<GitHubApiResult> GetAuthenticatedUserAsync(CancellationToken cancellationToken = default) =>
        GetAsync("/user", cancellationToken);

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? ifNoneMatch = null)
    {
        var request = new HttpRequestMessage(method, BuildUri(path));

        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }

        if (!string.IsNullOrWhiteSpace(ifNoneMatch))
        {
            request.Headers.IfNoneMatch.Clear();
            request.Headers.IfNoneMatch.Add(EntityTagHeaderValue.Parse(ifNoneMatch));
        }

        return request;
    }

    private static HttpClient CreateDefaultHttpClient() =>
        new()
        {
            BaseAddress = new Uri(ApiBaseUrl),
        };

    private void ApplyDefaultHeaders()
    {
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        _httpClient.DefaultRequestHeaders.Remove("X-GitHub-Api-Version");
        _httpClient.DefaultRequestHeaders.Add("X-GitHub-Api-Version", GitHubApiVersion);

        _httpClient.DefaultRequestHeaders.Remove("User-Agent");
        _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
    }

    private static Uri BuildUri(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Путь API не может быть пустым.", nameof(path));
        }

        var normalized = path.StartsWith('/') ? path : $"/{path}";
        return new Uri(normalized, UriKind.Relative);
    }

    private static string? NormalizeToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        return token.Trim();
    }

    private static string BuildUserAgent()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionText = version is null ? "0.0" : version.ToString(3);
        return $"GitHubWallpaper/{versionText}";
    }

    private static TimeSpan? TryParseRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter?.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter?.Date is { } date)
        {
            var delay = date - DateTimeOffset.UtcNow;
            return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }

        return null;
    }
}
