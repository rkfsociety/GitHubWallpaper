namespace GitHubWallpaper;

internal static class AppIcons
{
    private static Icon? _tray;

    public static Icon Tray => _tray ??= CreateTrayIcon();

    private static Icon CreateTrayIcon()
    {
        const int size = 32;

        using var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.Transparent);
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            using var background = new SolidBrush(Color.FromArgb(36, 41, 46));
            graphics.FillEllipse(background, 2, 2, size - 4, size - 4);

            using var accent = new SolidBrush(Color.FromArgb(88, 166, 255));
            graphics.FillEllipse(accent, 10, 10, 12, 12);
        }

        var handle = bitmap.GetHicon();
        var icon = Icon.FromHandle(handle);
        return (Icon)icon.Clone();
    }
}
