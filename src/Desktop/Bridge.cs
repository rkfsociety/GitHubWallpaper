using System.Diagnostics;
using System.Text.Json;
using GitHubWallpaper.GitHub;
using Microsoft.Web.WebView2.Core;

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
    private bool _webMessageHooked;
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
        _repoPoller.PullsUpdated += OnPullsUpdated;
        _repoPoller.IssuesUpdated += OnIssuesUpdated;
        _repoPoller.ReleasesUpdated += OnReleasesUpdated;
        _repoPoller.CiRunUpdated += OnCiRunUpdated;
        _repoPoller.HeatmapUpdated += OnHeatmapUpdated;
        _repoPoller.ActivityFeedUpdated += OnActivityFeedUpdated;
        _repoPoller.PollFailed += OnPollFailed;
        _repoPoller.RepositoriesChanged += OnRepositoriesChanged;
        _wallpaperController.Applied += OnWallpaperApplied;
        _githubSession.TokenChanged += OnTokenChanged;

        _started = true;
        EnsureWebMessageHandler();
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
            _repoPoller.PullsUpdated -= OnPullsUpdated;
            _repoPoller.IssuesUpdated -= OnIssuesUpdated;
            _repoPoller.ReleasesUpdated -= OnReleasesUpdated;
            _repoPoller.CiRunUpdated -= OnCiRunUpdated;
            _repoPoller.HeatmapUpdated -= OnHeatmapUpdated;
            _repoPoller.ActivityFeedUpdated -= OnActivityFeedUpdated;
            _repoPoller.PollFailed -= OnPollFailed;
            _repoPoller.RepositoriesChanged -= OnRepositoriesChanged;
            _wallpaperController.Applied -= OnWallpaperApplied;
            _githubSession.TokenChanged -= OnTokenChanged;
        }

        RemoveWebMessageHandler();
        _disposed = true;
    }

    private void OnWallpaperApplied(object? sender, EventArgs e)
    {
        EnsureWebMessageHandler();
        PushInitialState();
    }

    private void OnRepositoriesChanged(object? sender, EventArgs e)
    {
        PushRepoList();
        PushCachedState();
    }

    private void OnTokenChanged(object? sender, EventArgs e) => PushAuthStatus();

    private void OnMetadataUpdated(object? sender, RepoPollUpdatedEventArgs<RepoMetadataSnapshot> e) =>
        Post(CreateMetadataMessage(e.Repository, e.Data));

    private void OnCommitsUpdated(object? sender, RepoPollUpdatedEventArgs<IReadOnlyList<RepoCommitSnapshot>> e) =>
        Post(CreateCommitsMessage(e.Repository, e.Data));

    private void OnPullsUpdated(object? sender, RepoPollUpdatedEventArgs<IReadOnlyList<RepoPullSnapshot>> e) =>
        Post(CreatePullsMessage(e.Repository, e.Data));

    private void OnIssuesUpdated(object? sender, RepoPollUpdatedEventArgs<IReadOnlyList<RepoIssueSnapshot>> e) =>
        Post(CreateIssuesMessage(e.Repository, e.Data));

    private void OnReleasesUpdated(object? sender, RepoPollUpdatedEventArgs<IReadOnlyList<RepoReleaseSnapshot>> e) =>
        Post(CreateReleasesMessage(e.Repository, e.Data));

    private void OnCiRunUpdated(object? sender, RepoPollUpdatedEventArgs<RepoCiRunSnapshot?> e) =>
        Post(CreateCiRunMessage(e.Repository, e.Data));

    private void OnHeatmapUpdated(object? sender, RepoPollUpdatedEventArgs<RepoHeatmapSnapshot> e) =>
        Post(CreateHeatmapMessage(e.Repository, e.Data));

    private void OnActivityFeedUpdated(object? sender, RepoPollUpdatedEventArgs<IReadOnlyList<ActivityFeedItem>> e) =>
        Post(CreateActivityFeedMessage(e.Repository, e.Data));

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

            var pulls = _repoPoller.GetCachedPulls(repository);
            if (pulls is not null)
            {
                Post(CreatePullsMessage(repository, pulls));
            }

            var issues = _repoPoller.GetCachedIssues(repository);
            if (issues is not null)
            {
                Post(CreateIssuesMessage(repository, issues));
            }

            var releases = _repoPoller.GetCachedReleases(repository);
            if (releases is not null)
            {
                Post(CreateReleasesMessage(repository, releases));
            }

            var ciRun = _repoPoller.GetCachedCiRun(repository);
            if (ciRun is not null)
            {
                Post(CreateCiRunMessage(repository, ciRun));
            }

            var heatmap = _repoPoller.GetCachedHeatmap(repository);
            if (heatmap is not null)
            {
                Post(CreateHeatmapMessage(repository, heatmap));
            }

            var feed = _repoPoller.GetCachedActivityFeed(repository);
            if (feed is not null)
            {
                Post(CreateActivityFeedMessage(repository, feed));
            }
        }
    }

    private void EnsureWebMessageHandler()
    {
        _wallpaperController.InvokeOnSurfaceThread(() =>
        {
            if (_webMessageHooked)
                return;

            var core = _wallpaperController.Surface?.WebView.CoreWebView2;
            if (core is null)
                return;

            core.WebMessageReceived += OnWebMessageReceived;
            _webMessageHooked = true;
        });
    }

    private void RemoveWebMessageHandler()
    {
        if (!_webMessageHooked)
            return;

        _wallpaperController.InvokeOnSurfaceThread(() =>
        {
            if (!_webMessageHooked)
                return;

            var core = _wallpaperController.Surface?.WebView.CoreWebView2;
            if (core is not null)
                core.WebMessageReceived -= OnWebMessageReceived;

            _webMessageHooked = false;
        });
    }

    private static void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("type", out var typeElement)
                || typeElement.GetString() != "open-url")
            {
                return;
            }

            if (!root.TryGetProperty("url", out var urlElement))
            {
                return;
            }

            var url = urlElement.GetString();
            if (string.IsNullOrWhiteSpace(url)
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = uri.ToString(),
                UseShellExecute = true,
            });
        }
        catch (JsonException)
        {
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

    private static object CreatePullsMessage(
        RepoReference repository,
        IReadOnlyList<RepoPullSnapshot> pulls) =>
        new
        {
            type = "repo:pulls",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = pulls.Select(pull => new
            {
                number = pull.Number,
                title = pull.Title,
                userLogin = pull.UserLogin,
                createdAt = pull.CreatedAt,
                htmlUrl = pull.HtmlUrl,
            }).ToArray(),
        };

    private static object CreateIssuesMessage(
        RepoReference repository,
        IReadOnlyList<RepoIssueSnapshot> issues) =>
        new
        {
            type = "repo:issues",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = issues.Select(issue => new
            {
                number = issue.Number,
                title = issue.Title,
                userLogin = issue.UserLogin,
                createdAt = issue.CreatedAt,
                htmlUrl = issue.HtmlUrl,
            }).ToArray(),
        };

    private static object CreateReleasesMessage(
        RepoReference repository,
        IReadOnlyList<RepoReleaseSnapshot> releases) =>
        new
        {
            type = "repo:releases",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = releases.Select(release => new
            {
                id = release.Id,
                tagName = release.TagName,
                name = release.Name,
                isPrerelease = release.IsPrerelease,
                publishedAt = release.PublishedAt,
                htmlUrl = release.HtmlUrl,
            }).ToArray(),
        };

    private static object CreateCiRunMessage(RepoReference repository, RepoCiRunSnapshot? run) =>
        new
        {
            type = "repo:ci-run",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = run is null
                ? null
                : new
                {
                    id = run.Id,
                    name = run.Name,
                    status = run.Status,
                    conclusion = run.Conclusion,
                    updatedAt = run.UpdatedAt,
                    htmlUrl = run.HtmlUrl,
                },
        };

    private static object CreateHeatmapMessage(RepoReference repository, RepoHeatmapSnapshot heatmap) =>
        new
        {
            type = "repo:heatmap",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = new
            {
                weeks = heatmap.Weeks.Select(week => new
                {
                    total = week.Total,
                    days = week.Days,
                }).ToArray(),
                fetchedAt = heatmap.FetchedAt,
            },
        };

    private static object CreateActivityFeedMessage(
        RepoReference repository,
        IReadOnlyList<ActivityFeedItem> feed) =>
        new
        {
            type = "repo:activity-feed",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = feed.Select(item => new
            {
                id = item.Id,
                kind = item.Kind,
                title = item.Title,
                subtitle = item.Subtitle,
                timestamp = item.Timestamp,
                htmlUrl = item.HtmlUrl,
                isNew = item.IsNew,
            }).ToArray(),
        };

    private static object CreatePollFailedMessage(
        RepoReference repository,
        RepoPollKind kind,
        Exception exception)
    {
        var error = GitHubPollError.FromException(exception);

        return new
        {
            type = "repo:poll-failed",
            owner = repository.Owner,
            repo = repository.Repo,
            payload = new
            {
                kind = kind.ToString().ToLowerInvariant(),
                code = error.CodeName,
                message = error.Message,
                hint = error.Hint,
            },
        };
    }

    internal static string Serialize(object message) =>
        JsonSerializer.Serialize(message, JsonOptions);
}
