namespace GitHubWallpaper.Update;

/// <summary>Параметры релизов GitHubWallpaper на GitHub.</summary>
internal static class AppUpdateDefaults
{
    public const string Owner = "rkfsociety";

    public const string Repo = "GitHubWallpaper";

    public const string ReleaseTag = "latest";

    public const string ExeAssetName = "GitHubWallpaper.exe";

    public const string VersionAssetName = "version.json";

    public static string ReleaseApiUrl =>
        $"https://api.github.com/repos/{Owner}/{Repo}/releases/tags/{ReleaseTag}";

    public static string ReleasePageUrl =>
        $"https://github.com/{Owner}/{Repo}/releases/tag/{ReleaseTag}";

    public static TimeSpan AutomaticCheckInterval => TimeSpan.FromHours(24);
}
