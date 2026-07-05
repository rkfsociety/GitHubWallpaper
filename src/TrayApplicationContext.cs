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
        _trayService.ExitRequested += OnExitRequested;
        _wallpaperController.Paused += OnWallpaperPaused;
        _wallpaperController.Resumed += OnWallpaperResumed;
        _repoPoller.Start();
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
        _repoPoller.Dispose();
        _wallpaperController.Dispose();
        _githubSession.Dispose();
        ExitThread();
    }
}
