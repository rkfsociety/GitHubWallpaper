namespace GitHubWallpaper.Settings;

/// <summary>
/// Сериализуемые настройки приложения (без PAT).
/// </summary>
internal sealed class AppSettings
{
    public List<string> Repositories { get; set; } = [];

    public PollIntervalPreset PollIntervalPreset { get; set; } = PollIntervalPreset.Normal;

    public bool AutoStart { get; set; }

    public bool PauseOnFullscreen { get; set; } = true;

    public bool PauseOnBattery { get; set; } = true;
}
