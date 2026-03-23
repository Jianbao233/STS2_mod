using System;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace NoClientCheats;

/// <summary>
/// Hook NTopBar._Ready，在游戏顶栏注入一个「记录」呼出按钮。
/// 放在 PauseButton 左侧，点击呼出/隐藏作弊拦截历史面板。
/// 使用与游戏内 PauseButton 相同的齿轮图标，纯图标无文字。
/// </summary>
[HarmonyPatch]
internal static class TopBarHistoryButtonPatch
{
    private static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("MegaCrit.Sts2.Core.Nodes.CommonUi.NTopBar")
                 ?? AccessTools.TypeByName("NTopBar");
        GD.Print($"[NCCTopBar] TargetMethod: type={type?.FullName ?? "NOT FOUND"}");
        return type?.GetMethod("_Ready", AccessTools.all);
    }

    [HarmonyPostfix]
    private static void Postfix(object __instance)
    {
        GD.Print($"[NCCTopBar] Postfix fired. ShowTopBarButton={NoClientCheatsMod.ShowTopBarButton}");

        if (!NoClientCheatsMod.ShowTopBarButton) return;
        if (__instance == null) return;

        try
        {
            var node = __instance as Node;
            if (node == null) return;

            GD.Print($"[NCCTopBar] NTopBar node: {node.Name}, tree={node.IsInsideTree()}");

            var pauseBtn = node.GetNodeOrNull<Control>("%PauseButton");
            GD.Print($"[NCCTopBar] pauseBtn={pauseBtn?.Name ?? "NOT FOUND"}");

            if (pauseBtn == null) return;

            var parent = pauseBtn.GetParent();
            if (parent == null) return;

            if (parent.HasNode("NCCHistoryButton"))
            {
                GD.Print("[NCCTopBar] NCCHistoryButton already exists, skipping.");
                return;
            }

            GD.Print($"[NCCTopBar] Injecting button into {parent.Name}, pauseBtn index={pauseBtn.GetIndex(false)}");

            // ── 从 PauseButton 克隆 Icon 子节点（复用同一纹理对象）───────────
            var pauseIcon = pauseBtn.GetNodeOrNull<Control>("Control/Icon");
            GD.Print($"[NCCTopBar] pauseIcon={pauseIcon?.Name ?? "NOT FOUND"}");

            // ── 创建按钮 ──────────────────────────────────────────────────
            var btn = new Button
            {
                Name = "NCCHistoryButton",
                Flat = true,
                FocusMode = Control.FocusModeEnum.None,
                TooltipText = "作弊拦截记录（F6）"
            };
            // 尺寸与游戏按钮一致
            btn.CustomMinimumSize = new Vector2(40f, 40f);

            // 透明背景（与游戏按钮风格一致）
            var normalStyle = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0) };
            btn.AddThemeStyleboxOverride("normal", normalStyle);
            btn.AddThemeStyleboxOverride("hover", normalStyle);
            btn.AddThemeStyleboxOverride("pressed", normalStyle);

            // 克隆 PauseButton 的 Icon 节点作为子节点
            if (pauseIcon != null && pauseIcon is TextureRect srcRect)
            {
                var icon = new TextureRect
                {
                    Name = "Icon",
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    CustomMinimumSize = new Vector2(24f, 24f),
                    Texture = srcRect.Texture,
                    Material = srcRect.Material != null ? (Material)srcRect.Material.Duplicate() : null
                };
                // 居中锚点（Godot 4: LayoutPreset.Center）
                icon.SetAnchorsPreset(Control.LayoutPreset.Center);
                btn.AddChild(icon);
                GD.Print("[NCCTopBar] Icon cloned from PauseButton.");
            }
            else
            {
                GD.PushWarning("[NCCTopBar] Could not find Icon node in PauseButton to clone.");
            }

            btn.Pressed += () => NoClientCheatsMod.ToggleHistoryPanel();

            parent.AddChild(btn, false, Node.InternalMode.Disabled);
            parent.MoveChild(btn, pauseBtn.GetIndex(false));
            GD.Print("[NCCTopBar] Button injected successfully.");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[NCCTopBar] Failed to inject top bar button: {ex.Message}\n{ex.StackTrace}");
        }
    }
}
