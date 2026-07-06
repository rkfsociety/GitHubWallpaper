using System.Runtime.InteropServices;
using System.Text;
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

    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;

    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private static readonly IntPtr HwndBottom = new(1);

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
    private Rectangle? _targetBounds;
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

    /// <summary>Задаёт область монитора для обоев (координаты виртуального экрана).</summary>
    public void SetTargetBounds(Rectangle bounds)
    {
        _targetBounds = bounds;

        if (_attachedHandle != IntPtr.Zero)
            FitToBounds(_attachedHandle, bounds);
    }

    /// <summary>Прикрепляет WinForms-окно к WorkerW и растягивает на целевой монитор.</summary>
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
        FitToTargetBounds(windowHandle);
        SendToBottomOfParent(windowHandle);
        EnsureDesktopIconsAboveWallpaper();

        _attachedHandle = windowHandle;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    /// <summary>WS_EX_TRANSPARENT — клики проходят к окнам под WebView2 (иконки рабочего стола).</summary>
    internal static void EnableMouseClickThrough(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero)
            return;

        const uint wsExTransparent = 0x0000_0020;
        var exStyle = (uint)GetWindowLongPtr(windowHandle, GwlExStyle);
        SetWindowLongPtr(windowHandle, GwlExStyle, (nint)(exStyle | wsExTransparent));
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

    /// <summary>Обновляет размер прикреплённого окна под целевой монитор.</summary>
    public void ResizeAttached()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_attachedHandle != IntPtr.Zero)
            FitToTargetBounds(_attachedHandle);
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

        if (TryFindEmptyWorkerWindow(out var emptyWorker))
            return emptyWorker;

        if (TryFindWorkerWSiblingOfShellViewHost(out var siblingWorker))
            return siblingWorker;

        throw new InvalidOperationException(
            "Окно WorkerW для обоев не найдено. Перезапустите проводник или приложение.");
    }

    /// <summary>WorkerW без SHELLDLL_DefView — основной вариант для Win10/11.</summary>
    private static bool TryFindEmptyWorkerWindow(out IntPtr workerWindow)
    {
        workerWindow = IntPtr.Zero;

        EnumWindows(
            (topLevel, _) =>
            {
                if (!HasWindowClass(topLevel, "WorkerW"))
                    return true;

                if (FindWindowEx(topLevel, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                    return true;

                workerWindow = topLevel;
                return false;
            },
            IntPtr.Zero);

        return workerWindow != IntPtr.Zero;
    }

    /// <summary>WorkerW-сосед Progman — запасной вариант для старых сборок Windows.</summary>
    private static bool TryFindWorkerWSiblingOfShellViewHost(out IntPtr workerWindow)
    {
        workerWindow = IntPtr.Zero;

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

        return workerWindow != IntPtr.Zero;
    }

    private static void SendToBottomOfParent(IntPtr windowHandle) =>
        SetWindowPos(
            windowHandle,
            HwndBottom,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoActivate);

    private static void EnsureDesktopIconsAboveWallpaper()
    {
        var progman = FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
            return;

        var shellView = FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
        if (shellView == IntPtr.Zero)
            return;

        var flags = SwpNoMove | SwpNoSize | SwpNoActivate;
        SetWindowPos(progman, HwndTop, 0, 0, 0, 0, flags);
        SetWindowPos(shellView, HwndTop, 0, 0, 0, 0, flags);
    }

    private static bool HasWindowClass(IntPtr windowHandle, string className)
    {
        var buffer = new StringBuilder(256);
        return GetClassName(windowHandle, buffer, buffer.Capacity) > 0
            && buffer.ToString() == className;
    }

    private void FitToTargetBounds(IntPtr windowHandle)
    {
        var bounds = _targetBounds ?? GetVirtualScreenBounds();
        FitToBounds(windowHandle, bounds);
    }

    private static Rectangle GetVirtualScreenBounds() => new(
        GetSystemMetrics(SmXVirtualScreen),
        GetSystemMetrics(SmYVirtualScreen),
        GetSystemMetrics(SmCxVirtualScreen),
        GetSystemMetrics(SmCyVirtualScreen));

    private void FitToBounds(IntPtr windowHandle, Rectangle screenBounds)
    {
        var position = new Point(screenBounds.X, screenBounds.Y);

        if (_workerWindow != IntPtr.Zero)
            ScreenToClient(_workerWindow, ref position);

        SetWindowPos(
            windowHandle,
            IntPtr.Zero,
            position.X,
            position.Y,
            screenBounds.Width,
            screenBounds.Height,
            SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpShowWindow);
    }

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

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

    [DllImport("user32.dll")]
    private static extern bool ScreenToClient(IntPtr hWnd, ref Point lpPoint);

    private static nint GetWindowLongPtr(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

    private static nint SetWindowLongPtr(IntPtr hWnd, int nIndex, nint newValue) =>
        IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, newValue) : SetWindowLongPtr32(hWnd, nIndex, newValue);
}
