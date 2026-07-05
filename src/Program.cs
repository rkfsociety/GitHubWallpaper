using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Settings;
using GitHubWallpaper.Tray;

namespace GitHubWallpaper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        if (WebView2RuntimeChecker.GetInstalledVersion() is null)
        {
            WebView2RuntimeChecker.ShowMissingRuntimeDialog();
            return;
        }

        var wallpaperController = new WallpaperController();
        var pauseCoordinator = new WallpaperPauseCoordinator(wallpaperController);
        var autoPauseMonitor = new AutoPauseMonitor(pauseCoordinator);
        var githubSession = new GitHubSession();
        var settingsStore = new SettingsStore();
        var repoPoller = new RepoPoller(githubSession.Client);
        var trayService = new TrayService(
            wallpaperController,
            pauseCoordinator,
            githubSession,
            settingsStore,
            repoPoller,
            autoPauseMonitor);

        Application.Run(new TrayApplicationContext(
            wallpaperController,
            githubSession,
            repoPoller,
            trayService,
            settingsStore,
            autoPauseMonitor));
    }
}
