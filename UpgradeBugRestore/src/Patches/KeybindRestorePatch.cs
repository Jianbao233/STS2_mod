using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace UpgradeBugRestore.Patches;

/// <summary>
/// 键位恢复补丁。
///
/// 目标：MegaCrit.Sts2.Core.Nodes.CommonUi.NInputManager.ProcessHotkeyInput
///
/// 官方在 v0.110.0 的输入重构中把键鼠模式（MouseAndKeyboard）下
/// MegaInput.select（"ui_select"）的键位从映射移除（v0.109.0 默认是 Enter）。
/// 键鼠模式下按键由 NInputManager._UnhandledKeyInput → ProcessHotkeyInput
/// 按 _mKbInputMap 手动转换成动作；select 键位缺失导致所有卡牌选择界面在
/// 键鼠模式下无法用键盘"确认"选中焦点所在的卡（卡牌选中只走 ui_select 动作，
/// NCardHolder._GuiInput 的 select 分支 → EmitPressed）。
///
/// 本补丁在键鼠按键转换入口把 select 重新映射为 Enter（等效 v0.109.0 默认键位）。
/// 每次按键幂等赋值，玩家重置键位（ResetToDefaults）后也会自动恢复。
/// 不影响 KeyboardOnlyMode（走 _fKbInputMap，select=Space）与手柄映射。
/// </summary>
[HarmonyPatch]
internal static class KeybindRestorePatch
{
    private const string TargetTypeName = "MegaCrit.Sts2.Core.Nodes.CommonUi.NInputManager";
    private const string TargetMethodName = "ProcessHotkeyInput";

    private static readonly StringName SelectAction = new StringName("ui_select");

    private static FieldInfo? _mkbMapField;
    private static bool _fieldResolved;

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

    static void Prefix(object __instance)
    {
        try
        {
            var map = ResolveMkbMap(__instance);
            if (map == null) return;

            // 幂等：把键鼠模式 select 键位恢复为 Enter。
            map[SelectAction] = Key.Enter;
        }
        catch (Exception ex)
        {
            GD.PushError($"[UpgradeBugRestore] keybind restore failure: {ex}");
        }
    }

    private static Dictionary<StringName, Key>? ResolveMkbMap(object instance)
    {
        if (!_fieldResolved)
        {
            _fieldResolved = true;
            _mkbMapField = AccessTools.Field(AccessTools.TypeByName(TargetTypeName), "_mKbInputMap");
            if (_mkbMapField == null)
            {
                GD.PushWarning("[UpgradeBugRestore] cannot locate _mKbInputMap; keybind patch will no-op");
            }
        }

        return _mkbMapField?.GetValue(instance) as Dictionary<StringName, Key>;
    }
}
