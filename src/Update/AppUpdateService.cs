using GitHubWallpaper.GitHub;
using GitHubWallpaper.Settings;

namespace GitHubWallpaper.Update;

/// <summary>Проверка и установка обновлений из GitHub Releases.</summary>
internal sealed class AppUpdateService : IDisposable
{
    private readonly AppUpdateChecker _checker;
    private readonly SettingsStore _settingsStore;

    public AppUpdateService(SettingsStore settingsStore, GitHubApiClient? githubApiClient = null)
    {
        _settingsStore = settingsStore;
        _checker = new AppUpdateChecker(githubApiClient);
    }

    public Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default) =>
        _checker.CheckAsync(cancellationToken);

    public bool ShouldCheckAutomatically()
    {
        var settings = _settingsStore.Load();
        if (!settings.AutoCheckForUpdates)
        {
            return false;
        }

        if (settings.LastUpdateCheckUtc is not { } lastCheck)
        {
            return true;
        }

        return DateTimeOffset.UtcNow - lastCheck >= AppUpdateDefaults.AutomaticCheckInterval;
    }

    public void RecordAutomaticCheck()
    {
        try
        {
            var settings = _settingsStore.Load();
            settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            _settingsStore.Save(settings);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public async Task DownloadAndApplyAsync(
        AppUpdateInfo update,
        IProgress<AppUpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var downloadedPath = await AppUpdateInstaller
            .DownloadAsync(update, progress, cancellationToken)
            .ConfigureAwait(false);

        AppUpdateInstaller.ScheduleRestart(downloadedPath);
    }

    public void Dispose() => _checker.Dispose();
}
