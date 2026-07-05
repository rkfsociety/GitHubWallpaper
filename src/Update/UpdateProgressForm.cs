namespace GitHubWallpaper.Update;

/// <summary>Диалог скачивания обновления.</summary>
internal sealed class UpdateProgressForm : Form
{
    private readonly ProgressBar _progressBar;
    private readonly Label _statusLabel;

    public UpdateProgressForm(string version)
    {
        Text = "GitHub Wallpaper — обновление";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ControlBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 110);
        ShowInTaskbar = true;

        var titleLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 16),
            Text = $"Скачивание версии {version}…",
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 40),
            Size = new Size(388, 20),
            ForeColor = SystemColors.GrayText,
            Text = "Подключение к GitHub…",
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(16, 68),
            Size = new Size(388, 24),
            Style = ProgressBarStyle.Continuous,
        };

        Controls.AddRange([titleLabel, _statusLabel, _progressBar]);
    }

    public void Report(AppUpdateDownloadProgress progress)
    {
        if (IsDisposed)
        {
            return;
        }

        if (progress.Percent is { } percent)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = Math.Clamp(percent, 0, 100);
            _statusLabel.Text = $"Загружено {FormatSize(progress.BytesReceived)} из {FormatSize(progress.TotalBytes!.Value)}";
        }
        else
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
            _statusLabel.Text = $"Загружено {FormatSize(progress.BytesReceived)}…";
        }
    }

    private static string FormatSize(long bytes)
    {
        if (bytes >= 1_000_000)
        {
            return $"{bytes / 1_000_000d:0.0} МБ";
        }

        if (bytes >= 1_000)
        {
            return $"{bytes / 1_000d:0} КБ";
        }

        return $"{bytes} Б";
    }
}
