namespace GitHubWallpaper;

internal static class AppPaths
{
    public const string ExecutableFileName = "GitHubWallpaper.exe";

    public static string WallpaperRoot =>
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "wallpaper");

    public static string AppData =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GitHubWallpaper");

    public static string SettingsFile => Path.Combine(AppData, "settings.json");

    /// <summary>Постоянная папка установки portable-версии.</summary>
    public static string InstallDirectory => AppData;

    /// <summary>Путь к exe в AppData после первого запуска.</summary>
    public static string InstalledExecutablePath =>
        Path.Combine(InstallDirectory, ExecutableFileName);
}
