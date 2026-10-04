using System;

namespace SpireInstrument.Core;

/// <summary>
/// 设置数据模型（**纯 POCO，不引用 Godot**，因此可离线自检 JSON 往返与取值修正）。
/// 字段只放"需要存盘"的东西；运行时临时状态（当前按住哪些键等）不在这里。
/// </summary>
public sealed class SpireInstrumentSettings
{
    /// <summary>存档格式版本，便于以后迁移。</summary>
    public int Version { get; set; } = 1;

    /// <summary>默认音色 id。</summary>
    public string InstrumentId { get; set; } = "harp";

    /// <summary>五声音阶吸附（默认开：怎么按都不难听）。</summary>
    public bool ScaleSnap { get; set; } = true;

    /// <summary>八度（低八度起始，C4 = MIDI 60）。</summary>
    public int Octave { get; set; } = 4;

    /// <summary>音量百分比 0..100。</summary>
    public int VolumePercent { get; set; } = 80;

    /// <summary>浮窗尺寸档位名（Mini / Standard / Large）。</summary>
    public string PanelSize { get; set; } = "Standard";

    /// <summary>上次是否处于展开状态。</summary>
    public bool PanelVisible { get; set; } = true;

    /// <summary>唤出/收起浮窗的按键（Godot 键名，如 "P"）。</summary>
    public string SummonKey { get; set; } = "P";

    /// <summary>自动伴奏（M3 曲目播放用）。</summary>
    public bool Accompany { get; set; } = true;

    /// <summary>跟弹辅助补音（M3 用）。</summary>
    public bool Assist { get; set; } = true;

    // ---- 观众侧（我听队友演奏的音量/开关）----

    /// <summary>队友演奏的音量百分比 0..100（0 = 等同静音）。</summary>
    public int AudienceVolumePercent { get; set; } = 100;

    /// <summary>完全静音队友的演奏。</summary>
    public bool MuteAudience { get; set; }

    // ---- 上次会话的续接 ----

    /// <summary>上次的演奏模式："Free" / "Auto" / "Learn"。</summary>
    public string PlayMode { get; set; } = "Free";

    /// <summary>上次选中的曲目 id（内置曲 id；导入曲存 "file:" 前缀路径）。</summary>
    public string LastSongId { get; set; } = "";

    /// <summary>
    /// 联机同步补偿（毫秒，0–500）。
    /// 收到队友曲谱时，本机起拍再推迟这么多 —— 用于补偿跨地域网络延迟。
    /// 默认 0（不补偿）：同机/局域网本来就齐；跨地域若听出固定偏移，用这个手动校准。
    /// </summary>
    public int SyncCompensationMs { get; set; }

    /// <summary>面板与字号缩放百分比（100–200，默认 130）。高分屏上太小就往上调。</summary>
    public int UiScalePercent { get; set; } = 115;

    /// <summary>我在战斗中时自动静音队友演奏（默认开：不打扰自己打牌）。</summary>
    public bool MuteAudienceInCombat { get; set; } = true;

    /// <summary>把取值收进合法范围，并修正无法识别的枚举名。</summary>
    public SpireInstrumentSettings Normalize()
    {
        if (string.IsNullOrWhiteSpace(InstrumentId)) InstrumentId = "harp";
        SummonKey = NormalizeKeyName(SummonKey);
        Octave = Math.Clamp(Octave, 2, 6);
        VolumePercent = Math.Clamp(VolumePercent, 0, 100);
        AudienceVolumePercent = Math.Clamp(AudienceVolumePercent, 0, 100);
        SyncCompensationMs = Math.Clamp(SyncCompensationMs, 0, 500);
        UiScalePercent = Math.Clamp(UiScalePercent, 100, 200);

        if (PanelSize != "Mini" && PanelSize != "Standard" && PanelSize != "Large") PanelSize = "Standard";
        if (PlayMode != "Free" && PlayMode != "Auto" && PlayMode != "Learn") PlayMode = "Free";
        if (LastSongId == null) LastSongId = "";
        if (Version < 1) Version = 1;
        return this;
    }

    /// <summary>
    /// 键名归一：把 "Ctrl+Shift+P" 这类组合写法取成纯键名 "P"（纯逻辑，可离线自检）。
    /// 设置页的键位捕获可能写入带修饰键的写法，而我们要的是"哪个键唤出浮窗"。
    /// </summary>
    public static string NormalizeKeyName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "P";
        string s = raw.Trim();
        int plus = s.LastIndexOf('+');
        if (plus >= 0 && plus + 1 < s.Length) s = s.Substring(plus + 1).Trim();
        return s.Length == 0 ? "P" : s;
    }
}
