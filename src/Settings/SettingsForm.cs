using GitHubWallpaper.GitHub;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Окно настроек. Полный функционал репозиториев — на этапе 3.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly GitHubSession _githubSession;
    private readonly TextBox _tokenTextBox;
    private readonly Label _tokenStatusLabel;

    public SettingsForm(GitHubSession githubSession)
    {
        ArgumentNullException.ThrowIfNull(githubSession);
        _githubSession = githubSession;

        Text = "GitHub Wallpaper — Настройки";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 220);
        ShowInTaskbar = true;

        var tokenLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 16),
            Text = "GitHub Personal Access Token:",
        };

        _tokenTextBox = new TextBox
        {
            Location = new Point(16, 40),
            Size = new Size(448, 23),
            UseSystemPasswordChar = true,
        };

        _tokenStatusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 72),
            Size = new Size(448, 32),
            ForeColor = SystemColors.GrayText,
        };

        var saveButton = new Button
        {
            Location = new Point(16, 112),
            Size = new Size(100, 28),
            Text = "Сохранить",
        };
        saveButton.Click += OnSaveClick;

        var verifyButton = new Button
        {
            Location = new Point(124, 112),
            Size = new Size(100, 28),
            Text = "Проверить",
        };
        verifyButton.Click += OnVerifyClick;

        var clearButton = new Button
        {
            Location = new Point(232, 112),
            Size = new Size(100, 28),
            Text = "Очистить",
        };
        clearButton.Click += OnClearClick;

        var infoLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 152),
            Size = new Size(448, 52),
            Text = "Токен хранится в Windows Credential Manager, не в файлах настроек.\n"
                   + "Настройки репозиториев будут доступны на следующем этапе.",
        };

        Controls.AddRange([
            tokenLabel,
            _tokenTextBox,
            _tokenStatusLabel,
            saveButton,
            verifyButton,
            clearButton,
            infoLabel,
        ]);

        UpdateTokenStatus();
    }

    private void UpdateTokenStatus()
    {
        _tokenStatusLabel.Text = _githubSession.HasStoredToken
            ? "Токен сохранён в Credential Manager. Введите новый, чтобы заменить."
            : "Токен не задан — лимит API 60 запросов/час, только публичные репозитории.";
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        var token = _tokenTextBox.Text.Trim();
        if (string.IsNullOrEmpty(token))
        {
            MessageBox.Show(
                "Введите Personal Access Token или нажмите «Очистить».",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        try
        {
            _githubSession.SaveToken(token);
            _tokenTextBox.Clear();
            UpdateTokenStatus();
            MessageBox.Show(
                "Токен сохранён в Windows Credential Manager.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось сохранить токен:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private async void OnVerifyClick(object? sender, EventArgs e)
    {
        var token = _tokenTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(token))
        {
            _githubSession.SaveToken(token);
            _tokenTextBox.Clear();
            UpdateTokenStatus();
        }
        else if (!_githubSession.HasStoredToken)
        {
            MessageBox.Show(
                "Сначала введите и сохраните токен.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }
        else
        {
            _githubSession.ReloadTokenFromStore();
        }

        try
        {
            UseWaitCursor = true;
            await _githubSession.Client.GetAuthenticatedUserAsync().ConfigureAwait(true);
            UpdateTokenStatus();
            MessageBox.Show(
                "Токен действителен.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (GitHubApiException ex)
        {
            MessageBox.Show(
                $"Токен отклонён GitHub API:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось проверить токен:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private void OnClearClick(object? sender, EventArgs e)
    {
        try
        {
            _githubSession.ClearToken();
            _tokenTextBox.Clear();
            UpdateTokenStatus();
            MessageBox.Show(
                "Токен удалён из Credential Manager.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось удалить токен:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}
