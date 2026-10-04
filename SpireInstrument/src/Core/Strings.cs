using System;
using System.Collections.Generic;

namespace SpireInstrument.Core;

/// <summary>
/// UI 文案本地化（**纯逻辑，不引用 Godot**，因此可离线自检）。
///
/// 做法：**以中文原文为 key**，英文玩家看到时查表替换。
/// 这样调用点几乎不用改（面板里所有中文标签保持原样），
/// 再由 <c>Ui.Localizer.LocalizeTree</c> 在建好 UI 后整棵树走一遍 —— 覆盖面最广、改动面最小。
/// </summary>
public static class Strings
{
    /// <summary>强制语言（自检用；null = 跟随系统）。</summary>
    public static string? ForcedLanguage { get; set; }

    private static bool? _cachedEnglish;

    /// <summary>当前是否用英文。</summary>
    public static bool UseEnglish
    {
        get
        {
            if (ForcedLanguage != null) return UseEnglishFor(ForcedLanguage);
            _cachedEnglish ??= UseEnglishFor(SystemLocale());
            return _cachedEnglish.Value;
        }
    }

    /// <summary>系统语言（由 Godot 层注入，避免本文件依赖 Godot）。</summary>
    public static Func<string?> SystemLocale { get; set; } = () => "en";

    /// <summary>纯函数：给定 locale 判断是否用英文（zh* → 中文，其余 → 英文）。</summary>
    public static bool UseEnglishFor(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale)) return true;
        return !locale.Trim().ToLowerInvariant().StartsWith("zh");
    }

    /// <summary>翻译一条文案：中文环境下原样返回，英文环境查表（查不到则原样返回）。</summary>
    public static string Tr(string zh)
    {
        if (string.IsNullOrEmpty(zh)) return zh;
        if (!UseEnglish) return zh;
        return En.TryGetValue(zh, out var en) ? en : zh;
    }

    /// <summary>动态拼接文案用：按当前语言二选一。</summary>
    public static string Pick(string zh, string en) => UseEnglish ? en : zh;

    /// <summary>表里有多少条（自检用）。</summary>
    public static int Count => En.Count;

    /// <summary>中文原文 → 英文。key 必须与代码里的字面量完全一致。</summary>
    private static readonly Dictionary<string, string> En = new()
    {
        // 标题栏
        ["演奏"] = "Play",
        ["依赖 RitsuLib"] = "Requires RitsuLib",
        ["迷你"] = "Mini",
        ["标准"] = "Standard",
        ["大"] = "Large",
        ["收起为音符图标（P）"] = "Collapse to the note icon (P)",
        ["关闭（Esc）"] = "Close (Esc)",

        // 左栏
        ["音阶吸附"] = "Pentatonic snap",
        ["五声音阶：怎么按都不会难听"] = "Pentatonic: nothing you press sounds wrong",
        ["乐器 / 音色"] = "Instrument",

        // 乐器名
        ["竖琴 / 拨弦"] = "Harp / Pluck",
        ["八音盒"] = "Music Box",
        ["马林巴"] = "Marimba",
        ["大钢琴"] = "Grand Piano",
        ["芯片音"] = "Chiptune",
        ["钟琴"] = "Bell",
        ["口风琴"] = "Melodica",
        ["管风琴"] = "Organ",
        ["弦乐"] = "Strings",

        // 曲目区
        ["曲目"] = "Song",
        ["▶ 播放"] = "▶ Play",
        ["■ 停止"] = "■ Stop",
        ["打开歌曲文件夹：把 .mid 放进去就能导入（不放也行，内置 6 首）"] =
            "Open the songs folder — drop a .mid in to import (optional; 6 songs are built in)",
        ["自由弹"] = "Free play",
        ["自动演奏"] = "Auto-play",
        ["跟弹"] = "Learn",
        ["未选择曲目"] = "No song selected",
        ["最近演奏"] = "Recently played",

        // 右栏
        ["音量"] = "Volume",
        ["八度"] = "Octave",
        ["状态"] = "Status",
        ["队友演奏"] = "Teammates",
        ["静音队友"] = "Mute teammates",
        ["别人弹给你听时的音量"] = "How loud other players' playing is",

        // 底部 / 提示
        ["就绪"] = "Ready",
        ["按下琴键开始演奏"] = "Press a key to start playing",
        ["键位：下排 Z S X D C V G B H N J M , ／ 上排 Q 2 W 3 E R 5 T 6 Y 7 U I　·　[ ] 移调　·　P 收起"] =
            "Keys: lower row Z S X D C V G B H N J M , / upper row Q 2 W 3 E R 5 T 6 Y 7 U I · [ ] transpose · P collapse",
        ["自由弹：随便按"] = "Free play: just press keys",
        ["自动演奏：选好曲目按播放，mod 会把整曲弹出来"] =
            "Auto-play: pick a song and press Play — the mod performs it",
        ["跟弹引导：照着下落的方块按键，漏按会由辅助补音兜底"] =
            "Learn: press the keys as the blocks fall; missed notes are covered by the assist",
    };
}
