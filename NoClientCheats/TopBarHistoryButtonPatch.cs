using System;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace NoClientCheats;

/// <summary>
/// Hook NTopBar._Ready，在游戏顶栏注入一个「记录」呼出按钮。
/// 放在 PauseButton 左侧，点击呼出/隐藏作弊拦截历史面板。
/// NTopBar 在运行时解析，不依赖编译时类型引用。
/// </summary>
[HarmonyPatch]
internal static class TopBarHistoryButtonPatch
{
    private static MethodBase TargetMethod()
    {
        // MegaCrit.Sts2.Core.Nodes.CommonUi.NTopBar 在运行时可用
        var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.CommonUi.NTopBar")
                 ?? AccessTools.TypeByName("NTopBar");
        return type?.GetMethod("_Ready", AccessTools.all);
    }

    [HarmonyPostfix]
    private static void Postfix(object __instance)
    {
        if (!NoClientCheatsMod.ShowTopBarButton) return;
        if (__instance == null) return;

        try
        {
            var node = __instance as Node;
            if (node == null) return;

            var pauseBtn = node.GetNodeOrNull<Control>("%PauseButton");
            if (pauseBtn == null) return;
            var parent = pauseBtn.GetParent();
            if (parent == null) return;

            // 跳过已注入
            if (parent.HasNode("NCCHistoryButton")) return;

            var btn = new Button
            {
                Name = "NCCHistoryButton",
                Flat = true,
                FocusMode = Control.FocusModeEnum.None,
                Text = "记录",
                TooltipText = "呼出作弊拦截历史面板（F6）"
            };
            btn.CustomMinimumSize = new Vector2(52f, 40f);

            var normalStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.08f, 0.08f, 0.1f, 0.85f),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                BorderWidthTop = 1, BorderWidthBottom = 1,
                BorderWidthLeft = 1, BorderWidthRight = 1,
                BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.6f)
            };
            var hoverStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.18f, 0.18f, 0.22f, 0.9f),
                CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
                CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
                BorderWidthTop = 1, BorderWidthBottom = 1,
                BorderWidthLeft = 1, BorderWidthRight = 1,
                BorderColor = new Color(0.45f, 0.45f, 0.5f, 0.8f)
            };
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeStyleboxOverride("hover", hoverStyle);
            btn.AddThemeStyleboxOverride("pressed", normalStyle);
            btn.AddThemeFontSizeOverride("font_size", 12);
            btn.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.75f, 1f));
            btn.AddThemeColorOverride("font_hover_color", new Color(0.9f, 0.7f, 0.5f, 1f));
            btn.AddThemeColorOverride("font_pressed_color", new Color(0.9f, 0.7f, 0.5f, 1f));

            btn.Pressed += () => NoClientCheatsMod.ToggleHistoryPanel();

            parent.AddChild(btn, false, Node.InternalMode.Disabled);
            parent.MoveChild(btn, pauseBtn.GetIndex(false));
            GD.Print("[NoClientCheats] Top bar history button injected.");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[NoClientCheats] Failed to inject top bar button: {ex.Message}");
        }
    }
}
