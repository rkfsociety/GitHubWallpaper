namespace GitHubWallpaper.Settings.Ui;

/// <summary>Область содержимого с градиентным фоном.</summary>
internal sealed class ThemedContentPanel : Panel
{
    public ThemedContentPanel()
    {
        SettingsTheme.ApplySurfaceBackground(this);
        Dock = DockStyle.Fill;
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (!AutoScroll)
            {
                cp.Style &= ~0x00200000; // WS_VSCROLL
                cp.Style &= ~0x00100000; // WS_HSCROLL
            }

            return cp;
        }
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

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        using var brush = new SolidBrush(BackColor);
        pevent.Graphics.FillRectangle(brush, ClientRectangle);
    }
}

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
        using (var baseFill = new SolidBrush(SettingsTheme.CardFill))
        {
            pevent.Graphics.FillPath(baseFill, path);
        }

        using var fill = new SolidBrush(Color.FromArgb(_hover ? 48 : 24, _accent));
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
        Padding = new Padding(
            SettingsTheme.InputPaddingX,
            SettingsTheme.InputPaddingY,
            SettingsTheme.InputPaddingX,
            SettingsTheme.InputPaddingY);
        inner.BorderStyle = BorderStyle.None;
        inner.Margin = Padding.Empty;
        Controls.Add(inner);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);

        var area = DisplayRectangle;
        foreach (Control control in Controls)
        {
            control.Bounds = area;
        }
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

internal sealed class ThemedNumericUpDown : NumericUpDown
{
    public ThemedNumericUpDown()
    {
        SettingsTheme.ApplyToNumeric(this);
        InterceptArrowKeys = true;
        BackColor = Color.Transparent;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        HideNativeSpinner();
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        HideNativeSpinner();
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        HideNativeSpinner();

        foreach (Control control in Controls)
        {
            if (control is not TextBox textBox)
            {
                continue;
            }

            textBox.BorderStyle = BorderStyle.None;
            textBox.TextAlign = HorizontalAlignment.Center;
            textBox.BackColor = Color.Transparent;
            textBox.SetBounds(0, 0, Width, Height);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (Focused)
        {
            if (e.Delta > 0)
            {
                Value = Math.Min(Maximum, Value + Increment);
            }
            else if (e.Delta < 0)
            {
                Value = Math.Max(Minimum, Value - Increment);
            }
        }

        base.OnMouseWheel(e);
    }

    private void HideNativeSpinner()
    {
        foreach (Control control in Controls)
        {
            if (control is TextBox)
            {
                continue;
            }

            control.Visible = false;
            control.Enabled = false;
            control.SetBounds(-100, -100, 0, 0);
        }
    }
}

internal sealed class NumericSpinButton : Control
{
    private readonly bool _increment;
    private bool _hover;

    public NumericSpinButton(bool increment)
    {
        _increment = increment;
        BackColor = Color.Transparent;
        Cursor = Cursors.Hand;
        TabStop = false;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        SettingsTheme.EnableDoubleBuffer(this);
    }

    public NumericUpDown? Target { get; init; }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        if (Target is null)
        {
            return;
        }

        if (_increment)
        {
            if (Target.Value + Target.Increment <= Target.Maximum)
            {
                Target.Value += Target.Increment;
            }
        }
        else if (Target.Value - Target.Increment >= Target.Minimum)
        {
            Target.Value -= Target.Increment;
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        if (_hover)
        {
            using var hover = new SolidBrush(Color.FromArgb(32, SettingsTheme.TextPrimary));
            e.Graphics.FillRectangle(hover, bounds);
        }

        var cx = bounds.Width / 2;
        var cy = bounds.Height / 2;
        const int size = 4;
        Point[] points = _increment
            ?
            [
                new(cx, cy - size),
                new(cx - size, cy + size - 1),
                new(cx + size, cy + size - 1),
            ]
            :
            [
                new(cx, cy + size),
                new(cx - size, cy - size + 1),
                new(cx + size, cy - size + 1),
            ];

        using var brush = new SolidBrush(SettingsTheme.TextMuted);
        e.Graphics.FillPolygon(brush, points);
    }
}

internal sealed class NumericField : Panel
{
    private const int ArrowWidth = 18;

    private readonly NumericUpDown _inner;
    private readonly NumericSpinButton _upButton;
    private readonly NumericSpinButton _downButton;

    public NumericField(NumericUpDown inner, int width = 76, int height = SettingsTheme.ControlHeight)
    {
        _inner = inner;
        BackColor = SettingsTheme.CardFill;
        SettingsTheme.EnableDoubleBuffer(this);
        Width = width;
        Height = height;
        Padding = Padding.Empty;

        inner.BorderStyle = BorderStyle.None;
        inner.TextAlign = HorizontalAlignment.Center;
        inner.Margin = Padding.Empty;

        _upButton = new NumericSpinButton(increment: true) { Target = inner };
        _downButton = new NumericSpinButton(increment: false) { Target = inner };

        Controls.Add(inner);
        Controls.Add(_upButton);
        Controls.Add(_downButton);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var innerWidth = Math.Max(1, w - ArrowWidth);
        var half = h / 2;
        _inner.SetBounds(0, 0, innerWidth, h);
        _upButton.SetBounds(innerWidth, 0, ArrowWidth, half);
        _downButton.SetBounds(innerWidth, half, ArrowWidth, h - half);
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

        var separatorX = ClientSize.Width - ArrowWidth;
        if (separatorX > 4)
        {
            using var sep = new Pen(SettingsTheme.InputBorder);
            e.Graphics.DrawLine(sep, separatorX, 6, separatorX, ClientSize.Height - 6);
        }
    }
}

internal sealed class ComboField : Panel
{
    public ComboField(ComboBox inner, int height = SettingsTheme.ControlHeight)
    {
        BackColor = SettingsTheme.CardFill;
        SettingsTheme.EnableDoubleBuffer(this);
        Height = height;
        Padding = new Padding(
            SettingsTheme.InputPaddingX,
            SettingsTheme.InputPaddingY,
            10,
            SettingsTheme.InputPaddingY);
        inner.FlatStyle = FlatStyle.Flat;
        inner.Margin = Padding.Empty;
        Controls.Add(inner);
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);

        var area = DisplayRectangle;
        foreach (Control control in Controls)
        {
            control.Bounds = area;
        }
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

/// <summary>Переключатель в стиле toggle (оранжевый — вкл.).</summary>
internal sealed class ToggleSwitch : Control
{
    private bool _checked;
    private bool _hover;

    public ToggleSwitch()
    {
        Size = new Size(44, 24);
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        UpdateStyles();
        SettingsTheme.EnableDoubleBuffer(this);
    }

    public bool Checked
    {
        get => _checked;
        set
        {
            if (_checked == value)
            {
                return;
            }

            _checked = value;
            CheckedChanged?.Invoke(this, EventArgs.Empty);
            Invalidate();
        }
    }

    public event EventHandler? CheckedChanged;

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hover = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hover = false;
        Invalidate();
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        Checked = !Checked;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            Checked = !Checked;
            e.Handled = true;
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        var bounds = ClientRectangle;
        bounds.Width -= 1;
        bounds.Height -= 1;

        var trackColor = Checked
            ? (_hover ? SettingsTheme.AccentHover : SettingsTheme.Accent)
            : Color.FromArgb(52, 58, 72);
        using var trackPath = SettingsTheme.CreateRoundedRectangle(bounds, bounds.Height / 2);
        using var trackBrush = new SolidBrush(trackColor);
        e.Graphics.FillPath(trackBrush, trackPath);

        var thumbSize = bounds.Height - 6;
        var thumbX = Checked
            ? bounds.Right - thumbSize - 3
            : bounds.Left + 3;
        var thumbRect = new Rectangle(thumbX, bounds.Top + 3, thumbSize, thumbSize);
        using var thumbBrush = new SolidBrush(Color.White);
        e.Graphics.FillEllipse(thumbBrush, thumbRect);
    }
}

/// <summary>Строка настройки: подпись слева, toggle справа.</summary>
internal sealed class ToggleSettingRow : Panel
{
    private readonly Label _label;
    private readonly ToggleSwitch _switch;

    public ToggleSettingRow(string text)
    {
        BackColor = SettingsTheme.CardFill;
        SettingsTheme.EnableDoubleBuffer(this);

        _label = new Label
        {
            AutoSize = false,
            BackColor = SettingsTheme.CardFill,
            Font = SettingsTheme.BodyFont,
            ForeColor = SettingsTheme.TextPrimary,
            Text = text,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _switch = new ToggleSwitch();
        Controls.Add(_label);
        Controls.Add(_switch);
        Height = SettingsTheme.ControlHeight;
    }

    public bool Checked
    {
        get => _switch.Checked;
        set => _switch.Checked = value;
    }

    public event EventHandler? CheckedChanged
    {
        add => _switch.CheckedChanged += value;
        remove => _switch.CheckedChanged -= value;
    }

    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);

        const int switchWidth = 44;
        const int switchHeight = 24;
        _switch.Size = new Size(switchWidth, switchHeight);
        _switch.Location = new Point(
            Math.Max(0, Width - switchWidth),
            Math.Max(0, (Height - switchHeight) / 2));
        _label.Location = new Point(0, 0);
        _label.Size = new Size(Math.Max(0, Width - switchWidth - 10), Height);
    }
}
