using System;

namespace SpireInstrument.Core;

/// <summary>
/// 音阶数学（不引用 Godot，便于离线自检）。
///
/// 这是"不懂乐器的玩家也能玩"的核心：
///   * 五声吸附 —— 槽位映射到五声音阶（C D E G A），任意组合都协和；
///   * 任意音高都能吸附到最近音阶级数，因此**任何 MIDI 曲目都能在 13 个键上演**。
/// </summary>
public static class ScaleMath
{
    /// <summary>五声音阶级数（相对基音的半音数），13 级 ≈ 2.33 个八度。</summary>
    public static readonly int[] Pentatonic = { 0, 2, 4, 7, 9, 12, 14, 16, 19, 21, 24, 26, 28 };

    public const int ChromaticSlotCount = 25;

    public static readonly string[] NoteNames =
        { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    public static string NoteName(int midi) => NoteNames[((midi % 12) + 12) % 12] + (midi / 12 - 1);

    public static int BaseMidi(int octave) => 12 * (octave + 1);

    public static int SlotCount(bool snap) => snap ? Pentatonic.Length : ChromaticSlotCount;

    /// <summary>槽位 → 实际音高。</summary>
    public static int SlotToMidi(int slot, int rowOffset, bool snap, int octave)
    {
        int b = BaseMidi(octave) + rowOffset;
        if (!snap) return b + slot;
        return b + Pentatonic[Math.Clamp(slot, 0, Pentatonic.Length - 1)];
    }

    /// <summary>任意音高 → 最近的可按键槽位。</summary>
    public static int MidiToSlot(int midi, bool snap, int octave)
    {
        int b = BaseMidi(octave);
        if (snap)
        {
            double rel = midi - b;
            int best = 0;
            double bestDist = double.MaxValue;
            for (int i = 0; i < Pentatonic.Length; i++)
            {
                double d = Math.Abs(Pentatonic[i] - rel);
                if (d < bestDist) { bestDist = d; best = i; }
            }
            return best;
        }

        int s = midi - b;
        while (s < 0) s += 12;
        while (s > ChromaticSlotCount - 1) s -= 12;
        return s;
    }

    /// <summary>槽位音名（琴键标注用）。</summary>
    public static string SlotNoteName(int slot, bool snap, int octave)
    {
        if (!snap) return NoteNames[slot % 12];
        return NoteName(BaseMidi(octave) + Pentatonic[Math.Clamp(slot, 0, Pentatonic.Length - 1)]);
    }

    /// <summary>是否黑键（仅半音模式有意义）。</summary>
    public static bool IsBlackSlot(int slot, bool snap)
        => !snap && (slot % 12 == 1 || slot % 12 == 3 || slot % 12 == 6 || slot % 12 == 8 || slot % 12 == 10);

    /// <summary>该音高是否正好落在音阶上（用于统计"被吸附"的音符数）。</summary>
    public static bool IsInScale(int midi, int octave)
    {
        int rel = ((midi - BaseMidi(octave)) % 12 + 12) % 12;
        foreach (int p in Pentatonic)
        {
            if (p % 12 == rel) return true;
        }
        return false;
    }
}
