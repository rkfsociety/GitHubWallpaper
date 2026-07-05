using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Settings;
using GitHubWallpaper.Tray;
using GitHubWallpaper.Update;

namespace GitHubWallpaper;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WallpaperController _wallpaperController;
    private readonly GitHubSession _githubSession;
    private readonly RepoPoller _repoPoller;
    private readonly TrayService _trayService;
    private readonly Bridge _bridge;
    private readonly AutoPauseMonitor _autoPauseMonitor;
    private readonly SettingsStore _settingsStore;
    private readonly AppUpdateService _appUpdateService;
    private readonly bool _justInstalled;
    private readonly HashSet<string> _notifiedRepoErrors = new(StringComparer.OrdinalIgnoreCase);

    public TrayApplicationContext(
        WallpaperController wallpaperController,
        GitHubSession githubSession,
        RepoPoller repoPoller,
        TrayService trayService,
        SettingsStore settingsStore,
        AutoPauseMonitor autoPauseMonitor,
        AppUpdateService appUpdateService,
        bool justInstalled = false)
    {
        _wallpaperController = wallpaperController;
        _githubSession = githubSession;
        _repoPoller = repoPoller;
        _trayService = trayService;
        _autoPauseMonitor = autoPauseMonitor;
        _settingsStore = settingsStore;
        _appUpdateService = appUpdateService;
        _justInstalled = justInstalled;
        _bridge = new Bridge(wallpaperController, githubSession, repoPoller);
        _trayService.ExitRequested += OnExitRequested;
        _wallpaperController.Paused += OnWallpaperPaused;
        _wallpaperController.Resumed += OnWallpaperResumed;
        _repoPoller.PollFailed += OnPollFailed;
        _repoPoller.RepositoriesChanged += OnRepositoriesChanged;

        var settings = settingsStore.Load();
        _repoPoller.ConfigurePollIntervals(settings.PollIntervalPreset);
        SyncAutostart(settings.AutoStart);
        AutostartManager.RefreshPathIfEnabled();
        _autoPauseMonitor.Configure(settings);
        _wallpaperController.ConfigureDisplay(settings.DisplayDeviceName);

        // WebView2 требует STA и работающий message pump — инициализация после Application.Run.
        Application.Idle += OnStartupIdle;
    }

    private async void OnStartupIdle(object? sender, EventArgs e)
    {
        Application.Idle -= OnStartupIdle;

        try
        {
            await _wallpaperController.ApplyAsync().ConfigureAwait(true);
            _bridge.Start();
            _repoPoller.Start(_settingsStore.LoadRepositories());
            _ = CheckForUpdatesInBackgroundAsync();

            if (_justInstalled)
            {
                _trayService.ShowInstallNotification();
            }
        }
        catch (Exception ex)
        {
            _trayService.ExitRequested -= OnExitRequested;
            _wallpaperController.Paused -= OnWallpaperPaused;
            _wallpaperController.Resumed -= OnWallpaperResumed;
            _repoPoller.PollFailed -= OnPollFailed;
            _repoPoller.RepositoriesChanged -= OnRepositoriesChanged;
            _trayService.Dispose();
            _autoPauseMonitor.Dispose();
            _bridge.Dispose();
            _repoPoller.Dispose();
            _wallpaperController.Dispose();
            _githubSession.Dispose();

            MessageBox.Show(
                $"Не удалось запустить обои:\n{ex.Message}",
                "GitHub Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            ExitThread();
        }
    }

    private async Task CheckForUpdatesInBackgroundAsync()
    {
        try
        {
            await _trayService.CheckForUpdatesAutomaticallyAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private static void SyncAutostart(bool enabled)
    {
        if (AutostartManager.IsEnabled() != enabled)
        {
            AutostartManager.SetEnabled(enabled);
        }
    }

    private void OnRepositoriesChanged(object? sender, EventArgs e) =>
        _notifiedRepoErrors.Clear();

    private void OnPollFailed(object? sender, RepoPollFailedEventArgs e)
    {
        if (e.Kind != RepoPollKind.Metadata)
            return;

        var error = GitHubPollError.FromException(e.Exception);
        if (!error.IsFatalForRepo)
            return;

        var slug = e.Repository.Slug;
        if (!_notifiedRepoErrors.Add(slug))
            return;

        _trayService.NotifyRepositoryError(slug, error.Message);
    }

    private void OnWallpaperPaused(object? sender, EventArgs e) =>
        _repoPoller.SetPaused(true);

    private void OnWallpaperResumed(object? sender, EventArgs e) =>
        _repoPoller.SetPaused(false);

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _trayService.ExitRequested -= OnExitRequested;
        _wallpaperController.Paused -= OnWallpaperPaused;
        _wallpaperController.Resumed -= OnWallpaperResumed;
        _repoPoller.PollFailed -= OnPollFailed;
        _repoPoller.RepositoriesChanged -= OnRepositoriesChanged;
        _trayService.Dispose();
        _autoPauseMonitor.Dispose();
        _bridge.Dispose();
        _repoPoller.Dispose();
        _wallpaperController.Dispose();
        _githubSession.Dispose();
        _appUpdateService.Dispose();
        ExitThread();
    }
}
