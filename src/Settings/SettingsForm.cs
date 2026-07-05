using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Окно настроек: PAT, список репозиториев и поведение приложения.
/// </summary>
internal sealed class SettingsForm : Form
{
    private readonly GitHubSession _githubSession;
    private readonly SettingsStore _settingsStore;
    private readonly RepoPoller _repoPoller;
    private readonly AutoPauseMonitor _autoPauseMonitor;
    private readonly WallpaperController _wallpaperController;
    private readonly TextBox _tokenTextBox;
    private readonly Label _tokenStatusLabel;
    private readonly ListBox _repoListBox;
    private readonly TextBox _repoInputTextBox;
    private readonly Button _removeRepoButton;
    private readonly Button _moveUpButton;
    private readonly Button _moveDownButton;
    private readonly RadioButton _economyRadio;
    private readonly RadioButton _normalRadio;
    private readonly RadioButton _frequentRadio;
    private readonly CheckBox _autoStartCheckBox;
    private readonly CheckBox _pauseFullscreenCheckBox;
    private readonly CheckBox _pauseBatteryCheckBox;
    private readonly ComboBox _monitorComboBox;
    private readonly List<RepoReference> _repositories;
    private bool _suppressBehaviorEvents;

    public SettingsForm(
        GitHubSession githubSession,
        SettingsStore settingsStore,
        RepoPoller repoPoller,
        AutoPauseMonitor autoPauseMonitor,
        WallpaperController wallpaperController)
    {
        ArgumentNullException.ThrowIfNull(githubSession);
        ArgumentNullException.ThrowIfNull(settingsStore);
        ArgumentNullException.ThrowIfNull(repoPoller);
        ArgumentNullException.ThrowIfNull(autoPauseMonitor);
        ArgumentNullException.ThrowIfNull(wallpaperController);
        _githubSession = githubSession;
        _settingsStore = settingsStore;
        _repoPoller = repoPoller;
        _autoPauseMonitor = autoPauseMonitor;
        _wallpaperController = wallpaperController;
        _repositories = repoPoller.Repositories.ToList();

        Text = "GitHub Wallpaper — Настройки";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 640);
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
            Size = new Size(488, 110),
            IntegralHeight = false,
        };
        _repoListBox.SelectedIndexChanged += (_, _) => UpdateRepoButtons();

        _repoInputTextBox = new TextBox
        {
            Location = new Point(16, 298),
            Size = new Size(360, 23),
            PlaceholderText = "owner/repo или https://github.com/owner/repo",
        };
        _repoInputTextBox.KeyDown += OnRepoInputKeyDown;

        var addRepoButton = new Button
        {
            Location = new Point(384, 296),
            Size = new Size(120, 28),
            Text = "Добавить",
        };
        addRepoButton.Click += OnAddRepoClick;

        _removeRepoButton = new Button
        {
            Location = new Point(16, 334),
            Size = new Size(100, 28),
            Text = "Удалить",
        };
        _removeRepoButton.Click += OnRemoveRepoClick;

        _moveUpButton = new Button
        {
            Location = new Point(124, 334),
            Size = new Size(100, 28),
            Text = "Вверх",
        };
        _moveUpButton.Click += OnMoveUpClick;

        _moveDownButton = new Button
        {
            Location = new Point(232, 334),
            Size = new Size(100, 28),
            Text = "Вниз",
        };
        _moveDownButton.Click += OnMoveDownClick;

        var displayGroup = new GroupBox
        {
            Location = new Point(16, 376),
            Size = new Size(488, 58),
            Text = "Экран",
        };

        var monitorLabel = new Label
        {
            AutoSize = true,
            Location = new Point(12, 28),
            Text = "Монитор:",
        };

        _monitorComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(84, 24),
            Size = new Size(392, 23),
        };
        _monitorComboBox.SelectedIndexChanged += OnBehaviorChanged;

        displayGroup.Controls.AddRange([monitorLabel, _monitorComboBox]);

        var behaviorGroup = new GroupBox
        {
            Location = new Point(16, 442),
            Size = new Size(488, 150),
            Text = "Поведение",
        };

        var pollLabel = new Label
        {
            AutoSize = true,
            Location = new Point(12, 28),
            Text = "Интервал опроса GitHub API:",
        };

        _economyRadio = new RadioButton
        {
            AutoSize = true,
            Location = new Point(12, 52),
            Text = "Экономный (15 / 10 мин)",
        };
        _economyRadio.CheckedChanged += OnBehaviorChanged;

        _normalRadio = new RadioButton
        {
            AutoSize = true,
            Location = new Point(168, 52),
            Text = "Нормальный (5 / 2 мин)",
        };
        _normalRadio.CheckedChanged += OnBehaviorChanged;

        _frequentRadio = new RadioButton
        {
            AutoSize = true,
            Location = new Point(324, 52),
            Text = "Частый (2 / 1 мин)",
        };
        _frequentRadio.CheckedChanged += OnBehaviorChanged;

        _autoStartCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(12, 84),
            Text = "Запускать при старте Windows",
        };
        _autoStartCheckBox.CheckedChanged += OnBehaviorChanged;

        _pauseFullscreenCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(12, 110),
            Text = "Пауза при полноэкранных приложениях",
        };
        _pauseFullscreenCheckBox.CheckedChanged += OnBehaviorChanged;

        _pauseBatteryCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(12, 126),
            Text = "Пауза при работе от батареи",
        };
        _pauseBatteryCheckBox.CheckedChanged += OnBehaviorChanged;

        behaviorGroup.Controls.AddRange([
            pollLabel,
            _economyRadio,
            _normalRadio,
            _frequentRadio,
            _autoStartCheckBox,
            _pauseFullscreenCheckBox,
            _pauseBatteryCheckBox,
        ]);

        var repoHintLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 600),
            Size = new Size(488, 32),
            ForeColor = SystemColors.GrayText,
            Text = "Токен хранится в Credential Manager. Остальные настройки — в settings.json.",
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
            displayGroup,
            behaviorGroup,
            repoHintLabel,
        ]);

        PopulateMonitorComboBox();
        LoadBehaviorSettings();
        RefreshRepoListBox();
        UpdateTokenStatus();
    }

    private void LoadBehaviorSettings()
    {
        var settings = _settingsStore.Load();

        _suppressBehaviorEvents = true;

        switch (settings.PollIntervalPreset)
        {
            case PollIntervalPreset.Economy:
                _economyRadio.Checked = true;
                break;
            case PollIntervalPreset.Frequent:
                _frequentRadio.Checked = true;
                break;
            default:
                _normalRadio.Checked = true;
                break;
        }

        _autoStartCheckBox.Checked = settings.AutoStart;
        _pauseFullscreenCheckBox.Checked = settings.PauseOnFullscreen;
        _pauseBatteryCheckBox.Checked = settings.PauseOnBattery;
        SelectMonitor(settings.DisplayDeviceName);

        _suppressBehaviorEvents = false;
    }

    private void PopulateMonitorComboBox()
    {
        _monitorComboBox.Items.Clear();

        var screens = DisplayScreenHelper.GetAllScreens();
        for (var index = 0; index < screens.Count; index++)
        {
            var screen = screens[index];
            _monitorComboBox.Items.Add(new MonitorListItem(screen, index));
        }
    }

    private void SelectMonitor(string? deviceName)
    {
        var resolved = DisplayScreenHelper.Resolve(deviceName);

        for (var index = 0; index < _monitorComboBox.Items.Count; index++)
        {
            if (_monitorComboBox.Items[index] is MonitorListItem item
                && item.Screen.DeviceName.Equals(resolved.DeviceName, StringComparison.OrdinalIgnoreCase))
            {
                _monitorComboBox.SelectedIndex = index;
                return;
            }
        }

        if (_monitorComboBox.Items.Count > 0)
            _monitorComboBox.SelectedIndex = 0;
    }

    private string? GetSelectedDisplayDeviceName() =>
        _monitorComboBox.SelectedItem is MonitorListItem item
            ? item.Screen.DeviceName
            : null;

    private sealed class MonitorListItem
    {
        public MonitorListItem(Screen screen, int index)
        {
            Screen = screen;
            Index = index;
        }

        public Screen Screen { get; }

        public int Index { get; }

        public override string ToString() => DisplayScreenHelper.FormatLabel(Screen, Index);
    }

    private void OnBehaviorChanged(object? sender, EventArgs e)
    {
        if (_suppressBehaviorEvents)
        {
            return;
        }

        SaveBehaviorSettings();
    }

    private void SaveBehaviorSettings()
    {
        try
        {
            var settings = _settingsStore.Load();
            settings.PollIntervalPreset = GetSelectedPollPreset();
            settings.AutoStart = _autoStartCheckBox.Checked;
            settings.PauseOnFullscreen = _pauseFullscreenCheckBox.Checked;
            settings.PauseOnBattery = _pauseBatteryCheckBox.Checked;
            settings.DisplayDeviceName = GetSelectedDisplayDeviceName() ?? string.Empty;

            _settingsStore.Save(settings);
            _repoPoller.ConfigurePollIntervals(settings.PollIntervalPreset);
            AutostartManager.SetEnabled(settings.AutoStart);
            _autoPauseMonitor.Configure(settings);
            _wallpaperController.ConfigureDisplay(settings.DisplayDeviceName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось сохранить настройки поведения:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private PollIntervalPreset GetSelectedPollPreset()
    {
        if (_economyRadio.Checked)
        {
            return PollIntervalPreset.Economy;
        }

        if (_frequentRadio.Checked)
        {
            return PollIntervalPreset.Frequent;
        }

        return PollIntervalPreset.Normal;
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
            var error = GitHubPollError.FromException(ex);
            MessageBox.Show(
                $"{error.Message}\n{error.Hint}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            var error = GitHubPollError.FromException(ex);
            MessageBox.Show(
                $"Не удалось проверить токен:\n{error.Message}",
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
