using Godot;

public static class TacticalTheme
{
    public static readonly Color Backdrop = new(0.035f, 0.04f, 0.045f, 1.0f);
    public static readonly Color Iron = new(0.075f, 0.08f, 0.085f, 0.98f);
    public static readonly Color IronRaised = new(0.115f, 0.12f, 0.12f, 0.98f);
    public static readonly Color IronInset = new(0.05f, 0.052f, 0.05f, 0.98f);
    public static readonly Color Brass = new(0.67f, 0.53f, 0.27f, 1.0f);
    public static readonly Color BrassBright = new(0.88f, 0.72f, 0.38f, 1.0f);
    public static readonly Color Parchment = new(0.9f, 0.85f, 0.72f, 1.0f);
    public static readonly Color ParchmentMuted = new(0.68f, 0.64f, 0.54f, 1.0f);
    public static readonly Color Crimson = new(0.42f, 0.1f, 0.09f, 1.0f);
    public static readonly Color CrimsonHover = new(0.56f, 0.14f, 0.11f, 1.0f);
    public static readonly Color Positive = new(0.39f, 0.68f, 0.4f, 1.0f);
    public static readonly Color Negative = new(0.83f, 0.32f, 0.25f, 1.0f);

    public static StyleBoxFlat CreatePanel(bool inset = false, int margin = 10)
    {
        return new StyleBoxFlat
        {
            BgColor = inset ? IronInset : Iron,
            BorderColor = inset ? new Color(0.27f, 0.23f, 0.15f, 1.0f) : Brass,
            BorderWidthTop = inset ? 1 : 2,
            BorderWidthRight = inset ? 1 : 2,
            BorderWidthBottom = inset ? 1 : 2,
            BorderWidthLeft = inset ? 1 : 2,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusBottomLeft = 2,
            ContentMarginTop = margin,
            ContentMarginRight = margin,
            ContentMarginBottom = margin,
            ContentMarginLeft = margin,
            ShadowColor = new Color(0.0f, 0.0f, 0.0f, inset ? 0.2f : 0.65f),
            ShadowSize = inset ? 1 : 5,
            AntiAliasing = true
        };
    }

    public static void ApplyLabel(Label label, Color color, int fontSize)
    {
        if (label == null)
        {
            return;
        }

        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_shadow_color", new Color(0.0f, 0.0f, 0.0f, 0.75f));
        label.AddThemeConstantOverride("shadow_offset_x", 1);
        label.AddThemeConstantOverride("shadow_offset_y", 1);
        label.AddThemeFontSizeOverride("font_size", fontSize);
    }

    public static void ApplyButton(Button button, bool primary = false, int fontSize = 14)
    {
        if (button == null)
        {
            return;
        }

        var normal = primary ? Crimson : IronRaised;
        var hover = primary ? CrimsonHover : new Color(0.19f, 0.18f, 0.15f, 1.0f);
        var pressed = primary ? new Color(0.31f, 0.07f, 0.06f, 1.0f) : IronInset;

        button.AddThemeStyleboxOverride("normal", CreateButton(normal, Brass));
        button.AddThemeStyleboxOverride("hover", CreateButton(hover, BrassBright));
        button.AddThemeStyleboxOverride("pressed", CreateButton(pressed, Brass));
        button.AddThemeStyleboxOverride("focus", CreateButton(hover, BrassBright, 2));
        button.AddThemeStyleboxOverride("disabled", CreateButton(IronInset, new Color(0.25f, 0.23f, 0.19f, 1.0f)));
        button.AddThemeColorOverride("font_color", Parchment);
        button.AddThemeColorOverride("font_hover_color", new Color(1.0f, 0.94f, 0.76f, 1.0f));
        button.AddThemeColorOverride("font_focus_color", new Color(1.0f, 0.94f, 0.76f, 1.0f));
        button.AddThemeColorOverride("font_pressed_color", ParchmentMuted);
        button.AddThemeColorOverride("font_disabled_color", new Color(0.38f, 0.37f, 0.33f, 1.0f));
        button.AddThemeFontSizeOverride("font_size", fontSize);
    }

    private static StyleBoxFlat CreateButton(Color background, Color border, int borderWidth = 1)
    {
        return new StyleBoxFlat
        {
            BgColor = background,
            BorderColor = border,
            BorderWidthTop = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthBottom = borderWidth,
            BorderWidthLeft = borderWidth,
            CornerRadiusTopLeft = 2,
            CornerRadiusTopRight = 2,
            CornerRadiusBottomRight = 2,
            CornerRadiusBottomLeft = 2,
            ContentMarginTop = 7,
            ContentMarginRight = 12,
            ContentMarginBottom = 7,
            ContentMarginLeft = 12,
            AntiAliasing = true
        };
    }
}