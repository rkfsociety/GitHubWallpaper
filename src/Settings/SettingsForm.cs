using GitHubWallpaper.GitHub;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Окно настроек: PAT и список отслеживаемых репозиториев.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly GitHubSession _githubSession;
    private readonly SettingsStore _settingsStore;
    private readonly RepoPoller _repoPoller;
    private readonly TextBox _tokenTextBox;
    private readonly Label _tokenStatusLabel;
    private readonly ListBox _repoListBox;
    private readonly TextBox _repoInputTextBox;
    private readonly Button _removeRepoButton;
    private readonly Button _moveUpButton;
    private readonly Button _moveDownButton;
    private readonly List<RepoReference> _repositories;

    public SettingsForm(
        GitHubSession githubSession,
        SettingsStore settingsStore,
        RepoPoller repoPoller)
    {
        ArgumentNullException.ThrowIfNull(githubSession);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(repoPoller);
        _githubSession = githubSession;
        _settingsStore = settingsStore;
        _repoPoller = repoPoller;
        _repositories = repoPoller.Repositories.ToList();

        Text = "GitHub Wallpaper — Настройки";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 460);
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
            Size = new Size(488, 23),
            UseSystemPasswordChar = true,
        };

        _tokenStatusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 72),
            Size = new Size(488, 32),
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

        var reposLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 156),
            Text = "Репозитории (порядок = порядок на обоях):",
        };

        _repoListBox = new ListBox
        {
            Location = new Point(16, 180),
            Size = new Size(488, 140),
            IntegralHeight = false,
        };
        _repoListBox.SelectedIndexChanged += (_, _) => UpdateRepoButtons();

        _repoInputTextBox = new TextBox
        {
            Location = new Point(16, 328),
            Size = new Size(360, 23),
            PlaceholderText = "owner/repo или https://github.com/owner/repo",
        };
        _repoInputTextBox.KeyDown += OnRepoInputKeyDown;

        var addRepoButton = new Button
        {
            Location = new Point(384, 326),
            Size = new Size(120, 28),
            Text = "Добавить",
        };
        addRepoButton.Click += OnAddRepoClick;

        _removeRepoButton = new Button
        {
            Location = new Point(16, 364),
            Size = new Size(100, 28),
            Text = "Удалить",
        };
        _removeRepoButton.Click += OnRemoveRepoClick;

        _moveUpButton = new Button
        {
            Location = new Point(124, 364),
            Size = new Size(100, 28),
            Text = "Вверх",
        };
        _moveUpButton.Click += OnMoveUpClick;

        _moveDownButton = new Button
        {
            Location = new Point(232, 364),
            Size = new Size(100, 28),
            Text = "Вниз",
        };
        _moveDownButton.Click += OnMoveDownClick;

        var repoHintLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 404),
            Size = new Size(488, 44),
            ForeColor = SystemColors.GrayText,
            Text = "Изменения списка применяются сразу и сохраняются в settings.json.\n"
                   + "Токен хранится в Windows Credential Manager, не в файлах настроек.",
        };

        Controls.AddRange([
            tokenLabel,
            _tokenTextBox,
            _tokenStatusLabel,
            saveButton,
            verifyButton,
            clearButton,
            reposLabel,
            _repoListBox,
            _repoInputTextBox,
            addRepoButton,
            _removeRepoButton,
            _moveUpButton,
            _moveDownButton,
            repoHintLabel,
        ]);

        RefreshRepoListBox();
        UpdateTokenStatus();
    }

    private void RefreshRepoListBox()
    {
        var selectedSlug = _repoListBox.SelectedItem as string;

        _repoListBox.BeginUpdate();
        _repoListBox.Items.Clear();

        foreach (var repository in _repositories)
        {
            _repoListBox.Items.Add(repository.Slug);
        }

        _repoListBox.EndUpdate();

        if (selectedSlug is not null)
        {
            var index = _repositories.FindIndex(repository =>
                repository.Slug.Equals(selectedSlug, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                _repoListBox.SelectedIndex = index;
            }
        }

        UpdateRepoButtons();
    }

    private void UpdateRepoButtons()
    {
        var index = _repoListBox.SelectedIndex;
        var hasSelection = index >= 0;

        _removeRepoButton.Enabled = hasSelection && _repositories.Count > 1;
        _moveUpButton.Enabled = hasSelection && index > 0;
        _moveDownButton.Enabled = hasSelection && index >= 0 && index < _repositories.Count - 1;
    }

    private void OnRepoInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            OnAddRepoClick(sender, EventArgs.Empty);
        }
    }

    private void OnAddRepoClick(object? sender, EventArgs e)
    {
        var input = _repoInputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(input))
        {
            MessageBox.Show(
                "Введите owner/repo или URL репозитория GitHub.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!RepoUrlParser.TryParse(input, out var reference))
        {
            MessageBox.Show(
                "Неверный формат. Используйте owner/repo или https://github.com/owner/repo.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (_repositories.Any(existing =>
                existing.Slug.Equals(reference.Slug, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                $"Репозиторий {reference.Slug} уже в списке.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        _repositories.Add(reference);
        _repoInputTextBox.Clear();
        ApplyRepositories(reference.Slug);
    }

    private void OnRemoveRepoClick(object? sender, EventArgs e)
    {
        var index = _repoListBox.SelectedIndex;
        if (index < 0)
        {
            return;
        }

        if (_repositories.Count <= 1)
        {
            MessageBox.Show(
                "В списке должен остаться хотя бы один репозиторий.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _repositories.RemoveAt(index);
        ApplyRepositories(selectSlug: _repositories[Math.Min(index, _repositories.Count - 1)].Slug);
    }

    private void OnMoveUpClick(object? sender, EventArgs e)
    {
        var index = _repoListBox.SelectedIndex;
        if (index <= 0)
        {
            return;
        }

        var repository = _repositories[index];
        _repositories.RemoveAt(index);
        _repositories.Insert(index - 1, repository);
        ApplyRepositories(repository.Slug);
    }

    private void OnMoveDownClick(object? sender, EventArgs e)
    {
        var index = _repoListBox.SelectedIndex;
        if (index < 0 || index >= _repositories.Count - 1)
        {
            return;
        }

        var repository = _repositories[index];
        _repositories.RemoveAt(index);
        _repositories.Insert(index + 1, repository);
        ApplyRepositories(repository.Slug);
    }

    private void ApplyRepositories(string? selectSlug = null)
    {
        try
        {
            _settingsStore.SaveRepositories(_repositories);
            _repoPoller.Start(_repositories);
            RefreshRepoListBox();

            if (selectSlug is not null)
            {
                var index = _repositories.FindIndex(repository =>
                    repository.Slug.Equals(selectSlug, StringComparison.OrdinalIgnoreCase));

                if (index >= 0)
                {
                    _repoListBox.SelectedIndex = index;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось сохранить список репозиториев:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
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
