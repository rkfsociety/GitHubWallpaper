namespace GitHubWallpaper.Update;

/// <summary>Доступное обновление с GitHub Releases.</summary>
internal sealed record AppUpdateInfo(
    string Version,
    string DownloadUrl,
    string ReleasePageUrl,
    long? AssetSizeBytes);

/// <summary>Результат проверки обновлений.</summary>
internal abstract record AppUpdateCheckResult
{
    public sealed record UpToDate(string CurrentVersion) : AppUpdateCheckResult;

    public sealed record UpdateAvailable(AppUpdateInfo Update, string CurrentVersion) : AppUpdateCheckResult;

    public sealed record Skipped(string Reason) : AppUpdateCheckResult;

    public sealed record Failed(string Message) : AppUpdateCheckResult;
}
