using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Tray;

namespace GitHubWallpaper;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WallpaperController _wallpaperController;
    private readonly GitHubSession _githubSession;
    private readonly TrayService _trayService;

    public TrayApplicationContext(
        WallpaperController wallpaperController,
        GitHubSession githubSession,
        TrayService trayService)
    {
        _wallpaperController = wallpaperController;
        _githubSession = githubSession;
        _trayService = trayService;
        _trayService.ExitRequested += OnExitRequested;
    }

    private void OnExitRequested(object? sender, EventArgs e)
    {
        _trayService.ExitRequested -= OnExitRequested;
        _trayService.Dispose();
        _wallpaperController.Dispose();
        _githubSession.Dispose();
        ExitThread();
    }
}
