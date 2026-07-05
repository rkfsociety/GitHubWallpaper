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
    private string? _token;

    public GitHubApiClient(string? token = null, HttpClient? httpClient = null)
    {
        _token = NormalizeToken(token);

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

    /// <summary>Обновляет PAT; пустая строка снимает авторизацию.</summary>
    public void SetToken(string? token) => _token = NormalizeToken(token);

    /// <summary>
    /// Выполняет GET-запрос к GitHub REST API.
    /// </summary>
    /// <param name="path">Относительный путь, например <c>/repos/{owner}/{repo}</c>.</param>
    public async Task<GitHubApiResult> GetAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var rateLimit = GitHubRateLimit.TryParse(response);

        if (!response.IsSuccessStatusCode)
        {
            throw new GitHubApiException(response.StatusCode, path, body, rateLimit);
        }

        return new GitHubApiResult(
            response.StatusCode,
            body,
            rateLimit,
            response.Headers.ETag?.Tag);
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

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, BuildUri(path));

        if (_token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
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
}
