using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GitHubWallpaper.Desktop;

/// <summary>
/// Встраивает окно в иерархию рабочего стола Windows через WorkerW (Progman → 0x052C).
/// </summary>
public sealed class DesktopHost : IDisposable
{
    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;

    private const uint WsChild = 0x4000_0000;
    private const uint WsVisible = 0x1000_0000;
    private const uint WsCaption = 0x00C0_0000;
    private const uint WsThickFrame = 0x0004_0000;
    private const uint WsMinimizeBox = 0x0002_0000;
    private const uint WsMaximizeBox = 0x0001_0000;
    private const uint WsSysMenu = 0x0008_0000;
    private const uint WsBorder = 0x0080_0000;
    private const uint WsPopup = 0x8000_0000;

    private const uint WsExNoActivate = 0x0800_0000;
    private const uint WsExAppWindow = 0x0004_0000;
    private const uint WsExToolWindow = 0x0000_0080;

    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;

    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;

    private const uint WmSpawnWorker = 0x052C;
    private const int SpawnWorkerWParam = 0xD;
    private const uint SmtoNormal = 0x0000;

    private IntPtr _workerWindow;
    private IntPtr _attachedHandle;
    private nint _savedStyle;
    private nint _savedExStyle;
    private bool _disposed;

    /// <summary>HWND WorkerW, в который встроено окно обоев.</summary>
    public IntPtr WorkerWindow => _workerWindow;

    /// <summary>HWND прикреплённого окна, если есть.</summary>
    public IntPtr AttachedHandle => _attachedHandle;

    /// <summary>Находит или создаёт WorkerW через Progman.</summary>
    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_workerWindow != IntPtr.Zero)
            return;

        _workerWindow = ResolveWorkerWindow();
    }

    /// <summary>Прикрепляет WinForms-окно к WorkerW и растягивает на виртуальный экран.</summary>
    public void Attach(Form surface)
    {
        ArgumentNullException.ThrowIfNull(surface);

        if (!surface.IsHandleCreated)
            surface.CreateControl();

        Attach(surface.Handle);
    }

    /// <summary>Прикрепляет окно по HWND к WorkerW.</summary>
    public void Attach(IntPtr windowHandle)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (windowHandle == IntPtr.Zero)
            throw new ArgumentException("Некорректный HWND.", nameof(windowHandle));

        if (_attachedHandle != IntPtr.Zero && _attachedHandle != windowHandle)
            Detach();

        Initialize();

        _savedStyle = GetWindowLongPtr(windowHandle, GwlStyle);
        _savedExStyle = GetWindowLongPtr(windowHandle, GwlExStyle);

        var style = (uint)_savedStyle;
        style &= ~(WsCaption | WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu | WsBorder | WsPopup);
        style |= WsChild | WsVisible;
        SetWindowLongPtr(windowHandle, GwlStyle, (nint)style);

        var exStyle = (uint)_savedExStyle;
        exStyle |= WsExNoActivate | WsExToolWindow;
        exStyle &= ~WsExAppWindow;
        SetWindowLongPtr(windowHandle, GwlExStyle, (nint)exStyle);

        SetParent(windowHandle, _workerWindow);
        FitToVirtualScreen(windowHandle);

        _attachedHandle = windowHandle;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>Открепляет окно и восстанавливает стили.</summary>
    public void Detach()
    {
        if (_attachedHandle == IntPtr.Zero)
            return;

        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        SetParent(_attachedHandle, IntPtr.Zero);
        SetWindowLongPtr(_attachedHandle, GwlStyle, _savedStyle);
        SetWindowLongPtr(_attachedHandle, GwlExStyle, _savedExStyle);

        _attachedHandle = IntPtr.Zero;
    }

    /// <summary>Обновляет размер прикреплённого окна под виртуальный экран.</summary>
    public void ResizeAttached()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_attachedHandle != IntPtr.Zero)
            FitToVirtualScreen(_attachedHandle);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Detach();
        _workerWindow = IntPtr.Zero;
        _disposed = true;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => ResizeAttached();

    private static IntPtr ResolveWorkerWindow()
    {
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
            throw new InvalidOperationException("Окно Progman не найдено.");

        SendMessageTimeout(
            progman,
            WmSpawnWorker,
            (IntPtr)SpawnWorkerWParam,
            IntPtr.Zero,
            SmtoNormal,
            1000,
            out _);

        var workerWindow = IntPtr.Zero;

        EnumWindows(
            (topLevel, _) =>
            {
                var shellView = FindWindowEx(topLevel, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView == IntPtr.Zero)
                    return true;

                workerWindow = FindWindowEx(IntPtr.Zero, topLevel, "WorkerW", null);
                return workerWindow == IntPtr.Zero;
            },
            IntPtr.Zero);

        return workerWindow != IntPtr.Zero ? workerWindow : progman;
    }

    private static void FitToVirtualScreen(IntPtr windowHandle)
    {
        var x = GetSystemMetrics(SmXVirtualScreen);
        var y = GetSystemMetrics(SmYVirtualScreen);
        var width = GetSystemMetrics(SmCxVirtualScreen);
        var height = GetSystemMetrics(SmCyVirtualScreen);

        SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            x,
            y,
            width,
            height,
            SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowName);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern nint GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern nint SetWindowLongPtr32(IntPtr hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    private static nint GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

    private static nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint newValue) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, newValue) : SetWindowLongPtr32(hWnd, nIndex, newValue);
}
