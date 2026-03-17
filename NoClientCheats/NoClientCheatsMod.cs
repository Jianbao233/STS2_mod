using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace NoClientCheats;

/// <summary>
/// 禁止客机作弊 Mod：多人联机时，房主可禁止客机使用控制台作弊指令。
/// 仅房主需安装；通过 Patch ActionQueueSynchronizer.HandleRequestEnqueueActionMessage 实现。
/// </summary>
public static class NoClientCheatsMod
{
    public const string ModId = "NoClientCheats";
    private const string HarmonyId = "com.vc.noclientcheats";

    /// <summary>是否启用禁止客机作弊。由 ModConfig 或默认 true 控制。</summary>
    internal static bool BlockEnabled = true;

    /// <summary>是否从联机 Mod 列表中隐藏本 Mod（屏蔽检测）。由 ModConfig 或默认 true 控制。</summary>
    internal static bool HideFromModList = true;

    private static bool _initialized;
    private static bool _harmonyPatched;

    /// <summary>由 Harmony ModManagerInitPostfix 或 PatchAll 静态构造触发</summary>
    internal static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        ModConfigIntegration.Register();
        GD.Print("[NoClientCheats] Mod loaded. Block client cheats in multiplayer (host-only).");
    }

    internal static void ApplyHarmonyPatches()
    {
        if (_harmonyPatched) return;
        try
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            _harmonyPatched = true;
            GD.Print("[NoClientCheats] Harmony patches applied.");
        }
        catch (Exception e)
        {
            GD.PushError($"[NoClientCheats] Harmony patch failed: {e.Message}");
        }
    }
}
