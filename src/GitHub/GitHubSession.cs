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

    public void ReloadTokenFromStore() =>
        _client.SetToken(GitHubPatCredentialStore.Read());

    public void SaveToken(string token)
    {
        GitHubPatCredentialStore.Save(token);
        _client.SetToken(token);
    }

    public void ClearToken()
    {
        GitHubPatCredentialStore.Delete();
        _client.SetToken(null);
    }

    public void Dispose() => _client.Dispose();
}
