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

    /// <summary>Возвращает все интервалы опроса активности для пресета.</summary>
    public static ActivityPollIntervals ForActivityPreset(PollIntervalPreset preset) =>
        preset switch
        {
            PollIntervalPreset.Economy => new ActivityPollIntervals(
                Metadata: TimeSpan.FromMinutes(10),
                Commits: TimeSpan.FromMinutes(5),
                PullRequests: TimeSpan.FromMinutes(6),
                Issues: TimeSpan.FromMinutes(6),
                Releases: TimeSpan.FromMinutes(20),
                CiRuns: TimeSpan.FromMinutes(6),
                Heatmap: TimeSpan.FromHours(2),
                Events: TimeSpan.FromMinutes(4)),
            PollIntervalPreset.Frequent => new ActivityPollIntervals(
                Metadata: TimeSpan.FromMinutes(2),
                Commits: TimeSpan.FromMinutes(1),
                PullRequests: TimeSpan.FromSeconds(90),
                Issues: TimeSpan.FromSeconds(90),
                Releases: TimeSpan.FromMinutes(5),
                CiRuns: TimeSpan.FromSeconds(90),
                Heatmap: TimeSpan.FromMinutes(30),
                Events: TimeSpan.FromMinutes(1)),
            _ => new ActivityPollIntervals(
                Metadata: TimeSpan.FromMinutes(5),
                Commits: TimeSpan.FromMinutes(2),
                PullRequests: TimeSpan.FromMinutes(3),
                Issues: TimeSpan.FromMinutes(3),
                Releases: TimeSpan.FromMinutes(10),
                CiRuns: TimeSpan.FromMinutes(3),
                Heatmap: TimeSpan.FromHours(1),
                Events: TimeSpan.FromMinutes(2)),
        };
}
