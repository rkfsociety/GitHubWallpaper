using System.Drawing.Drawing2D;

namespace GitHubWallpaper.Settings.Ui;

/// <summary>Палитра и типографика окна настроек.</summary>
internal static class SettingsTheme
{
    public static readonly Color BackgroundTop = Color.FromArgb(15, 17, 26);
    public static readonly Color BackgroundBottom = Color.FromArgb(24, 28, 44);
    public static readonly Color GlassFill = Color.FromArgb(36, 255, 255, 255);
    public static readonly Color GlassBorder = Color.FromArgb(48, 255, 255, 255);
    public static readonly Color InputFill = Color.FromArgb(28, 32, 48);
    public static readonly Color InputBorder = Color.FromArgb(64, 255, 255, 255);
    public static readonly Color TextPrimary = Color.FromArgb(245, 247, 252);
    public static readonly Color TextMuted = Color.FromArgb(136, 144, 164);
    public static readonly Color Accent = Color.FromArgb(255, 140, 0);
    public static readonly Color AccentHover = Color.FromArgb(255, 168, 46);
    public static readonly Color AccentPressed = Color.FromArgb(220, 118, 0);
    public static readonly Color SlotFill = Color.FromArgb(32, 38, 56);
    public static readonly Color SlotEmpty = Color.FromArgb(96, 104, 124);
    public static readonly Color SlotSelected = Color.FromArgb(255, 140, 0);

    public const int CornerRadius = 12;
    public const int ControlCornerRadius = 8;
    public const int SectionPadding = 16;
    public const int SectionGap = 14;

    public static Font TitleFont { get; } = new("Segoe UI Semibold", 10.5F, FontStyle.Bold);
    public static Font SectionFont { get; } = new("Segoe UI Semibold", 10F, FontStyle.Bold);
    public static Font BodyFont { get; } = new("Segoe UI", 9.25F);
    public static Font SmallFont { get; } = new("Segoe UI", 8.75F);

    public static void EnableDoubleBuffer(Control control)
    {
        control.GetType()
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(control, true, null);
    }

    /// <summary>Фон контейнера без «прозрачности» WinForms — убирает артефакты по краям.</summary>
    public static void ApplySurfaceBackground(Control control)
    {
        control.BackColor = BackgroundTop;
        EnableDoubleBuffer(control);
    }

    public static GraphicsPath CreateRoundedRectangle(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void PaintFormBackground(Graphics graphics, Rectangle bounds)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new LinearGradientBrush(
            bounds,
            BackgroundTop,
            BackgroundBottom,
            92f);
        graphics.FillRectangle(brush, bounds);

        using var glow = new SolidBrush(Color.FromArgb(18, 88, 112, 255));
        graphics.FillEllipse(glow, bounds.Width / 2 - 180, -80, 360, 220);
        using var glow2 = new SolidBrush(Color.FromArgb(14, 255, 140, 0));
        graphics.FillEllipse(glow2, 0, bounds.Height - 180, 220, 200);
    }

    public static void PaintGlassPanel(Graphics graphics, Rectangle bounds, int radius)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        using var path = CreateRoundedRectangle(bounds, radius);
        using var fill = new SolidBrush(GlassFill);
        graphics.FillPath(fill, path);
        using var border = new Pen(GlassBorder, 1f);
        graphics.DrawPath(border, path);
    }

    public static void ApplyToLabel(Label label, bool muted = false)
    {
        label.ForeColor = muted ? TextMuted : TextPrimary;
        label.BackColor = Color.Transparent;
        label.Font = muted ? SmallFont : BodyFont;
    }

    public static void ApplyToTextBox(TextBox textBox)
    {
        textBox.BackColor = InputFill;
        textBox.ForeColor = TextPrimary;
        textBox.BorderStyle = BorderStyle.None;
        textBox.Font = BodyFont;
    }

    public static void ApplyToLink(LinkLabel link)
    {
        link.LinkColor = Accent;
        link.ActiveLinkColor = AccentHover;
        link.VisitedLinkColor = AccentPressed;
        link.BackColor = Color.Transparent;
        link.Font = BodyFont;
    }

    public static void ApplyToCheckBox(CheckBox checkBox)
    {
        checkBox.ForeColor = TextPrimary;
        checkBox.BackColor = Color.Transparent;
        checkBox.Font = BodyFont;
    }

    public static void ApplyToRadio(RadioButton radio)
    {
        radio.ForeColor = TextPrimary;
        radio.BackColor = Color.Transparent;
        radio.Font = BodyFont;
    }

    public static void ApplyToComboBox(ComboBox comboBox)
    {
        comboBox.BackColor = InputFill;
        comboBox.ForeColor = TextPrimary;
        comboBox.FlatStyle = FlatStyle.Flat;
        comboBox.Font = BodyFont;
    }

    public static void ApplyToNumeric(NumericUpDown numeric)
    {
        numeric.BackColor = InputFill;
        numeric.ForeColor = TextPrimary;
        numeric.BorderStyle = BorderStyle.FixedSingle;
        numeric.Font = BodyFont;
    }
}
