using System.Diagnostics;

namespace GitHubWallpaper.Desktop;

/// <summary>Открывает URL в браузере по умолчанию.</summary>
internal static class BrowserLauncher
{
    public static void Open(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }
}
