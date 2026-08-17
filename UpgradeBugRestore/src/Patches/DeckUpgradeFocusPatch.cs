using System;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace UpgradeBugRestore.Patches;

/// <summary>
/// 核心补丁（焦点路由恢复，覆盖全部卡牌选择场景）。
///
/// 目标：MegaCrit.Sts2.Core.Nodes.Cards.NCardGrid.OnHolderPressed
///
/// 所有卡牌选择界面（升级 NDeckUpgradeSelectScreen / 变化 NDeckTransformSelectScreen /
/// 删除 NDeckCardSelectScreen / 附魔 NDeckEnchantSelectScreen / 战斗牌堆 /
/// 简单选择，均为 NCardGridSelectionScreen 子类）的卡牌按下都汇聚到
/// NCardGrid.HolderPressed 信号 → 子类 OnCardClicked。
///
/// 官方在 v0.104~v0.105 起在各 OnCardClicked 弹出预览时把网格的
/// FocusBehaviorRecursive 切到 Disabled，封锁连续选牌（卡 bug）。
/// 本补丁在 OnHolderPressed 之后统一把它还原为 Inherited，
/// 一次覆盖全部选择场景，恢复多选行为。
///
/// 时序：OnCardClicked（设置 Disabled）在 HolderPressed 信号 emit 时同步执行，
/// Postfix 在其后运行，因此最终状态一定是 Inherited。
/// </summary>
[HarmonyPatch]
internal static class DeckUpgradeFocusPatch
{
    private const string TargetTypeName = "MegaCrit.Sts2.Core.Nodes.Cards.NCardGrid";
    private const string TargetMethodName = "OnHolderPressed";

    static MethodBase? TargetMethod()
    {
        var t = AccessTools.TypeByName(TargetTypeName);
        if (t == null)
        {
            GD.PushWarning($"[UpgradeBugRestore] type not found: {TargetTypeName}");
            return null;
        }

        var method = AccessTools.Method(t, TargetMethodName);
        if (method == null)
        {
            GD.PushWarning($"[UpgradeBugRestore] method {TargetMethodName} not found on {TargetTypeName}");
        }
        return method;
    }

    static void Postfix(object __instance)
    {
        // __instance 就是 NCardGrid（= 各选择界面 OnCardClicked 里的 _grid）。
        if (__instance is Control grid)
        {
            grid.FocusBehaviorRecursive = Control.FocusBehaviorRecursiveEnum.Inherited;
        }
    }
}
