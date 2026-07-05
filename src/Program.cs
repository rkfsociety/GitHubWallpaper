using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Tray;

namespace GitHubWallpaper;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        ApplicationConfiguration.Initialize();

        var wallpaperController = new WallpaperController();
        var githubSession = new GitHubSession();
        var repoPoller = new RepoPoller(githubSession.Client);
        var trayService = new TrayService(wallpaperController, githubSession);

        try
        {
            await wallpaperController.ApplyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            trayService.Dispose();
            repoPoller.Dispose();
            wallpaperController.Dispose();
            githubSession.Dispose();
            MessageBox.Show(
                $"Не удалось запустить обои:\n{ex.Message}",
                "GitHub Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.Run(new TrayApplicationContext(wallpaperController, githubSession, repoPoller, trayService));
    }
}
