namespace GitHubWallpaper.GitHub;

/// <summary>Ошибка OAuth-авторизации GitHub.</summary>
internal sealed class GitHubOAuthException : Exception
{
    public GitHubOAuthException(string message)
        : base(message)
    {
    }

    public GitHubOAuthException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
