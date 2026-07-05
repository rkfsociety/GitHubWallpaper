namespace GitHubWallpaper.GitHub;

/// <summary>
/// Идентификатор репозитория GitHub: владелец и имя репо.
/// </summary>
internal readonly record struct RepoReference(string Owner, string Repo)
{
    public string Slug => $"{Owner}/{Repo}";

    public Uri HtmlUrl => new($"https://github.com/{Owner}/{Repo}");
}
