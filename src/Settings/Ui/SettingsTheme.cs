using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GitHubWallpaper.Settings.Ui;

/// <summary>Палитра и типографика окна настроек.</summary>
internal static class SettingsTheme
{
    public static readonly Color BackgroundTop = Color.FromArgb(10, 12, 20);
    public static readonly Color BackgroundBottom = Color.FromArgb(18, 22, 34);
    public static readonly Color CardFill = Color.FromArgb(24, 28, 40);
    public static readonly Color CardBorder = Color.FromArgb(52, 58, 72);
    public static readonly Color InnerPanelFill = Color.FromArgb(16, 19, 28);
    public static readonly Color InputFill = Color.FromArgb(12, 14, 22);
    public static readonly Color InputBorder = Color.FromArgb(52, 58, 72);
    public static readonly Color TextPrimary = Color.FromArgb(240, 244, 252);
    public static readonly Color TextMuted = Color.FromArgb(148, 156, 172);
    public static readonly Color Accent = Color.FromArgb(255, 140, 0);
    public static readonly Color AccentHover = Color.FromArgb(255, 168, 46);
    public static readonly Color AccentPressed = Color.FromArgb(220, 118, 0);
    public static readonly Color AccentBlue = Color.FromArgb(88, 166, 255);
    public static readonly Color AccentPurple = Color.FromArgb(168, 85, 247);
    public static readonly Color SegmentFill = Color.FromArgb(32, 38, 54);
    public static readonly Color SegmentActive = Color.FromArgb(48, 56, 78);
    public static readonly Color SlotFill = Color.FromArgb(12, 14, 22);
    public static readonly Color SlotEmpty = Color.FromArgb(108, 116, 132);
    public static readonly Color SlotSelected = Color.FromArgb(255, 140, 0);

    public const int CornerRadius = 16;
    public const int ControlCornerRadius = 10;
    public const int SectionPadding = 20;
    public const int SectionGap = 16;
    public const int ContentGap = 12;
    public const int ControlHeight = 36;
    public const int TitleHeight = 22;

    public static Font TitleFont { get; } = new("Segoe UI Semibold", 11F, FontStyle.Bold);
    public static Font SectionFont { get; } = new("Segoe UI Semibold", 10F, FontStyle.Bold);
    public static Font BodyFont { get; } = new("Segoe UI", 9.25F);
    public static Font SmallFont { get; } = new("Segoe UI", 8.75F);

    public static void EnableDoubleBuffer(Control control)
    {
        control.GetType()
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(control, true, null);
    }

    public static void ApplySurfaceBackground(Control control)
    {
        control.BackColor = BackgroundTop;
        EnableDoubleBuffer(control);
    }

    public static void ApplyTransparentBackground(Control control)
    {
        control.BackColor = Color.Transparent;
        EnableDoubleBuffer(control);
    }

    public static void ApplyCardContentBackground(Control control)
    {
        control.BackColor = CardFill;
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
        using var brush = new LinearGradientBrush(bounds, BackgroundTop, BackgroundBottom, 100f);
        graphics.FillRectangle(brush, bounds);

        using var blueGlow = new SolidBrush(Color.FromArgb(36, 56, 120, 255));
        graphics.FillEllipse(blueGlow, bounds.Width / 2 - 220, -120, 440, 300);
        using var purpleGlow = new SolidBrush(Color.FromArgb(22, 120, 70, 220));
        graphics.FillEllipse(purpleGlow, bounds.Width - 300, bounds.Height - 260, 320, 280);
    }

    public static void PaintCard(Graphics graphics, Rectangle bounds, int radius)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var card = bounds;
        card.Width -= 1;
        card.Height -= 1;
        using var path = CreateRoundedRectangle(card, radius);
        using var fill = new SolidBrush(CardFill);
        graphics.FillPath(fill, path);
        using var border = new Pen(CardBorder, 1f);
        graphics.DrawPath(border, path);
    }

    public static void PaintInnerPanel(Graphics graphics, Rectangle bounds, int radius)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var panel = bounds;
        panel.Width -= 1;
        panel.Height -= 1;
        using var path = CreateRoundedRectangle(panel, radius);
        using var fill = new SolidBrush(InnerPanelFill);
        graphics.FillPath(fill, path);
        using var border = new Pen(Color.FromArgb(40, 255, 255, 255), 1f);
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
        link.LinkColor = AccentBlue;
        link.ActiveLinkColor = Color.FromArgb(121, 192, 255);
        link.VisitedLinkColor = AccentBlue;
        link.BackColor = Color.Transparent;
        link.Font = BodyFont;
    }

    public static void ApplyToCheckBox(CheckBox checkBox)
    {
        checkBox.ForeColor = TextPrimary;
        checkBox.BackColor = Color.Transparent;
        checkBox.Font = BodyFont;
        checkBox.FlatStyle = FlatStyle.Flat;
        checkBox.FlatAppearance.BorderSize = 0;
        checkBox.Margin = new Padding(0, 0, 0, 8);
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
