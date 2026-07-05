using System.Diagnostics;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Копирует portable exe в AppData, создаёт ярлыки и перезапускает из постоянной папки.
/// </summary>
internal static class AppInstaller
{
    public const string InstalledArgument = "--installed";

    private const string ShortcutName = "GitHub Wallpaper.lnk";

    /// <summary>
    /// При запуске не из AppData копирует приложение и перезапускает установленную копию.
    /// Возвращает <c>true</c>, если текущий процесс должен завершиться.
    /// </summary>
    public static bool TryMigrateToInstallLocation()
    {
        if (!ShouldMigrate())
        {
            return false;
        }

        try
        {
            InstallFromCurrentLocation();
            RelaunchInstalled(InstalledArgument);
            return true;
        }
        catch (IOException) when (IsInstalledCopyInUse())
        {
            // Установленная копия уже работает — второй запуск перехватит SingleInstanceGuard.
            return false;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось скопировать приложение в:\n{AppPaths.AppData}\n\n{ex.Message}\n\n" +
                "Приложение продолжит работу из текущей папки.",
                "GitHub Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return false;
        }
    }

    private static bool IsInstalledCopyInUse()
    {
        if (!File.Exists(AppPaths.InstalledExecutablePath))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                AppPaths.InstalledExecutablePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }

    /// <summary>
    /// Синхронизирует HTML/CSS/JS обоев из bundle в AppData для portable single-file.
    /// </summary>
    public static void EnsureWallpaperAssets()
    {
        if (IsDevelopmentBuild())
        {
            return;
        }

        var sourceRoot = FindBundledWallpaperRoot();
        if (sourceRoot is null)
        {
            return;
        }

        var targetRoot = AppPaths.InstalledWallpaperRoot;
        Directory.CreateDirectory(targetRoot);

        foreach (var file in Directory.GetFiles(sourceRoot))
        {
            CopyFileWithRetry(file, Path.Combine(targetRoot, Path.GetFileName(file)));
        }
    }

    /// <summary>
    /// Запускает новую копию приложения после короткой задержки.
    /// Текущий процесс должен завершиться, чтобы не блокировать single-instance mutex.
    /// </summary>
    public static void ScheduleRestart()
    {
        var exePath = ResolveRestartExecutablePath();
        var workingDirectory = IsRunningFromInstallLocation()
            ? AppPaths.AppData
            : Path.GetDirectoryName(exePath) ?? AppPaths.AppData;

        var workDirectory = Path.Combine(Path.GetTempPath(), "GitHubWallpaper");
        Directory.CreateDirectory(workDirectory);
        var scriptPath = Path.Combine(workDirectory, "restart.cmd");

        var script = $"""
            @echo off
            setlocal
            ping 127.0.0.1 -n 3 >nul
            start "" /D "{EscapeCmdPath(workingDirectory)}" "{EscapeCmdPath(exePath)}"
            del "%~f0"
            """;

        File.WriteAllText(scriptPath, script);

        Process.Start(new ProcessStartInfo
        {
            FileName = scriptPath,
            UseShellExecute = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
        });
    }

    private static string ResolveRestartExecutablePath()
    {
        if (IsRunningFromInstallLocation() && File.Exists(AppPaths.InstalledExecutablePath))
        {
            return AppPaths.InstalledExecutablePath;
        }

        return GetExecutablePath();
    }

    private static string? FindBundledWallpaperRoot()
    {
        foreach (var candidate in EnumerateWallpaperSourceCandidates())
        {
            if (File.Exists(Path.Combine(candidate, "index.html")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumerateWallpaperSourceCandidates()
    {
        yield return Path.Combine(AppContext.BaseDirectory, "wwwroot", "wallpaper");

        var exeDirectory = Path.GetDirectoryName(GetExecutablePath());
        if (string.IsNullOrWhiteSpace(exeDirectory))
        {
            yield break;
        }

        yield return Path.Combine(exeDirectory, "wwwroot", "wallpaper");
    }

    /// <summary>Создаёт ярлыки в меню «Пуск» и на рабочем столе.</summary>
    public static void EnsureShortcuts()
    {
        if (!File.Exists(AppPaths.InstalledExecutablePath))
        {
            throw new FileNotFoundException(
                "Установленная копия не найдена. Запустите exe из загрузок ещё раз.",
                AppPaths.InstalledExecutablePath);
        }

        EnsureInstalledIcon();
        CreateShortcuts(AppPaths.InstalledExecutablePath);
    }

    private static void EnsureInstalledIcon()
    {
        if (File.Exists(AppPaths.InstalledIconPath))
        {
            return;
        }

        var sourceIcon = Path.Combine(AppContext.BaseDirectory, AppPaths.IconFileName);
        if (!File.Exists(sourceIcon))
        {
            return;
        }

        CopyFileWithRetry(sourceIcon, AppPaths.InstalledIconPath);
    }

    public static bool IsRunningFromInstallLocation()
    {
        var current = NormalizePath(GetExecutablePath());
        var installed = NormalizePath(AppPaths.InstalledExecutablePath);
        return string.Equals(current, installed, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldMigrate()
    {
        if (IsDevelopmentBuild())
        {
            return false;
        }

        return !IsRunningFromInstallLocation();
    }

    private static void InstallFromCurrentLocation()
    {
        var sourceExe = GetExecutablePath();
        Directory.CreateDirectory(AppPaths.AppData);

        CopyFileWithRetry(sourceExe, AppPaths.InstalledExecutablePath);

        var sourceWwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (!Directory.Exists(sourceWwwroot))
        {
            var bundledWallpaper = FindBundledWallpaperRoot();
            if (bundledWallpaper is not null)
            {
                sourceWwwroot = Path.GetDirectoryName(bundledWallpaper)!;
            }
        }

        if (Directory.Exists(sourceWwwroot))
        {
            CopyDirectory(sourceWwwroot, Path.Combine(AppPaths.AppData, "wwwroot"));
        }

        CopyIconIfPresent(AppContext.BaseDirectory, AppPaths.AppData);

        CreateShortcuts(AppPaths.InstalledExecutablePath);
    }

    private static void CopyIconIfPresent(string sourceDirectory, string destinationDirectory)
    {
        var sourceIcon = Path.Combine(sourceDirectory, AppPaths.IconFileName);
        if (!File.Exists(sourceIcon))
        {
            return;
        }

        CopyFileWithRetry(sourceIcon, AppPaths.InstalledIconPath);
    }

    private static void CreateShortcuts(string targetExe)
    {
        var iconPath = File.Exists(AppPaths.InstalledIconPath)
            ? AppPaths.InstalledIconPath
            : Path.Combine(Path.GetDirectoryName(targetExe) ?? AppPaths.AppData, AppPaths.IconFileName);

        if (!File.Exists(iconPath))
        {
            iconPath = null;
        }

        var startMenuPrograms = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        ShellShortcut.Create(
            Path.Combine(startMenuPrograms, ShortcutName),
            targetExe,
            "Динамические обои с активностью GitHub",
            iconPath);

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        ShellShortcut.Create(
            Path.Combine(desktop, ShortcutName),
            targetExe,
            "Динамические обои с активностью GitHub",
            iconPath);
    }

    private static void RelaunchInstalled(string? arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = AppPaths.InstalledExecutablePath,
            Arguments = arguments ?? string.Empty,
            UseShellExecute = true,
            WorkingDirectory = AppPaths.AppData,
        });
    }

    private static void CopyFileWithRetry(string source, string destination)
    {
        const int attempts = 5;
        IOException? lastError = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                File.Copy(source, destination, overwrite: true);
                return;
            }
            catch (IOException ex) when (attempt < attempts)
            {
                lastError = ex;
                Thread.Sleep(200);
            }
        }

        throw new IOException(
            $"Не удалось скопировать файл в {destination}.",
            lastError);
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            var targetFile = Path.Combine(destination, Path.GetFileName(file));
            CopyFileWithRetry(file, targetFile);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private static bool IsDevelopmentBuild()
    {
        var path = GetExecutablePath();
        return path.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase)
            || path.Contains(@"\obj\", StringComparison.OrdinalIgnoreCase);
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

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static string EscapeCmdPath(string path) => path.Replace("\"", "\"\"");
}
