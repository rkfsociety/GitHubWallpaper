namespace GitHubWallpaper.Settings;

/// <summary>
/// Какие секции показывать на карточке репозитория на обоях.
/// </summary>
internal sealed class CardDisplaySettings
{
    public bool ShowDescription { get; set; } = true;

    public bool ShowStats { get; set; } = true;

    public bool ShowCi { get; set; } = true;

    public bool ShowRelease { get; set; } = true;

    public bool ShowHeatmap { get; set; } = true;

    public bool ShowFeed { get; set; } = true;

    public bool ShowPullRequests { get; set; } = true;

    public bool ShowIssues { get; set; } = true;

    public bool ShowCommits { get; set; } = true;

    /// <summary>Все секции включены (значения по умолчанию).</summary>
    public static CardDisplaySettings CreateDefault() => new();

    /// <summary>Копия настроек для хранения в poller без общей ссылки.</summary>
    public CardDisplaySettings Clone() => new()
    {
        ShowDescription = ShowDescription,
        ShowStats = ShowStats,
        ShowCi = ShowCi,
        ShowRelease = ShowRelease,
        ShowHeatmap = ShowHeatmap,
        ShowFeed = ShowFeed,
        ShowPullRequests = ShowPullRequests,
        ShowIssues = ShowIssues,
        ShowCommits = ShowCommits,
    };

    /// <summary>Payload для Bridge → WebView (camelCase).</summary>
    internal object ToBridgePayload() => new
    {
        description = ShowDescription,
        stats = ShowStats,
        ci = ShowCi,
        release = ShowRelease,
        heatmap = ShowHeatmap,
        feed = ShowFeed,
        pullRequests = ShowPullRequests,
        issues = ShowIssues,
        commits = ShowCommits,
    };
}
