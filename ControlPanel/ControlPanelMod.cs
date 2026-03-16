using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Logging;
using System;
using System.Reflection;

namespace ControlPanel;

/// <summary>
/// 控制面板 Mod：F7 打开/隐藏，支持卡牌、药水（分类）、战斗等快捷生成。
/// </summary>
[ModInitializer("ModLoaded")]
public static class ControlPanelMod
{
    public const string ModId = "ControlPanel";
    private const string HarmonyId = "com.vc.controlpanel";

    /// <summary>面板切换快捷键，ModConfig 可配置</summary>
    internal static Key ToggleKey = Key.F7;

    private static bool _initialized;
    private static NControlPanel _panel;
    private static F7InputLayer _f7Layer;
    private static bool _harmonyPatched;

    public static void ModLoaded()
    {
        EnsureInitialized();
        ApplyHarmonyPatches();
    }

    /// <summary>显式应用 Harmony 补丁，确保 F7 面板能被挂载（游戏可能不自动 PatchAll）</summary>
    private static void ApplyHarmonyPatches()
    {
        if (_harmonyPatched) return;
        try
        {
            var harmony = new HarmonyLib.Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            _harmonyPatched = true;
            Log.Info("[ControlPanel] Harmony patches applied.");
        }
        catch (Exception e)
        {
            Log.Warn($"[ControlPanel] Harmony patch failed: {e.Message}");
        }
    }

    internal static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        ModConfigIntegration.Register();
        Log.Info("[ControlPanel] Mod loaded. F7 toggles panel.");
    }

    /// <summary>�?Harmony �?ModManager.Initialize 后调用，创建并挂载面�?/summary>
    internal static void CreateAndAttachPanel()
    {
        try
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree?.Root == null) return;

            if (_panel != null)
            {
                if (!_panel.IsInsideTree())
                    tree.Root.AddChild(_panel);
                return;
            }

            _panel = new NControlPanel();
            _panel.Visible = false;
            tree.Root.AddChild(_panel);

            // F7 输入层须挂在 Root，与面板同级，确保面板隐藏时仍能接收 F7
            if (_f7Layer == null || !_f7Layer.IsInsideTree())
            {
                _f7Layer = new F7InputLayer();
                tree.Root.AddChild(_f7Layer);
            }

            Log.Info("[ControlPanel] Panel attached. Press F7 to toggle.");
        }
        catch (Exception e)
        {
            Log.Warn($"[ControlPanel] Failed to attach panel: {e.Message}");
        }
    }

    /// <summary>F7 切换面板显隐</summary>
    internal static void TogglePanel()
    {
        if (_panel == null) return;
        _panel.Visible = !_panel.Visible;
    }
}
