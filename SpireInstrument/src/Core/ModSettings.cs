using System;
using Godot;

namespace SpireInstrument.Core;

/// <summary>浮窗尺寸档位（对应 UI 上的 迷你 / 标准 / 大）。</summary>
public enum PanelSize
{
    Mini,
    Standard,
    Large,
}

/// <summary>
/// 运行时设置：**门面**（façade）—— 对外仍是 <c>ModSettings.ScaleSnap</c> 这样的简单属性，
/// 内部读写可存盘的数据模型，并做防抖落盘。
///
/// 之所以不让各处直接摸模型：调用点零改动，且"什么时候落盘"集中在一处控制
/// （拖音量滑块会连续触发几十次写入，必须防抖）。
/// </summary>
public static class ModSettings
{
    private static SpireInstrumentSettings _data = new();
    private static bool _loaded;
    private static bool _dirty;
    private static ulong _dirtySinceMsec;
    private const ulong SaveDebounceMsec = 500;

    public static SpireInstrumentSettings Data
    {
        get
        {
            EnsureLoaded();
            return _data;
        }
    }

    private static void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;
        _data = SettingsStore.Load();
        GD.Print($"{SpireInstrumentMod.LogTag} settings loaded: {Describe()} path={SettingsStore.AbsolutePath()}");
    }

    // ---- 对外属性（调用点无需改动）----

    public static string InstrumentId
    {
        get => Data.InstrumentId;
        set { if (Data.InstrumentId != value) { Data.InstrumentId = value; MarkDirty(); } }
    }

    public static bool ScaleSnap
    {
        get => Data.ScaleSnap;
        set { if (Data.ScaleSnap != value) { Data.ScaleSnap = value; MarkDirty(); } }
    }

    public static int Octave
    {
        get => Data.Octave;
        set { int v = Math.Clamp(value, 2, 6); if (Data.Octave != v) { Data.Octave = v; MarkDirty(); } }
    }

    public static float Volume
    {
        get => Data.VolumePercent / 100f;
        set
        {
            int pct = Math.Clamp((int)Math.Round(value * 100f), 0, 100);
            if (Data.VolumePercent != pct) { Data.VolumePercent = pct; MarkDirty(); }
        }
    }

    public static PanelSize Size
    {
        get => Data.PanelSize switch
        {
            "Mini" => PanelSize.Mini,
            "Large" => PanelSize.Large,
            _ => PanelSize.Standard,
        };
        set
        {
            string name = value.ToString();
            if (Data.PanelSize != name) { Data.PanelSize = name; MarkDirty(); }
        }
    }

    public static bool PanelVisible
    {
        get => Data.PanelVisible;
        set { if (Data.PanelVisible != value) { Data.PanelVisible = value; MarkDirty(); } }
    }

    public static bool Accompany
    {
        get => Data.Accompany;
        set { if (Data.Accompany != value) { Data.Accompany = value; MarkDirty(); } }
    }

    public static bool Assist
    {
        get => Data.Assist;
        set { if (Data.Assist != value) { Data.Assist = value; MarkDirty(); } }
    }

    /// <summary>队友演奏的音量（0..1）。观众侧：不想听可以拉低或静音。</summary>
    public static float AudienceVolume
    {
        get => MuteAudience ? 0f : Data.AudienceVolumePercent / 100f;
        set
        {
            int pct = Math.Clamp((int)Math.Round(value * 100f), 0, 100);
            if (Data.AudienceVolumePercent != pct) { Data.AudienceVolumePercent = pct; MarkDirty(); }
        }
    }

    /// <summary>完全静音队友演奏。</summary>
    public static bool MuteAudience
    {
        get => Data.MuteAudience;
        set { if (Data.MuteAudience != value) { Data.MuteAudience = value; MarkDirty(); } }
    }

    /// <summary>上次的演奏模式（字符串形式，直接落盘）。</summary>
    public static string PlayMode
    {
        get => Data.PlayMode;
        set { if (Data.PlayMode != value) { Data.PlayMode = value; MarkDirty(); } }
    }

    /// <summary>上次选中的曲目 id。</summary>
    public static string LastSongId
    {
        get => Data.LastSongId;
        set { if (Data.LastSongId != value) { Data.LastSongId = value; MarkDirty(); } }
    }

    /// <summary>我在战斗中时自动静音队友演奏。</summary>
    public static bool MuteAudienceInCombat
    {
        get => Data.MuteAudienceInCombat;
        set { if (Data.MuteAudienceInCombat != value) { Data.MuteAudienceInCombat = value; MarkDirty(); } }
    }

    /// <summary>面板与字号缩放（1.0–2.0）。</summary>
    public static float UiScale => Data.UiScalePercent / 100f;

    /// <summary>缩放百分比（100–200）。</summary>
    public static int UiScalePercent
    {
        get => Data.UiScalePercent;
        set
        {
            int pct = Math.Clamp(value, 100, 200);
            if (Data.UiScalePercent != pct) { Data.UiScalePercent = pct; MarkDirty(); }
        }
    }

    /// <summary>联机同步补偿（秒）。</summary>
    public static double SyncCompensationSeconds => Data.SyncCompensationMs / 1000.0;

    /// <summary>联机同步补偿（毫秒）。</summary>
    public static int SyncCompensationMs
    {
        get => Data.SyncCompensationMs;
        set
        {
            int ms = Math.Clamp(value, 0, 500);
            if (Data.SyncCompensationMs != ms) { Data.SyncCompensationMs = ms; MarkDirty(); }
        }
    }

    /// <summary>唤出浮窗的按键（已解析为 Godot 键值）。</summary>
    public static Key SummonKey
    {
        get
        {
            var parsed = ParseKey(Data.SummonKey);
            return parsed == Key.None ? Key.P : parsed;
        }
    }

    /// <summary>把设置里存的键名解析成 Godot 键值（兼容 "P" 与 "Ctrl+P" 两种写法）。</summary>
    public static Key ParseKey(string? raw)
    {
        string s = SpireInstrumentSettings.NormalizeKeyName(raw);
        if (s.Length == 0) return Key.None;

        try
        {
            var k = OS.FindKeycodeFromString(s);
            if (k != Key.None) return k;
        }
        catch { /* 落到下面的枚举解析 */ }

        return Enum.TryParse<Key>(s, ignoreCase: true, out var parsed) ? parsed : Key.None;
    }

    // ---- 落盘 ----

    public static void MarkDirty()
    {
        _dirty = true;
        _dirtySinceMsec = Time.GetTicksMsec();
    }

    /// <summary>由宿主每帧调用：静默 500ms 后落盘（拖滑块不会写几十次）。</summary>
    public static void FlushIfDirty()
    {
        if (!_dirty) return;
        if (Time.GetTicksMsec() - _dirtySinceMsec < SaveDebounceMsec) return;
        _dirty = false;
        SettingsStore.Save(_data);
    }

    /// <summary>立即落盘（自检/退出时用）。</summary>
    public static bool SaveNow()
    {
        _dirty = false;
        return SettingsStore.Save(_data);
    }

    /// <summary>一行设置摘要（日志与自检用）。</summary>
    public static string Describe()
        => $"inst={InstrumentId} snap={ScaleSnap} oct={Octave} vol={Data.VolumePercent} size={Data.PanelSize} " +
           $"visible={PanelVisible} key={Data.SummonKey} audience={Data.AudienceVolumePercent}" +
           $"{(Data.MuteAudience ? "(muted)" : "")}";
}
