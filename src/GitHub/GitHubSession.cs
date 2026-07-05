using GitHubWallpaper.Settings;

namespace GitHubWallpaper.GitHub;

/// <summary>
/// Жизненный цикл GitHub API-клиента с PAT из Credential Manager.
/// </summary>
internal sealed class GitHubSession : IDisposable
{
    private readonly GitHubApiClient _client = new(GitHubPatCredentialStore.Read());

    public GitHubApiClient Client => _client;

    public bool HasStoredToken => GitHubPatCredentialStore.Exists();

    public bool HasToken => _client.HasToken;

    /// <summary>PAT изменён (сохранён, удалён или перезагружен из хранилища).</summary>
    public event EventHandler? TokenChanged;

    public void ReloadTokenFromStore()
    {
        _client.SetToken(GitHubPatCredentialStore.Read());
        TokenChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SaveToken(string token)
    {
        GitHubPatCredentialStore.Save(token);
        _client.SetToken(token);
        TokenChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearToken()
    {
        GitHubPatCredentialStore.Delete();
        _client.SetToken(null);
        TokenChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _client.Dispose();
}
