using System;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace StandardSeedHost;

/// <summary>
/// StandardSeedHost / 标准房种子
///
/// 给标准模式（多人房 + 单机开局）的选人界面注入种子输入框，
/// 视觉复刻自定义模式的种子框；输入通过原生 StartRunLobby.SetSeed 广播同步。
/// </summary>
public static class StandardSeedHostMod
{
    public const string ModId = "StandardSeedHost";
    private const string HarmonyId = "com.jianbao233.standardseedhost";

    private static bool _initialized;
    private static bool _patched;

    internal static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        GD.Print("[StandardSeedHost] Loaded. Standard room seed input enabled.");
    }

    internal static void ApplyHarmonyPatches()
    {
        if (_patched) return;

        try
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            _patched = true;
            GD.Print("[StandardSeedHost] Harmony patches applied.");
        }
        catch (Exception ex)
        {
            GD.PushError($"[StandardSeedHost] Harmony patch failed: {ex}");
        }
    }
}
