using Godot;
using HarmonyLib;
using System;
using System.Reflection;

namespace HostPriority;

/// <summary>
/// 房主优先 Mod：多人联机时，让房主在遗物猜拳、地图路径、事件选项分歧中胜出。
/// 仅房主需安装。
/// </summary>
public static class HostPriorityMod
{
    public const string ModId = "HostPriority";
    private const string HarmonyId = "com.vc.hostpriority";

    /// <summary>是否启用房主优先。由 ModConfig 或默认 true 控制。</summary>
    internal static bool Enabled = true;

    private static bool _initialized;
    private static bool _harmonyPatched;

    internal static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        ModConfigIntegration.Register();
        GD.Print("[HostPriority] Mod loaded. Host wins in relic/map/event splits (host-only).");
    }

    internal static void ApplyHarmonyPatches()
    {
        if (_harmonyPatched) return;
        try
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            _harmonyPatched = true;
            GD.Print("[HostPriority] Harmony patches applied.");
        }
        catch (Exception e)
        {
            GD.PushError($"[HostPriority] Harmony patch failed: {e}");
        }
    }
}
