using System;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace UpgradeBugRestore;

/// <summary>
/// UpgradeBugRestore / 升级bug恢复
///
/// 在 STS2 v0.107.x 中，官方修复了 NDeckUpgradeSelectScreen 在弹出升级预览面板后
/// 仍允许通过焦点路由（键盘 / 手柄 / 某些点击穿透）继续点选第二张卡的 UI 漏洞。
/// 修复核心是把卡牌网格 _grid 的 FocusBehaviorRecursive 切到 Disabled。
///
/// 本 mod 在 OnCardClicked 后把 _grid.FocusBehaviorRecursive 还原为 Inherited，
/// 还原 bug 修复前的行为：玩家可以在预览面板已弹出时继续点击更多卡牌，
/// 按 Confirm 后 SmithRestSiteOption.OnSelect 会把整个 _selectedCards 都升级。
///
/// 联机说明：
/// - 升级走 PlayerChoiceSynchronizer → PlayerChoiceMessage（携带卡 ID 列表），无主机权威审核。
/// - 引擎层不会 desync。但作为非官方 buff 行为，建议联机时全员同装或仅单机使用。
/// </summary>
public static class UpgradeBugRestoreMod
{
    public const string ModId = "UpgradeBugRestore";
    private const string HarmonyId = "com.jianbao233.upgradebugrestore";

    private static bool _initialized;
    private static bool _patched;

    internal static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        GD.Print("[UpgradeBugRestore] Loaded. Restoring smith multi-select UI behavior.");
    }

    internal static void ApplyHarmonyPatches()
    {
        if (_patched) return;

        try
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
            _patched = true;
            GD.Print("[UpgradeBugRestore] Harmony patches applied.");
        }
        catch (Exception ex)
        {
            GD.PushError($"[UpgradeBugRestore] Harmony patch failed: {ex}");
        }
    }
}