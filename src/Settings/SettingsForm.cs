using GitHubWallpaper.Desktop;
using GitHubWallpaper.GitHub;
using GitHubWallpaper.Settings.Ui;

namespace GitHubWallpaper.Settings;

/// <summary>
/// Окно настроек: PAT, список репозиториев и поведение приложения.
/// </summary>
internal sealed class SettingsForm : Form
{
    private const int FormPadding = 28;
    private const int CompactVerticalPadding = 16;
    private const int ContentWidth = 880;
    private const int ColumnGap = 16;
    private const int SidebarWidth = 320;
    private static int SettingsColumnWidth => (ContentWidth - ColumnGap) / 2;
    private static int WorkspaceMainWidth =>
        SettingsCard.BodyWidth(ContentWidth) - SidebarWidth - ColumnGap;

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
    private ThemedContentPanel _contentPanel = null!;
    private TableLayoutPanel _pageLayout = null!;
    private SettingsCard _authCard = null!;
    private SettingsCard _reposCard = null!;
    private SettingsCard _displayCard = null!;
    private SettingsCard _monitorCard = null!;
    private SettingsCard _pollCard = null!;
    private SettingsCard _startupCard = null!;
    private Panel _authSignInPanel = null!;
    private Panel _authSignedInPanel = null!;
    private Label _authUserLabel = null!;
    private Button _authLogoutButton = null!;
    private Label _repoHintLabel = null!;
    private Panel _reposLeftPanel = null!;
    private GridLayoutEditor _gridLayoutEditor = null!;
    private TextBox _repoInputTextBox = null!;
    private Button _removeRepoButton = null!;
    private RadioButton _economyRadio = null!;
    private RadioButton _normalRadio = null!;
    private RadioButton _frequentRadio = null!;
    private CheckBox _autoStartCheckBox = null!;
    private CheckBox _pauseFullscreenCheckBox = null!;
    private CheckBox _pauseBatteryCheckBox = null!;
    private CheckBox _autoCheckUpdatesCheckBox = null!;
    private ComboBox _monitorComboBox = null!;
    private ToggleSettingRow _showDescriptionToggle = null!;
    private ToggleSettingRow _showStatsToggle = null!;
    private ToggleSettingRow _showCiToggle = null!;
    private ToggleSettingRow _showReleaseToggle = null!;
    private ToggleSettingRow _showHeatmapToggle = null!;
    private ToggleSettingRow _showFeedToggle = null!;
    private ToggleSettingRow _showPullRequestsToggle = null!;
    private ToggleSettingRow _showIssuesToggle = null!;
    private ToggleSettingRow _showCommitsToggle = null!;
    private bool _suppressBehaviorEvents;
    private bool _suppressGridEvents;
    private bool _suppressCardDisplayEvents;
    private bool _windowPositionApplied;

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
        StartPosition = FormStartPosition.Manual;
        ShowInTaskbar = true;
        BackColor = SettingsTheme.BackgroundTop;
        ForeColor = SettingsTheme.TextPrimary;
        Font = SettingsTheme.BodyFont;
        SettingsTheme.EnableDoubleBuffer(this);

        _contentPanel = new ThemedContentPanel
        {
            Padding = new Padding(FormPadding),
        };
        _pageLayout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.BackgroundTop,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Padding = Padding.Empty,
            Width = ContentWidth,
        };
        _pageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        SettingsTheme.EnableDoubleBuffer(_pageLayout);

        BuildMainColumns(_pageLayout);

        _repoHintLabel = CreateMutedLabel(
            "Токен и Client Secret — в Credential Manager. Device Flow не требует Secret.");
        _repoHintLabel.AutoSize = true;
        _repoHintLabel.Margin = new Padding(0, 4, 0, 0);
        _repoHintLabel.MaximumSize = new Size(ContentWidth, 0);
        AddPageRow(_repoHintLabel);

        _contentPanel.Controls.Add(_pageLayout);
        Controls.Add(_contentPanel);

        PopulateMonitorComboBox();
        LoadBehaviorSettings();
        LoadCardDisplaySettings();
        LoadOAuthClientId();
        LoadOAuthClientSecret();
        LoadGridLayout();
        UpdateTokenStatus();
        UpdateAuthView();
        RefreshAuthUserAsync();

        Load += (_, _) => RefreshAllCardHeights();
        Shown += (_, _) => RefreshAllCardHeights();
        FormClosing += (_, _) => SaveWindowPosition();
    }

    private void RefreshAllCardHeights()
    {
        UpdateAuthView();
        RefreshSettingsCardHeights();
        FitClientToContent();
    }

    private void RefreshSettingsCardHeights()
    {
        RefreshReposCardHeight();
        RefreshSidebarCardHeights();
        RefreshStartupCardHeight();
    }

    private void RefreshSidebarCardHeights()
    {
        RefreshDisplayCardHeight();
        RefreshMonitorCardHeight();
        RefreshPollCardHeight();
    }

    private void RefreshMonitorCardHeight()
    {
        if (_monitorCard?.Body.Controls.Count is not > 0)
        {
            return;
        }

        var layout = _monitorCard.Body.Controls[0];
        _monitorCard.ApplyBodyHeight(SettingsCard.MeasureControl(layout));
    }

    private void FitClientToContent()
    {
        var clientWidth = ContentWidth + FormPadding * 2;
        var maxHeight = GetMaxClientHeight();

        ApplyPagePadding(FormPadding);
        if (_gridLayoutEditor is not null)
        {
            _gridLayoutEditor.SetSlotRowHeight(GridLayoutEditor.DefaultSlotRowHeight);
        }
        var height = RemeasureContentHeight();

        if (height > maxHeight)
        {
            for (var slot = GridLayoutEditor.DefaultSlotRowHeight - 1;
                 slot >= GridLayoutEditor.MinSlotRowHeight;
                 slot--)
            {
                _gridLayoutEditor?.SetSlotRowHeight(slot);
                height = RemeasureContentHeight();
                if (height <= maxHeight)
                {
                    break;
                }
            }
        }

        if (height > maxHeight)
        {
            ApplyPagePadding(CompactVerticalPadding);
            height = RemeasureContentHeight();
        }

        var clientHeight = Math.Min(height, maxHeight);
        ClientSize = new Size(clientWidth, clientHeight);

        var needsScroll = height > maxHeight;
        _contentPanel.AutoScroll = needsScroll;
        _contentPanel.AutoScrollMinSize = needsScroll
            ? new Size(ContentWidth + FormPadding * 2, height)
            : Size.Empty;

        ApplyWindowPosition();
    }

    private void ApplyWindowPosition()
    {
        if (_windowPositionApplied)
        {
            return;
        }

        _windowPositionApplied = true;

        var settings = _settingsStore.Load();
        if (settings.SettingsWindowLeft is int left && settings.SettingsWindowTop is int top)
        {
            Location = ClampToWorkingArea(new Point(left, top));
            return;
        }

        CenterOnWorkingArea();
    }

    private void CenterOnWorkingArea()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var area = screen.WorkingArea;
        var x = area.Left + Math.Max(0, (area.Width - Width) / 2);
        var y = area.Top + Math.Max(0, (area.Height - Height) / 2);
        Location = new Point(x, y);
    }

    private Point ClampToWorkingArea(Point location)
    {
        var screen = Screen.FromPoint(location);
        var area = screen.WorkingArea;
        var maxX = Math.Max(area.Left, area.Right - Width);
        var maxY = Math.Max(area.Top, area.Bottom - Height);
        return new Point(
            Math.Clamp(location.X, area.Left, maxX),
            Math.Clamp(location.Y, area.Top, maxY));
    }

    private void SaveWindowPosition()
    {
        var settings = _settingsStore.Load();
        settings.SettingsWindowLeft = Location.X;
        settings.SettingsWindowTop = Location.Y;
        _settingsStore.Save(settings);
    }

    private void ApplyPagePadding(int verticalPadding)
    {
        _contentPanel.Padding = new Padding(FormPadding, verticalPadding, FormPadding, verticalPadding);
    }

    private int RemeasureContentHeight()
    {
        UpdateAuthCardHeight();
        RefreshSettingsCardHeights();
        _pageLayout.PerformLayout();
        return _contentPanel.Padding.Vertical + _pageLayout.Height;
    }

    private int GetMaxClientHeight()
    {
        var screen = IsHandleCreated
            ? Screen.FromControl(this)
            : Screen.FromPoint(Location != Point.Empty ? Location : Cursor.Position);
        var chrome = SystemInformation.FrameBorderSize.Height * 2 + SystemInformation.CaptionHeight;
        return Math.Max(420, screen.WorkingArea.Height - chrome - 24);
    }

    private void RefreshReposCardHeight()
    {
        if (_reposCard?.Body.Controls.Count is not > 0)
        {
            return;
        }

        _reposCard.ApplyBodyHeight(MeasureReposCardBodyHeight());
    }

    private int MeasureReposCardBodyHeight()
    {
        if (_reposCard?.Body is not { } body)
        {
            return 1;
        }

        var height = 0;
        foreach (Control control in body.Controls)
        {
            if (!control.Visible)
            {
                continue;
            }

            height += SettingsCard.MeasureControl(control) + control.Margin.Vertical;
        }

        return Math.Max(1, height);
    }

    private void RefreshDisplayCardHeight()
    {
        if (_displayCard?.Body.Controls.Count is not > 0)
        {
            return;
        }

        var layout = _displayCard.Body.Controls[0];
        _displayCard.ApplyBodyHeight(SettingsCard.MeasureControl(layout));
    }

    private void RefreshPollCardHeight()
    {
        if (_pollCard?.Body.Controls.Count is not > 0)
        {
            return;
        }

        var layout = _pollCard.Body.Controls[0];
        _pollCard.ApplyBodyHeight(SettingsCard.MeasureControl(layout));
    }

    private void RefreshStartupCardHeight()
    {
        if (_startupCard?.Body.Controls.Count is not > 0)
        {
            return;
        }

        var layout = _startupCard.Body.Controls[0];
        _startupCard.ApplyBodyHeight(SettingsCard.MeasureControl(layout));
    }

    private void AddPageRow(Control control)
    {
        var row = _pageLayout.RowCount;
        _pageLayout.RowCount++;
        _pageLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _pageLayout.Controls.Add(control, 0, row);
    }

    private void BuildMainColumns(TableLayoutPanel page)
    {
        BuildAuthCard(page);
        BuildWorkspaceCard(page);
        BuildStartupCard(page);
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        SettingsTheme.PaintFormBackground(e.Graphics, ClientRectangle);

    private void BuildAuthCard(TableLayoutPanel page)
    {
        _authCard = new SettingsCard("Авторизация GitHub", ContentWidth);
        var body = _authCard.Body;
        var innerWidth = SettingsCard.BodyWidth(ContentWidth);

        _authSignInPanel = CreateAuthSignInPanel(innerWidth);
        _authSignedInPanel = CreateAuthSignedInPanel(WorkspaceMainWidth);
        AddPageRow(_authCard);
    }

    private void UpdateAuthCardHeight()
    {
        if (!_authCard.Visible)
        {
            return;
        }

        _authCard.ApplyBodyHeight(SettingsCard.MeasureControl(_authSignInPanel));
    }

    private Panel CreateAuthSignedInPanel(int innerWidth)
    {
        var panel = new Panel
        {
            BackColor = SettingsTheme.CardFill,
            Dock = DockStyle.Top,
            Height = SettingsTheme.ControlHeight,
            Width = innerWidth,
        };

        _authUserLabel = new Label
        {
            AutoEllipsis = true,
            Font = SettingsTheme.SectionFont,
            ForeColor = SettingsTheme.TextPrimary,
            Location = new Point(0, 0),
            Size = new Size(innerWidth - 116, SettingsTheme.ControlHeight),
            Text = "Авторизован",
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _authLogoutButton = new OutlineButton(SettingsTheme.AccentPurple)
        {
            Location = new Point(innerWidth - 108, 0),
            Size = new Size(108, SettingsTheme.ControlHeight),
            Text = "Выйти",
        };
        _authLogoutButton.Click += OnLogoutClick;

        panel.Controls.Add(_authUserLabel);
        panel.Controls.Add(_authLogoutButton);
        return panel;
    }

    private Panel CreateAuthSignInPanel(int innerWidth)
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        void AddRow(Control control, SizeType height = SizeType.AutoSize, int absoluteHeight = 0)
        {
            var row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(absoluteHeight > 0
                ? new RowStyle(SizeType.Absolute, absoluteHeight)
                : new RowStyle(height));
            control.Dock = absoluteHeight > 0 ? DockStyle.Fill : DockStyle.Top;
            control.Margin = new Padding(0, 0, 0, SettingsTheme.ContentGap);
            layout.Controls.Add(control, 0, row);
        }

        var signInRow = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };
        signInRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 196f));
        signInRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        signInRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108f));
        signInRow.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsTheme.ControlHeight));

        _signInWithGitHubButton = new GlowButton { Text = "Войти через GitHub" };
        _signInWithGitHubButton.Click += OnSignInWithGitHubClick;

        _deviceSignInLinkLabel = new LinkLabel
        {
            AutoSize = true,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Padding = new Padding(8, 10, 0, 0),
            Text = "Вход по коду устройства",
        };
        SettingsTheme.ApplyToLink(_deviceSignInLinkLabel);
        _deviceSignInLinkLabel.LinkClicked += OnDeviceSignInLinkClicked;

        _createTokenLinkLabel = new LinkLabel
        {
            AutoSize = true,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Padding = new Padding(0, 10, 0, 0),
            Text = "Токен вручную",
            TextAlign = ContentAlignment.TopRight,
        };
        SettingsTheme.ApplyToLink(_createTokenLinkLabel);
        _createTokenLinkLabel.LinkClicked += OnCreateTokenLinkClicked;

        signInRow.Controls.Add(_signInWithGitHubButton, 0, 0);
        signInRow.Controls.Add(_deviceSignInLinkLabel, 1, 0);
        signInRow.Controls.Add(_createTokenLinkLabel, 2, 0);
        AddRow(signInRow);

        var oauthIdRow = CreateLabeledFieldRow(innerWidth, "OAuth Client ID", out _oauthClientIdTextBox);
        _oauthClientIdTextBox.PlaceholderText = "из GitHub → Developer settings";
        _oauthClientIdTextBox.Leave += OnOAuthClientIdLeave;
        AddRow(oauthIdRow);

        var oauthRegisterLinkLabel = new LinkLabel
        {
            AutoSize = true,
            LinkBehavior = LinkBehavior.HoverUnderline,
            Text = "Создать OAuth App",
        };
        SettingsTheme.ApplyToLink(oauthRegisterLinkLabel);
        oauthRegisterLinkLabel.LinkClicked += OnOAuthRegisterLinkClicked;
        AddRow(oauthRegisterLinkLabel);

        var oauthSecretRow = CreateLabeledFieldRow(innerWidth, "Client Secret", out _oauthClientSecretTextBox);
        _oauthClientSecretTextBox.PlaceholderText = "для входа через браузер (необязательно)";
        _oauthClientSecretTextBox.UseSystemPasswordChar = true;
        _oauthClientSecretTextBox.Leave += OnOAuthClientSecretLeave;
        AddRow(oauthSecretRow);

        AddRow(CreateMutedLabel("Или вставьте Personal Access Token:"));

        _tokenTextBox = new ThemedTextBox { UseSystemPasswordChar = true };
        AddRow(new TextField(_tokenTextBox), SizeType.Absolute, SettingsTheme.ControlHeight);

        _tokenStatusLabel = CreateMutedLabel(string.Empty);
        _tokenStatusLabel.Height = 36;
        AddRow(_tokenStatusLabel);

        var buttonsRow = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        buttonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        buttonsRow.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsTheme.ControlHeight));

        var saveButton = new GlowButton { Text = "Сохранить" };
        saveButton.Click += OnSaveClick;
        var verifyButton = new GhostButton { Text = "Проверить" };
        verifyButton.Click += OnVerifyClick;
        var clearButton = new GhostButton { Text = "Очистить" };
        clearButton.Click += OnClearClick;

        buttonsRow.Controls.Add(saveButton, 0, 0);
        buttonsRow.Controls.Add(verifyButton, 1, 0);
        buttonsRow.Controls.Add(clearButton, 2, 0);
        AddRow(buttonsRow);

        panel.Controls.Add(layout);
        return panel;
    }

    private static TableLayoutPanel CreateLabeledFieldRow(int width, string labelText, out TextBox textBox)
    {
        var row = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Width = width,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        row.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsTheme.ControlHeight));

        var label = new Label
        {
            AutoSize = true,
            ForeColor = SettingsTheme.TextPrimary,
            Padding = new Padding(0, 10, 0, 0),
            Text = labelText,
        };

        textBox = new ThemedTextBox();
        var field = new TextField(textBox) { Dock = DockStyle.Fill };
        row.Controls.Add(label, 0, 0);
        row.Controls.Add(field, 1, 0);
        return row;
    }

    private void UpdateAuthView()
    {
        if (_repoHintLabel is null || _reposLeftPanel is null || _authCard is null)
        {
            return;
        }

        var signedIn = _githubSession.HasStoredToken;
        _repoHintLabel.Visible = !signedIn;
        _authCard.SetTitleVisible(!signedIn);
        _authCard.Visible = !signedIn;

        _authCard.Body.Controls.Clear();
        _authSignInPanel.Visible = !signedIn;
        _authSignedInPanel.Visible = signedIn;

        if (signedIn)
        {
            _authSignedInPanel.Dock = DockStyle.Top;
            _authSignedInPanel.Margin = Padding.Empty;
            _authSignedInPanel.Width = WorkspaceMainWidth;
        }
        else
        {
            _authSignInPanel.Dock = DockStyle.Top;
            _authCard.Body.Controls.Add(_authSignInPanel);
        }

        UpdateAuthCardHeight();
        RefreshReposCardHeight();
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
            RefreshAllCardHeights();
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

    private void BuildWorkspaceCard(TableLayoutPanel page)
    {
        _reposCard = new SettingsCard("Сетка обоев", ContentWidth);
        var body = _reposCard.Body;
        var innerWidth = SettingsCard.BodyWidth(ContentWidth);
        var mainWidth = WorkspaceMainWidth;

        var split = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 2,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, mainWidth));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, SidebarWidth));
        split.RowCount = 1;
        split.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _reposLeftPanel = BuildReposLeftPanel(mainWidth);
        split.Controls.Add(_reposLeftPanel, 0, 0);
        split.Controls.Add(BuildSidebar(SidebarWidth), 1, 0);

        body.Controls.Add(split);
        _reposCard.ApplyBodyHeight(MeasureReposCardBodyHeight());
        AddPageRow(_reposCard);
    }

    private Panel BuildReposLeftPanel(int innerWidth)
    {
        var panel = new Panel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        void AddBodyRow(Control control, SizeType rowSize = SizeType.AutoSize, int absoluteHeight = 0)
        {
            var row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(absoluteHeight > 0
                ? new RowStyle(SizeType.Absolute, absoluteHeight)
                : new RowStyle(rowSize));
            control.Dock = DockStyle.Top;
            control.Margin = new Padding(0, 0, 0, SettingsTheme.ContentGap);
            layout.Controls.Add(control, 0, row);
        }

        var gridHost = new GridHostPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };

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
        AddBodyRow(gridHost);

        _repoInputTextBox = new ThemedTextBox
        {
            PlaceholderText = "owner/repo или https://github.com/owner/repo",
        };
        _repoInputTextBox.KeyDown += OnRepoInputKeyDown;
        AddBodyRow(new TextField(_repoInputTextBox) { Dock = DockStyle.Top, Width = innerWidth });

        var actionsRow = new Panel
        {
            BackColor = SettingsTheme.CardFill,
            Dock = DockStyle.Top,
            Height = SettingsTheme.ControlHeight,
            Width = innerWidth,
        };

        const int actionButtonWidth = 128;
        var addRepoButton = new GlowButton
        {
            Location = Point.Empty,
            Size = new Size(actionButtonWidth, SettingsTheme.ControlHeight),
            Text = "Добавить",
        };
        addRepoButton.Click += OnAddRepoClick;

        _removeRepoButton = new GhostButton
        {
            Location = new Point(actionButtonWidth + 8, 0),
            Size = new Size(actionButtonWidth, SettingsTheme.ControlHeight),
            Text = "Удалить",
        };
        _removeRepoButton.Click += OnRemoveRepoClick;

        actionsRow.Controls.Add(addRepoButton);
        actionsRow.Controls.Add(_removeRepoButton);
        AddBodyRow(actionsRow);

        _authSignedInPanel.Visible = false;
        AddBodyRow(_authSignedInPanel);

        panel.Controls.Add(layout);
        return panel;
    }

    private Control BuildSidebar(int sidebarWidth)
    {
        var sidebar = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Width = sidebarWidth,
        };
        sidebar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        void AddSection(Control section)
        {
            var row = sidebar.RowCount;
            sidebar.RowCount++;
            sidebar.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            section.Dock = DockStyle.Top;
            section.Margin = new Padding(0, 0, 0, SettingsTheme.ContentGap);
            sidebar.Controls.Add(section, 0, row);
        }

        BuildDisplayCard(sidebarWidth);
        AddSection(_displayCard);
        BuildMonitorCard(sidebarWidth);
        AddSection(_monitorCard);
        BuildPollCard(sidebarWidth);
        AddSection(_pollCard);

        return sidebar;
    }

    private Control CreateCardDisplayPanel(int innerWidth)
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        void AddToggleRow(ToggleSettingRow toggle)
        {
            var row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            toggle.Dock = DockStyle.Top;
            toggle.Margin = new Padding(0, 0, 0, 4);
            toggle.Width = innerWidth;
            layout.Controls.Add(toggle, 0, row);
        }

        _showDescriptionToggle = CreateDisplayToggle("Описание");
        _showStatsToggle = CreateDisplayToggle("Статистика");
        _showCiToggle = CreateDisplayToggle("CI статус");
        _showReleaseToggle = CreateDisplayToggle("Последний релиз");
        _showHeatmapToggle = CreateDisplayToggle("Активность (heatmap)");
        _showFeedToggle = CreateDisplayToggle("Лента событий");
        _showPullRequestsToggle = CreateDisplayToggle("Pull requests");
        _showIssuesToggle = CreateDisplayToggle("Issues");
        _showCommitsToggle = CreateDisplayToggle("Последние коммиты");

        AddToggleRow(_showDescriptionToggle);
        AddToggleRow(_showStatsToggle);
        AddToggleRow(_showCiToggle);
        AddToggleRow(_showReleaseToggle);
        AddToggleRow(_showHeatmapToggle);
        AddToggleRow(_showFeedToggle);
        AddToggleRow(_showPullRequestsToggle);
        AddToggleRow(_showIssuesToggle);
        AddToggleRow(_showCommitsToggle);

        return layout;
    }

    private ToggleSettingRow CreateDisplayToggle(string text)
    {
        var toggle = new ToggleSettingRow(text);
        toggle.CheckedChanged += OnCardDisplayChanged;
        return toggle;
    }

    private void LoadCardDisplaySettings()
    {
        if (_showDescriptionToggle is null)
        {
            return;
        }

        var display = _settingsStore.Load().CardDisplay;

        _suppressCardDisplayEvents = true;

        _showDescriptionToggle.Checked = display.ShowDescription;
        _showStatsToggle.Checked = display.ShowStats;
        _showCiToggle.Checked = display.ShowCi;
        _showReleaseToggle.Checked = display.ShowRelease;
        _showHeatmapToggle.Checked = display.ShowHeatmap;
        _showFeedToggle.Checked = display.ShowFeed;
        _showPullRequestsToggle.Checked = display.ShowPullRequests;
        _showIssuesToggle.Checked = display.ShowIssues;
        _showCommitsToggle.Checked = display.ShowCommits;

        _suppressCardDisplayEvents = false;
    }

    private void OnCardDisplayChanged(object? sender, EventArgs e)
    {
        if (_suppressCardDisplayEvents)
        {
            return;
        }

        SaveCardDisplaySettings();
    }

    private CardDisplaySettings ReadCardDisplaySettings() => new()
    {
        ShowDescription = _showDescriptionToggle.Checked,
        ShowStats = _showStatsToggle.Checked,
        ShowCi = _showCiToggle.Checked,
        ShowRelease = _showReleaseToggle.Checked,
        ShowHeatmap = _showHeatmapToggle.Checked,
        ShowFeed = _showFeedToggle.Checked,
        ShowPullRequests = _showPullRequestsToggle.Checked,
        ShowIssues = _showIssuesToggle.Checked,
        ShowCommits = _showCommitsToggle.Checked,
    };

    private void SaveCardDisplaySettings()
    {
        try
        {
            var settings = _settingsStore.Load();
            settings.CardDisplay = ReadCardDisplaySettings();
            _settingsStore.Save(settings);

            _repoPoller.ConfigureCardDisplay(settings.CardDisplay);

            _wallpaperController.PostMessageAsJson(new
            {
                type = "display:update",
                payload = settings.CardDisplay.ToBridgePayload(),
            });

            RefreshAllCardHeights();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Не удалось сохранить настройки карточек:\n{ex.Message}",
                Text,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void BuildDisplayCard(int cardWidth)
    {
        _displayCard = new SettingsCard("Содержимое карточек", cardWidth);
        var innerWidth = SettingsCard.BodyWidth(cardWidth);
        var panel = CreateCardDisplayPanel(innerWidth);
        _displayCard.Body.Controls.Add(panel);
        _displayCard.ApplyBodyHeight(SettingsCard.MeasureControl(panel));
    }

    private void BuildMonitorCard(int cardWidth)
    {
        _monitorCard = new SettingsCard("Экран", cardWidth);
        var body = _monitorCard.Body;
        var innerWidth = SettingsCard.BodyWidth(cardWidth);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        layout.RowCount = 1;
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, SettingsTheme.ControlHeight));

        _monitorComboBox = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        SettingsTheme.ApplyToComboBox(_monitorComboBox);
        _monitorComboBox.SelectedIndexChanged += OnBehaviorChanged;
        layout.Controls.Add(new ComboField(_monitorComboBox) { Dock = DockStyle.Fill }, 0, 0);

        body.Controls.Add(layout);
        _monitorCard.ApplyBodyHeight(SettingsCard.MeasureControl(layout));
    }

    private void BuildPollCard(int cardWidth)
    {
        _pollCard = new SettingsCard("Интервал обновления", cardWidth);
        var body = _pollCard.Body;
        var innerWidth = SettingsCard.BodyWidth(cardWidth);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            ColumnCount = 1,
            Dock = DockStyle.Top,
            Width = innerWidth,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        void AddBodyRow(Control control)
        {
            var row = layout.RowCount;
            layout.RowCount++;
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            control.Dock = DockStyle.Top;
            control.Margin = new Padding(0, 0, 0, 6);
            layout.Controls.Add(control, 0, row);
        }

        _economyRadio = new RadioButton { Text = "Экономный — каждые 20 минут" };
        SettingsTheme.ApplyToRadio(_economyRadio);
        _economyRadio.CheckedChanged += OnBehaviorChanged;
        AddBodyRow(_economyRadio);

        _normalRadio = new RadioButton { Text = "Нормальный — каждые 10 минут" };
        SettingsTheme.ApplyToRadio(_normalRadio);
        _normalRadio.CheckedChanged += OnBehaviorChanged;
        AddBodyRow(_normalRadio);

        _frequentRadio = new RadioButton { Text = "Частый — каждую минуту" };
        SettingsTheme.ApplyToRadio(_frequentRadio);
        _frequentRadio.CheckedChanged += OnBehaviorChanged;
        AddBodyRow(_frequentRadio);

        body.Controls.Add(layout);
        _pollCard.ApplyBodyHeight(SettingsCard.MeasureControl(layout));
    }

    private void BuildStartupCard(TableLayoutPanel page)
    {
        _startupCard = new SettingsCard("Запуск и поведение", ContentWidth);
        var body = _startupCard.Body;
        var innerWidth = SettingsCard.BodyWidth(ContentWidth);

        var flow = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = SettingsTheme.CardFill,
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Width = innerWidth,
        };

        _autoStartCheckBox = new CheckBox { Text = "Запускать при старте Windows", AutoSize = true };
        SettingsTheme.ApplyToCheckBox(_autoStartCheckBox);
        _autoStartCheckBox.CheckedChanged += OnBehaviorChanged;
        _autoStartCheckBox.Margin = new Padding(0, 0, 24, 8);
        flow.Controls.Add(_autoStartCheckBox);

        _pauseFullscreenCheckBox = new CheckBox { Text = "Пауза при полноэкранных приложениях", AutoSize = true };
        SettingsTheme.ApplyToCheckBox(_pauseFullscreenCheckBox);
        _pauseFullscreenCheckBox.CheckedChanged += OnBehaviorChanged;
        _pauseFullscreenCheckBox.Margin = new Padding(0, 0, 24, 8);
        flow.Controls.Add(_pauseFullscreenCheckBox);

        _pauseBatteryCheckBox = new CheckBox { Text = "Пауза при работе от батареи", AutoSize = true };
        SettingsTheme.ApplyToCheckBox(_pauseBatteryCheckBox);
        _pauseBatteryCheckBox.CheckedChanged += OnBehaviorChanged;
        _pauseBatteryCheckBox.Margin = new Padding(0, 0, 24, 8);
        flow.Controls.Add(_pauseBatteryCheckBox);

        _autoCheckUpdatesCheckBox = new CheckBox { Text = "Автопроверка обновлений (раз в сутки)", AutoSize = true };
        SettingsTheme.ApplyToCheckBox(_autoCheckUpdatesCheckBox);
        _autoCheckUpdatesCheckBox.CheckedChanged += OnBehaviorChanged;
        _autoCheckUpdatesCheckBox.Margin = new Padding(0, 0, 24, 8);
        flow.Controls.Add(_autoCheckUpdatesCheckBox);

        body.Controls.Add(flow);
        _startupCard.ApplyBodyHeight(SettingsCard.MeasureControl(flow));
        AddPageRow(_startupCard);
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
            AutoSize = true,
            BackColor = SettingsTheme.CardFill,
            Text = text,
        };
        SettingsTheme.ApplyToLabel(label, muted: true);
        return label;
    }

    private void LoadGridLayout()
    {
        if (_gridLayoutEditor is null)
        {
            return;
        }

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
        RefreshAllCardHeights();
    }

    private void LoadOAuthClientId()
    {
        if (_oauthClientIdTextBox is null)
        {
            return;
        }

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
        if (_oauthClientSecretTextBox is null)
        {
            return;
        }

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
        if (_economyRadio is null || _normalRadio is null || _frequentRadio is null
            || _autoStartCheckBox is null || _pauseFullscreenCheckBox is null
            || _pauseBatteryCheckBox is null || _autoCheckUpdatesCheckBox is null
            || _monitorComboBox is null)
        {
            return;
        }

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
        if (_monitorComboBox is null)
        {
            return;
        }

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
            RefreshAllCardHeights();
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
            RefreshAllCardHeights();
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
            RefreshAllCardHeights();
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
            RefreshAllCardHeights();
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
