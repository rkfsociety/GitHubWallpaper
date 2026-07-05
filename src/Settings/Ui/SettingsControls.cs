namespace GitHubWallpaper.Settings.Ui;

/// <summary>Прокручиваемая область с градиентным фоном без артефактов по краям.</summary>
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

/// <summary>Контент секции без собственного фона — виден слой карточки родителя.</summary>
internal sealed class GlassContentPanel : Panel
{
    public GlassContentPanel()
    {
        BackColor = Color.Transparent;
        SettingsTheme.EnableDoubleBuffer(this);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }
}

/// <summary>Карточка-секция с бейджем заголовка (как на обоях).</summary>
internal sealed class GlassSection : Panel
{
    private readonly string _title;
    private readonly int _titleBadgeWidth;

    public GlassSection(string title, int width)
    {
        _title = title;
        _titleBadgeWidth = SettingsTheme.MeasureTitleBadgeWidth(title);

        SettingsTheme.ApplySurfaceBackground(this);
        Width = width;
        Margin = new Padding(0, 0, 0, SettingsTheme.SectionGap);

        var contentTop = SettingsTheme.SectionPadding
            + SettingsTheme.TitleBadgeHeight
            + SettingsTheme.SectionTitleGap;

        ContentPanel = new GlassContentPanel
        {
            Location = new Point(
                SettingsTheme.SectionPadding + SettingsTheme.ContentPadding,
                contentTop),
            Width = width - (SettingsTheme.SectionPadding + SettingsTheme.ContentPadding) * 2,
        };

        Controls.Add(ContentPanel);
    }

    public Panel ContentPanel { get; }

    public static int ContentWidth(int sectionWidth) =>
        sectionWidth - (SettingsTheme.SectionPadding + SettingsTheme.ContentPadding) * 2;

    /// <summary>Задаёт высоту контента и полную высоту секции с учётом заголовка и отступов.</summary>
    public void SetContentHeight(int height)
    {
        ContentPanel.Height = height;
        Height = ContentPanel.Bottom + SettingsTheme.SectionPadding;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(SettingsTheme.BackgroundTop);

        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        SettingsTheme.PaintCard(e.Graphics, bounds, SettingsTheme.CornerRadius);

        var badgeBounds = new Rectangle(
            SettingsTheme.SectionPadding,
            SettingsTheme.SectionPadding,
            _titleBadgeWidth,
            SettingsTheme.TitleBadgeHeight);
        SettingsTheme.PaintTitleBadge(e.Graphics, badgeBounds, _title);
    }
}

/// <summary>Базовая кнопка с полностью кастомной отрисовкой.</summary>
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
        Height = 34;
        SettingsTheme.EnableDoubleBuffer(this);
    }
}

/// <summary>Кнопка с оранжевым свечением.</summary>
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
        pevent.Graphics.SetClip(ClientRectangle);
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
        using var glow = new SolidBrush(Color.FromArgb(_hover ? 64 : 44, SettingsTheme.Accent));
        var glowBounds = Rectangle.Inflate(bounds, 3, 3);
        using var glowPath = SettingsTheme.CreateRoundedRectangle(glowBounds, SettingsTheme.ControlCornerRadius + 1);
        pevent.Graphics.FillPath(glow, glowPath);
        using var brush = new SolidBrush(fill);
        pevent.Graphics.FillPath(brush, path);

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(pevent.Graphics, Text, Font, bounds, ForeColor, flags);
    }
}

/// <summary>Полупрозрачная вторичная кнопка.</summary>
internal sealed class GhostButton : ThemedButtonBase
{
    private bool _hover;

    public GhostButton()
    {
        BackColor = Color.Transparent;
        ForeColor = SettingsTheme.TextPrimary;
        Font = SettingsTheme.BodyFont;
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SetClip(ClientRectangle);
        pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        var fill = _hover
            ? Color.FromArgb(40, 48, 54, 61)
            : Color.FromArgb(24, 48, 54, 61);
        using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
        using var brush = new SolidBrush(fill);
        pevent.Graphics.FillPath(brush, path);
        using var border = new Pen(SettingsTheme.CardBorder, 1f);
        pevent.Graphics.DrawPath(border, path);

        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(pevent.Graphics, Text, Font, bounds, ForeColor, flags);
    }
}

/// <summary>Текстовое поле без собственной рамки — обводку рисует <see cref="TextField"/>.</summary>
internal sealed class ThemedTextBox : TextBox
{
    public ThemedTextBox()
    {
        SettingsTheme.ApplyToTextBox(this);
        Margin = Padding.Empty;
    }
}

/// <summary>Обёртка для текстового поля со скруглённой рамкой.</summary>
internal sealed class TextField : Panel
{
    public TextField(TextBox inner, int height = 34)
    {
        SettingsTheme.ApplySurfaceBackground(this);
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
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
        using var fill = new SolidBrush(SettingsTheme.InputFill);
        e.Graphics.FillPath(fill, path);
        using var border = new Pen(SettingsTheme.InputBorder, 1f);
        e.Graphics.DrawPath(border, path);
    }
}

/// <summary>Обёртка для выпадающего списка со скруглённой рамкой.</summary>
internal sealed class ComboField : Panel
{
    public ComboField(ComboBox inner, int height = 34)
    {
        SettingsTheme.ApplySurfaceBackground(this);
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
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
        using var fill = new SolidBrush(SettingsTheme.InputFill);
        e.Graphics.FillPath(fill, path);
        using var border = new Pen(SettingsTheme.InputBorder, 1f);
        e.Graphics.DrawPath(border, path);
    }
}
