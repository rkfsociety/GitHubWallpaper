namespace GitHubWallpaper.Settings;

/// <summary>
/// Интервалы опроса GitHub API для каждого пресета.
/// </summary>
internal sealed record ActivityPollIntervals(
    TimeSpan Metadata,
    TimeSpan Commits,
    TimeSpan PullRequests,
    TimeSpan Issues,
    TimeSpan Releases,
    TimeSpan CiRuns,
    TimeSpan Heatmap,
    TimeSpan Events);

/// <summary>
/// Интервалы опроса для каждого пресета.
/// </summary>
internal static class PollIntervals
{
    /// <summary>Возвращает интервалы метаданных и коммитов для пресета.</summary>
    public static (TimeSpan Metadata, TimeSpan Commits) ForPreset(PollIntervalPreset preset)
    {
        var intervals = ForActivityPreset(preset);
        return (intervals.Metadata, intervals.Commits);
    }

    /// <summary>Базовый интервал опроса для пресета (один для всей активности карточки).</summary>
    public static TimeSpan BaseInterval(PollIntervalPreset preset) =>
        preset switch
        {
            PollIntervalPreset.Economy => TimeSpan.FromMinutes(20),
            PollIntervalPreset.Frequent => TimeSpan.FromMinutes(1),
            _ => TimeSpan.FromMinutes(10),
        };

    /// <summary>Возвращает все интервалы опроса активности для пресета.</summary>
    public static ActivityPollIntervals ForActivityPreset(PollIntervalPreset preset)
    {
        var baseInterval = BaseInterval(preset);
        var heatmapInterval = preset switch
        {
            PollIntervalPreset.Economy => TimeSpan.FromHours(2),
            PollIntervalPreset.Frequent => TimeSpan.FromMinutes(30),
            _ => TimeSpan.FromHours(1),
        };

        return new ActivityPollIntervals(
            Metadata: baseInterval,
            Commits: baseInterval,
            PullRequests: baseInterval,
            Issues: baseInterval,
            Releases: baseInterval,
            CiRuns: baseInterval,
            Heatmap: heatmapInterval,
            Events: baseInterval);
    }
}
