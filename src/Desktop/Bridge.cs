using System.Text.Json;
using GitHubWallpaper.GitHub;

namespace GitHubWallpaper.Desktop;

/// <summary>
/// Передаёт события <see cref="RepoPoller"/> в страницу обоев через
/// <see cref="WallpaperController.PostMessageAsJson"/>.
/// </summary>
internal sealed class Bridge : IDisposable
{
    private const int UnauthenticatedRateLimit = 60;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly WallpaperController _wallpaperController;
    private readonly GitHubSession _githubSession;
    private readonly RepoPoller _repoPoller;
    private bool _started;
    private bool _disposed;

    public Bridge(
        WallpaperController wallpaperController,
        GitHubSession githubSession,
        RepoPoller repoPoller)
    {
        ArgumentNullException.ThrowIfNull(wallpaperController);
        ArgumentNullException.ThrowIfNull(githubSession);
        ArgumentNullException.ThrowIfNull(repoPoller);
        _wallpaperController = wallpaperController;
        _githubSession = githubSession;
        _repoPoller = repoPoller;
    }

    /// <summary>Подписывается на poller и отправляет кэш при старте и после применения обоев.</summary>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_started)
            return;

        _repoPoller.MetadataUpdated += OnMetadataUpdated;
        _repoPoller.CommitsUpdated += OnCommitsUpdated;
        _repoPoller.PollFailed += OnPollFailed;
        _wallpaperController.Applied += OnWallpaperApplied;
        _githubSession.TokenChanged += OnTokenChanged;

        _started = true;
        PushInitialState();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_started)
        {
            _repoPoller.MetadataUpdated -= OnMetadataUpdated;
            _repoPoller.CommitsUpdated -= OnCommitsUpdated;
            _repoPoller.PollFailed -= OnPollFailed;
            _wallpaperController.Applied -= OnWallpaperApplied;
            _githubSession.TokenChanged -= OnTokenChanged;
        }

        _disposed = true;
    }

    private void OnWallpaperApplied(object? sender, EventArgs e) => PushInitialState();

    private void OnTokenChanged(object? sender, EventArgs e) => PushAuthStatus();

    private void OnMetadataUpdated(object? sender, RepoPollUpdatedEventArgs<RepoMetadataSnapshot> e) =>
        Post(CreateMetadataMessage(e.Repository, e.Data));

    private void OnCommitsUpdated(object? sender, RepoPollUpdatedEventArgs<IReadOnlyList<RepoCommitSnapshot>> e) =>
        Post(CreateCommitsMessage(e.Repository, e.Data));

    private void OnPollFailed(object? sender, RepoPollFailedEventArgs e) =>
        Post(CreatePollFailedMessage(e.Repository, e.Kind, e.Exception));

    private void PushInitialState()
    {
        PushAuthStatus();
        PushRepoList();
        PushCachedState();
    }

    private void PushAuthStatus()
    {
        var rateLimit = _githubSession.Client.RateLimit.Current;
        var hasToken = _githubSession.HasToken;
        var limit = rateLimit.Limit ?? (hasToken ? 5000 : UnauthenticatedRateLimit);
        var remaining = rateLimit.Remaining;

        Post(new
        {
            type = "auth:status",
            payload = new
            {
                hasToken,
                rateLimit = limit,
                rateLimitRemaining = remaining,
                message = hasToken
                    ? null
                    : "GitHub token не задан — лимит 60 запросов/час. Добавьте PAT в Настройках.",
            },
        });
    }

    private void PushRepoList()
    {
        Post(new
        {
            type = "repos:init",
            payload = _repoPoller.Repositories
                .Select(repository => new
                {
                    owner = repository.Owner,
                    repo = repository.Repo,
                })
                .ToArray(),
        });
    }

    private void PushCachedState()
    {
        foreach (var repository in _repoPoller.Repositories)
        {
            var metadata = _repoPoller.GetCachedMetadata(repository);
            if (metadata is not null)
            {
                Post(CreateMetadataMessage(repository, metadata));
            }

            var commits = _repoPoller.GetCachedCommits(repository);
            if (commits is not null)
            {
                Post(CreateCommitsMessage(repository, commits));
            }
        }
    }

    private void Post(object message) => _wallpaperController.PostMessageAsJson(message);

    private static object CreateMetadataMessage(RepoReference repository, RepoMetadataSnapshot snapshot) =>
        new
        {
            type = "repo:metadata",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = new
            {
                fullName = snapshot.FullName,
                description = snapshot.Description,
                stars = snapshot.StargazersCount,
                forks = snapshot.ForksCount,
                openIssues = snapshot.OpenIssuesCount,
                htmlUrl = snapshot.HtmlUrl,
                fetchedAt = snapshot.FetchedAt,
            },
        };

    private static object CreateCommitsMessage(
        RepoReference repository,
        IReadOnlyList<RepoCommitSnapshot> commits) =>
        new
        {
            type = "repo:commits",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = commits.Select(commit => new
            {
                sha = commit.Sha,
                message = commit.Message,
                authorName = commit.AuthorName,
                authorDate = commit.AuthorDate,
                htmlUrl = commit.HtmlUrl,
            }).ToArray(),
        };

    private static object CreatePollFailedMessage(
        RepoReference repository,
        RepoPollKind kind,
        Exception exception) =>
        new
        {
            type = "repo:poll-failed",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = new
            {
                kind = kind.ToString().ToLowerInvariant(),
                message = exception.Message,
            },
        };

    internal static string Serialize(object message) =>
        JsonSerializer.Serialize(message, JsonOptions);
}
