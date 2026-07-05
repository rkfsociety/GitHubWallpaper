using System.Diagnostics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace GitHubWallpaper.Update;

/// <summary>Текущая версия запущенного приложения.</summary>
internal static class AppVersion
{
    private static readonly Lazy<string> Current = new(ResolveCurrentVersion);

    public static string CurrentVersion => Current.Value;

    /// <summary>Portable single-file exe, который может обновить сам себя.</summary>
    public static bool CanSelfUpdate
    {
        get
        {
            var processPath = Environment.ProcessPath;
            return !string.IsNullOrWhiteSpace(processPath)
                && processPath.EndsWith("GitHubWallpaper.exe", StringComparison.OrdinalIgnoreCase)
                && File.Exists(processPath);
        }
    }

    public static bool IsDevelopmentBuild =>
        CurrentVersion.Contains("-dev", StringComparison.OrdinalIgnoreCase)
        || CurrentVersion.Contains("-pr.", StringComparison.OrdinalIgnoreCase);

    public static bool TryParse(string? text, out Version version)
    {
        version = new Version(0, 0);

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (Version.TryParse(text.Trim(), out var parsed))
        {
            version = parsed;
            return true;
        }

        var match = Regex.Match(text, @"(\d+)\.(\d+)\.(\d+)");
        if (!match.Success)
        {
            return false;
        }

        version = new Version(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value));

        return true;
    }

    public static bool IsRemoteNewer(string remoteVersion)
    {
        if (!TryParse(CurrentVersion, out var current) || !TryParse(remoteVersion, out var remote))
        {
            return false;
        }

        return remote > current;
    }

    private static string ResolveCurrentVersion()
    {
        var processPath = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
        {
            var fileVersion = FileVersionInfo.GetVersionInfo(processPath).ProductVersion;
            if (!string.IsNullOrWhiteSpace(fileVersion))
            {
                return fileVersion.Split('+')[0].Trim();
            }
        }

        var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
        return assemblyVersion?.ToString(3) ?? "0.0.0";
    }
}
