namespace GitHubWallpaper.Settings.Ui;

/// <summary>Прокручиваемая область с градиентным фоном.</summary>
internal sealed class ThemedScrollPanel : Panel
{
    public ThemedScrollPanel()
    {
        SettingsTheme.ApplySurfaceBackground(this);
        AutoScroll = true;
        Dock = DockStyle.Fill;
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        SettingsTheme.PaintFormBackground(e.Graphics, ClientRectangle);
}

/// <summary>Контент внутри карточки.</summary>
internal sealed class CardContentPanel : Panel
{
    public CardContentPanel()
    {
        SettingsTheme.ApplyCardContentBackground(this);
    }
}

/// <summary>Карточка-секция с заголовком и контентом.</summary>
internal sealed class GlassSection : Panel
{
    public GlassSection(string title, int width, bool showTitle = true)
    {
        SettingsTheme.ApplySurfaceBackground(this);
        Width = width;
        Margin = new Padding(0, 0, 0, SettingsTheme.SectionGap);

        var contentTop = SettingsTheme.SectionPadding;
        if (showTitle)
        {
            TitleLabel = new Label
            {
                AutoSize = true,
                Font = SettingsTheme.SectionFont,
                ForeColor = SettingsTheme.TextPrimary,
                Location = new Point(SettingsTheme.SectionPadding, SettingsTheme.SectionPadding),
                Text = title,
            };
            Controls.Add(TitleLabel);
            contentTop += SettingsTheme.TitleHeight + SettingsTheme.ContentGap;
        }

        ContentPanel = new CardContentPanel
        {
            Location = new Point(SettingsTheme.SectionPadding, contentTop),
            Width = width - SettingsTheme.SectionPadding * 2,
        };
        Controls.Add(ContentPanel);
    }

    public Label? TitleLabel { get; }

    public Panel ContentPanel { get; }

    public static int ContentWidth(int sectionWidth) =>
        sectionWidth - SettingsTheme.SectionPadding * 2;

    public void SetContentHeight(int height)
    {
        ContentPanel.Height = Math.Max(1, height);
        Height = ContentPanel.Bottom + SettingsTheme.SectionPadding;
    }

    public void FitToContent()
    {
        ContentPanel.PerformLayout();
        var height = 0;
        foreach (Control child in ContentPanel.Controls)
        {
            if (child.AutoSize)
            {
                child.PerformLayout();
            }

            height = Math.Max(height, child.Bottom);
        }

        if (height <= 0 && ContentPanel.Controls.Count == 1)
        {
            height = ContentPanel.Controls[0].PreferredSize.Height;
        }

        SetContentHeight(Math.Max(1, height));
    }

    public void SetTitleVisible(bool visible)
    {
        if (TitleLabel is null)
        {
            return;
        }

        TitleLabel.Visible = visible;
        var contentTop = SettingsTheme.SectionPadding
            + (visible ? SettingsTheme.TitleHeight + SettingsTheme.ContentGap : 0);
        ContentPanel.Location = new Point(SettingsTheme.SectionPadding, contentTop);
        ContentPanel.Width = Width - SettingsTheme.SectionPadding * 2;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(SettingsTheme.BackgroundTop);
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        SettingsTheme.PaintCard(e.Graphics, bounds, SettingsTheme.CornerRadius);
    }
}

/// <summary>Внутренняя панель с чуть более тёмным фоном (зона сетки).</summary>
internal sealed class InnerPanel : Panel
{
    public InnerPanel()
    {
        BackColor = SettingsTheme.InnerPanelFill;
        SettingsTheme.EnableDoubleBuffer(this);
        Padding = new Padding(12);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        SettingsTheme.PaintInnerPanel(e.Graphics, bounds, SettingsTheme.ControlCornerRadius);
    }
}

/// <summary>Базовая кнопка с кастомной отрисовкой.</summary>
internal abstract class ThemedButtonBase : Button
{
    protected ThemedButtonBase()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        UpdateStyles();
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Height = SettingsTheme.ControlHeight;
        SettingsTheme.EnableDoubleBuffer(this);
    }
}

/// <summary>Оранжевая кнопка действия.</summary>
internal sealed class GlowButton : ThemedButtonBase
{
    private bool _hover;

    public GlowButton()
    {
        BackColor = SettingsTheme.Accent;
        ForeColor = Color.White;
        Font = new Font("Segoe UI Semibold", 9.25F, FontStyle.Bold);
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        var fill = _hover ? SettingsTheme.AccentHover : SettingsTheme.Accent;
        if (!Enabled)
        {
            fill = Color.FromArgb(120, fill);
        }

        using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
        using var brush = new SolidBrush(fill);
        pevent.Graphics.FillPath(brush, path);

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(pevent.Graphics, Text, Font, bounds, ForeColor, flags);
    }
}

/// <summary>Вторичная кнопка с рамкой.</summary>
internal sealed class GhostButton : ThemedButtonBase
{
    private bool _hover;

    public GhostButton()
    {
        BackColor = SettingsTheme.CardFill;
        ForeColor = SettingsTheme.TextPrimary;
        Font = SettingsTheme.BodyFont;
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        var fill = _hover ? Color.FromArgb(36, 52, 58, 72) : Color.FromArgb(20, 52, 58, 72);
        using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
        using var brush = new SolidBrush(fill);
        pevent.Graphics.FillPath(brush, path);
        using var border = new Pen(SettingsTheme.CardBorder, 1f);
        pevent.Graphics.DrawPath(border, path);

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(pevent.Graphics, Text, Font, bounds, ForeColor, flags);
    }
}

/// <summary>Кнопка с цветной обводкой (например «Выйти»).</summary>
internal sealed class OutlineButton : ThemedButtonBase
{
    private readonly Color _accent;
    private bool _hover;

    public OutlineButton(Color accent)
    {
        _accent = accent;
        BackColor = SettingsTheme.CardFill;
        ForeColor = SettingsTheme.TextPrimary;
        Font = SettingsTheme.BodyFont;
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
        using var fill = new SolidBrush(Color.FromArgb(_hover ? 32 : 16, _accent));
        pevent.Graphics.FillPath(fill, path);
        using var border = new Pen(_accent, 1.5f);
        pevent.Graphics.DrawPath(border, path);

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(pevent.Graphics, Text, Font, bounds, ForeColor, flags);
    }
}

/// <summary>Горизонтальный переключатель из pill-кнопок.</summary>
internal sealed class SegmentedChoice : Panel
{
    private readonly TableLayoutPanel _layout;
    private readonly List<SegmentButton> _segments = [];
    private bool _suppressEvents;
    private int _selectedIndex;

    public SegmentedChoice()
    {
        SettingsTheme.ApplyCardContentBackground(this);
        Height = SettingsTheme.ControlHeight;

        _layout = new TableLayoutPanel
        {
            ColumnCount = 3,
            Dock = DockStyle.Fill,
            Margin = Padding.Empty,
            RowCount = 1,
        };
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34f));
        _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        SettingsTheme.ApplyCardContentBackground(_layout);
        Controls.Add(_layout);
    }

    public event EventHandler? SelectionChanged;

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (value < 0 || value >= _segments.Count)
            {
                return;
            }

            if (value == _selectedIndex)
            {
                return;
            }

            _selectedIndex = value;
            UpdateSegmentVisuals();
            if (!_suppressEvents)
            {
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void SetSegments(IReadOnlyList<string> labels)
    {
        _layout.Controls.Clear();
        _segments.Clear();

        for (var index = 0; index < labels.Count; index++)
        {
            var segmentIndex = index;
            var button = new SegmentButton(labels[index])
            {
                Dock = DockStyle.Fill,
                Margin = index < labels.Count - 1 ? new Padding(0, 0, 6, 0) : Padding.Empty,
            };
            button.Click += (_, _) => SelectedIndex = segmentIndex;
            _segments.Add(button);
            _layout.Controls.Add(button, index, 0);
        }

        if (_segments.Count > 0)
        {
            _selectedIndex = Math.Clamp(_selectedIndex, 0, _segments.Count - 1);
            UpdateSegmentVisuals();
        }
    }

    public void SetSelectedIndexSilent(int index)
    {
        if (index < 0 || index >= _segments.Count)
        {
            return;
        }

        _suppressEvents = true;
        _selectedIndex = index;
        UpdateSegmentVisuals();
        _suppressEvents = false;
    }

    private void UpdateSegmentVisuals()
    {
        for (var index = 0; index < _segments.Count; index++)
        {
            _segments[index].IsSelected = index == _selectedIndex;
        }
    }

    private sealed class SegmentButton : ThemedButtonBase
    {
        private bool _selected;
        private bool _hover;

        public SegmentButton(string text)
        {
            Text = text;
            Font = SettingsTheme.SmallFont;
            BackColor = SettingsTheme.SegmentFill;
            MouseEnter += (_, _) => { _hover = true; Invalidate(); };
            MouseLeave += (_, _) => { _hover = false; Invalidate(); };
        }

        public bool IsSelected
        {
            get => _selected;
            set
            {
                _selected = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var bounds = ClientRectangle;
            bounds.Width -= 1;
            bounds.Height -= 1;

            Color fill;
            Color border;
            Color textColor;
            if (_selected)
            {
                fill = SettingsTheme.SegmentActive;
                border = SettingsTheme.AccentBlue;
                textColor = SettingsTheme.TextPrimary;
            }
            else if (_hover)
            {
                fill = Color.FromArgb(40, SettingsTheme.SegmentFill);
                border = SettingsTheme.CardBorder;
                textColor = SettingsTheme.TextPrimary;
            }
            else
            {
                fill = SettingsTheme.SegmentFill;
                border = SettingsTheme.CardBorder;
                textColor = SettingsTheme.TextMuted;
            }

            using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
            using var brush = new SolidBrush(fill);
            pevent.Graphics.FillPath(brush, path);
            using var pen = new Pen(border, _selected ? 1.5f : 1f);
            pevent.Graphics.DrawPath(pen, path);

            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(pevent.Graphics, Text, Font, bounds, textColor, flags);
        }
    }
}

/// <summary>Текстовое поле — обводку рисует <see cref="TextField"/>.</summary>
internal sealed class ThemedTextBox : TextBox
{
    public ThemedTextBox()
    {
        SettingsTheme.ApplyToTextBox(this);
        Margin = Padding.Empty;
    }
}

/// <summary>Обёртка текстового поля со скруглённой рамкой.</summary>
internal sealed class TextField : Panel
{
    public TextField(TextBox inner, int height = SettingsTheme.ControlHeight)
    {
        BackColor = SettingsTheme.CardFill;
        SettingsTheme.EnableDoubleBuffer(this);
        Height = height;
        Padding = new Padding(12, 0, 12, 0);
        inner.Dock = DockStyle.Fill;
        inner.Margin = Padding.Empty;
        Controls.Add(inner);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
        using var fill = new SolidBrush(SettingsTheme.InputFill);
        e.Graphics.FillPath(fill, path);
        using var border = new Pen(SettingsTheme.InputBorder, 1f);
        e.Graphics.DrawPath(border, path);
    }
}

/// <summary>Обёртка выпадающего списка.</summary>
internal sealed class ComboField : Panel
{
    public ComboField(ComboBox inner, int height = SettingsTheme.ControlHeight)
    {
        BackColor = SettingsTheme.CardFill;
        SettingsTheme.EnableDoubleBuffer(this);
        Height = height;
        Padding = new Padding(10, 0, 8, 0);
        inner.Dock = DockStyle.Fill;
        inner.Margin = Padding.Empty;
        inner.FlatStyle = FlatStyle.Flat;
        Controls.Add(inner);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
        using var fill = new SolidBrush(SettingsTheme.InputFill);
        e.Graphics.FillPath(fill, path);
        using var border = new Pen(SettingsTheme.InputBorder, 1f);
        e.Graphics.DrawPath(border, path);
    }
}
