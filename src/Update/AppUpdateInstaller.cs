using System.Diagnostics;
using System.Net;
using System.Reflection;

namespace GitHubWallpaper.Update;

/// <summary>Скачивает новый exe и перезапускает приложение.</summary>
internal static class AppUpdateInstaller
{
    private const int MaxDownloadAttempts = 3;
    private const long MinimumExeSizeBytes = 1_000_000;

    public static string UpdateWorkDirectory =>
        Path.Combine(Path.GetTempPath(), "GitHubWallpaper", "update");

    public static async Task<string> DownloadAsync(
        AppUpdateInfo update,
        IProgress<AppUpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        Exception? lastError = null;

        for (var attempt = 1; attempt <= MaxDownloadAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return await DownloadOnceAsync(update, progress, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < MaxDownloadAttempts && IsRetryableDownloadError(ex))
            {
                lastError = ex;
                progress?.Report(new AppUpdateDownloadProgress(0, update.AssetSizeBytes));
                await Task.Delay(TimeSpan.FromSeconds(attempt * 2), cancellationToken).ConfigureAwait(false);
            }
        }

        throw lastError ?? new InvalidOperationException("Не удалось скачать обновление.");
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

    internal static string FormatDownloadError(Exception ex)
    {
        var message = ex.Message;
        if (ContainsResponseEnded(message))
        {
            return "Соединение оборвалось во время скачивания. Проверьте интернет и повторите.";
        }

        if (ex is HttpRequestException { StatusCode: HttpStatusCode.RequestTimeout or HttpStatusCode.GatewayTimeout })
        {
            return "Истекло время ожидания ответа GitHub. Повторите позже.";
        }

        return message;
    }

    private static async Task<string> DownloadOnceAsync(
        AppUpdateInfo update,
        IProgress<AppUpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(UpdateWorkDirectory);
        var targetPath = Path.Combine(UpdateWorkDirectory, AppUpdateDefaults.ExeAssetName);

        if (File.Exists(targetPath))
        {
            File.Delete(targetPath);
        }

        using var httpClient = CreateDownloadClient();

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

        if (totalBytes is > 0 && fileInfo.Length != totalBytes)
        {
            File.Delete(targetPath);
            throw new InvalidDataException(
                $"Скачано {fileInfo.Length} из {totalBytes} байт — файл загружен не полностью.");
        }

        if (update.AssetSizeBytes is > 0 && fileInfo.Length != update.AssetSizeBytes)
        {
            File.Delete(targetPath);
            throw new InvalidDataException(
                $"Скачано {fileInfo.Length} из {update.AssetSizeBytes} байт — файл загружен не полностью.");
        }

        return targetPath;
    }

    private static bool IsRetryableDownloadError(Exception ex)
    {
        if (ex is OperationCanceledException)
        {
            return false;
        }

        if (ex is InvalidDataException or IOException or HttpRequestException)
        {
            return true;
        }

        return ContainsResponseEnded(ex.Message);
    }

    private static bool ContainsResponseEnded(string? message) =>
        message is not null
        && message.Contains("ResponseEnded", StringComparison.OrdinalIgnoreCase);

    private static HttpClient CreateDownloadClient()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            AutomaticDecompression = DecompressionMethods.All,
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromMinutes(15),
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd($"GitHubWallpaper/{GetVersionText()}");
        return client;
    }

    private static string GetVersionText()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return version is null ? "0.0" : version.ToString(3);
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
