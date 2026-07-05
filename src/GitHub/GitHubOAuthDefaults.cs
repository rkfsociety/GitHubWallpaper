namespace GitHubWallpaper.GitHub;

/// <summary>
/// Параметры OAuth-приложения GitHub для входа через браузер.
/// </summary>
internal static class GitHubOAuthDefaults
{
    /// <summary>
    /// Client ID OAuth App (публичный, вшивается в сборку).
    /// Переопределяется переменной окружения <c>GITHUBWALLPAPER_OAUTH_CLIENT_ID</c>
    /// или полем <see cref="AppSettings.GitHubOAuthClientId"/> в settings.json.
    /// </summary>
    public const string EmbeddedClientId = "";

    public const string Scope = "repo read:user";

    public const int LoopbackPort = 8791;

    public const string LoopbackPath = "/callback";

    public static string RedirectUri => $"http://127.0.0.1:{LoopbackPort}{LoopbackPath}";

    public static string AuthorizeUrl => "https://github.com/login/oauth/authorize";

    public static string AccessTokenUrl => "https://github.com/login/oauth/access_token";

    public static string DeviceCodeUrl => "https://github.com/login/device/code";

    public static string DeviceVerificationUrl => "https://github.com/login/device";

    /// <summary>Возвращает client_id или <c>null</c>, если не настроен.</summary>
    public static string? ResolveClientId(string? settingsClientId = null)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("GITHUBWALLPAPER_OAUTH_CLIENT_ID");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment.Trim();
        }

        if (!string.IsNullOrWhiteSpace(settingsClientId))
        {
            return settingsClientId.Trim();
        }

        return string.IsNullOrWhiteSpace(EmbeddedClientId) ? null : EmbeddedClientId.Trim();
    }

    public static string RegistrationUrl => "https://github.com/settings/applications/new";
}
