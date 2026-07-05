using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Settings;
using GitHubWallpaper.Tray;
using GitHubWallpaper.Update;

namespace GitHubWallpaper;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (AppInstaller.TryMigrateToInstallLocation())
        {
            return;
        }

        var justInstalled = args.Contains(AppInstaller.InstalledArgument, StringComparer.OrdinalIgnoreCase);

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
        var appUpdateService = new AppUpdateService(settingsStore);
        var repoPoller = new RepoPoller(githubSession.Client);
        var trayService = new TrayService(
            wallpaperController,
            pauseCoordinator,
            githubSession,
            settingsStore,
            repoPoller,
            autoPauseMonitor,
            appUpdateService);

        Application.Run(new TrayApplicationContext(
            wallpaperController,
            githubSession,
            repoPoller,
            trayService,
            settingsStore,
            autoPauseMonitor,
            appUpdateService,
            justInstalled));
    }
}
