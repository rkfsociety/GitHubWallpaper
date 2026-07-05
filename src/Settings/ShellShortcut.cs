namespace GitHubWallpaper.Settings;

/// <summary>Создание ярлыков Windows (.lnk) через WScript.Shell.</summary>
internal static class ShellShortcut
{
    public static void Create(string shortcutPath, string targetPath, string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var shortcutDirectory = Path.GetDirectoryName(shortcutPath);
        if (!string.IsNullOrWhiteSpace(shortcutDirectory))
        {
            Directory.CreateDirectory(shortcutDirectory);
        }

        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell недоступен на этой системе.");

        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(shortcutPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = Path.GetDirectoryName(targetPath) ?? string.Empty;
        shortcut.Description = description ?? "GitHub Wallpaper";
        shortcut.IconLocation = $"{targetPath},0";
        shortcut.Save();
    }
}
