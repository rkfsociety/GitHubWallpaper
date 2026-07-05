namespace GitHubWallpaper.Settings.Ui;

/// <summary>Область содержимого с градиентным фоном (прокрутка включается только при нехватке экрана).</summary>
internal sealed class ThemedScrollPanel : Panel
{
    public ThemedScrollPanel()
    {
        SettingsTheme.ApplySurfaceBackground(this);
        AutoScroll = false;
        Dock = DockStyle.Fill;
    }

    protected override void OnPaintBackground(PaintEventArgs e) =>
        SettingsTheme.PaintFormBackground(e.Graphics, ClientRectangle);
}

/// <summary>Карточка секции с явным расчётом высоты (без AutoSize на Panel).</summary>
internal sealed class SettingsCard : Panel
{
    private readonly Label? _titleLabel;
    private int _bodyTop;

    public SettingsCard(string? title, int width)
    {
        Width = width;
        Dock = DockStyle.Top;
        Margin = new Padding(0, 0, 0, SettingsTheme.SectionGap);
        Padding = Padding.Empty;
        BackColor = SettingsTheme.CardFill;
        SettingsTheme.EnableDoubleBuffer(this);

        var y = SettingsTheme.SectionPadding;
        if (!string.IsNullOrEmpty(title))
        {
            _titleLabel = new Label
            {
                AutoSize = true,
                BackColor = SettingsTheme.CardFill,
                Font = SettingsTheme.SectionFont,
                ForeColor = SettingsTheme.TextPrimary,
                Location = new Point(SettingsTheme.SectionPadding, y),
                Text = title,
            };
            Controls.Add(_titleLabel);
            y += SettingsTheme.TitleHeight + SettingsTheme.ContentGap;
        }

        _bodyTop = y;
        var innerWidth = BodyWidth(width);
        Body = new Panel
        {
            BackColor = SettingsTheme.CardFill,
            Location = new Point(SettingsTheme.SectionPadding, _bodyTop),
            Width = innerWidth,
        };
        SettingsTheme.EnableDoubleBuffer(Body);
        Controls.Add(Body);
    }

    public Panel Body { get; }

    public static int BodyWidth(int cardWidth) =>
        cardWidth - SettingsTheme.SectionPadding * 2;

    public void SetTitleVisible(bool visible)
    {
        if (_titleLabel is null)
        {
            return;
        }

        _titleLabel.Visible = visible;
        _bodyTop = visible
            ? SettingsTheme.SectionPadding + SettingsTheme.TitleHeight + SettingsTheme.ContentGap
            : SettingsTheme.SectionPadding;
        Body.Location = new Point(SettingsTheme.SectionPadding, _bodyTop);
        ApplyBodyHeight(Body.Height);
    }

    public void ApplyBodyHeight(int bodyHeight)
    {
        Body.Height = Math.Max(1, bodyHeight);
        Height = _bodyTop + Body.Height + SettingsTheme.SectionPadding;
    }

    public static int MeasureControl(Control control)
    {
        control.PerformLayout();
        var width = control.Width > 0 ? control.Width : control.PreferredSize.Width;
        var preferred = control.GetPreferredSize(new Size(width, 0));
        return Math.Max(preferred.Height, control.PreferredSize.Height);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;
        SettingsTheme.PaintCardBorder(e.Graphics, bounds, SettingsTheme.CornerRadius);
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

internal sealed class GlowButton : ThemedButtonBase
{
    private bool _hover;

    public GlowButton()
    {
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
        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            bounds,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

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
        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            bounds,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

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
        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            bounds,
            ForeColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}

internal sealed class ThemedTextBox : TextBox
{
    public ThemedTextBox()
    {
        SettingsTheme.ApplyToTextBox(this);
        Margin = Padding.Empty;
    }
}

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

internal sealed class GridHostPanel : Panel
{
    public GridHostPanel()
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
