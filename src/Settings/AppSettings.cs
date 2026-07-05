namespace GitHubWallpaper.Settings;

/// <summary>
/// Сериализуемые настройки приложения (без PAT).
/// </summary>
internal sealed class AppSettings
{
    public List<string> Repositories { get; set; } = [];

    /// <summary>Число колонок сетки на обоях.</summary>
    public int GridColumns { get; set; } = 3;

    /// <summary>Число строк сетки на обоях (для масштабирования).</summary>
    public int GridRows { get; set; } = 2;

    /// <summary>
    /// Репозитории по ячейкам сетки (слева направо, сверху вниз).
    /// Пустая строка — пустая ячейка.
    /// </summary>
    public List<string> RepositorySlots { get; set; } = [];

    public PollIntervalPreset PollIntervalPreset { get; set; } = PollIntervalPreset.Normal;

    public bool AutoStart { get; set; }

    public bool PauseOnFullscreen { get; set; } = true;

    public bool PauseOnBattery { get; set; } = true;

    /// <summary>
    /// <see cref="Screen.DeviceName"/> выбранного монитора; пусто — основной монитор.
    /// </summary>
    public string DisplayDeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Client ID OAuth App GitHub (публичный). Если пусто — используется встроенный или env.
    /// </summary>
    public string GitHubOAuthClientId { get; set; } = string.Empty;

    public bool AutoCheckForUpdates { get; set; } = true;

    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    /// <summary>Какие блоки показывать на карточках репозиториев.</summary>
    public CardDisplaySettings CardDisplay { get; set; } = CardDisplaySettings.CreateDefault();

    /// <summary>Последняя позиция окна настроек (левый верхний угол). Null — центрировать при первом открытии.</summary>
    public int? SettingsWindowLeft { get; set; }

    /// <summary>Последняя позиция окна настроек (левый верхний угол). Null — центрировать при первом открытии.</summary>
    public int? SettingsWindowTop { get; set; }
}
