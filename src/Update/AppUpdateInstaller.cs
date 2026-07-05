using System.Diagnostics;

namespace GitHubWallpaper.Update;

/// <summary>Скачивает новый exe и перезапускает приложение.</summary>
internal static class AppUpdateInstaller
{
    private const long MinimumExeSizeBytes = 1_000_000;

    public static string UpdateWorkDirectory =>
        Path.Combine(Path.GetTempPath(), "GitHubWallpaper", "update");

    public static async Task<string> DownloadAsync(
        AppUpdateInfo update,
        IProgress<AppUpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(UpdateWorkDirectory);
        var targetPath = Path.Combine(UpdateWorkDirectory, AppUpdateDefaults.ExeAssetName);

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        using var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(15),
        };

        using var response = await httpClient
            .GetAsync(update.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? update.AssetSizeBytes;
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var output = new FileStream(
            targetPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        long downloadedBytes = 0;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            downloadedBytes += read;

            if (totalBytes is > 0)
            {
                progress?.Report(new AppUpdateDownloadProgress(downloadedBytes, totalBytes.Value));
            }
            else
            {
                progress?.Report(new AppUpdateDownloadProgress(downloadedBytes, null));
            }
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);

        var fileInfo = new FileInfo(targetPath);
        if (fileInfo.Length < MinimumExeSizeBytes)
        {
            File.Delete(targetPath);
            throw new InvalidDataException("Скачанный файл слишком маленький — обновление отменено.");
        }

        return targetPath;
    }

    public static void ScheduleRestart(string downloadedExePath)
    {
        var currentExePath = File.Exists(AppPaths.InstalledExecutablePath)
            ? AppPaths.InstalledExecutablePath
            : Environment.ProcessPath
                ?? throw new InvalidOperationException("Не удалось определить путь к текущему exe.");

        if (!File.Exists(downloadedExePath))
        {
            throw new FileNotFoundException("Скачанный файл обновления не найден.", downloadedExePath);
        }

        Directory.CreateDirectory(UpdateWorkDirectory);
        var scriptPath = Path.Combine(UpdateWorkDirectory, "apply-update.cmd");

        var script = $"""
            @echo off
            setlocal
            ping 127.0.0.1 -n 3 >nul
            :retry
            move /Y "{EscapeCmdPath(downloadedExePath)}" "{EscapeCmdPath(currentExePath)}" >nul 2>&1
            if errorlevel 1 (
              ping 127.0.0.1 -n 1 >nul
              goto retry
            )
            start "" "{EscapeCmdPath(currentExePath)}"
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

    private static string EscapeCmdPath(string path) => path.Replace("\"", "\"\"");
}

internal readonly record struct AppUpdateDownloadProgress(long BytesReceived, long? TotalBytes)
{
    public int? Percent =>
        TotalBytes is > 0
            ? (int)Math.Clamp(BytesReceived * 100 / TotalBytes.Value, 0, 100)
            : null;
}
