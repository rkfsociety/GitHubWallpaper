namespace GitHubWallpaper.Settings;

/// <summary>
/// Интервалы опроса для каждого пресета.
/// </summary>
internal static class PollIntervals
{
    /// <summary>Возвращает интервалы метаданных и коммитов для пресета.</summary>
    public static (TimeSpan Metadata, TimeSpan Commits) ForPreset(PollIntervalPreset preset) =>
        preset switch
        {
            PollIntervalPreset.Economy => (TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(10)),
            PollIntervalPreset.Frequent => (TimeSpan.FromMinutes(2), TimeSpan.FromMinutes(1)),
            _ => (TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(2)),
        };
}
