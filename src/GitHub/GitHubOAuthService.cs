namespace GitHubWallpaper.GitHub;

/// <summary>
/// Единая точка входа для OAuth-авторизации GitHub через браузер.
/// Сначала пробует веб-flow (authorize + PKCE), затем Device Flow.
/// </summary>
internal sealed class GitHubOAuthService : IDisposable
{
    private readonly GitHubOAuthWebAuth _webAuth = new();
    private readonly GitHubOAuthDeviceAuth _deviceAuth = new();
    private readonly string? _clientId;
    private readonly string? _clientSecret;

    public GitHubOAuthService(string? settingsClientId = null, string? storedClientSecret = null)
    {
        _clientId = GitHubOAuthDefaults.ResolveClientId(settingsClientId);
        _clientSecret = GitHubOAuthDefaults.ResolveClientSecret(storedClientSecret);
    }

    /// <summary>Результат успешного входа.</summary>
    public sealed record SignInResult(string AccessToken, GitHubOAuthMethod Method, string? UserCode);

    public async Task<SignInResult> SignInAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var clientId = RequireClientId();

        progress?.Report("Открываю github.com в браузере…");

        try
        {
            var token = await _webAuth
                .SignInAsync(clientId, _clientSecret, cancellationToken)
                .ConfigureAwait(false);
            return new SignInResult(token, GitHubOAuthMethod.WebBrowser, null);
        }
        catch (GitHubOAuthException webEx) when (ShouldFallbackToDeviceFlow(webEx))
        {
            progress?.Report("Переключаюсь на вход по коду устройства…");
            return await SignInWithDeviceFlowAsync(clientId, progress, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    public async Task<SignInResult> SignInWithDeviceFlowAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var clientId = RequireClientId();
        return await SignInWithDeviceFlowAsync(clientId, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    private string RequireClientId() =>
        _clientId
        ?? throw new GitHubOAuthException(
            "OAuth client_id не настроен. Создайте OAuth App на GitHub, включите Device Flow, " +
            "укажите callback http://127.0.0.1:8791/callback и сохраните Client ID в настройках.");

    private async Task<SignInResult> SignInWithDeviceFlowAsync(
        string clientId,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var authorization = await _deviceAuth
            .RequestDeviceCodeAsync(clientId, cancellationToken)
            .ConfigureAwait(false);

        progress?.Report($"Введите на GitHub код: {authorization.UserCode}");

        var token = await _deviceAuth
            .PollForAccessTokenAsync(clientId, authorization, progress, cancellationToken)
            .ConfigureAwait(false);

        return new SignInResult(token, GitHubOAuthMethod.DeviceFlow, authorization.UserCode);
    }

    public void Dispose()
    {
        _webAuth.Dispose();
        _deviceAuth.Dispose();
    }

    private static bool ShouldFallbackToDeviceFlow(GitHubOAuthException exception) =>
        exception.Message.Contains("локальный порт", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("callback", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("client_id and/or client_secret", StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains("incorrect_client_credentials", StringComparison.OrdinalIgnoreCase);
}

internal enum GitHubOAuthMethod
{
    WebBrowser,
    DeviceFlow,
}
