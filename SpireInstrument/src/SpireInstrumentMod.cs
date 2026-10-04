using System;
using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace SpireInstrument;

/// <summary>
/// 尖塔乐器 / Spire Instrument —— 多人可听的游戏内演奏浮窗。
///
/// 第一阶段（M1）目标：证明工具链与运行时挂载可行 —— 进游戏后按 P 唤出浮窗，
/// 用键盘/鼠标在浮窗上演奏，能出声。
/// </summary>
[ModInitializer(nameof(Init))]
public static class SpireInstrumentMod
{
    public const string ModId = "SpireInstrument";
    public const string LogTag = "[SpireInstrument]";

    private static bool _initialized;

    /// <summary>游戏加载 mod 时调用（见 ModInitializerAttribute 约定）。</summary>
    public static void Init()
    {
        if (_initialized) return;
        _initialized = true;

        try
        {
            Ui.PanelHost.Install();
            Settings.SettingsPage.Register();   // 注册进 RitsuLib 原生 mod 设置页
            SelfTest.Install();                 // 默认惰性：仅当存在标记文件时才跑
            GD.Print($"{LogTag} v0.1.0 initialized.");
        }
        catch (Exception ex)
        {
            GD.PushError($"{LogTag} init failed: {ex}");
        }
    }
}
