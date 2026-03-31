using Godot;

namespace MultiplayerTools.Panel
{
    /// <summary>
    /// UI colors and controls aligned with MP_PlayerManager v2 (CustomTkinter dark / blue theme).
    /// </summary>
    internal static class Styles
    {
        // ── MP v2 palette (from MP_PlayerManager_v2/main.py) ─────────────────
        internal static readonly Color MpGold = new Color("F5A623");
        internal static readonly Color MpTopBar = new Color("1A2A3A");
        internal static readonly Color MpNavBg = new Color("111827");
        internal static readonly Color MpNavSelected = new Color("1F3460");
        internal static readonly Color MpNavHover = new Color("2A5090");
        internal static readonly Color MpTextNav = new Color("D1D5DB");
        internal static readonly Color MpBlueAccent = new Color("3B82F6");
        internal static readonly Color MpTextMuted = new Color("A0A0B0");
        internal static readonly Color MpGray = new Color("6B7280");
        internal static readonly Color MpPrimaryBtn = new Color("2A3F7A");
        internal static readonly Color MpPrimaryBtnHover = new Color("3A5FAA");
        internal static readonly Color MpCard = new Color("1A2F50");
        internal static readonly Color MpContentBg = new Color("0D1117");
        internal static readonly Color MpSeparator = new Color("1F2937");

        // Legacy names (tabs / lists still reference these)
        internal static readonly Color Gold = MpGold;
        internal static readonly Color Cream = MpTextNav;
        internal static readonly Color Red = new Color("E74C3C");
        internal static readonly Color Green = new Color("27AE60");
        internal static readonly Color Blue = MpPrimaryBtn;
        internal static readonly Color Purple = new Color("9B59B6");
        internal static readonly Color Gray = MpGray;

        internal static readonly Color PanelBg = new Color(0.05f, 0.07f, 0.09f, 0.98f);
        internal static readonly Color PanelBorder = new Color("374151");
        internal static readonly Color PanelHover = MpNavHover;
        internal static readonly Color PanelPressed = MpNavSelected;
        internal static readonly Color Backdrop = new Color(0, 0, 0, 0.62f);
        internal static readonly Color Divider = new Color(0.25f, 0.28f, 0.35f, 0.85f);
        internal static readonly Color ToggleOn = new Color(0.15f, 0.25f, 0.15f, 0.90f);
        internal static readonly Color ToggleOnBorder = new Color(0.30f, 0.60f, 0.30f, 0.70f);

        internal static readonly Color OutlineColor = new Color(0.06f, 0.08f, 0.12f, 0.75f);

        private static readonly Color Transparent = new Color(0, 0, 0, 0);

        internal static StyleBoxFlat CreateFlat(Color bg, Color border, int radius = 6, int borderW = 1)
        {
            var sb = new StyleBoxFlat { BgColor = bg, BorderColor = border };
            sb.SetBorderWidthAll(borderW);
            sb.SetCornerRadiusAll(radius);
            sb.SetContentMarginAll(8);
            return sb;
        }

        internal static StyleBoxFlat NavItemBox(Color bg, int radius = 6)
        {
            var sb = new StyleBoxFlat { BgColor = bg, BorderColor = Transparent };
            sb.SetBorderWidthAll(0);
            sb.SetCornerRadiusAll(radius);
            sb.SetContentMarginAll(10);
            return sb;
        }

        /// <summary>Left-sidebar nav item (MP v2 style).</summary>
        internal static void ApplyNavTabButton(Button btn, bool selected)
        {
            btn.CustomMinimumSize = new Vector2(0, 40);
            btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            btn.AddThemeFontSizeOverride("font_size", 15);
            btn.AddThemeColorOverride("font_outline_color", OutlineColor);
            btn.AddThemeConstantOverride("outline_size", 2);

            if (selected)
            {
                btn.AddThemeStyleboxOverride("normal", NavItemBox(MpNavSelected));
                btn.AddThemeStyleboxOverride("hover", NavItemBox(MpNavHover));
                btn.AddThemeStyleboxOverride("pressed", NavItemBox(MpNavSelected));
                btn.AddThemeStyleboxOverride("focus", NavItemBox(MpNavSelected));
                btn.AddThemeColorOverride("font_color", MpGold);
                btn.AddThemeColorOverride("font_hover_color", MpGold);
                btn.AddThemeColorOverride("font_pressed_color", MpGold);
            }
            else
            {
                btn.AddThemeStyleboxOverride("normal", NavItemBox(Transparent));
                btn.AddThemeStyleboxOverride("hover", NavItemBox(MpNavHover));
                btn.AddThemeStyleboxOverride("pressed", NavItemBox(MpNavSelected));
                btn.AddThemeStyleboxOverride("focus", NavItemBox(Transparent));
                btn.AddThemeColorOverride("font_color", MpTextNav);
                btn.AddThemeColorOverride("font_hover_color", MpGold);
                btn.AddThemeColorOverride("font_pressed_color", MpTextMuted);
            }
        }

        internal static void ApplyCloseButton(Button btn)
        {
            btn.CustomMinimumSize = new Vector2(40, 36);
            btn.AddThemeFontSizeOverride("font_size", 18);
            btn.AddThemeStyleboxOverride("normal", NavItemBox(Transparent));
            btn.AddThemeStyleboxOverride("hover", NavItemBox(MpNavHover));
            btn.AddThemeStyleboxOverride("pressed", NavItemBox(MpNavSelected));
            btn.AddThemeStyleboxOverride("focus", NavItemBox(Transparent));
            btn.AddThemeColorOverride("font_color", MpTextMuted);
            btn.AddThemeColorOverride("font_hover_color", MpGold);
            btn.AddThemeColorOverride("font_pressed_color", Red);
        }

        /// <summary>List row button (save file rows) — v2 card #1F3460, hover #2A5090.</summary>
        internal static void ApplyListRowButton(Button btn)
        {
            btn.AddThemeFontSizeOverride("font_size", 13);
            btn.AddThemeStyleboxOverride("normal", NavItemBox(MpNavSelected));
            btn.AddThemeStyleboxOverride("hover", NavItemBox(MpNavHover));
            btn.AddThemeStyleboxOverride("pressed", NavItemBox(MpNavSelected));
            btn.AddThemeStyleboxOverride("focus", NavItemBox(MpNavHover));
            btn.AddThemeColorOverride("font_color", MpTextNav);
            btn.AddThemeColorOverride("font_hover_color", MpGold);
            btn.AddThemeColorOverride("font_pressed_color", MpGold);
        }

        internal static void ApplyFlatButton(Button btn)
        {
            btn.AddThemeStyleboxOverride("normal", CreateFlat(MpCard, PanelBorder, 6));
            btn.AddThemeStyleboxOverride("hover", CreateFlat(MpNavHover, MpGold, 6));
            btn.AddThemeStyleboxOverride("pressed", CreateFlat(MpNavSelected, MpGold, 6));
            btn.AddThemeStyleboxOverride("focus", CreateFlat(MpNavHover, MpGold, 6));
        }

        internal static void ApplyTabButton(Button btn)
        {
            btn.CustomMinimumSize = new Vector2(90, 36);
            btn.AddThemeFontSizeOverride("font_size", 16);
            btn.AddThemeColorOverride("font_color", MpTextNav);
            btn.AddThemeColorOverride("font_hover_color", MpGold);
            btn.AddThemeColorOverride("font_pressed_color", MpGray);
            btn.AddThemeColorOverride("font_outline_color", OutlineColor);
            btn.AddThemeConstantOverride("outline_size", 2);
            ApplyFlatButton(btn);
        }

        internal static void ApplyActionButton(Button btn, Color? fontColor = null)
        {
            btn.CustomMinimumSize = new Vector2(72, 32);
            btn.AddThemeFontSizeOverride("font_size", 13);
            btn.AddThemeColorOverride("font_color", fontColor ?? MpTextNav);
            btn.AddThemeColorOverride("font_hover_color", MpGold);
            btn.AddThemeColorOverride("font_pressed_color", MpGray);
            btn.AddThemeColorOverride("font_outline_color", OutlineColor);
            btn.AddThemeConstantOverride("outline_size", 2);
            var normal = NavItemBox(MpPrimaryBtn);
            var hover = NavItemBox(MpPrimaryBtnHover);
            btn.AddThemeStyleboxOverride("normal", normal);
            btn.AddThemeStyleboxOverride("hover", hover);
            btn.AddThemeStyleboxOverride("pressed", NavItemBox(MpNavSelected));
            btn.AddThemeStyleboxOverride("focus", hover);
        }

        internal static void ApplyToggleButton(Button btn, bool active)
        {
            btn.CustomMinimumSize = new Vector2(90, 36);
            btn.AddThemeFontSizeOverride("font_size", 14);
            btn.AddThemeColorOverride("font_color", active ? MpGold : MpTextNav);
            btn.AddThemeColorOverride("font_hover_color", MpGold);
            btn.AddThemeColorOverride("font_pressed_color", MpGray);
            btn.AddThemeColorOverride("font_outline_color", OutlineColor);
            btn.AddThemeConstantOverride("outline_size", 2);
            if (active)
                btn.AddThemeStyleboxOverride("normal", CreateFlat(ToggleOn, ToggleOnBorder, 6));
            else
                btn.AddThemeStyleboxOverride("normal", CreateFlat(MpCard, PanelBorder, 6));
            btn.AddThemeStyleboxOverride("hover", CreateFlat(MpNavHover, MpGold, 6));
            btn.AddThemeStyleboxOverride("pressed", CreateFlat(MpNavSelected, MpGold, 6));
        }
    }
}
