namespace GitHubWallpaper.Settings.Ui;

/// <summary>Секция с «стеклянным» фоном и заголовком.</summary>
internal sealed class GlassSection : Panel
{
    public GlassSection(string title, int width)
    {
        SettingsTheme.EnableDoubleBuffer(this);
        BackColor = Color.Transparent;
        Width = width;
        Margin = new Padding(0, 0, 0, SettingsTheme.SectionGap);
        Padding = new Padding(
            SettingsTheme.SectionPadding,
            SettingsTheme.SectionPadding,
            SettingsTheme.SectionPadding,
            SettingsTheme.SectionPadding);

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = SettingsTheme.SectionFont,
            ForeColor = SettingsTheme.TextPrimary,
            Location = new Point(SettingsTheme.SectionPadding, SettingsTheme.SectionPadding),
            Text = title,
        };

        ContentPanel = new Panel
        {
            BackColor = Color.Transparent,
            Location = new Point(SettingsTheme.SectionPadding, SettingsTheme.SectionPadding + 28),
            Width = width - SettingsTheme.SectionPadding * 2,
        };

        Controls.Add(ContentPanel);
        Controls.Add(titleLabel);
    }

    public Panel ContentPanel { get; }

    /// <summary>Задаёт высоту контента и полную высоту секции с учётом заголовка и отступов.</summary>
    public void SetContentHeight(int height)
    {
        ContentPanel.Height = height;
        Height = Padding.Top + ContentPanel.Top + height + Padding.Bottom;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        SettingsTheme.PaintGlassPanel(e.Graphics, bounds, SettingsTheme.CornerRadius);
        base.OnPaint(e);
    }
}

/// <summary>Кнопка с оранжевым свечением.</summary>
internal sealed class GlowButton : Button
{
    private bool _hover;

    public GlowButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = SettingsTheme.Accent;
        ForeColor = Color.White;
        Font = new Font("Segoe UI Semibold", 9.25F, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Height = 32;
        SettingsTheme.EnableDoubleBuffer(this);
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
        using var glow = new SolidBrush(Color.FromArgb(_hover ? 72 : 52, SettingsTheme.Accent));
        var glowBounds = Rectangle.Inflate(bounds, 4, 4);
        using var glowPath = SettingsTheme.CreateRoundedRectangle(glowBounds, SettingsTheme.ControlCornerRadius + 2);
        pevent.Graphics.FillPath(glow, glowPath);
        using var brush = new SolidBrush(fill);
        pevent.Graphics.FillPath(brush, path);

        var text = Text;
        var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
        TextRenderer.DrawText(pevent.Graphics, text, Font, bounds, ForeColor, flags);
    }
}

/// <summary>Полупрозрачная вторичная кнопка.</summary>
internal sealed class GhostButton : Button
{
    private bool _hover;

    public GhostButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = SettingsTheme.TextPrimary;
        Font = SettingsTheme.BodyFont;
        Cursor = Cursors.Hand;
        Height = 32;
        SettingsTheme.EnableDoubleBuffer(this);
        MouseEnter += (_, _) => { _hover = true; Invalidate(); };
        MouseLeave += (_, _) => { _hover = false; Invalidate(); };
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        var fill = _hover
            ? Color.FromArgb(56, 255, 255, 255)
            : Color.FromArgb(28, 255, 255, 255);
        using var path = SettingsTheme.CreateRoundedRectangle(bounds, SettingsTheme.ControlCornerRadius);
        using var brush = new SolidBrush(fill);
        pevent.Graphics.FillPath(brush, path);
        using var border = new Pen(SettingsTheme.GlassBorder, 1f);
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
    public TextField(TextBox inner, int height = 32)
    {
        SettingsTheme.EnableDoubleBuffer(this);
        BackColor = Color.Transparent;
        Height = height;
        Padding = new Padding(12, 0, 12, 0);
        inner.Dock = DockStyle.Fill;
        inner.Margin = Padding.Empty;
        Controls.Add(inner);
    }

    protected override void OnPaint(PaintEventArgs e)
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
        base.OnPaint(e);
    }
}
