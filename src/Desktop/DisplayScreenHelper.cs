namespace GitHubWallpaper.Desktop;

/// <summary>
/// Список мониторов и разрешение сохранённого <see cref="Screen.DeviceName"/>.
/// </summary>
internal static class DisplayScreenHelper
{
    /// <summary>Все подключённые мониторы.</summary>
    public static IReadOnlyList<Screen> GetAllScreens() => Screen.AllScreens;

    /// <summary>Основной монитор или первый из списка.</summary>
    public static Screen ResolvePrimary() =>
        Screen.PrimaryScreen ?? Screen.AllScreens[0];

    /// <summary>
    /// Находит монитор по <see cref="Screen.DeviceName"/>; при отсутствии — основной.
    /// Пустое имя трактуется как основной монитор.
    /// </summary>
    public static Screen Resolve(string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return ResolvePrimary();

        foreach (var screen in Screen.AllScreens)
        {
            if (screen.DeviceName.Equals(deviceName, StringComparison.OrdinalIgnoreCase))
                return screen;
        }

        return ResolvePrimary();
    }

    /// <summary>Подпись монитора для списка в настройках.</summary>
    public static string FormatLabel(Screen screen, int index)
    {
        var primary = screen.Primary ? " (основной)" : string.Empty;
        return $"{index + 1}: {screen.Bounds.Width}×{screen.Bounds.Height}{primary}";
    }
}
