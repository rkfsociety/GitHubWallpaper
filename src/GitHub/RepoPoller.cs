namespace GitHubWallpaper.GitHub;

/// <summary>
/// Периодический опрос метаданных и коммитов GitHub-репозиториев.
/// </summary>
internal sealed class RepoPoller : IDisposable
{
    public static readonly TimeSpan MetadataPollInterval = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan CommitsPollInterval = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LoopTick = TimeSpan.FromSeconds(30);

    /// <summary>Репозиторий по умолчанию до появления настроек (этап 3).</summary>
    public static readonly RepoReference DefaultRepository = new("microsoft", "vscode");

    private readonly GitHubApiClient _client;
    private readonly object _sync = new();
    private readonly Dictionary<string, RepoPollState> _states = new(StringComparer.OrdinalIgnoreCase);

    private IReadOnlyList<RepoReference> _repositories = [DefaultRepository];
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

    /// <summary>Последние метаданные репозитория из кэша poller.</summary>
    public RepoMetadataSnapshot? GetCachedMetadata(RepoReference repository)
    {
        lock (_sync)
        {
            return _states.TryGetValue(repository.Slug, out var state) ? state.Metadata : null;
        }
    }

    /// <summary>Последний список коммитов из кэша poller.</summary>
    public IReadOnlyList<RepoCommitSnapshot>? GetCachedCommits(RepoReference repository)
    {
        lock (_sync)
        {
            return _states.TryGetValue(repository.Slug, out var state) ? state.Commits : null;
        }
    }

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

        foreach (var repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var state = GetOrCreateState(repository);
            var metadataDue = includeNeverPolled
                || state.LastMetadataPoll is null
                || now - state.LastMetadataPoll.Value >= MetadataPollInterval;
            var commitsDue = includeNeverPolled
                || state.LastCommitsPoll is null
                || now - state.LastCommitsPoll.Value >= CommitsPollInterval;

            if (metadataDue)
            {
                await PollMetadataAsync(repository, cancellationToken).ConfigureAwait(false);
            }

            if (commitsDue)
            {
                await PollCommitsAsync(repository, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task PollMetadataAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client
                .GetAsync($"/repos/{repository.Owner}/{repository.Repo}", cancellationToken)
                .ConfigureAwait(false);

            var snapshot = RepoApiParser.ParseMetadata(repository, result.Body);

            lock (_sync)
            {
                var state = GetOrCreateState(repository);
                state.Metadata = snapshot;
                state.LastMetadataPoll = DateTimeOffset.UtcNow;
            }

            MetadataUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<RepoMetadataSnapshot>(repository, snapshot));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.Metadata, ex));
        }
    }

    private async Task PollCommitsAsync(RepoReference repository, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _client
                .GetAsync($"/repos/{repository.Owner}/{repository.Repo}/commits?per_page=5", cancellationToken)
                .ConfigureAwait(false);

            var commits = RepoApiParser.ParseCommits(result.Body);

            lock (_sync)
            {
                var state = GetOrCreateState(repository);
                state.Commits = commits;
                state.LastCommitsPoll = DateTimeOffset.UtcNow;
            }

            CommitsUpdated?.Invoke(this, new RepoPollUpdatedEventArgs<IReadOnlyList<RepoCommitSnapshot>>(repository, commits));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            PollFailed?.Invoke(this, new RepoPollFailedEventArgs(repository, RepoPollKind.Commits, ex));
        }
    }

    private RepoPollState GetOrCreateState(RepoReference repository)
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

    private sealed class RepoPollState
    {
        public DateTimeOffset? LastMetadataPoll { get; set; }

        public DateTimeOffset? LastCommitsPoll { get; set; }

        public RepoMetadataSnapshot? Metadata { get; set; }

        public IReadOnlyList<RepoCommitSnapshot>? Commits { get; set; }
    }
}
