using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Settings;

namespace GitHubWallpaper.Tray;

/// <summary>
/// Иконка в системном трее: настройки, пауза/возобновление обоев, выход.
/// </summary>
internal sealed class TrayService : IDisposable
{
    private readonly WallpaperController _wallpaperController;
    private readonly WallpaperPauseCoordinator _pauseCoordinator;
    private readonly GitHubSession _githubSession;
    private readonly SettingsStore _settingsStore;
    private readonly RepoPoller _repoPoller;
    private readonly AutoPauseMonitor _autoPauseMonitor;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _pauseMenuItem;
    private SettingsForm? _settingsForm;
    private bool _disposed;

    public TrayService(
        WallpaperController wallpaperController,
        WallpaperPauseCoordinator pauseCoordinator,
        GitHubSession githubSession,
        SettingsStore settingsStore,
        RepoPoller repoPoller,
        AutoPauseMonitor autoPauseMonitor)
    {
        ArgumentNullException.ThrowIfNull(wallpaperController);
        ArgumentNullException.ThrowIfNull(pauseCoordinator);
        ArgumentNullException.ThrowIfNull(githubSession);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(repoPoller);
        ArgumentNullException.ThrowIfNull(autoPauseMonitor);
        _wallpaperController = wallpaperController;
        _pauseCoordinator = pauseCoordinator;
        _githubSession = githubSession;
        _settingsStore = settingsStore;
        _repoPoller = repoPoller;
        _autoPauseMonitor = autoPauseMonitor;

        _pauseMenuItem = new ToolStripMenuItem("Пауза", null, OnPauseClick);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Настройки", null, OnSettingsClick));
        menu.Items.Add(_pauseMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Выход", null, OnExitClick));

        _notifyIcon = new NotifyIcon
        {
            Icon = AppIcons.Tray,
            Text = "GitHub Wallpaper",
            ContextMenuStrip = menu,
            Visible = true,
        };

        _notifyIcon.DoubleClick += OnSettingsClick;

        _wallpaperController.Paused += OnWallpaperPauseStateChanged;
        _wallpaperController.Resumed += OnWallpaperPauseStateChanged;
        _wallpaperController.Applied += OnWallpaperPauseStateChanged;

        UpdatePauseMenuItem();
        ShowTokenWarningIfNeeded();
    }

    /// <summary>Запрошен выход из приложения.</summary>
    public event EventHandler? ExitRequested;

    public void Dispose()
    {
        if (_disposed)
            return;

        _wallpaperController.Paused -= OnWallpaperPauseStateChanged;
        _wallpaperController.Resumed -= OnWallpaperPauseStateChanged;
        _wallpaperController.Applied -= OnWallpaperPauseStateChanged;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();

        if (_settingsForm is { IsDisposed: false })
            _settingsForm.Close();

        _settingsForm?.Dispose();
        _disposed = true;
    }

    private void OnSettingsClick(object? sender, EventArgs e)
    {
        if (_settingsForm is { IsDisposed: false })
        {
            if (_settingsForm.WindowState == FormWindowState.Minimized)
                _settingsForm.WindowState = FormWindowState.Normal;

            _settingsForm.Activate();
            _settingsForm.BringToFront();
            return;
        }

        _settingsForm = new SettingsForm(
            _githubSession,
            _settingsStore,
            _repoPoller,
            _autoPauseMonitor);
        _settingsForm.FormClosed += (_, _) =>
        {
            _settingsForm?.Dispose();
            _settingsForm = null;
        };
        _settingsForm.Show();
    }

    private async void OnPauseClick(object? sender, EventArgs e)
    {
        _pauseMenuItem.Enabled = false;

        try
        {
            await _pauseCoordinator.ToggleUserPauseAsync().ConfigureAwait(true);
        }
        finally
        {
            UpdatePauseMenuItem();
        }
    }

    private void OnExitClick(object? sender, EventArgs e) =>
        ExitRequested?.Invoke(this, EventArgs.Empty);

    private void OnWallpaperPauseStateChanged(object? sender, EventArgs e) =>
        UpdatePauseMenuItem();

    private void UpdatePauseMenuItem()
    {
        if (_disposed)
            return;

        _pauseMenuItem.Text = _wallpaperController.IsPaused ? "Продолжить" : "Пауза";
        _pauseMenuItem.Enabled = _wallpaperController.IsApplied;
    }

    private void ShowTokenWarningIfNeeded()
    {
        if (_githubSession.HasToken)
            return;

        _notifyIcon.ShowBalloonTip(
            5000,
            "GitHub Wallpaper",
            "GitHub token не задан — лимит API 60 запросов/час. Откройте Настройки, чтобы добавить PAT.",
            ToolTipIcon.Warning);
    }
}
