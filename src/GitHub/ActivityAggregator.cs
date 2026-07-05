namespace GitHubWallpaper.GitHub;

/// <summary>
/// Объединяет события GitHub API и дельты polling в единую ленту активности.
/// </summary>
internal sealed class ActivityAggregator
{
    private const int MaxFeedItems = 30;

    private readonly object _sync = new();
    private readonly Dictionary<string, FeedState> _feeds = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Обновляет ленту событиями из <c>GET /events</c>.</summary>
    public IReadOnlyList<ActivityFeedItem> IntegrateEvents(
        RepoReference repository,
        IReadOnlyList<RepoEventSnapshot> events)
    {
        var items = events
            .Select(evt => new ActivityFeedItem(
                $"event-{evt.Id}",
                MapEventKind(evt.Type),
                evt.Summary,
                evt.ActorLogin,
                evt.CreatedAt ?? DateTimeOffset.UtcNow,
                evt.HtmlUrl,
                IsNew: false))
            .ToList();

        return MergeFeed(repository, items);
    }

    /// <summary>Добавляет в ленту новые коммиты, обнаруженные при polling.</summary>
    public IReadOnlyList<ActivityFeedItem> IntegrateCommits(
        RepoReference repository,
        IReadOnlyList<RepoCommitSnapshot> commits)
    {
        var state = GetState(repository);
        var deltaItems = new List<ActivityFeedItem>();
        var isBootstrap = state.SeenCommitShas.Count == 0;

        foreach (var commit in commits)
        {
            if (string.IsNullOrWhiteSpace(commit.Sha) || !state.SeenCommitShas.Add(commit.Sha))
            {
                continue;
            }

            if (isBootstrap)
            {
                continue;
            }

            deltaItems.Add(new ActivityFeedItem(
                $"commit-{commit.Sha}",
                "push",
                commit.Message,
                commit.AuthorName,
                commit.AuthorDate ?? DateTimeOffset.UtcNow,
                commit.HtmlUrl,
                IsNew: true));
        }

        if (deltaItems.Count == 0)
        {
            return GetFeed(repository);
        }

        return MergeFeed(repository, deltaItems, prepend: true);
    }

    /// <summary>Добавляет в ленту новые pull request'ы.</summary>
    public IReadOnlyList<ActivityFeedItem> IntegratePulls(
        RepoReference repository,
        IReadOnlyList<RepoPullSnapshot> pulls)
    {
        return IntegrateNumberedItems(
            repository,
            pulls,
            state => state.SeenPullNumbers,
            pull => pull.Number,
            pull => new ActivityFeedItem(
                $"pr-{pull.Number}",
                "pr",
                pull.Title,
                pull.UserLogin,
                pull.CreatedAt ?? DateTimeOffset.UtcNow,
                pull.HtmlUrl,
                IsNew: true));
    }

    /// <summary>Добавляет в ленту новые issues.</summary>
    public IReadOnlyList<ActivityFeedItem> IntegrateIssues(
        RepoReference repository,
        IReadOnlyList<RepoIssueSnapshot> issues)
    {
        return IntegrateNumberedItems(
            repository,
            issues,
            state => state.SeenIssueNumbers,
            issue => issue.Number,
            issue => new ActivityFeedItem(
                $"issue-{issue.Number}",
                "issue",
                issue.Title,
                issue.UserLogin,
                issue.CreatedAt ?? DateTimeOffset.UtcNow,
                issue.HtmlUrl,
                IsNew: true));
    }

    /// <summary>Добавляет в ленту новые релизы.</summary>
    public IReadOnlyList<ActivityFeedItem> IntegrateReleases(
        RepoReference repository,
        IReadOnlyList<RepoReleaseSnapshot> releases)
    {
        return IntegrateNumberedItems(
            repository,
            releases,
            state => state.SeenReleaseIds,
            release => release.Id,
            release => new ActivityFeedItem(
                $"release-{release.Id}",
                "release",
                release.Name,
                release.TagName,
                release.PublishedAt ?? DateTimeOffset.UtcNow,
                release.HtmlUrl,
                IsNew: true));
    }

    /// <summary>Обновляет ленту при смене статуса CI run.</summary>
    public IReadOnlyList<ActivityFeedItem> IntegrateCiRun(
        RepoReference repository,
        RepoCiRunSnapshot? run)
    {
        if (run is null)
        {
            return GetFeed(repository);
        }

        var state = GetState(repository);
        var signature = $"{run.Id}:{run.Status}:{run.Conclusion}";
        if (state.LastCiSignature == signature)
        {
            return GetFeed(repository);
        }

        var isBootstrap = state.LastCiSignature is null;
        state.LastCiSignature = signature;

        if (isBootstrap)
        {
            return GetFeed(repository);
        }

        var title = run.Conclusion switch
        {
            "success" => $"CI passed: {run.Name}",
            "failure" => $"CI failed: {run.Name}",
            "cancelled" => $"CI cancelled: {run.Name}",
            _ => $"CI {run.Status}: {run.Name}",
        };

        var item = new ActivityFeedItem(
            $"ci-{run.Id}-{run.UpdatedAt?.ToUnixTimeSeconds() ?? 0}",
            "ci",
            title,
            run.Conclusion ?? run.Status,
            run.UpdatedAt ?? DateTimeOffset.UtcNow,
            run.HtmlUrl,
            IsNew: true);

        return MergeFeed(repository, [item], prepend: true);
    }

    /// <summary>Текущая лента для репозитория.</summary>
    public IReadOnlyList<ActivityFeedItem> GetFeed(RepoReference repository)
    {
        lock (_sync)
        {
            return _feeds.TryGetValue(repository.Slug, out var state)
                ? state.Items.ToList()
                : [];
        }
    }

    /// <summary>Сбрасывает состояние ленты репозитория.</summary>
    public void Clear(RepoReference repository)
    {
        lock (_sync)
        {
            _feeds.Remove(repository.Slug);
        }
    }

    private IReadOnlyList<ActivityFeedItem> IntegrateNumberedItems<TItem, TKey>(
        RepoReference repository,
        IReadOnlyList<TItem> items,
        Func<FeedState, HashSet<TKey>> seenSelector,
        Func<TItem, TKey> keySelector,
        Func<TItem, ActivityFeedItem> map)
        where TKey : notnull
    {
        var state = GetState(repository);
        var seen = seenSelector(state);
        var deltaItems = new List<ActivityFeedItem>();
        var isBootstrap = seen.Count == 0;

        foreach (var item in items)
        {
            var key = keySelector(item);
            if (!seen.Add(key))
            {
                continue;
            }

            if (isBootstrap)
            {
                continue;
            }

            deltaItems.Add(map(item));
        }

        if (deltaItems.Count == 0)
        {
            return GetFeed(repository);
        }

        return MergeFeed(repository, deltaItems, prepend: true);
    }

    private IReadOnlyList<ActivityFeedItem> MergeFeed(
        RepoReference repository,
        IReadOnlyList<ActivityFeedItem> incoming,
        bool prepend = false)
    {
        lock (_sync)
        {
            var state = GetState(repository);
            var merged = new Dictionary<string, ActivityFeedItem>(StringComparer.Ordinal);

            foreach (var existing in state.Items)
            {
                merged[existing.Id] = existing with { IsNew = false };
            }

            foreach (var item in incoming)
            {
                if (merged.TryGetValue(item.Id, out var existing))
                {
                    merged[item.Id] = item with { IsNew = item.IsNew || existing.IsNew };
                }
                else
                {
                    merged[item.Id] = item;
                }
            }

            var ordered = merged.Values
                .OrderByDescending(item => item.Timestamp)
                .Take(MaxFeedItems)
                .ToList();

            if (prepend)
            {
                var newestIds = incoming.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
                ordered = ordered
                    .Select(item => item with { IsNew = item.IsNew || newestIds.Contains(item.Id) })
                    .ToList();
            }

            state.Items = ordered;
            return state.Items.ToList();
        }
    }

    private FeedState GetState(RepoReference repository)
    {
        lock (_sync)
        {
            if (!_feeds.TryGetValue(repository.Slug, out var state))
            {
                state = new FeedState();
                _feeds[repository.Slug] = state;
            }

            return state;
        }
    }

    private static string MapEventKind(string type) =>
        type switch
        {
            "PushEvent" => "push",
            "PullRequestEvent" => "pr",
            "IssuesEvent" => "issue",
            "ReleaseEvent" => "release",
            "WatchEvent" => "watch",
            "ForkEvent" => "fork",
            _ => "event",
        };

    private sealed class FeedState
    {
        public List<ActivityFeedItem> Items { get; set; } = [];

        public HashSet<string> SeenCommitShas { get; } = new(StringComparer.Ordinal);

        public HashSet<int> SeenPullNumbers { get; } = [];

        public HashSet<int> SeenIssueNumbers { get; } = [];

        public HashSet<long> SeenReleaseIds { get; } = [];

        public string? LastCiSignature { get; set; }
    }
}
