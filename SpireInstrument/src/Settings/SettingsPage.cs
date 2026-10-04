using System;
using System.Collections.Generic;
using Godot;
using SpireInstrument.Audio;
using SpireInstrument.Core;
using STS2RitsuLib;
using STS2RitsuLib.Settings;
using STS2RitsuLib.Utils.Persistence;

namespace SpireInstrument.Settings;

/// <summary>
/// 把本 mod 的设置注册进 **RitsuLib 的原生 mod 设置页**（游戏内「设置 → 模组」里能直接改）。
///
/// 绑定用 <see cref="ModSettingsCallbackValueBinding{T}"/>：读写都走我们自己的
/// <see cref="ModSettings"/> 门面与 <see cref="SettingsStore"/>，
/// 这样"面板里改"和"设置页里改"是同一份数据、同一个文件，不会出现两份真相。
/// </summary>
public static class SettingsPage
{
    private const string SectionId = "spire_instrument";
    private const string DataKey = "settings";
    private static bool _registered;

    public static void Register()
    {
        if (_registered) return;
        _registered = true;

        try
        {
            RitsuLibFramework.RegisterModSettings(SpireInstrumentMod.ModId, page =>
            {
                page.AddSection(SectionId, section => section
                    .AddToggle("scale_snap", Text("音阶吸附（五声音阶：怎么按都不难听）"),
                        Bind(s => s.ScaleSnap, (s, v) => s.ScaleSnap = v),
                        Text("关闭后是完整 12 音，按错会难听"))
                    .AddIntSlider("ui_scale", Text("面板与字号缩放（%）"),
                        Bind(s => s.UiScalePercent, (s, v) => s.UiScalePercent = v),
                        100, 200, 10, v => v + "%",
                        Text("高分屏上觉得面板小、字小就往上调；重开游戏后生效"))
                    .AddIntSlider("volume", Text("音量"),
                        Bind(s => s.VolumePercent, (s, v) => s.VolumePercent = v),
                        0, 100, 5, v => v + "%")
                    .AddIntSlider("octave", Text("八度"),
                        Bind(s => s.Octave, (s, v) => s.Octave = v),
                        2, 6, 1, v => "C" + v)
                    .AddChoice("instrument", Text("默认音色"),
                        Bind(s => s.InstrumentId, (s, v) => s.InstrumentId = v),
                        InstrumentOptions())
                    .AddChoice("panel_size", Text("浮窗尺寸"),
                        Bind(s => s.PanelSize, (s, v) => s.PanelSize = v),
                        SizeOptions())
                    .AddKeyBinding("summon_key", Text("唤出/收起浮窗按键"),
                        Bind(s => s.SummonKey, (s, v) => s.SummonKey = v))
                    .AddToggle("accompany", Text("自动伴奏（曲目播放时）"),
                        Bind(s => s.Accompany, (s, v) => s.Accompany = v))
                    .AddToggle("assist", Text("跟弹辅助补音（漏按自动补）"),
                        Bind(s => s.Assist, (s, v) => s.Assist = v))
                    .AddIntSlider("audience_volume", Text("队友演奏音量"),
                        Bind(s => s.AudienceVolumePercent, (s, v) => s.AudienceVolumePercent = v),
                        0, 100, 10, v => v + "%",
                        Text("别人弹给你听时的音量；不想听就拉到 0"))
                    .AddToggle("mute_combat", Text("战斗中自动静音队友"),
                        Bind(s => s.MuteAudienceInCombat, (s, v) => s.MuteAudienceInCombat = v))
                    .AddToggle("mute_audience", Text("静音队友演奏"),
                        Bind(s => s.MuteAudience, (s, v) => s.MuteAudience = v))
                    .AddIntSlider("sync_comp", Text("联机同步补偿（毫秒）"),
                        Bind(s => s.SyncCompensationMs, (s, v) => s.SyncCompensationMs = v),
                        0, 500, 10, v => v + " ms",
                        Text("队友演奏听起来偏早/偏晚时用它校准；同机与局域网保持 0"))
                );
            });

            GD.Print($"{SpireInstrumentMod.LogTag} settings page registered (RitsuLib).");
        }
        catch (Exception e)
        {
            // 设置页注册失败不该影响演奏功能
            GD.PushWarning($"{SpireInstrumentMod.LogTag} settings page registration failed: {e.Message}");
        }
    }

    private static ModSettingsText Text(string s) => ModSettingsText.Literal(s);

    /// <summary>读写都落到我们自己的设置门面（改完标脏，由宿主防抖落盘）。</summary>
    private static ModSettingsCallbackValueBinding<T> Bind<T>(
        Func<SpireInstrumentSettings, T> read,
        Action<SpireInstrumentSettings, T> write)
        => new(
            SpireInstrumentMod.ModId,
            DataKey,
            SaveScope.Global,
            () => read(ModSettings.Data),
            v =>
            {
                write(ModSettings.Data, v);
                ModSettings.MarkDirty();
            },
            () => ModSettings.SaveNow());

    private static IEnumerable<ModSettingsChoiceOption<string>> InstrumentOptions()
    {
        foreach (var inst in InstrumentLibrary.Items)
        {
            yield return new ModSettingsChoiceOption<string>(inst.Id, Text(inst.Name));
        }
    }

    private static IEnumerable<ModSettingsChoiceOption<string>> SizeOptions() => new[]
    {
        new ModSettingsChoiceOption<string>("Mini", Text("迷你（等队友时随手弹）")),
        new ModSettingsChoiceOption<string>("Standard", Text("标准")),
        new ModSettingsChoiceOption<string>("Large", Text("大")),
    };
}
