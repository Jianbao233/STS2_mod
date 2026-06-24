using System;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace UpgradeBugRestore.Patches;

/// <summary>
/// 核心补丁。
///
/// 目标：MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckUpgradeSelectScreen.OnCardClicked
///
/// 新版（已修复）的 OnCardClicked 在弹出预览面板时会把基类网格的 FocusBehaviorRecursive
/// 切到 Disabled，导致玩家无法继续点选第二张牌。
///
/// 本 mod 在 Postfix 强制把它还原为 Inherited，恢复旧版（含 bug）的视觉与数据通路：
/// - 玩家可以连续点击卡牌，每张都被加进 _selectedCards
/// - Confirm 按下时 SmithRestSiteOption.OnSelect 把整个集合都 Upgrade
/// - PlayerChoiceMessage 携带完整 ID 列表，联机各端一致升级，无 desync
///
/// 注意：Postfix 只做一件事（重置 FocusBehaviorRecursive），不接触 _selectedCards 与
/// _completionSource，保持原有控制流。
/// </summary>
[HarmonyPatch]
internal static class DeckUpgradeFocusPatch
{
    private const string TargetTypeName =
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NDeckUpgradeSelectScreen";
    private const string BaseTypeName =
        "MegaCrit.Sts2.Core.Nodes.Screens.CardSelection.NCardGridSelectionScreen";

    private static FieldInfo? _gridField;
    private static bool _resolved;

    static MethodBase? TargetMethod()
    {
        var t = AccessTools.TypeByName(TargetTypeName);
        if (t == null)
        {
            GD.PushWarning($"[UpgradeBugRestore] type not found: {TargetTypeName}");
            return null;
        }

        var method = AccessTools.Method(t, "OnCardClicked");
        if (method == null)
        {
            GD.PushWarning($"[UpgradeBugRestore] method OnCardClicked not found on {TargetTypeName}");
        }
        return method;
    }

    static void Postfix(object __instance)
    {
        if (__instance == null) return;

        try
        {
            var grid = ResolveGrid(__instance);
            if (grid == null) return;

            if (grid is Control c)
            {
                c.FocusBehaviorRecursive = Control.FocusBehaviorRecursiveEnum.Inherited;
            }
        }
        catch (Exception ex)
        {
            GD.PushError($"[UpgradeBugRestore] postfix failure: {ex}");
        }
    }

    private static object? ResolveGrid(object screenInstance)
    {
        if (!_resolved)
        {
            _resolved = true;
            var baseType = AccessTools.TypeByName(BaseTypeName);
            if (baseType != null)
            {
                _gridField = AccessTools.Field(baseType, "_grid");
            }
            if (_gridField == null)
            {
                _gridField = AccessTools.Field(screenInstance.GetType(), "_grid");
            }
            if (_gridField == null)
            {
                GD.PushWarning("[UpgradeBugRestore] cannot locate _grid field; patch will no-op");
            }
        }

        return _gridField?.GetValue(screenInstance);
    }
}