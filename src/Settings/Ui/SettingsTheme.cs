using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace GitHubWallpaper.Settings.Ui;

/// <summary>Палитра и типографика окна настроек (в духе GitHub dark / обоев).</summary>
internal static class SettingsTheme
{
    public static readonly Color BackgroundTop = Color.FromArgb(13, 17, 23);
    public static readonly Color BackgroundBottom = Color.FromArgb(22, 27, 34);
    public static readonly Color CardFill = Color.FromArgb(22, 27, 34);
    public static readonly Color CardBorder = Color.FromArgb(48, 54, 61);
    public static readonly Color TitleBadgeFill = Color.FromArgb(33, 38, 45);
    public static readonly Color InputFill = Color.FromArgb(13, 17, 23);
    public static readonly Color InputBorder = Color.FromArgb(48, 54, 61);
    public static readonly Color TextPrimary = Color.FromArgb(230, 237, 243);
    public static readonly Color TextMuted = Color.FromArgb(139, 148, 158);
    public static readonly Color Accent = Color.FromArgb(255, 140, 0);
    public static readonly Color AccentHover = Color.FromArgb(255, 168, 46);
    public static readonly Color AccentPressed = Color.FromArgb(220, 118, 0);
    public static readonly Color AccentBlue = Color.FromArgb(56, 139, 253);
    public static readonly Color SlotFill = Color.FromArgb(13, 17, 23);
    public static readonly Color SlotEmpty = Color.FromArgb(110, 118, 129);
    public static readonly Color SlotSelected = Color.FromArgb(255, 140, 0);

    // Совместимость со старыми вызовами PaintGlassPanel.
    public static readonly Color GlassFill = CardFill;
    public static readonly Color GlassBorder = CardBorder;

    public const int CornerRadius = 14;
    public const int ControlCornerRadius = 8;
    public const int SectionPadding = 18;
    public const int SectionGap = 16;
    public const int ContentPadding = 16;
    public const int TitleBadgeHeight = 28;
    public const int TitleBadgePaddingH = 12;
    public const int SectionTitleGap = 14;

    public static Font TitleFont { get; } = new("Segoe UI Semibold", 10.5F, FontStyle.Bold);
    public static Font SectionFont { get; } = new("Segoe UI Semibold", 9.75F, FontStyle.Bold);
    public static Font BodyFont { get; } = new("Segoe UI", 9.25F);
    public static Font SmallFont { get; } = new("Segoe UI", 8.75F);

    public static void EnableDoubleBuffer(Control control)
    {
        control.GetType()
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(control, true, null);
    }

    /// <summary>Фон холста (вне карточек).</summary>
    public static void ApplySurfaceBackground(Control control)
    {
        control.BackColor = BackgroundTop;
        EnableDoubleBuffer(control);
    }

    /// <summary>Прозрачный фон для вложенных layout внутри карточки.</summary>
    public static void ApplyTransparentBackground(Control control)
    {
        control.BackColor = Color.Transparent;
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
            105f);
        graphics.FillRectangle(brush, bounds);

        using var blueGlow = new SolidBrush(Color.FromArgb(28, 56, 139, 253));
        graphics.FillEllipse(blueGlow, bounds.Width / 2 - 200, -100, 400, 260);
        using var purpleGlow = new SolidBrush(Color.FromArgb(18, 136, 87, 255));
        graphics.FillEllipse(purpleGlow, bounds.Width - 260, bounds.Height - 220, 280, 240);
        using var orangeGlow = new SolidBrush(Color.FromArgb(12, 255, 140, 0));
        graphics.FillEllipse(orangeGlow, -40, bounds.Height - 160, 200, 180);
    }

    public static void PaintCard(Graphics graphics, Rectangle bounds, int radius)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var shadow = bounds;
        shadow.Offset(0, 4);
        shadow.Width -= 1;
        shadow.Height -= 1;
        using (var shadowPath = CreateRoundedRectangle(shadow, radius))
        using (var shadowBrush = new SolidBrush(Color.FromArgb(72, 1, 4, 9)))
        {
            graphics.FillPath(shadowBrush, shadowPath);
        }

        var card = bounds;
        card.Width -= 1;
        card.Height -= 1;
        using var path = CreateRoundedRectangle(card, radius);
        using var fill = new SolidBrush(CardFill);
        graphics.FillPath(fill, path);
        using var border = new Pen(CardBorder, 1f);
        graphics.DrawPath(border, path);
    }

    public static int MeasureTitleBadgeWidth(string title)
    {
        var textSize = TextRenderer.MeasureText(title, SectionFont);
        return textSize.Width + TitleBadgePaddingH * 2;
    }

    public static void PaintTitleBadge(Graphics graphics, Rectangle bounds, string title)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

        var badge = bounds;
        badge.Width -= 1;
        badge.Height -= 1;
        using var path = CreateRoundedRectangle(badge, 6);
        using var fill = new SolidBrush(TitleBadgeFill);
        graphics.FillPath(fill, path);
        using var border = new Pen(CardBorder, 1f);
        graphics.DrawPath(border, path);

        var textBounds = badge;
        textBounds.X += TitleBadgePaddingH;
        textBounds.Width -= TitleBadgePaddingH * 2;
        TextRenderer.DrawText(
            graphics,
            title,
            SectionFont,
            textBounds,
            TextPrimary,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }

    public static void PaintGlassPanel(Graphics graphics, Rectangle bounds, int radius) =>
        PaintCard(graphics, bounds, radius);

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
    }

    public static void ApplyToRadio(RadioButton radio)
    {
        radio.ForeColor = TextPrimary;
        radio.BackColor = Color.Transparent;
        radio.Font = BodyFont;
        radio.FlatStyle = FlatStyle.Flat;
        radio.FlatAppearance.BorderSize = 0;
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
