using Godot;

namespace SpireInstrument.Core;

/// <summary>
/// 键位与音阶的 Godot 侧封装：键位表 + 委托给 <see cref="ScaleMath"/> 的音阶数学。
///
/// 键位：两排各 13 键 —— 下排（Z S X D C V G B H N J M ,）＝低八度，
/// 上排（Q 2 W 3 E R 5 T 6 Y 7 U I）＝高八度。
/// </summary>
public static class MusicScale
{
    public static readonly Key[] RowLow =
    {
        Key.Z, Key.S, Key.X, Key.D, Key.C, Key.V, Key.G,
        Key.B, Key.H, Key.N, Key.J, Key.M, Key.Comma,
    };

    public static readonly Key[] RowHigh =
    {
        Key.Q, Key.Key2, Key.W, Key.Key3, Key.E, Key.R, Key.Key5,
        Key.T, Key.Key6, Key.Y, Key.Key7, Key.U, Key.I,
    };

    public const int RowOffsetSemitones = 12;

    // ---- 委托给纯数学层 ----
    public static int[] Pentatonic => ScaleMath.Pentatonic;
    public static string NoteName(int midi) => ScaleMath.NoteName(midi);
    public static int BaseMidi(int octave) => ScaleMath.BaseMidi(octave);
    public static int SlotCount(bool snap) => ScaleMath.SlotCount(snap);
    public static int SlotToMidi(int slot, int rowOffset, bool snap, int octave) => ScaleMath.SlotToMidi(slot, rowOffset, snap, octave);
    public static int MidiToSlot(int midi, bool snap, int octave) => ScaleMath.MidiToSlot(midi, snap, octave);
    public static string SlotNoteName(int slot, bool snap, int octave) => ScaleMath.SlotNoteName(slot, snap, octave);
    public static bool IsBlackSlot(int slot, bool snap) => ScaleMath.IsBlackSlot(slot, snap);

    /// <summary>槽位的键位提示，例如 "Z/Q"。</summary>
    public static string SlotKeyHint(int slot)
    {
        string low = slot < RowLow.Length ? KeyLabel(RowLow[slot]) : "";
        string high = slot < RowHigh.Length ? KeyLabel(RowHigh[slot]) : "";
        if (low.Length > 0 && high.Length > 0) return low + "/" + high;
        return low.Length > 0 ? low : high;
    }

    private static string KeyLabel(Key k)
    {
        string s = k.ToString();
        if (s.StartsWith("Key")) s = s.Substring(3);
        return s == "Comma" ? "," : s;
    }
}
