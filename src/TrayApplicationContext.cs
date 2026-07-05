using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Settings;
using GitHubWallpaper.Tray;

namespace GitHubWallpaper;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WallpaperController _wallpaperController;
    private readonly GitHubSession _githubSession;
    private readonly RepoPoller _repoPoller;
    private readonly TrayService _trayService;
    private readonly Bridge _bridge;
    private readonly AutoPauseMonitor _autoPauseMonitor;

    public TrayApplicationContext(
        WallpaperController wallpaperController,
        GitHubSession githubSession,
        RepoPoller repoPoller,
        TrayService trayService,
        SettingsStore settingsStore,
        AutoPauseMonitor autoPauseMonitor)
    {
        _wallpaperController = wallpaperController;
        _githubSession = githubSession;
        _repoPoller = repoPoller;
        _trayService = trayService;
        _autoPauseMonitor = autoPauseMonitor;
        _bridge = new Bridge(wallpaperController, githubSession, repoPoller);
        _trayService.ExitRequested += OnExitRequested;
        _wallpaperController.Paused += OnWallpaperPaused;
        _wallpaperController.Resumed += OnWallpaperResumed;

        var settings = settingsStore.Load();
        _repoPoller.ConfigurePollIntervals(settings.PollIntervalPreset);
        SyncAutostart(settings.AutoStart);
        _autoPauseMonitor.Configure(settings);
        _repoPoller.Start(settingsStore.LoadRepositories());
        _bridge.Start();
    }

    private static void SyncAutostart(bool enabled)
    {
        if (AutostartManager.IsEnabled() != enabled)
        {
            AutostartManager.SetEnabled(enabled);
        }
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
        _trayService.Dispose();
        _autoPauseMonitor.Dispose();
        _bridge.Dispose();
        _repoPoller.Dispose();
        _wallpaperController.Dispose();
        _githubSession.Dispose();
        ExitThread();
    }
}
