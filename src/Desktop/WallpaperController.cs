using Microsoft.Web.WebView2.Core;

namespace GitHubWallpaper.Desktop;

/// <summary>
/// Управляет жизненным циклом обоев: применение, пауза и возобновление рендера.
/// </summary>
public sealed class WallpaperController : IDisposable
{
    public const string VirtualHostName = "app.local";

    private static readonly Uri DefaultWallpaperUri = new($"https://{VirtualHostName}/index.html");

    private WallpaperSurface? _surface;
    private bool _virtualHostMapped;
    private bool _disposed;
    private bool _paused;

    /// <summary>Обои прикреплены к рабочему столу.</summary>
    public bool IsApplied => _surface?.IsAttached ?? false;

    /// <summary>Рендер обоев приостановлен.</summary>
    public bool IsPaused => _paused;

    /// <summary>Активная поверхность обоев, если создана.</summary>
    public WallpaperSurface? Surface => _surface;

    /// <summary>Обои применены и отображаются.</summary>
    public event EventHandler? Applied;

    /// <summary>Рендер обоев приостановлен.</summary>
    public event EventHandler? Paused;

    /// <summary>Рендер обоев возобновлён.</summary>
    public event EventHandler? Resumed;

    /// <summary>Обои откреплены от рабочего стола.</summary>
    public event EventHandler? Removed;

    /// <summary>
    /// Отправляет JSON-сообщение в страницу обоев через <c>PostWebMessageAsJson</c>.
    /// Вызов с фонового потока безопасен — маршалится на UI-поток поверхности.
    /// </summary>
    public void PostMessageAsJson(object message)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(message);

        var surface = _surface;
        if (surface is null)
            return;

        var json = Bridge.Serialize(message);

        void Send()
        {
            if (surface.WebView.CoreWebView2 is null)
                return;

            surface.WebView.CoreWebView2.PostWebMessageAsJson(json);
        }

        if (surface.InvokeRequired)
            surface.BeginInvoke(Send);
        else
            Send();
    }

    /// <summary>
    /// Инициализирует WebView2, настраивает virtual host и прикрепляет обои к WorkerW.
    /// Повторный вызов обновляет URL и снимает паузу, если обои уже применены.
    /// </summary>
    /// <param name="wallpaperUri">URI страницы обоев; по умолчанию <c>https://app.local/index.html</c>.</param>
    public async Task ApplyAsync(Uri? wallpaperUri = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        _surface ??= new WallpaperSurface();

        await _surface.InitializeAsync().ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();

        EnsureVirtualHostMapping(_surface.WebView.CoreWebView2);

        ResumeCoreIfPaused();

        _surface.AttachToDesktop();

        var targetUri = wallpaperUri ?? DefaultWallpaperUri;
        _surface.WebView.CoreWebView2.Navigate(targetUri.AbsoluteUri);

        _paused = false;
        Applied?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Приостанавливает рендер WebView2 и уведомляет страницу обоев.</summary>
    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_surface is null || !_surface.IsCoreInitialized || _paused)
            return;

        cancellationToken.ThrowIfCancellationRequested();

        await _surface.WebView.CoreWebView2.TrySuspendAsync().ConfigureAwait(true);
        PostWallpaperCommand("pause");

        _paused = true;
        Paused?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Возобновляет рендер WebView2 и уведомляет страницу обоев.</summary>
    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        if (_surface is null || !_surface.IsCoreInitialized || !_paused)
            return Task.CompletedTask;

        _surface.WebView.CoreWebView2.Resume();
        PostWallpaperCommand("resume");

        _paused = false;
        Resumed?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    /// <summary>Открепляет обои от рабочего стола, не уничтожая поверхность.</summary>
    public void Remove()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_surface is null || !_surface.IsAttached)
            return;

        ResumeCoreIfPaused();
        _surface.DetachFromDesktop();
        Removed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        ResumeCoreIfPaused();
        _surface?.Dispose();
        _surface = null;
        _virtualHostMapped = false;
        _paused = false;
        _disposed = true;
    }

    private void EnsureVirtualHostMapping(CoreWebView2 coreWebView2)
    {
        if (_virtualHostMapped)
            return;

        var folder = AppPaths.WallpaperRoot;
        Directory.CreateDirectory(folder);

        coreWebView2.SetVirtualHostNameToFolderMapping(
            VirtualHostName,
            folder,
            CoreWebView2HostResourceAccessKind.Allow);

        _virtualHostMapped = true;
    }

    private void PostWallpaperCommand(string command) =>
        PostMessageAsJson(new { type = command });

    private void ResumeCoreIfPaused()
    {
        if (!_paused || _surface?.WebView.CoreWebView2 is null)
            return;

        _surface.WebView.CoreWebView2.Resume();
        _paused = false;
    }
}
