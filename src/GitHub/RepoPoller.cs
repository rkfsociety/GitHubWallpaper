using GitHubWallpaper.Settings;

namespace GitHubWallpaper.GitHub;

/// <summary>
/// Периодический опрос GitHub-репозиториев: метаданные, активность и лента событий.
/// </summary>
internal sealed class RepoPoller : IDisposable
{
    private static readonly TimeSpan LoopTick = TimeSpan.FromSeconds(30);

    /// <summary>Репозиторий по умолчанию до появления настроек (этап 3).</summary>
    public static readonly RepoReference DefaultRepository = new("microsoft", "vscode");

    private readonly GitHubApiClient _client;
    private readonly ActivityAggregator _activityAggregator = new();
    private readonly object _sync = new();
    private readonly Dictionary<string, RepoPollState> _states = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<RepoReference> _repositories = [DefaultRepository];
    private ActivityPollIntervals _intervals = PollIntervals.ForActivityPreset(PollIntervalPreset.Normal);
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _paused;
    private bool _disposed;

    public RepoPoller(GitHubApiClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
    }

    /// <summary>Метаданные репозитория обновлены.</summary>
    public event EventHandler<RepoPollUpdatedEventArgs<RepoMetadataSnapshot>>? MetadataUpdated;

    /// <summary>Список коммитов обновлён.</summary>
    public event EventHandler<RepoPollUpdatedEventArgs<IReadOnlyList<RepoCommitSnapshot>>>? CommitsUpdated;

    /// <summary>Открытые pull request'ы обновлены.</summary>
    public event EventHandler<RepoPollUpdatedEventArgs<IReadOnlyList<RepoPullSnapshot>>>? PullsUpdated;

    /// <summary>Открытые issues обновлены.</summary>
    public event EventHandler<RepoPollUpdatedEventArgs<IReadOnlyList<RepoIssueSnapshot>>>? IssuesUpdated;

    /// <summary>Релизы обновлены.</summary>
    public event EventHandler<RepoPollUpdatedEventArgs<IReadOnlyList<RepoReleaseSnapshot>>>? ReleasesUpdated;

    /// <summary>Последний CI run обновлён.</summary>
    public event EventHandler<RepoPollUpdatedEventArgs<RepoCiRunSnapshot?>>? CiRunUpdated;

    /// <summary>Heatmap commit activity обновлён.</summary>
    public event EventHandler<RepoPollUpdatedEventArgs<RepoHeatmapSnapshot>>? HeatmapUpdated;

    /// <summary>Лента активности обновлена.</summary>
    public event EventHandler<RepoPollUpdatedEventArgs<IReadOnlyList<ActivityFeedItem>>>? ActivityFeedUpdated;

    /// <summary>Список отслеживаемых репозиториев изменён.</summary>
    public event EventHandler? RepositoriesChanged;

    /// <summary>Ошибка при опросе; цикл продолжает работу.</summary>
    public event EventHandler<RepoPollFailedEventArgs>? PollFailed;

    /// <summary>Опрос приостановлен (например, при паузе обоев).</summary>
    public bool IsPaused
    {
        get
        {
            lock (_sync)
            {
                return _paused;
            }
        }
    }

    /// <summary>Текущий список отслеживаемых репозиториев.</summary>
    public IReadOnlyList<RepoReference> Repositories
    {
        get
        {
            lock (_sync)
            {
                return _repositories;
            }
        }
    }

    /// <summary>Задаёт интервалы опроса по пресету из настроек.</summary>
    public void ConfigurePollIntervals(PollIntervalPreset preset)
    {
        lock (_sync)
        {
            _intervals = PollIntervals.ForActivityPreset(preset);
        }
    }

    /// <summary>Запускает фоновый опрос; при повторном вызове перезапускает цикл.</summary>
    public void Start(IEnumerable<RepoReference>? repositories = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var list = (repositories ?? [DefaultRepository]).ToList();
        if (list.Count == 0)
        {
            list.Add(DefaultRepository);
        }

        lock (_sync)
        {
            _repositories = list;
        }

        RepositoriesChanged?.Invoke(this, EventArgs.Empty);

        StopCore();

        _cts = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_cts.Token);
    }

    /// <summary>Останавливает фоновый опрос.</summary>
    public void Stop()
    {
        if (_disposed)
        {
            return;
        }

        StopCore();
    }

    /// <summary>Приостанавливает или возобновляет опрос без остановки цикла.</summary>
    public void SetPaused(bool paused)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        lock (_sync)
        {
            _paused = paused;
        }
    }

    public RepoMetadataSnapshot? GetCachedMetadata(RepoReference repository) =>
        GetState(repository).Metadata;

    public IReadOnlyList<RepoCommitSnapshot>? GetCachedCommits(RepoReference repository) =>
        GetState(repository).Commits;

    public IReadOnlyList<RepoPullSnapshot>? GetCachedPulls(RepoReference repository) =>
        GetState(repository).Pulls;

    public IReadOnlyList<RepoIssueSnapshot>? GetCachedIssues(RepoReference repository) =>
        GetState(repository).Issues;

    public IReadOnlyList<RepoReleaseSnapshot>? GetCachedReleases(RepoReference repository) =>
        GetState(repository).Releases;

    public RepoCiRunSnapshot? GetCachedCiRun(RepoReference repository) =>
        GetState(repository).CiRun;

    public RepoHeatmapSnapshot? GetCachedHeatmap(RepoReference repository) =>
        GetState(repository).Heatmap;

    public IReadOnlyList<ActivityFeedItem>? GetCachedActivityFeed(RepoReference repository) =>
        GetState(repository).ActivityFeed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopCore();
        _disposed = true;
    }

    private void StopCore()
    {
        if (_cts is null)
        {
            return;
        }

        _cts.Cancel();

        try
        {
            _loopTask?.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
        _cts = null;
        _loopTask = null;
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        await PollAllDueAsync(includeNeverPolled: true, cancellationToken).ConfigureAwait(false);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(LoopTick, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await PollAllDueAsync(includeNeverPolled: false, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PollAllDueAsync(bool includeNeverPolled, CancellationToken cancellationToken)
    {
        if (IsPaused)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var repositories = Repositories;
        ActivityPollIntervals intervals;

        lock (_sync)
        {
            intervals = _intervals;
        }

        foreach (var repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var state = GetState(repository);

            if (IsDue(state.LastMetadataPoll, intervals.Metadata, includeNeverPolled))
            {
                await PollMetadataAsync(repository, cancellationToken).ConfigureAwait(false);
            }

            if (state.FatalError is null)
            {
                if (IsDue(state.LastCommitsPoll, intervals.Commits, includeNeverPolled))
                {
                    await PollCommitsAsync(repository, cancellationToken).ConfigureAwait(false);
                }

                if (IsDue(state.LastPullsPoll, intervals.PullRequests, includeNeverPolled))
                {
                    await PollPullsAsync(repository, cancellationToken).ConfigureAwait(false);
                }

                if (IsDue(state.LastIssuesPoll, intervals.Issues, includeNeverPolled))
                {
                    await PollIssuesAsync(repository, cancellationToken).ConfigureAwait(false);
                }

                if (IsDue(state.LastReleasesPoll, intervals.Releases, includeNeverPolled))
                {
                    await PollReleasesAsync(repository, cancellationToken).ConfigureAwait(false);
                }

                if (IsDue(state.LastCiPoll, intervals.CiRuns, includeNeverPolled))
                {
                    await PollCiRunAsync(repository, cancellationToken).ConfigureAwait(false);
                }

                if (IsDue(state.LastHeatmapPoll, intervals.Heatmap, includeNeverPolled))
                {
                    await PollHeatmapAsync(repository, cancellationToken).ConfigureAwait(false);
                }

                if (IsDue(state.LastEventsPoll, intervals.Events, includeNeverPolled))
                {
                    await PollEventsAsync(repository, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static bool IsDue(DateTimeOffset? lastPoll, TimeSpan interval, bool includeNeverPolled) =>
        includeNeverPolled
        || lastPoll is null
        || DateTimeOffset.UtcNow - lastPoll.Value >= interval;

    private async Task PollMetadataAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        var state = GetState(repository);

        try
        {
            var path = $"/repos/{repository.Owner}/{repository.Repo}";
            var result = await GetCachedAsync(state, EndpointKind.Metadata, path, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsNotModified)
            {
                state.LastMetadataPoll = DateTimeOffset.UtcNow;
                return;
            }

            var snapshot = RepoApiParser.ParseMetadata(repository, result.Body);

            lock (_sync)
            {
                state.Metadata = snapshot;
                state.LastMetadataPoll = DateTimeOffset.UtcNow;
                state.FatalError = null;
            }

            MetadataUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<RepoMetadataSnapshot>(repository, snapshot));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var error = GitHubPollError.FromException(ex);

            if (error.IsFatalForRepo)
            {
                lock (_sync)
                {
                    state.FatalError = error.Code;
                }
            }

            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.Metadata, ex));
        }
    }

    private async Task PollCommitsAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        try
        {
            var path = $"/repos/{repository.Owner}/{repository.Repo}/commits?per_page=5";
            var state = GetState(repository);
            var result = await GetCachedAsync(state, EndpointKind.Commits, path, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsNotModified)
            {
                state.LastCommitsPoll = DateTimeOffset.UtcNow;
                return;
            }

            var commits = RepoApiParser.ParseCommits(result.Body);

            lock (_sync)
            {
                state.Commits = commits;
                state.LastCommitsPoll = DateTimeOffset.UtcNow;
            }

            CommitsUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<IReadOnlyList<RepoCommitSnapshot>>(repository, commits));

            var feed = _activityAggregator.IntegrateCommits(repository, commits);
            PublishActivityFeed(repository, state, feed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.Commits, ex));
        }
    }

    private async Task PollPullsAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        try
        {
            var path = $"/repos/{repository.Owner}/{repository.Repo}/pulls?state=open&per_page=10";
            var state = GetState(repository);
            var result = await GetCachedAsync(state, EndpointKind.Pulls, path, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsNotModified)
            {
                state.LastPullsPoll = DateTimeOffset.UtcNow;
                return;
            }

            var pulls = ActivityApiParser.ParsePulls(result.Body);

            lock (_sync)
            {
                state.Pulls = pulls;
                state.LastPullsPoll = DateTimeOffset.UtcNow;
            }

            PullsUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<IReadOnlyList<RepoPullSnapshot>>(repository, pulls));

            var feed = _activityAggregator.IntegratePulls(repository, pulls);
            PublishActivityFeed(repository, state, feed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.PullRequests, ex));
        }
    }

    private async Task PollIssuesAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        try
        {
            var path = $"/repos/{repository.Owner}/{repository.Repo}/issues?state=open&per_page=10";
            var state = GetState(repository);
            var result = await GetCachedAsync(state, EndpointKind.Issues, path, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsNotModified)
            {
                state.LastIssuesPoll = DateTimeOffset.UtcNow;
                return;
            }

            var issues = ActivityApiParser.ParseIssues(result.Body);

            lock (_sync)
            {
                state.Issues = issues;
                state.LastIssuesPoll = DateTimeOffset.UtcNow;
            }

            IssuesUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<IReadOnlyList<RepoIssueSnapshot>>(repository, issues));

            var feed = _activityAggregator.IntegrateIssues(repository, issues);
            PublishActivityFeed(repository, state, feed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.Issues, ex));
        }
    }

    private async Task PollReleasesAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        try
        {
            var path = $"/repos/{repository.Owner}/{repository.Repo}/releases?per_page=5";
            var state = GetState(repository);
            var result = await GetCachedAsync(state, EndpointKind.Releases, path, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsNotModified)
            {
                state.LastReleasesPoll = DateTimeOffset.UtcNow;
                return;
            }

            var releases = ActivityApiParser.ParseReleases(result.Body);

            lock (_sync)
            {
                state.Releases = releases;
                state.LastReleasesPoll = DateTimeOffset.UtcNow;
            }

            ReleasesUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<IReadOnlyList<RepoReleaseSnapshot>>(repository, releases));

            var feed = _activityAggregator.IntegrateReleases(repository, releases);
            PublishActivityFeed(repository, state, feed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.Releases, ex));
        }
    }

    private async Task PollCiRunAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        try
        {
            var path = $"/repos/{repository.Owner}/{repository.Repo}/actions/runs?per_page=1";
            var state = GetState(repository);
            var result = await GetCachedAsync(state, EndpointKind.CiRun, path, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsNotModified)
            {
                state.LastCiPoll = DateTimeOffset.UtcNow;
                return;
            }

            var run = ActivityApiParser.ParseLatestCiRun(result.Body);

            lock (_sync)
            {
                state.CiRun = run;
                state.LastCiPoll = DateTimeOffset.UtcNow;
            }

            CiRunUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<RepoCiRunSnapshot?>(repository, run));

            var feed = _activityAggregator.IntegrateCiRun(repository, run);
            PublishActivityFeed(repository, state, feed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.CiRun, ex));
        }
    }

    private async Task PollHeatmapAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        try
        {
            var path = $"/repos/{repository.Owner}/{repository.Repo}/stats/commit_activity";
            var state = GetState(repository);
            var etag = state.GetETag(EndpointKind.Heatmap);
            var result = await _client.GetStatsAsync(path, etag, cancellationToken).ConfigureAwait(false);

            if (result.IsNotModified)
            {
                state.LastHeatmapPoll = DateTimeOffset.UtcNow;
                return;
            }

            state.SetETag(EndpointKind.Heatmap, result.ETag);
            var heatmap = ActivityApiParser.ParseHeatmap(result.Body);

            lock (_sync)
            {
                state.Heatmap = heatmap;
                state.LastHeatmapPoll = DateTimeOffset.UtcNow;
            }

            HeatmapUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<RepoHeatmapSnapshot>(repository, heatmap));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.Heatmap, ex));
        }
    }

    private async Task PollEventsAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        try
        {
            var path = $"/repos/{repository.Owner}/{repository.Repo}/events?per_page=30";
            var state = GetState(repository);
            var result = await GetCachedAsync(state, EndpointKind.Events, path, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsNotModified)
            {
                state.LastEventsPoll = DateTimeOffset.UtcNow;
                return;
            }

            var events = ActivityApiParser.ParseEvents(result.Body, repository);

            lock (_sync)
            {
                state.LastEventsPoll = DateTimeOffset.UtcNow;
            }

            var feed = _activityAggregator.IntegrateEvents(repository, events);
            PublishActivityFeed(repository, state, feed);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.Events, ex));
        }
    }

    private void PublishActivityFeed(
        RepoReference repository,
        RepoPollState state,
        IReadOnlyList<ActivityFeedItem> feed)
    {
        lock (_sync)
        {
            state.ActivityFeed = feed;
        }

        ActivityFeedUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<IReadOnlyList<ActivityFeedItem>>(repository, feed));
    }

    private async Task<GitHubApiResult> GetCachedAsync(
        RepoPollState state,
        EndpointKind kind,
        string path,
        CancellationToken cancellationToken)
    {
        var etag = state.GetETag(kind);
        var result = await _client.GetAsync(path, etag, cancellationToken).ConfigureAwait(false);

        if (!result.IsNotModified && result.ETag is not null)
        {
            state.SetETag(kind, result.ETag);
        }

        return result;
    }

    private RepoPollState GetState(RepoReference repository)
    {
        lock (_sync)
        {
            if (!_states.TryGetValue(repository.Slug, out var state))
            {
                state = new RepoPollState();
                _states[repository.Slug] = state;
            }

            return state;
        }
    }

    private enum EndpointKind
    {
        Metadata,
        Commits,
        Pulls,
        Issues,
        Releases,
        CiRun,
        Heatmap,
        Events,
    }

    private sealed class RepoPollState
    {
        private readonly Dictionary<EndpointKind, string?> _etags = new();

        public DateTimeOffset? LastMetadataPoll { get; set; }

        public DateTimeOffset? LastCommitsPoll { get; set; }

        public DateTimeOffset? LastPullsPoll { get; set; }

        public DateTimeOffset? LastIssuesPoll { get; set; }

        public DateTimeOffset? LastReleasesPoll { get; set; }

        public DateTimeOffset? LastCiPoll { get; set; }

        public DateTimeOffset? LastHeatmapPoll { get; set; }

        public DateTimeOffset? LastEventsPoll { get; set; }

        public RepoMetadataSnapshot? Metadata { get; set; }

        public IReadOnlyList<RepoCommitSnapshot>? Commits { get; set; }

        public IReadOnlyList<RepoPullSnapshot>? Pulls { get; set; }

        public IReadOnlyList<RepoIssueSnapshot>? Issues { get; set; }

        public IReadOnlyList<RepoReleaseSnapshot>? Releases { get; set; }

        public RepoCiRunSnapshot? CiRun { get; set; }

        public RepoHeatmapSnapshot? Heatmap { get; set; }

        public IReadOnlyList<ActivityFeedItem>? ActivityFeed { get; set; }

        public GitHubPollErrorCode? FatalError { get; set; }

        public string? GetETag(EndpointKind kind) =>
            _etags.TryGetValue(kind, out var etag) ? etag : null;

        public void SetETag(EndpointKind kind, string? etag)
        {
            if (string.IsNullOrWhiteSpace(etag))
            {
                return;
            }

            _etags[kind] = etag;
        }
    }
}
