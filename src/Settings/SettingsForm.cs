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
    private readonly Button _signInWithGitHubButton;
    private readonly LinkLabel _deviceSignInLinkLabel;
    private readonly LinkLabel _createTokenLinkLabel;
    private readonly TextBox _oauthClientIdTextBox;
    private readonly TextBox _oauthClientSecretTextBox;
    private readonly GridLayoutEditor _gridLayoutEditor;
    private readonly TextBox _repoInputTextBox;
    private readonly Button _removeRepoButton;
    private readonly RadioButton _economyRadio;
    private readonly RadioButton _normalRadio;
    private readonly RadioButton _frequentRadio;
    private readonly CheckBox _autoStartCheckBox;
    private readonly CheckBox _pauseFullscreenCheckBox;
    private readonly CheckBox _pauseBatteryCheckBox;
    private readonly CheckBox _autoCheckUpdatesCheckBox;
    private readonly ComboBox _monitorComboBox;
    private bool _suppressBehaviorEvents;
    private bool _suppressGridEvents;

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

        Text = "GitHub Wallpaper — Настройки";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(520, 820);
        ShowInTaskbar = true;

        var tokenLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 16),
            Text = "Авторизация GitHub:",
        };

        _signInWithGitHubButton = new Button
        {
            Location = new Point(16, 40),
            Size = new Size(180, 30),
            Text = "Войти через GitHub",
        };
        _signInWithGitHubButton.Click += OnSignInWithGitHubClick;

        _deviceSignInLinkLabel = new LinkLabel
        {
            AutoSize = true,
            Location = new Point(204, 46),
            Text = "Вход по коду устройства",
        };
        _deviceSignInLinkLabel.LinkClicked += OnDeviceSignInLinkClicked;

        _createTokenLinkLabel = new LinkLabel
        {
            AutoSize = true,
            Location = new Point(360, 46),
            Text = "Токен вручную",
        };
        _createTokenLinkLabel.LinkClicked += OnCreateTokenLinkClicked;

        var oauthClientIdLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 78),
            Text = "OAuth Client ID:",
        };

        _oauthClientIdTextBox = new TextBox
        {
            Location = new Point(120, 74),
            Size = new Size(260, 23),
            PlaceholderText = "из GitHub → Developer settings",
        };
        _oauthClientIdTextBox.Leave += OnOAuthClientIdLeave;

        var oauthRegisterLinkLabel = new LinkLabel
        {
            AutoSize = true,
            Location = new Point(388, 78),
            Text = "Создать OAuth App",
        };
        oauthRegisterLinkLabel.LinkClicked += OnOAuthRegisterLinkClicked;

        var oauthClientSecretLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 106),
            Text = "OAuth Client Secret:",
        };

        _oauthClientSecretTextBox = new TextBox
        {
            Location = new Point(120, 102),
            Size = new Size(260, 23),
            UseSystemPasswordChar = true,
            PlaceholderText = "для входа через браузер (необязательно)",
        };
        _oauthClientSecretTextBox.Leave += OnOAuthClientSecretLeave;

        var manualTokenLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 134),
            Text = "Или вставьте Personal Access Token:",
        };

        _tokenTextBox = new TextBox
        {
            Location = new Point(16, 158),
            Size = new Size(488, 23),
            UseSystemPasswordChar = true,
        };

        _tokenStatusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 190),
            Size = new Size(488, 40),
            ForeColor = SystemColors.GrayText,
        };

        var saveButton = new Button
        {
            Location = new Point(16, 236),
            Size = new Size(100, 28),
            Text = "Сохранить",
        };
        saveButton.Click += OnSaveClick;

        var verifyButton = new Button
        {
            Location = new Point(124, 236),
            Size = new Size(100, 28),
            Text = "Проверить",
        };
        verifyButton.Click += OnVerifyClick;

        var clearButton = new Button
        {
            Location = new Point(232, 236),
            Size = new Size(100, 28),
            Text = "Очистить",
        };
        clearButton.Click += OnClearClick;

        var reposLabel = new Label
        {
            AutoSize = true,
            Location = new Point(16, 280),
            Text = "Сетка обоев (перетащите репозитории между ячейками):",
        };

        _gridLayoutEditor = new GridLayoutEditor
        {
            Location = new Point(16, 304),
            Size = new Size(488, 148),
        };
        _gridLayoutEditor.LayoutChanged += OnGridLayoutChanged;

        _repoInputTextBox = new TextBox
        {
            Location = new Point(16, 460),
            Size = new Size(360, 23),
            PlaceholderText = "owner/repo или https://github.com/owner/repo",
        };
        _repoInputTextBox.KeyDown += OnRepoInputKeyDown;

        var addRepoButton = new Button
        {
            Location = new Point(384, 458),
            Size = new Size(120, 28),
            Text = "Добавить",
        };
        addRepoButton.Click += OnAddRepoClick;

        _removeRepoButton = new Button
        {
            Location = new Point(16, 494),
            Size = new Size(120, 28),
            Text = "Удалить",
        };
        _removeRepoButton.Click += OnRemoveRepoClick;

        var displayGroup = new GroupBox
        {
            Location = new Point(16, 536),
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
            Location = new Point(16, 602),
            Size = new Size(488, 172),
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

        _autoCheckUpdatesCheckBox = new CheckBox
        {
            AutoSize = true,
            Location = new Point(12, 148),
            Text = "Проверять обновления автоматически (раз в сутки)",
        };
        _autoCheckUpdatesCheckBox.CheckedChanged += OnBehaviorChanged;

        behaviorGroup.Controls.AddRange([
            pollLabel,
            _economyRadio,
            _normalRadio,
            _frequentRadio,
            _autoStartCheckBox,
            _pauseFullscreenCheckBox,
            _pauseBatteryCheckBox,
            _autoCheckUpdatesCheckBox,
        ]);

        var repoHintLabel = new Label
        {
            AutoSize = false,
            Location = new Point(16, 784),
            Size = new Size(488, 24),
            ForeColor = SystemColors.GrayText,
            Text = "Токен и Client Secret — в Credential Manager. Device Flow не требует Secret.",
        };

        Controls.AddRange([
            tokenLabel,
            _signInWithGitHubButton,
            _deviceSignInLinkLabel,
            _createTokenLinkLabel,
            oauthClientIdLabel,
            _oauthClientIdTextBox,
            oauthRegisterLinkLabel,
            oauthClientSecretLabel,
            _oauthClientSecretTextBox,
            manualTokenLabel,
            _tokenTextBox,
            _tokenStatusLabel,
            saveButton,
            verifyButton,
            clearButton,
            reposLabel,
            _gridLayoutEditor,
            _repoInputTextBox,
            addRepoButton,
            _removeRepoButton,
            displayGroup,
            behaviorGroup,
            repoHintLabel,
        ]);

        PopulateMonitorComboBox();
        LoadBehaviorSettings();
        LoadOAuthClientId();
        LoadOAuthClientSecret();
        LoadGridLayout();
        UpdateTokenStatus();
    }

    private void LoadGridLayout()
    {
        var settings = _settingsStore.Load();
        _suppressGridEvents = true;
        _gridLayoutEditor.LoadLayout(settings.GridColumns, settings.GridRows, settings.RepositorySlots);
        _suppressGridEvents = false;
    }

    private void OnGridLayoutChanged(object? sender, EventArgs e)
    {
        if (_suppressGridEvents)
        {
            return;
        }

        ApplyGridLayout();
    }

    private void LoadOAuthClientId()
    {
        var settings = _settingsStore.Load();
        _oauthClientIdTextBox.Text = settings.GitHubOAuthClientId;
    }

    private void OnOAuthClientIdLeave(object? sender, EventArgs e) => SaveOAuthClientId();

    private void SaveOAuthClientId()
    {
        try
        {
            var settings = _settingsStore.Load();
            settings.GitHubOAuthClientId = _oauthClientIdTextBox.Text.Trim();
            _settingsStore.Save(settings);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось сохранить OAuth Client ID:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void LoadOAuthClientSecret()
    {
        if (GitHubOAuthClientSecretCredentialStore.Exists())
        {
            _oauthClientSecretTextBox.PlaceholderText = "сохранён в Credential Manager";
        }
    }

    private void OnOAuthClientSecretLeave(object? sender, EventArgs e) => SaveOAuthClientSecret();

    private void SaveOAuthClientSecret()
    {
        var secret = _oauthClientSecretTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(secret))
        {
            return;
        }

        try
        {
            GitHubOAuthClientSecretCredentialStore.Save(secret);
            _oauthClientSecretTextBox.Clear();
            _oauthClientSecretTextBox.PlaceholderText = "сохранён в Credential Manager";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось сохранить OAuth Client Secret:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static string? ResolveStoredOAuthClientSecret() =>
        GitHubOAuthClientSecretCredentialStore.Read();

    private void OnOAuthRegisterLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (e.Link is not null)
        {
            e.Link.Visited = true;
        }

        BrowserLauncher.Open(GitHubOAuthDefaults.RegistrationUrl);
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
        _autoCheckUpdatesCheckBox.Checked = settings.AutoCheckForUpdates;
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
            settings.AutoCheckForUpdates = _autoCheckUpdatesCheckBox.Checked;
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

        if (_gridLayoutEditor.ContainsRepository(reference.Slug))
        {
            MessageBox.Show(
                $"Репозиторий {reference.Slug} уже в сетке.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        if (!_gridLayoutEditor.TryAddRepository(reference.Slug))
        {
            MessageBox.Show(
                "Сетка заполнена. Увеличьте число строк или колонок, либо удалите репозиторий.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _repoInputTextBox.Clear();
        ApplyGridLayout();
    }

    private void OnRemoveRepoClick(object? sender, EventArgs e)
    {
        if (_gridLayoutEditor.OccupiedSlotCount <= 1)
        {
            MessageBox.Show(
                "В сетке должен остаться хотя бы один репозиторий.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        if (!_gridLayoutEditor.TryRemoveSelectedRepository())
        {
            MessageBox.Show(
                "Выберите ячейку с репозиторием, который нужно удалить.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        ApplyGridLayout();
    }

    private void ApplyGridLayout()
    {
        try
        {
            _settingsStore.SaveGridLayout(
                _gridLayoutEditor.GridColumns,
                _gridLayoutEditor.GridRows,
                _gridLayoutEditor.GetSlots());

            _repoPoller.Start(_settingsStore.LoadRepositories());

            _wallpaperController.PostMessageAsJson(new
            {
                type = "layout:update",
                payload = new
                {
                    columns = _gridLayoutEditor.GridColumns,
                    rows = _gridLayoutEditor.GridRows,
                },
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось сохранить сетку репозиториев:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void UpdateTokenStatus()
    {
        _tokenStatusLabel.Text = _githubSession.HasStoredToken
            ? "Токен сохранён в Credential Manager. «Войти через GitHub» заменит текущий токен."
            : "Войдите через GitHub в браузере или вставьте PAT вручную. Без токена — лимит 60 запросов/час.";
    }

    private async void OnSignInWithGitHubClick(object? sender, EventArgs e) =>
        await RunOAuthSignInAsync(useDeviceFlowOnly: false).ConfigureAwait(true);

    private async void OnDeviceSignInLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (e.Link is not null)
        {
            e.Link.Visited = true;
        }

        await RunOAuthSignInAsync(useDeviceFlowOnly: true).ConfigureAwait(true);
    }

    private void OnCreateTokenLinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (e.Link is not null)
        {
            e.Link.Visited = true;
        }

        BrowserLauncher.Open("https://github.com/settings/tokens?type=beta");
    }

    private async Task RunOAuthSignInAsync(bool useDeviceFlowOnly)
    {
        SaveOAuthClientId();
        SaveOAuthClientSecret();

        if (GitHubOAuthDefaults.ResolveClientId(_oauthClientIdTextBox.Text.Trim()) is null)
        {
            var answer = MessageBox.Show(
                "Сначала создайте OAuth App на GitHub:\n\n" +
                "• Callback URL: http://127.0.0.1:8791/callback\n" +
                "• Включите Device Flow\n" +
                "• Client ID — строка вида Ov23li… (не номер из URL)\n" +
                "• Client Secret — для входа через браузер (или используйте Device Flow)\n\n" +
                "Открыть страницу регистрации OAuth App?",
                Text,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Information);

            if (answer == DialogResult.Yes)
            {
                BrowserLauncher.Open(GitHubOAuthDefaults.RegistrationUrl);
            }

            return;
        }

        _signInWithGitHubButton.Enabled = false;
        _deviceSignInLinkLabel.Enabled = false;
        UseWaitCursor = true;

        var previousStatus = _tokenStatusLabel.Text;
        var progress = new Progress<string>(message => _tokenStatusLabel.Text = message);

        try
        {
            using var oauth = new GitHubOAuthService(
                _oauthClientIdTextBox.Text.Trim(),
                ResolveStoredOAuthClientSecret());
            using var cancellationSource = new CancellationTokenSource(TimeSpan.FromMinutes(10));

            var result = useDeviceFlowOnly
                ? await oauth.SignInWithDeviceFlowAsync(progress, cancellationSource.Token)
                    .ConfigureAwait(true)
                : await oauth.SignInAsync(progress, cancellationSource.Token).ConfigureAwait(true);

            _githubSession.SaveToken(result.AccessToken);

            var user = await _githubSession.Client.GetAuthenticatedUserAsync(cancellationSource.Token)
                .ConfigureAwait(true);

            var login = TryReadGitHubLogin(user.Body);
            var methodText = result.Method == GitHubOAuthMethod.DeviceFlow
                ? "код устройства"
                : "браузер";

            UpdateTokenStatus();
            MessageBox.Show(
                string.IsNullOrWhiteSpace(login)
                    ? $"Авторизация через {methodText} успешна."
                    : $"Вы вошли как {login} через {methodText}.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            _tokenStatusLabel.Text = previousStatus;
            MessageBox.Show(
                "Время ожидания авторизации истекло.\n\n" +
                "Закройте вкладку GitHub в браузере и повторите вход.",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (GitHubOAuthException ex)
        {
            _tokenStatusLabel.Text = previousStatus;
            MessageBox.Show(
                ex.Message,
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (GitHubApiException ex)
        {
            _tokenStatusLabel.Text = previousStatus;
            var error = GitHubPollError.FromException(ex);
            MessageBox.Show(
                $"Токен получен, но проверка не прошла:\n{error.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            _tokenStatusLabel.Text = previousStatus;
            MessageBox.Show(
                $"Не удалось войти через GitHub:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _signInWithGitHubButton.Enabled = true;
            _deviceSignInLinkLabel.Enabled = true;
            UseWaitCursor = false;
            UpdateTokenStatus();
        }
    }

    private static string? TryReadGitHubLogin(string json)
    {
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("login", out var loginElement))
            {
                return loginElement.GetString();
            }
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return null;
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
