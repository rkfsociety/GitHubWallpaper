using System.Diagnostics;
using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Settings;
using GitHubWallpaper.Update;

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
    private readonly AppUpdateService _appUpdateService;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _pauseMenuItem;
    private readonly ToolStripMenuItem _checkUpdatesMenuItem;
    private SettingsForm? _settingsForm;
    private bool _disposed;

    public TrayService(
        WallpaperController wallpaperController,
        WallpaperPauseCoordinator pauseCoordinator,
        GitHubSession githubSession,
        SettingsStore settingsStore,
        RepoPoller repoPoller,
        AutoPauseMonitor autoPauseMonitor,
        AppUpdateService appUpdateService)
    {
        ArgumentNullException.ThrowIfNull(wallpaperController);
        ArgumentNullException.ThrowIfNull(pauseCoordinator);
        ArgumentNullException.ThrowIfNull(githubSession);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(repoPoller);
        ArgumentNullException.ThrowIfNull(autoPauseMonitor);
        ArgumentNullException.ThrowIfNull(appUpdateService);
        _wallpaperController = wallpaperController;
        _pauseCoordinator = pauseCoordinator;
        _githubSession = githubSession;
        _settingsStore = settingsStore;
        _repoPoller = repoPoller;
        _autoPauseMonitor = autoPauseMonitor;
        _appUpdateService = appUpdateService;

        _pauseMenuItem = new ToolStripMenuItem("Пауза", null, OnPauseClick);
        _checkUpdatesMenuItem = new ToolStripMenuItem("Проверить обновления…", null, OnCheckForUpdatesClick);

        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("Настройки", null, OnSettingsClick));
        menu.Items.Add(_pauseMenuItem);
        menu.Items.Add(_checkUpdatesMenuItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("Создать ярлык", null, OnCreateShortcutClick));
        menu.Items.Add(new ToolStripMenuItem("Открыть папку приложения", null, OnOpenAppFolderClick));
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
            _autoPauseMonitor,
            _wallpaperController);
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

    private void OnCreateShortcutClick(object? sender, EventArgs e)
    {
        try
        {
            AppInstaller.EnsureShortcuts();
            _notifyIcon.ShowBalloonTip(
                4000,
                "GitHub Wallpaper",
                "Ярлыки созданы в меню «Пуск» и на рабочем столе.",
                ToolTipIcon.Info);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось создать ярлык:\n{ex.Message}",
                "GitHub Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private void OnOpenAppFolderClick(object? sender, EventArgs e)
    {
        var folder = AppInstaller.IsRunningFromInstallLocation()
            ? AppPaths.InstallDirectory
            : Path.GetDirectoryName(Environment.ProcessPath ?? Application.ExecutablePath)
                ?? AppPaths.AppData;

        Directory.CreateDirectory(folder);
        Process.Start(new ProcessStartInfo
        {
            FileName = folder,
            UseShellExecute = true,
        });
    }

    /// <summary>Уведомление после первой установки в AppData.</summary>
    public void ShowInstallNotification()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(
            8000,
            "GitHub Wallpaper",
            $"Приложение установлено в:\n{AppPaths.AppData}\n\nЯрлык — на рабочем столе и в меню «Пуск».",
            ToolTipIcon.Info);
    }

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
            "GitHub token не задан — лимит API 60 запросов/час. Откройте Настройки → «Войти через GitHub».",
            ToolTipIcon.Warning);
    }

    /// <summary>Однократное уведомление в трее об ошибке доступа к репозиторию.</summary>
    public void NotifyRepositoryError(string repositorySlug, string message)
    {
        if (_disposed)
            return;

        _notifyIcon.ShowBalloonTip(
            7000,
            $"GitHub Wallpaper — {repositorySlug}",
            message,
            ToolTipIcon.Warning);
    }

    /// <summary>Фоновая проверка обновлений при старте.</summary>
    public async Task CheckForUpdatesAutomaticallyAsync(CancellationToken cancellationToken = default)
    {
        if (_disposed || !_appUpdateService.ShouldCheckAutomatically())
        {
            return;
        }

        try
        {
            var result = await _appUpdateService
                .CheckForUpdatesAsync(cancellationToken)
                .ConfigureAwait(false);

            if (result is AppUpdateCheckResult.UpdateAvailable available)
            {
                NotifyUpdateAvailable(available.Update.Version);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception)
        {
        }
        finally
        {
            _appUpdateService.RecordAutomaticCheck();
        }
    }

    public void NotifyUpdateAvailable(string version)
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(
            8000,
            "GitHub Wallpaper",
            $"Доступна версия {version}. ПКМ по иконке → «Проверить обновления…».",
            ToolTipIcon.Info);
    }

    private async void OnCheckForUpdatesClick(object? sender, EventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        _checkUpdatesMenuItem.Enabled = false;
        var previousCursor = Cursor.Current;

        try
        {
            Cursor.Current = Cursors.WaitCursor;
            var result = await _appUpdateService.CheckForUpdatesAsync().ConfigureAwait(true);

            switch (result)
            {
                case AppUpdateCheckResult.UpToDate upToDate:
                    MessageBox.Show(
                        $"Установлена последняя версия ({upToDate.CurrentVersion}).",
                        "GitHub Wallpaper",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;

                case AppUpdateCheckResult.UpdateAvailable available:
                    await PromptAndApplyUpdateAsync(available.Update).ConfigureAwait(true);
                    break;

                case AppUpdateCheckResult.Skipped skipped:
                    MessageBox.Show(
                        skipped.Reason,
                        "GitHub Wallpaper",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    break;

                case AppUpdateCheckResult.Failed failed:
                    MessageBox.Show(
                        failed.Message,
                        "GitHub Wallpaper",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    break;
            }
        }
        finally
        {
            _checkUpdatesMenuItem.Enabled = true;
            Cursor.Current = previousCursor;
        }
    }

    private async Task PromptAndApplyUpdateAsync(AppUpdateInfo update)
    {
        var answer = MessageBox.Show(
            $"Доступна новая версия {update.Version}.\n\n" +
            "Скачать и перезапустить приложение сейчас?",
            "GitHub Wallpaper",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        using var progressForm = new UpdateProgressForm(update.Version);
        progressForm.Show();
        progressForm.Refresh();

        try
        {
            var progress = new Progress<AppUpdateDownloadProgress>(value =>
            {
                progressForm.Report(value);
            });

            await _appUpdateService
                .DownloadAndApplyAsync(update, progress)
                .ConfigureAwait(true);

            progressForm.Close();
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            progressForm.Close();
            MessageBox.Show(
                $"Не удалось установить обновление:\n{ex.Message}",
                "GitHub Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
