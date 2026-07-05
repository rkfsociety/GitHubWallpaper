using System.Runtime.InteropServices;
using System.Text;
using GitHubWallpaper.Settings;

namespace GitHubWallpaper.Desktop;

/// <summary>
/// Фоновая проверка полноэкранного окна и питания от батареи.
/// </summary>
internal sealed class AutoPauseMonitor : IDisposable
{
    private const uint MonitorDefaultToNearest = 2;
    private const int FullscreenTolerancePx = 8;

    private readonly WallpaperPauseCoordinator _pauseCoordinator;
    private readonly System.Windows.Forms.Timer _timer;
    private bool _pauseOnFullscreen;
    private bool _pauseOnBattery;
    private bool _disposed;

    public AutoPauseMonitor(WallpaperPauseCoordinator pauseCoordinator)
    {
        ArgumentNullException.ThrowIfNull(pauseCoordinator);
        _pauseCoordinator = pauseCoordinator;

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += OnTimerTick;
    }

    /// <summary>Применяет настройки и запускает или останавливает мониторинг.</summary>
    public void Configure(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _pauseOnFullscreen = settings.PauseOnFullscreen;
        _pauseOnBattery = settings.PauseOnBattery;

        if (_pauseOnFullscreen || _pauseOnBattery)
        {
            _timer.Start();
            EvaluateConditions();
        }
        else
        {
            _timer.Stop();
            _ = ResetAutoPauseAsync();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _timer.Stop();
        _timer.Dispose();
        _disposed = true;
    }

    private void OnTimerTick(object? sender, EventArgs e) => EvaluateConditions();

    private void EvaluateConditions()
    {
        if (_pauseOnFullscreen)
        {
            _ = _pauseCoordinator.SetFullscreenPausedAsync(IsForegroundFullscreen());
        }
        else
        {
            _ = _pauseCoordinator.SetFullscreenPausedAsync(false);
        }

        if (_pauseOnBattery)
        {
            var onBattery = SystemInformation.PowerStatus.PowerLineStatus == PowerLineStatus.Offline;
            _ = _pauseCoordinator.SetBatteryPausedAsync(onBattery);
        }
        else
        {
            _ = _pauseCoordinator.SetBatteryPausedAsync(false);
        }
    }

    private async Task ResetAutoPauseAsync()
    {
        await _pauseCoordinator.SetFullscreenPausedAsync(false).ConfigureAwait(false);
        await _pauseCoordinator.SetBatteryPausedAsync(false).ConfigureAwait(false);
    }

    private static bool IsForegroundFullscreen()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero || ShouldSkipWindow(hwnd))
        {
            return false;
        }

        if (!GetWindowRect(hwnd, out var windowRect))
        {
            return false;
        }

        var monitor = MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }

        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref monitorInfo))
        {
            return false;
        }

        var monitorRect = monitorInfo.Monitor;
        return windowRect.Left <= monitorRect.Left + FullscreenTolerancePx
            && windowRect.Top <= monitorRect.Top + FullscreenTolerancePx
            && windowRect.Right >= monitorRect.Right - FullscreenTolerancePx
            && windowRect.Bottom >= monitorRect.Bottom - FullscreenTolerancePx;
    }

    private static bool ShouldSkipWindow(IntPtr hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == Environment.ProcessId)
        {
            return true;
        }

        var className = GetWindowClassName(hwnd);
        return className is "Progman" or "WorkerW" or "Shell_TrayWnd";
    }

    private static string GetWindowClassName(IntPtr hwnd)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(hwnd, buffer, buffer.Capacity) <= 0
            ? string.Empty
            : buffer.ToString();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out Rect lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
