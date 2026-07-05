namespace GitHubWallpaper;

internal static class AppPaths
{
    public static string WallpaperRoot =>
        Path.Combine(AppContext.BaseDirectory, "wwwroot", "wallpaper");

    public static string AppData =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GitHubWallpaper");
}
