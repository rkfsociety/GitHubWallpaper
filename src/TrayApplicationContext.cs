using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Tray;

namespace GitHubWallpaper;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WallpaperController _wallpaperController;
    private readonly GitHubSession _githubSession;
    private readonly RepoPoller _repoPoller;
    private readonly TrayService _trayService;
    private readonly Bridge _bridge;

    public TrayApplicationContext(
        WallpaperController wallpaperController,
        GitHubSession githubSession,
        RepoPoller repoPoller,
        TrayService trayService)
    {
        _wallpaperController = wallpaperController;
        _githubSession = githubSession;
        _repoPoller = repoPoller;
        _trayService = trayService;
        _bridge = new Bridge(wallpaperController, repoPoller);
        _trayService.ExitRequested += OnExitRequested;
        _wallpaperController.Paused += OnWallpaperPaused;
        _wallpaperController.Resumed += OnWallpaperResumed;
        _repoPoller.Start();
        _bridge.Start();
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
        _bridge.Dispose();
        _repoPoller.Dispose();
        _wallpaperController.Dispose();
        _githubSession.Dispose();
        ExitThread();
    }
}
