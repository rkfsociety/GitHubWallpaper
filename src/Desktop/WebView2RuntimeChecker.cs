using System.Diagnostics;
using Microsoft.Web.WebView2.Core;

namespace GitHubWallpaper.Desktop;

/// <summary>Проверка наличия WebView2 Runtime перед инициализацией обоев.</summary>
internal static class WebView2RuntimeChecker
{
    public const string BootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    /// <summary>Возвращает версию установленного WebView2 Runtime или <c>null</c>.</summary>
    public static string? GetInstalledVersion()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch (WebView2RuntimeNotFoundException)
        {
            return null;
        }
        catch (DllNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Показывает диалог с предложением установить WebView2 Runtime.</summary>
    public static void ShowMissingRuntimeDialog()
    {
        var result = MessageBox.Show(
            "Для работы обоев требуется Microsoft Edge WebView2 Runtime.\n\n" +
            "Нажмите «Да», чтобы открыть страницу загрузки установщика (Evergreen Bootstrapper).",
            "GitHub Wallpaper",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Error);

        if (result == DialogResult.Yes)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = BootstrapperUrl,
                UseShellExecute = true,
            });
        }
    }
}
