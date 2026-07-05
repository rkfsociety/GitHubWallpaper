using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Settings.Ui;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Окно настроек: PAT, список репозиториев и поведение приложения.
/// </summary>
internal sealed class SettingsForm : Form
{
    private const int FormContentWidth = 880;
    private const int GridColumnWidth = 520;
    private const int ColumnGap = 14;
    private static int SideColumnWidth => FormContentWidth - GridColumnWidth - ColumnGap;

    private readonly GitHubSession _githubSession;
    private readonly SettingsStore _settingsStore;
    private readonly RepoPoller _repoPoller;
    private readonly AutoPauseMonitor _autoPauseMonitor;
    private readonly WallpaperController _wallpaperController;
    private TextBox _tokenTextBox = null!;
    private Label _tokenStatusLabel = null!;
    private Button _signInWithGitHubButton = null!;
    private LinkLabel _deviceSignInLinkLabel = null!;
    private LinkLabel _createTokenLinkLabel = null!;
    private TextBox _oauthClientIdTextBox = null!;
    private TextBox _oauthClientSecretTextBox = null!;
    private FlowLayoutPanel _mainContent = null!;
    private GlassSection _authSection = null!;
    private GlassSection _reposSection = null!;
    private GlassSection _displaySection = null!;
    private GlassSection _behaviorSection = null!;
    private Panel _authSignInPanel = null!;
    private Panel _authSignedInPanel = null!;
    private Label _authUserLabel = null!;
    private Button _authLogoutButton = null!;
    private Label _repoHintLabel = null!;
    private int _authSignInContentHeight;
    private const int AuthSignedInContentHeight = 44;
    private GridLayoutEditor _gridLayoutEditor = null!;
    private TextBox _repoInputTextBox = null!;
    private Button _removeRepoButton = null!;
    private SegmentedChoice _pollPresetChoice = null!;
    private CheckBox _autoStartCheckBox = null!;
    private CheckBox _pauseFullscreenCheckBox = null!;
    private CheckBox _pauseBatteryCheckBox = null!;
    private CheckBox _autoCheckUpdatesCheckBox = null!;
    private ComboBox _monitorComboBox = null!;
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
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(FormContentWidth + 40, 620);
        ShowInTaskbar = true;
        BackColor = SettingsTheme.BackgroundTop;
        ForeColor = SettingsTheme.TextPrimary;
        Font = SettingsTheme.BodyFont;
        SettingsTheme.EnableDoubleBuffer(this);

        var scroll = new ThemedScrollPanel();

        var content = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.BackgroundTop,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(24, 20, 24, 20),
            WrapContents = false,
        };
        SettingsTheme.EnableDoubleBuffer(content);
        _mainContent = content;
        scroll.Controls.Add(content);
        scroll.Resize += (_, _) => SyncContentWidth(scroll, content);

        BuildAuthSection(content, FormContentWidth);

        var bottomRow = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.BackgroundTop,
            ColumnCount = 2,
            Margin = Padding.Empty,
            RowCount = 1,
            Width = FormContentWidth,
        };
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, GridColumnWidth));
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SideColumnWidth));
        SettingsTheme.EnableDoubleBuffer(bottomRow);

        var rightColumn = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.BackgroundTop,
            FlowDirection = FlowDirection.TopDown,
            Margin = new Padding(ColumnGap, 0, 0, 0),
            WrapContents = false,
            Width = SideColumnWidth,
        };
        SettingsTheme.EnableDoubleBuffer(rightColumn);

        BuildReposSection(bottomRow, GridColumnWidth);
        BuildDisplaySection(rightColumn, SideColumnWidth);
        BuildBehaviorSection(rightColumn, SideColumnWidth);
        bottomRow.Controls.Add(rightColumn, 1, 0);
        content.Controls.Add(bottomRow);

        var repoHintLabel = CreateMutedLabel(
            "Токен и Client Secret — в Credential Manager. Device Flow не требует Secret.");
        repoHintLabel.Width = FormContentWidth;
        repoHintLabel.Height = 36;
        _repoHintLabel = repoHintLabel;
        content.Controls.Add(repoHintLabel);

        Controls.Add(scroll);

        PopulateMonitorComboBox();
        LoadBehaviorSettings();
        LoadOAuthClientId();
        LoadOAuthClientSecret();
        LoadGridLayout();
        UpdateTokenStatus();
        UpdateAuthView();
        RefreshAuthUserAsync();

        Load += (_, _) =>
        {
            SyncContentWidth(scroll, content);
            RefreshSectionHeights();
            FitFormToContent(content);
        };
    }

    private void RefreshSectionHeights()
    {
        if (_githubSession.HasStoredToken)
        {
            _authSection.SetTitleVisible(false);
            _authSection.SetContentHeight(AuthSignedInContentHeight);
        }
        else
        {
            _authSection.SetTitleVisible(true);
            _authSection.SetContentHeight(_authSignInContentHeight);
        }

        _reposSection.FitToContent();
        _displaySection.FitToContent();
        _behaviorSection.FitToContent();
    }

    private static void SyncContentWidth(Panel scroll, FlowLayoutPanel content)
    {
        var width = scroll.ClientSize.Width;
        if (scroll.VerticalScroll.Visible)
        {
            width -= SystemInformation.VerticalScrollBarWidth;
        }

        content.Width = Math.Max(1, width);
    }

    private void FitFormToContent(FlowLayoutPanel content)
    {
        content.PerformLayout();
        var maxHeight = Screen.FromControl(this).WorkingArea.Height - 48;
        var desiredHeight = Math.Min(content.PreferredSize.Height, maxHeight);
        ClientSize = new Size(ClientSize.Width, Math.Max(desiredHeight, 480));
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        SettingsTheme.PaintFormBackground(e.Graphics, ClientRectangle);

    private void BuildAuthSection(FlowLayoutPanel parent, int sectionWidth)
    {
        _authSection = new GlassSection("Авторизация GitHub", sectionWidth);
        var panel = _authSection.ContentPanel;
        var innerWidth = GlassSection.ContentWidth(sectionWidth);

        _authSignInPanel = new Panel
        {
            Location = Point.Empty,
            Width = innerWidth,
        };
        SettingsTheme.ApplyCardContentBackground(_authSignInPanel);

        _authSignedInPanel = new Panel
        {
            Location = Point.Empty,
            Visible = false,
            Width = innerWidth,
            Height = AuthSignedInContentHeight,
        };
        SettingsTheme.ApplyCardContentBackground(_authSignedInPanel);

        var signedInRow = new TableLayoutPanel
        {
            ColumnCount = 2,
            Dock = DockStyle.Fill,
            Height = AuthSignedInContentHeight,
            RowCount = 1,
        };
        signedInRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        signedInRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108f));
        signedInRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        SettingsTheme.ApplyCardContentBackground(signedInRow);

        _authUserLabel = new Label
        {
            AutoEllipsis = true,
            Dock = DockStyle.Fill,
            Font = SettingsTheme.SectionFont,
            ForeColor = SettingsTheme.TextPrimary,
            Text = "Авторизован",
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _authLogoutButton = new OutlineButton(SettingsTheme.AccentPurple)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(8, 0, 0, 0),
            Text = "Выйти",
        };
        _authLogoutButton.Click += OnLogoutClick;

        signedInRow.Controls.Add(_authUserLabel, 0, 0);
        signedInRow.Controls.Add(_authLogoutButton, 1, 0);
        _authSignedInPanel.Controls.Add(signedInRow);

        var y = 0;

        _signInWithGitHubButton = new GlowButton
        {
            Location = new Point(0, y),
            Size = new Size(196, 34),
            Text = "Войти через GitHub",
        };
        _signInWithGitHubButton.Click += OnSignInWithGitHubClick;

        _deviceSignInLinkLabel = new LinkLabel
        {
            AutoSize = true,
            Location = new Point(208, y + 8),
            Text = "Вход по коду устройства",
        };
        SettingsTheme.ApplyToLink(_deviceSignInLinkLabel);
        _deviceSignInLinkLabel.LinkClicked += OnDeviceSignInLinkClicked;

        _createTokenLinkLabel = new LinkLabel
        {
            AutoSize = true,
            Location = new Point(innerWidth - 108, y + 8),
            Text = "Токен вручную",
        };
        SettingsTheme.ApplyToLink(_createTokenLinkLabel);
        _createTokenLinkLabel.LinkClicked += OnCreateTokenLinkClicked;

        y += 44;

        var oauthClientIdLabel = CreateFieldLabel("OAuth Client ID");
        oauthClientIdLabel.Location = new Point(0, y + 6);
        _oauthClientIdTextBox = new ThemedTextBox
        {
            PlaceholderText = "из GitHub → Developer settings",
            Width = 280,
        };
        _oauthClientIdTextBox.Leave += OnOAuthClientIdLeave;
        var oauthClientIdField = new TextField(_oauthClientIdTextBox)
        {
            Location = new Point(112, y),
            Width = innerWidth - 112 - 136,
        };

        var oauthRegisterLinkLabel = new LinkLabel
        {
            AutoSize = true,
            Location = new Point(innerWidth - 128, y + 8),
            Text = "Создать OAuth App",
        };
        SettingsTheme.ApplyToLink(oauthRegisterLinkLabel);
        oauthRegisterLinkLabel.LinkClicked += OnOAuthRegisterLinkClicked;

        y += 38;

        var oauthClientSecretLabel = CreateFieldLabel("Client Secret");
        oauthClientSecretLabel.Location = new Point(0, y + 6);
        _oauthClientSecretTextBox = new ThemedTextBox
        {
            PlaceholderText = "для входа через браузер (необязательно)",
            UseSystemPasswordChar = true,
            Width = 280,
        };
        _oauthClientSecretTextBox.Leave += OnOAuthClientSecretLeave;
        var oauthClientSecretField = new TextField(_oauthClientSecretTextBox)
        {
            Location = new Point(112, y),
            Width = innerWidth - 112,
        };

        y += 44;

        var manualTokenLabel = CreateMutedLabel("Или вставьте Personal Access Token:");
        manualTokenLabel.Location = new Point(0, y);
        manualTokenLabel.AutoSize = true;
        y += 22;

        _tokenTextBox = new ThemedTextBox
        {
            UseSystemPasswordChar = true,
            Width = innerWidth,
        };
        var tokenField = new TextField(_tokenTextBox)
        {
            Location = new Point(0, y),
            Width = innerWidth,
        };
        y += 38;

        _tokenStatusLabel = CreateMutedLabel(string.Empty);
        _tokenStatusLabel.Location = new Point(0, y);
        _tokenStatusLabel.Size = new Size(innerWidth, 42);
        y += 48;

        var saveButton = new GlowButton
        {
            Location = new Point(0, y),
            Size = new Size(112, 34),
            Text = "Сохранить",
        };
        saveButton.Click += OnSaveClick;

        var verifyButton = new GhostButton
        {
            Location = new Point(120, y),
            Size = new Size(112, 34),
            Text = "Проверить",
        };
        verifyButton.Click += OnVerifyClick;

        var clearButton = new GhostButton
        {
            Location = new Point(240, y),
            Size = new Size(112, 34),
            Text = "Очистить",
        };
        clearButton.Click += OnClearClick;

        y += 46;
        _authSignInContentHeight = y;
        _authSignInPanel.Height = y;

        _authSignInPanel.Controls.AddRange([
            _signInWithGitHubButton,
            _deviceSignInLinkLabel,
            _createTokenLinkLabel,
            oauthClientIdLabel,
            oauthClientIdField,
            oauthRegisterLinkLabel,
            oauthClientSecretLabel,
            oauthClientSecretField,
            manualTokenLabel,
            tokenField,
            _tokenStatusLabel,
            saveButton,
            verifyButton,
            clearButton,
        ]);

        panel.Controls.Add(_authSignInPanel);
        panel.Controls.Add(_authSignedInPanel);
        _authSection.SetContentHeight(_authSignInContentHeight);

        parent.Controls.Add(_authSection);
    }

    private void UpdateAuthView()
    {
        var signedIn = _githubSession.HasStoredToken;
        _authSignInPanel.Visible = !signedIn;
        _authSignedInPanel.Visible = signedIn;
        _repoHintLabel.Visible = !signedIn;
        _authSection.SetTitleVisible(!signedIn);
        _authSection.SetContentHeight(signedIn ? AuthSignedInContentHeight : _authSignInContentHeight);
        FitFormToContent(_mainContent);
    }

    private void SetAuthUserLogin(string? login)
    {
        _authUserLabel.Text = string.IsNullOrWhiteSpace(login)
            ? "Авторизован"
            : $"@{login.Trim()}";
    }

    private async void RefreshAuthUserAsync()
    {
        if (!_githubSession.HasStoredToken)
        {
            return;
        }

        SetAuthUserLogin(null);
        _authUserLabel.Text = "Загрузка…";

        try
        {
            var user = await _githubSession.Client.GetAuthenticatedUserAsync().ConfigureAwait(true);
            SetAuthUserLogin(TryReadGitHubLogin(user.Body));
        }
        catch
        {
            SetAuthUserLogin(null);
        }
    }

    private void OnLogoutClick(object? sender, EventArgs e)
    {
        try
        {
            _githubSession.ClearToken();
            _tokenTextBox.Clear();
            UpdateTokenStatus();
            UpdateAuthView();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось выйти:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void BuildReposSection(TableLayoutPanel parent, int sectionWidth)
    {
        var section = new GlassSection("Сетка обоев", sectionWidth);
        _reposSection = section;
        var panel = section.ContentPanel;
        var innerWidth = GlassSection.ContentWidth(sectionWidth);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            RowCount = 3,
            Width = innerWidth,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        SettingsTheme.ApplyCardContentBackground(layout);

        var gridHost = new InnerPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Margin = new Padding(0, 0, 0, SettingsTheme.ContentGap),
            Width = innerWidth,
        };
        SettingsTheme.EnableDoubleBuffer(gridHost);

        var gridHint = CreateMutedLabel("Перетащите репозитории между ячейками");
        gridHint.AutoSize = true;
        gridHint.Dock = DockStyle.Top;
        gridHint.Margin = new Padding(0, 0, 0, 8);
        gridHost.Controls.Add(gridHint);

        _gridLayoutEditor = new GridLayoutEditor
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Width = innerWidth - 24,
        };
        _gridLayoutEditor.LayoutChanged += OnGridLayoutChanged;
        gridHost.Controls.Add(_gridLayoutEditor);
        layout.Controls.Add(gridHost, 0, 0);

        var inputRow = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, SettingsTheme.ContentGap),
            RowCount = 1,
            Width = innerWidth,
        };
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        inputRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128f));
        inputRow.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsTheme.ControlHeight));
        SettingsTheme.ApplyCardContentBackground(inputRow);

        _repoInputTextBox = new ThemedTextBox
        {
            PlaceholderText = "owner/repo или https://github.com/owner/repo",
        };
        _repoInputTextBox.KeyDown += OnRepoInputKeyDown;
        var repoInputField = new TextField(_repoInputTextBox)
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 8, 0),
        };

        var addRepoButton = new GlowButton
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            Text = "Добавить",
        };
        addRepoButton.Click += OnAddRepoClick;

        inputRow.Controls.Add(repoInputField, 0, 0);
        inputRow.Controls.Add(addRepoButton, 1, 0);
        layout.Controls.Add(inputRow, 0, 1);

        _removeRepoButton = new GhostButton
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Size = new Size(128, SettingsTheme.ControlHeight),
            Text = "Удалить",
        };
        _removeRepoButton.Click += OnRemoveRepoClick;
        layout.Controls.Add(_removeRepoButton, 0, 2);

        panel.Controls.Add(layout);
        section.FitToContent();
        parent.Controls.Add(section, 0, 0);
    }

    private void BuildDisplaySection(FlowLayoutPanel parent, int sectionWidth)
    {
        var section = new GlassSection("Экран", sectionWidth);
        _displaySection = section;
        var panel = section.ContentPanel;
        var innerWidth = GlassSection.ContentWidth(sectionWidth);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            RowCount = 2,
            Width = innerWidth,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsTheme.ControlHeight));
        SettingsTheme.ApplyCardContentBackground(layout);

        var monitorLabel = CreateMutedLabel("Монитор");
        monitorLabel.AutoSize = true;
        monitorLabel.Margin = new Padding(0, 0, 0, 6);
        layout.Controls.Add(monitorLabel, 0, 0);

        _monitorComboBox = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        SettingsTheme.ApplyToComboBox(_monitorComboBox);
        _monitorComboBox.SelectedIndexChanged += OnBehaviorChanged;

        var monitorField = new ComboField(_monitorComboBox)
        {
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
        };
        layout.Controls.Add(monitorField, 0, 1);

        panel.Controls.Add(layout);
        section.FitToContent();
        parent.Controls.Add(section);
    }

    private void BuildBehaviorSection(FlowLayoutPanel parent, int sectionWidth)
    {
        var section = new GlassSection("Поведение", sectionWidth);
        _behaviorSection = section;
        var panel = section.ContentPanel;
        var innerWidth = GlassSection.ContentWidth(sectionWidth);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            RowCount = 6,
            Width = innerWidth,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsTheme.ControlHeight));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        SettingsTheme.ApplyCardContentBackground(layout);

        var pollLabel = CreateMutedLabel("Интервал опроса GitHub API");
        pollLabel.AutoSize = true;
        pollLabel.Margin = new Padding(0, 0, 0, 8);
        layout.Controls.Add(pollLabel, 0, 0);

        _pollPresetChoice = new SegmentedChoice
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 0, 0, SettingsTheme.ContentGap),
            Width = innerWidth,
        };
        _pollPresetChoice.SetSegments([
            "Экономный 15/10",
            "Нормальный 5/2",
            "Частый 2/1",
        ]);
        _pollPresetChoice.SelectionChanged += OnBehaviorChanged;
        layout.Controls.Add(_pollPresetChoice, 0, 1);

        _autoStartCheckBox = new CheckBox { AutoSize = true, Text = "Запускать при старте Windows" };
        SettingsTheme.ApplyToCheckBox(_autoStartCheckBox);
        _autoStartCheckBox.CheckedChanged += OnBehaviorChanged;
        layout.Controls.Add(_autoStartCheckBox, 0, 2);

        _pauseFullscreenCheckBox = new CheckBox { AutoSize = true, Text = "Пауза при полноэкранных приложениях" };
        SettingsTheme.ApplyToCheckBox(_pauseFullscreenCheckBox);
        _pauseFullscreenCheckBox.CheckedChanged += OnBehaviorChanged;
        layout.Controls.Add(_pauseFullscreenCheckBox, 0, 3);

        _pauseBatteryCheckBox = new CheckBox { AutoSize = true, Text = "Пауза при работе от батареи" };
        SettingsTheme.ApplyToCheckBox(_pauseBatteryCheckBox);
        _pauseBatteryCheckBox.CheckedChanged += OnBehaviorChanged;
        layout.Controls.Add(_pauseBatteryCheckBox, 0, 4);

        _autoCheckUpdatesCheckBox = new CheckBox { AutoSize = true, Text = "Автопроверка обновлений (раз в сутки)" };
        SettingsTheme.ApplyToCheckBox(_autoCheckUpdatesCheckBox);
        _autoCheckUpdatesCheckBox.CheckedChanged += OnBehaviorChanged;
        layout.Controls.Add(_autoCheckUpdatesCheckBox, 0, 5);

        panel.Controls.Add(layout);
        section.FitToContent();
        parent.Controls.Add(section);
    }

    private static Label CreateFieldLabel(string text)
    {
        var label = new Label
        {
            AutoSize = true,
            Text = text,
        };
        SettingsTheme.ApplyToLabel(label);
        return label;
    }

    private static Label CreateMutedLabel(string text)
    {
        var label = new Label
        {
            AutoSize = false,
            Text = text,
        };
        SettingsTheme.ApplyToLabel(label, muted: true);
        return label;
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
                _pollPresetChoice.SetSelectedIndexSilent(0);
                break;
            case PollIntervalPreset.Frequent:
                _pollPresetChoice.SetSelectedIndexSilent(2);
                break;
            default:
                _pollPresetChoice.SetSelectedIndexSilent(1);
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

    private PollIntervalPreset GetSelectedPollPreset() =>
        _pollPresetChoice.SelectedIndex switch
        {
            0 => PollIntervalPreset.Economy,
            2 => PollIntervalPreset.Frequent,
            _ => PollIntervalPreset.Normal,
        };

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
                "Сетка заполнена — достигнут максимум колонок (6). Удалите репозиторий.",
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
            UpdateAuthView();
            SetAuthUserLogin(login);
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
            UpdateAuthView();
            RefreshAuthUserAsync();
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
            var user = await _githubSession.Client.GetAuthenticatedUserAsync().ConfigureAwait(true);
            UpdateTokenStatus();
            UpdateAuthView();
            SetAuthUserLogin(TryReadGitHubLogin(user.Body));
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
            UpdateAuthView();
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
