namespace GitHubWallpaper.Desktop;

/// <summary>
/// Координирует паузу обоев: ручная, полноэкранная, батарея.
/// </summary>
internal sealed class WallpaperPauseCoordinator
{
    private readonly WallpaperController _wallpaperController;
    private bool _userPaused;
    private bool _fullscreenPaused;
    private bool _batteryPaused;
    private bool _suppressAutoPause;

    public WallpaperPauseCoordinator(WallpaperController wallpaperController)
    {
        ArgumentNullException.ThrowIfNull(wallpaperController);
        _wallpaperController = wallpaperController;
    }

    /// <summary>Ручная пауза из трея активна.</summary>
    public bool IsUserPaused => _userPaused;

    /// <summary>Переключает паузу из трея с учётом автопаузы.</summary>
    public async Task ToggleUserPauseAsync(CancellationToken cancellationToken = default)
    {
        if (_userPaused)
        {
            _userPaused = false;
        }
        else if (_wallpaperController.IsPaused)
        {
            _suppressAutoPause = true;
        }
        else
        {
            _userPaused = true;
        }

        await ApplyEffectivePauseAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Обновляет автопаузу из-за полноэкранного окна.</summary>
    public async Task SetFullscreenPausedAsync(bool paused, CancellationToken cancellationToken = default)
    {
        if (_fullscreenPaused == paused)
        {
            return;
        }

        _fullscreenPaused = paused;

        if (paused)
        {
            _suppressAutoPause = false;
        }

        await ApplyEffectivePauseAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Обновляет автопаузу при работе от батареи.</summary>
    public async Task SetBatteryPausedAsync(bool paused, CancellationToken cancellationToken = default)
    {
        if (_batteryPaused == paused)
        {
            return;
        }

        _batteryPaused = paused;

        if (paused)
        {
            _suppressAutoPause = false;
        }

        await ApplyEffectivePauseAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyEffectivePauseAsync(CancellationToken cancellationToken)
    {
        var autoPaused = !_suppressAutoPause && (_fullscreenPaused || _batteryPaused);
        var shouldPause = _userPaused || autoPaused;

        if (shouldPause && !_wallpaperController.IsPaused)
        {
            await _wallpaperController.PauseAsync(cancellationToken).ConfigureAwait(false);
        }
        else if (!shouldPause && _wallpaperController.IsPaused)
        {
            await _wallpaperController.ResumeAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
