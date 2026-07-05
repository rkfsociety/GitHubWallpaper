using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using GitHubWallpaper.Desktop;

namespace GitHubWallpaper.GitHub;

/// <summary>
/// OAuth Authorization Code + PKCE через loopback на <c>127.0.0.1</c>.
/// Открывает страницу авторизации на github.com в системном браузере.
/// </summary>
internal sealed class GitHubOAuthWebAuth : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public GitHubOAuthWebAuth(HttpClient? httpClient = null)
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

    /// <summary>
    /// Запускает веб-авторизацию: браузер → github.com → callback на localhost.
    /// </summary>
    public async Task<string> SignInAsync(
        string clientId,
        string? clientSecret = null,
        CancellationToken cancellationToken = default)
    {
        var (verifier, challenge, state) = GitHubPkce.CreateAuthorizationRequest();
        var authorizeUri = BuildAuthorizeUri(clientId, challenge, state);

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{GitHubOAuthDefaults.LoopbackPort}/");
        listener.Start();

        try
        {
            BrowserLauncher.Open(authorizeUri.ToString());

            var context = await WaitForCallbackAsync(listener, cancellationToken).ConfigureAwait(false);
            var query = ParseQueryString(context.Request.Url?.Query);

            if (!query.TryGetValue("state", out var returnedState)
                || !string.Equals(returnedState, state, StringComparison.Ordinal))
            {
                await WriteResponseAsync(context, success: false).ConfigureAwait(false);
                throw new GitHubOAuthException("Неверный параметр state — авторизация отклонена.");
            }

            if (query.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
            {
                await WriteResponseAsync(context, success: false).ConfigureAwait(false);
                query.TryGetValue("error_description", out var description);
                throw new GitHubOAuthException(
                    string.IsNullOrWhiteSpace(description)
                        ? $"GitHub отклонил авторизацию: {error}."
                        : description);
            }

            if (!query.TryGetValue("code", out var code) || string.IsNullOrWhiteSpace(code))
            {
                await WriteResponseAsync(context, success: false).ConfigureAwait(false);
                throw new GitHubOAuthException("GitHub не вернул код авторизации.");
            }

            var token = await ExchangeCodeAsync(clientId, code, verifier, clientSecret, cancellationToken)
                .ConfigureAwait(false);

            await WriteResponseAsync(context, success: true).ConfigureAwait(false);
            return token;
        }
        catch (HttpListenerException ex) when (ex.ErrorCode is 5 or 183 or 32 or 48 or 10048)
        {
            throw new GitHubOAuthException(
                $"Не удалось открыть локальный порт {GitHubOAuthDefaults.LoopbackPort} для callback. " +
                "Попробуйте вход через код устройства.",
                ex);
        }
        finally
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private static Uri BuildAuthorizeUri(string clientId, string challenge, string state)
    {
        var query = BuildQuery(
            ("client_id", clientId),
            ("redirect_uri", GitHubOAuthDefaults.RedirectUri),
            ("scope", GitHubOAuthDefaults.Scope),
            ("state", state),
            ("code_challenge", challenge),
            ("code_challenge_method", "S256"));

        return new Uri($"{GitHubOAuthDefaults.AuthorizeUrl}?{query}");
    }

    private static string BuildQuery(params (string Key, string Value)[] pairs) =>
        string.Join(
            "&",
            pairs.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

    private static Dictionary<string, string> ParseQueryString(string? queryString)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(queryString))
        {
            return result;
        }

        var trimmed = queryString.StartsWith('?') ? queryString[1..] : queryString;
        foreach (var segment in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator < 0)
            {
                continue;
            }

            var key = Uri.UnescapeDataString(segment[..separator]);
            var value = Uri.UnescapeDataString(segment[(separator + 1)..]);
            result[key] = value;
        }

        return result;
    }

    private static async Task<HttpListenerContext> WaitForCallbackAsync(
        HttpListener listener,
        CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(listener.Stop);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().ConfigureAwait(false);
                var path = context.Request.Url?.AbsolutePath ?? string.Empty;

                if (path.Equals(GitHubOAuthDefaults.LoopbackPath, StringComparison.OrdinalIgnoreCase)
                    || path.Equals($"{GitHubOAuthDefaults.LoopbackPath}/", StringComparison.OrdinalIgnoreCase))
                {
                    return context;
                }

                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                context.Response.Close();
            }
        }
        catch (HttpListenerException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }
        catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        throw new OperationCanceledException(cancellationToken);
    }

    private static async Task WriteResponseAsync(HttpListenerContext context, bool success)
    {
        const string successHtml =
            """
            <!DOCTYPE html>
            <html lang="ru">
            <head><meta charset="utf-8"><title>GitHub Wallpaper</title></head>
            <body style="font-family:Segoe UI,sans-serif;background:#0d1117;color:#e6edf3;text-align:center;padding:48px">
              <h1>Авторизация успешна</h1>
              <p>Можно закрыть эту вкладку и вернуться в GitHub Wallpaper.</p>
            </body>
            </html>
            """;

        const string failureHtml =
            """
            <!DOCTYPE html>
            <html lang="ru">
            <head><meta charset="utf-8"><title>GitHub Wallpaper</title></head>
            <body style="font-family:Segoe UI,sans-serif;background:#0d1117;color:#e6edf3;text-align:center;padding:48px">
              <h1>Ошибка авторизации</h1>
              <p>Закройте вкладку и повторите вход в настройках приложения.</p>
            </body>
            </html>
            """;

        var body = Encoding.UTF8.GetBytes(success ? successHtml : failureHtml);
        context.Response.StatusCode = (int)HttpStatusCode.OK;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
        context.Response.Close();
    }

    private async Task<string> ExchangeCodeAsync(
        string clientId,
        string code,
        string verifier,
        string? clientSecret,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, GitHubOAuthDefaults.AccessTokenUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var payload = string.IsNullOrWhiteSpace(clientSecret)
            ? (object)new
            {
                client_id = clientId,
                redirect_uri = GitHubOAuthDefaults.RedirectUri,
                code,
                code_verifier = verifier,
            }
            : new
            {
                client_id = clientId,
                client_secret = clientSecret.Trim(),
                redirect_uri = GitHubOAuthDefaults.RedirectUri,
                code,
                code_verifier = verifier,
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
                $"Не удалось обменять код на токен (HTTP {(int)response.StatusCode}).");
        }

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (root.TryGetProperty("error", out var errorElement))
        {
            var description = root.TryGetProperty("error_description", out var descriptionElement)
                ? descriptionElement.GetString()
                : errorElement.GetString();

            throw new GitHubOAuthException(
                string.IsNullOrWhiteSpace(description)
                    ? "GitHub отклонил обмен кода на токен."
                    : description);
        }

        if (!root.TryGetProperty("access_token", out var tokenElement))
        {
            throw new GitHubOAuthException("GitHub не вернул access_token.");
        }

        var token = tokenElement.GetString();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new GitHubOAuthException("GitHub вернул пустой access_token.");
        }

        return token.Trim();
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
