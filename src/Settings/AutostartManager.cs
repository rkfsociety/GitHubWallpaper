using Microsoft.Win32;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Автозапуск через ключ реестра <c>HKCU\...\Run</c>.
/// </summary>
internal static class AutostartManager
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GitHubWallpaper";

    /// <summary>Запись автозапуска присутствует в реестре.</summary>
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var value = key?.GetValue(ValueName) as string;
        return !string.IsNullOrWhiteSpace(value);
    }

    /// <summary>Включает или отключает автозапуск приложения.</summary>
    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (key is null)
        {
            throw new InvalidOperationException("Не удалось открыть ключ реестра автозапуска.");
        }

        if (enabled)
        {
            key.SetValue(ValueName, QuoteExecutablePath(GetAutostartExecutablePath()));
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    /// <summary>Обновляет путь автозапуска, если запись уже включена.</summary>
    public static void RefreshPathIfEnabled()
    {
        if (IsEnabled())
        {
            SetEnabled(true);
        }
    }

    private static string GetAutostartExecutablePath()
    {
        if (File.Exists(AppPaths.InstalledExecutablePath))
        {
            return AppPaths.InstalledExecutablePath;
        }

        return GetExecutablePath();
    }

    private static string GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = Application.ExecutablePath;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("Не удалось определить путь к исполняемому файлу.");
        }

        return path;
    }

    private static string QuoteExecutablePath(string path) =>
        path.Contains(' ') ? $"\"{path}\"" : path;
}
