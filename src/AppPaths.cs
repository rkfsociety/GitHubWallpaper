namespace GitHubWallpaper;

internal static class AppPaths
{
    public const string ExecutableFileName = "GitHubWallpaper.exe";

    public const string IconFileName = "app.ico";

    /// <summary>Постоянная папка HTML/CSS/JS обоев в AppData.</summary>
    public static string InstalledWallpaperRoot =>
        Path.Combine(AppData, "wwwroot", "wallpaper");

    /// <summary>
    /// Папка обоев для WebView2: в dev — из output, в portable — из AppData.
    /// </summary>
    public static string WallpaperRoot =>
        IsDevelopmentBuild()
            ? Path.Combine(AppContext.BaseDirectory, "wwwroot", "wallpaper")
            : InstalledWallpaperRoot;

    private static bool IsDevelopmentBuild()
    {
        var path = Environment.ProcessPath ?? Application.ExecutablePath ?? string.Empty;
        return path.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase);
    }

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

    /// <summary>Путь к иконке в AppData (для ярлыков).</summary>
    public static string InstalledIconPath =>
        Path.Combine(InstallDirectory, IconFileName);
}
