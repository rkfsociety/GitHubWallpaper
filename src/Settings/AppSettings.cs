namespace GitHubWallpaper.Settings;

/// <summary>
/// Сериализуемые настройки приложения (без PAT).
/// </summary>
internal sealed class AppSettings
{
    public List<string> Repositories { get; set; } = [];
}
