using Godot;

namespace MultiplayerTools.Panel
{
    /// <summary>
    /// UI color and style constants. KaylaMod-inspired dark theme.
    /// </summary>
    internal static class Styles
    {
        // Primary colors
        internal static readonly Color Gold   = new Color("E3A83D");
        internal static readonly Color Cream  = new Color("E3D5C1");
        internal static readonly Color Red    = new Color("C0392B");
        internal static readonly Color Green  = new Color("27AE60");
        internal static readonly Color Blue   = new Color("2980B9");
        internal static readonly Color Purple = new Color("9B59B6");
        internal static readonly Color Gray   = new Color("7F8C8D");

        // Panel background
        internal static readonly Color PanelBg      = new Color(0.08f, 0.06f, 0.10f, 0.96f);
        internal static readonly Color PanelBorder   = new Color(0.35f, 0.30f, 0.25f, 0.50f);
        internal static readonly Color PanelHover    = new Color(0.18f, 0.15f, 0.22f, 0.92f);
        internal static readonly Color PanelPressed  = new Color(0.08f, 0.06f, 0.10f, 0.95f);
        internal static readonly Color Backdrop      = new Color(0, 0, 0, 0.55f);
        internal static readonly Color Divider       = new Color(0.91f, 0.86f, 0.75f, 0.20f);
        internal static readonly Color ToggleOn      = new Color(0.15f, 0.25f, 0.15f, 0.90f);
        internal static readonly Color ToggleOnBorder= new Color(0.30f, 0.60f, 0.30f, 0.70f);

        // Font outline
        internal static readonly Color OutlineColor = new Color(0.10f, 0.15f, 0.18f, 0.80f);

        internal static StyleBoxFlat CreateFlat(Color bg, Color border)
        {
            var sb = new StyleBoxFlat { BgColor = bg, BorderColor = border };
            sb.SetBorderWidthAll(2);
            sb.SetCornerRadiusAll(6);
            sb.SetContentMarginAll(6);
            return sb;
        }

        internal static void ApplyFlatButton(Button btn)
        {
            btn.AddThemeStyleboxOverride("normal",   CreateFlat(PanelBg, PanelBorder));
            btn.AddThemeStyleboxOverride("hover",    CreateFlat(PanelHover, Gold));
            btn.AddThemeStyleboxOverride("pressed",  CreateFlat(PanelPressed, new Color("B89840")));
            btn.AddThemeStyleboxOverride("focus",    CreateFlat(PanelHover, Gold));
        }

        internal static void ApplyTabButton(Button btn)
        {
            btn.CustomMinimumSize = new Vector2(90, 36);
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.AddThemeColorOverride("font_color", Cream);
            btn.AddThemeColorOverride("font_hover_color", Gold);
            btn.AddThemeColorOverride("font_pressed_color", Gray);
            btn.AddThemeColorOverride("font_outline_color", OutlineColor);
            btn.AddThemeConstantOverride("outline_size", 4);
            ApplyFlatButton(btn);
        }

        internal static void ApplyActionButton(Button btn, Color? fontColor = null)
        {
            btn.CustomMinimumSize = new Vector2(70, 30);
            btn.AddThemeFontSizeOverride("font_size", 13);
            btn.AddThemeColorOverride("font_color", fontColor ?? Cream);
            btn.AddThemeColorOverride("font_hover_color", Gold);
            btn.AddThemeColorOverride("font_pressed_color", Gray);
            btn.AddThemeColorOverride("font_outline_color", OutlineColor);
            btn.AddThemeConstantOverride("outline_size", 4);
            ApplyFlatButton(btn);
        }

        internal static void ApplyToggleButton(Button btn, bool active)
        {
            btn.CustomMinimumSize = new Vector2(90, 36);
            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.AddThemeColorOverride("font_color", active ? Gold : Cream);
            btn.AddThemeColorOverride("font_hover_color", Gold);
            btn.AddThemeColorOverride("font_pressed_color", Gray);
            btn.AddThemeColorOverride("font_outline_color", OutlineColor);
            btn.AddThemeConstantOverride("outline_size", 4);
            if (active)
                btn.AddThemeStyleboxOverride("normal", CreateFlat(ToggleOn, ToggleOnBorder));
            else
                btn.AddThemeStyleboxOverride("normal", CreateFlat(PanelBg, PanelBorder));
            btn.AddThemeStyleboxOverride("hover",    CreateFlat(PanelHover, Gold));
            btn.AddThemeStyleboxOverride("pressed",  CreateFlat(PanelPressed, new Color("B89840")));
        }
    }
}
