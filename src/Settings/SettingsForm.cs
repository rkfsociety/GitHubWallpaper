namespace GitHubWallpaper.Settings;

/// <summary>
/// Окно настроек. Полный функционал — на этапе 3 (мульти-репо, токен, автозапуск).
/// </summary>
internal sealed class SettingsForm : Form
{
    public SettingsForm()
    {
        Text = "GitHub Wallpaper — Настройки";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 160);
        ShowInTaskbar = true;

        var label = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            Text = "Настройки репозиториев и GitHub-токена будут доступны на следующем этапе.",
            TextAlign = ContentAlignment.MiddleCenter,
        };

        Controls.Add(label);
    }
}
