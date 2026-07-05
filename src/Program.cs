using GitHubWallpaper.Desktop;
using GitHubWallpaper.Tray;

namespace GitHubWallpaper;

internal static class Program
{
    [STAThread]
    private static async Task Main()
    {
        ApplicationConfiguration.Initialize();

        var wallpaperController = new WallpaperController();
        var trayService = new TrayService(wallpaperController);

        try
        {
            await wallpaperController.ApplyAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            trayService.Dispose();
            wallpaperController.Dispose();
            MessageBox.Show(
                $"Не удалось запустить обои:\n{ex.Message}",
                "GitHub Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.Run(new TrayApplicationContext(wallpaperController, trayService));
    }
}
