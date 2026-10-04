using System;
using System.Collections.Generic;
using SpireInstrument.Core;

namespace SpireInstrument.Songs;

/// <summary>跟弹状态下单个音符的进度。</summary>
public enum NoteState
{
    /// <summary>还没到 / 还没按。</summary>
    Pending,

    /// <summary>玩家按中了。</summary>
    Hit,

    /// <summary>辅助补音替玩家补上了。</summary>
    Auto,

    /// <summary>漏按（未开辅助补音）。</summary>
    Missed,
}

/// <summary>一个音符。时间单位为秒（相对曲首）。</summary>
public sealed class SongNote
{
    public double Time { get; init; }
    public int Midi { get; init; }
    public double Duration { get; init; } = 0.3;
    public float Velocity { get; init; } = 0.8f;

    /// <summary>伴奏声部（低音），跟弹模式会忽略它。</summary>
    public bool Bass { get; init; }

    /// <summary>跟弹模式下对应的琴键槽位（-1 = 无）。</summary>
    public int Slot { get; set; } = -1;

    /// <summary>跟弹模式下真正要按下的音高（可能为适配键盘而移八度）。</summary>
    public int KeyMidi { get; set; }

    /// <summary>跟弹进度状态。</summary>
    public NoteState State { get; set; } = NoteState.Pending;
}

/// <summary>一首曲子（已展开为绝对时间的音符序列）。</summary>
public sealed class Song
{
    public string Name { get; init; } = "";
    public List<SongNote> Notes { get; init; } = new();
    public double Duration { get; set; }

    /// <summary>是否为导入的 MIDI（UI 上标注）。</summary>
    public bool Imported { get; init; }

    /// <summary>跟弹折叠统计：因超出键盘范围被吸附/移八度的音符数。</summary>
    public int Snapped { get; set; }
    public int Folded { get; set; }

    public int MelodyCount
    {
        get
        {
            int n = 0;
            foreach (var note in Notes) if (!note.Bass) n++;
            return n;
        }
    }
}

/// <summary>内置曲目的紧凑记谱定义。</summary>
public sealed class SongDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required double Bpm { get; init; }

    /// <summary>记谱："音名:拍数"，空格分隔，例如 "C4:1 E4:1 G4:2"。</summary>
    public required string Melody { get; init; }

    /// <summary>是否公有领域/自编（工坊发布红线：必须是 true 才能内置）。</summary>
    public bool PublicDomain { get; init; } = true;

    /// <summary>来源说明（发布审核用）。</summary>
    public string Credit { get; init; } = "";
}

/// <summary>
/// 记谱解析 + 编曲 + 键盘折叠（**纯逻辑，不引用 Godot**，因此可离线自检）。
/// </summary>
public static class SongBuilder
{
    /// <summary>音名 → MIDI 音高（C4 = 60）。无法识别返回 -1。</summary>
    public static int NameToMidi(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return -1;
        string s = name.Trim();

        char letter = char.ToUpperInvariant(s[0]);
        int baseSemi = letter switch
        {
            'C' => 0, 'D' => 2, 'E' => 4, 'F' => 5, 'G' => 7, 'A' => 9, 'B' => 11,
            _ => -1,
        };
        if (baseSemi < 0) return -1;

        int i = 1;
        int acc = 0;
        while (i < s.Length && (s[i] == '#' || s[i] == 'b'))
        {
            acc += s[i] == '#' ? 1 : -1;
            i++;
        }

        if (i >= s.Length || !int.TryParse(s.Substring(i), out int octave)) return -1;

        int midi = baseSemi + acc + (octave + 1) * 12;
        return midi is >= 0 and <= 127 ? midi : -1;
    }

    /// <summary>把紧凑记谱展开成绝对时间的音符序列。</summary>
    public static Song FromDefinition(SongDefinition def, bool withAccompaniment = true)
    {
        double secPerBeat = 60.0 / Math.Max(20.0, def.Bpm);
        var notes = new List<SongNote>();
        double t = 0;

        foreach (string token in def.Melody.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = token.Split(':');
            double beats = 1.0;
            if (parts.Length > 1 && !double.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out beats))
            {
                beats = 1.0;
            }
            if (beats <= 0) beats = 1.0;

            int midi = NameToMidi(parts[0]);
            if (midi >= 0)
            {
                notes.Add(new SongNote
                {
                    Time = t,
                    Midi = midi,
                    Duration = beats * secPerBeat * 0.95,
                    Velocity = 0.85f,
                });
            }
            t += beats * secPerBeat;
        }

        var song = new Song { Name = def.Name, Notes = notes, Duration = t + 0.6 };
        if (withAccompaniment) AddAccompaniment(song, secPerBeat);
        return song;
    }

    /// <summary>
    /// 极简伴奏：每小节取旋律首音下移两个八度，落在第 1、3 拍。
    /// 永远协和 —— 不需要玩家懂乐理，这是"不懂乐器也能弹"的一部分。
    /// </summary>
    public static void AddAccompaniment(Song song, double secPerBeat)
    {
        double bar = secPerBeat * 4;
        if (bar <= 0) return;

        var extra = new List<SongNote>();
        int bars = (int)Math.Ceiling(song.Duration / bar);
        for (int b = 0; b < bars; b++)
        {
            double barStart = b * bar;
            SongNote? first = null;
            foreach (var n in song.Notes)
            {
                if (n.Bass) continue;
                if (n.Time >= barStart && n.Time < barStart + bar) { first = n; break; }
            }
            if (first == null) continue;

            int bassMidi = first.Midi - 24;
            if (bassMidi < 12) bassMidi = first.Midi - 12;

            foreach (int beat in new[] { 0, 2 })
            {
                double bt = barStart + beat * secPerBeat;
                if (bt >= song.Duration) continue;
                extra.Add(new SongNote
                {
                    Time = bt,
                    Midi = bassMidi,
                    Duration = secPerBeat * 1.6,
                    Velocity = 0.42f,
                    Bass = true,
                });
            }
        }

        song.Notes.AddRange(extra);
        song.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
    }

    /// <summary>
    /// 把旋律映射到琴键槽位（跟弹模式用）。
    /// 五声吸附：整曲居中后吸附到最近音阶级数 —— 任何曲子都能在 13 键上弹，且永远协和。
    /// 半音模式：整曲居中 + 超界音按八度折叠。
    /// </summary>
    public static void FoldToKeyboard(Song song, bool snap, int octave)
    {
        song.Snapped = 0;
        song.Folded = 0;

        var melody = new List<SongNote>();
        foreach (var n in song.Notes) if (!n.Bass) melody.Add(n);
        if (melody.Count == 0) return;

        int baseMidi = ScaleMath.BaseMidi(octave);
        var mids = new List<int>(melody.Count);
        foreach (var n in melody) mids.Add(n.Midi);
        mids.Sort();
        int median = mids[mids.Count / 2];
        int mn = mids[0], mx = mids[mids.Count - 1];

        if (snap)
        {
            int span = ScaleMath.Pentatonic[ScaleMath.Pentatonic.Length - 1];   // 13 键覆盖的半音跨度
            int shift = ChooseShift(mn, mx, baseMidi, span, median, ScaleMath.Pentatonic[6]);

            foreach (var n in song.Notes)
            {
                if (n.Bass) { n.Slot = -1; n.KeyMidi = n.Midi; continue; }

                double rel = n.Midi + shift - baseMidi;
                int best = 0;
                double bestDist = double.MaxValue;
                for (int i = 0; i < ScaleMath.Pentatonic.Length; i++)
                {
                    double d = Math.Abs(ScaleMath.Pentatonic[i] - rel);
                    if (d < bestDist) { bestDist = d; best = i; }
                }
                if (bestDist > 0.01) song.Snapped++;
                n.Slot = best;
                n.KeyMidi = baseMidi + ScaleMath.Pentatonic[best];
            }
            return;
        }

        int lo = baseMidi, hi = baseMidi + 24;
        int shiftSemi = ChooseShift(mn, mx, baseMidi, 24, median, 12);

        foreach (var n in song.Notes)
        {
            if (n.Bass) { n.Slot = -1; n.KeyMidi = n.Midi; continue; }

            int s = n.Midi + shiftSemi - lo;
            while (s < 0) { s += 12; song.Folded++; }
            while (s > 24) { s -= 12; song.Folded++; }
            n.Slot = s;
            n.KeyMidi = lo + s;
        }
    }

    /// <summary>
    /// 选择整曲的移调量（按八度）。
    ///
    /// 规则：**放得下就不移调** —— 只有在曲子超出键盘范围时才移八度；
    /// 范围比键盘还宽时才退化为"居中"，靠后续逐音折叠兜底。
    /// 这条很关键：早期版本一律把整曲居中，结果《小星星》被整整抬高一个八度，
    /// 新手被推到高音区（截图验证时发现的）。
    /// </summary>
    public static int ChooseShift(int minMidi, int maxMidi, int baseMidi, int spanSemitones,
                                  int medianMidi, int centerOffset)
    {
        int span = maxMidi - minMidi;

        if (span <= spanSemitones)
        {
            // 先看原八度能不能放下
            if (minMidi - baseMidi >= 0 && maxMidi - baseMidi <= spanSemitones) return 0;

            // 不能：找一个能把整曲装进去的八度（优先离原八度最近）
            for (int octave = 0; octave <= 6; octave++)
            {
                foreach (int sign in new[] { 1, -1 })
                {
                    int shift = sign * octave * 12;
                    if (minMidi + shift - baseMidi >= 0 && maxMidi + shift - baseMidi <= spanSemitones)
                        return shift;
                }
            }
            return 0;
        }

        // 范围比键盘宽：居中（后续逐音按八度折叠）
        return (int)(Math.Round((centerOffset - (medianMidi - baseMidi)) / 12.0) * 12.0);
    }
}
