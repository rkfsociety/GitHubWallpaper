using Microsoft.Web.WebView2.WinForms;

namespace GitHubWallpaper.Desktop;

/// <summary>
/// Borderless WinForms-окно с WebView2, встроенное в WorkerW через <see cref="DesktopHost"/>.
/// </summary>
public sealed class WallpaperSurface : Form
{
    private readonly DesktopHost _desktopHost = new();
    private readonly WebView2 _webView = new();
    private bool _attached;
    private bool _coreInitialized;
    private bool _disposed;

    /// <summary>WebView2-контрол обоев.</summary>
    public WebView2 WebView => _webView;

    /// <summary>Хост WorkerW, к которому прикреплено окно.</summary>
    public DesktopHost DesktopHost => _desktopHost;

    /// <summary>Окно прикреплено к рабочему столу.</summary>
    public bool IsAttached => _attached;

    /// <summary>Core WebView2 инициализирован.</summary>
    public bool IsCoreInitialized => _coreInitialized;

    public WallpaperSurface()
    {
        FormBorderStyle = FormBorderStyle.None;
        ControlBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        MaximizeBox = false;
        MinimizeBox = false;
        TopMost = false;

        _webView.Dock = DockStyle.Fill;
        Controls.Add(_webView);
    }

    /// <summary>Создаёт Core WebView2 и настраивает его для режима обоев.</summary>
    public async Task InitializeAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_coreInitialized)
            return;

        if (!IsHandleCreated)
            CreateControl();

        await _webView.EnsureCoreWebView2Async().ConfigureAwait(true);

        var settings = _webView.CoreWebView2.Settings;
        settings.IsStatusBarEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.AreDevToolsEnabled = false;

        _coreInitialized = true;
    }

    /// <summary>Прикрепляет окно к WorkerW и показывает поверхность.</summary>
    public void AttachToDesktop()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_attached)
            return;

        _desktopHost.Attach(this);
        _attached = true;

        if (!Visible)
            Show();
    }

    /// <summary>Открепляет окно от WorkerW и скрывает поверхность.</summary>
    public void DetachFromDesktop()
    {
        if (!_attached)
            return;

        _desktopHost.Detach();
        _attached = false;
        Hide();
    }

    /// <summary>Задаёт монитор, на котором отображаются обои.</summary>
    public void SetDisplayBounds(Rectangle bounds) =>
        _desktopHost.SetTargetBounds(bounds);

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            DetachFromDesktop();
            _webView.Dispose();
            _desktopHost.Dispose();
            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
