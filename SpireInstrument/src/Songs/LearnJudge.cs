using System;
using System.Collections.Generic;

namespace SpireInstrument.Songs;

/// <summary>跟弹判定等级。</summary>
public enum HitGrade
{
    None,
    Perfect,
    Good,
    Miss,
}

/// <summary>
/// 跟弹判定（**纯逻辑，不引用 Godot**，可离线自检）。
///
/// 判定窗口：±0.30s 内算命中，±0.09s 内算 Perfect。
/// 漏按超过 0.30s：辅助补音开启时标记为自动补上（不算失误），否则记 Miss 并断连击。
/// </summary>
public sealed class LearnJudge
{
    public const double PerfectWindow = 0.09;
    public const double HitWindow = 0.30;

    private readonly Song _song;

    public int Hits { get; private set; }
    public int Misses { get; private set; }
    public int Combo { get; private set; }
    public int BestCombo { get; private set; }
    public int AutoPlayed { get; private set; }
    public double LastDelta { get; private set; }

    public LearnJudge(Song song)
    {
        _song = song;
    }

    public double Accuracy
    {
        get
        {
            int total = Hits + Misses;
            return total == 0 ? 0 : (double)Hits / total;
        }
    }

    /// <summary>玩家按下一个键：返回判定结果（None = 附近没有该键对应的待弹音符）。</summary>
    public HitGrade Press(int midi, double now)
    {
        SongNote? best = null;
        double bestDist = double.MaxValue;

        foreach (var n in _song.Notes)
        {
            if (n.Bass || n.State != NoteState.Pending) continue;
            if (n.KeyMidi != midi) continue;
            double d = Math.Abs(n.Time - now);
            if (d < bestDist) { bestDist = d; best = n; }
        }

        if (best == null || bestDist > HitWindow) return HitGrade.None;

        LastDelta = now - best.Time;
        best.State = NoteState.Hit;
        Hits++;
        Combo++;
        if (Combo > BestCombo) BestCombo = Combo;
        return bestDist <= PerfectWindow ? HitGrade.Perfect : HitGrade.Good;
    }

    /// <summary>
    /// 推进时间：把超时未按的音符处理掉。
    /// <paramref name="autoPlay"/> 收集需要辅助补音的音符（调用方负责实际发声）。
    /// </summary>
    public void Tick(double now, bool assist, List<SongNote>? autoPlay = null)
    {
        foreach (var n in _song.Notes)
        {
            if (n.Bass || n.State != NoteState.Pending) continue;
            if (now <= n.Time + HitWindow) continue;

            if (assist)
            {
                n.State = NoteState.Auto;
                AutoPlayed++;
                autoPlay?.Add(n);
            }
            else
            {
                n.State = NoteState.Missed;
                Misses++;
                Combo = 0;
            }
        }
    }

    /// <summary>把整曲标记为结束（剩余待弹音符按漏按处理，避免结算时残留）。</summary>
    public void Finish(double now)
    {
        foreach (var n in _song.Notes)
        {
            if (n.Bass || n.State != NoteState.Pending) continue;
            n.State = NoteState.Missed;
            Misses++;
        }
        Combo = 0;
    }

    /// <summary>尚未处理的旋律音符数。</summary>
    public int PendingCount
    {
        get
        {
            int n = 0;
            foreach (var note in _song.Notes) if (!note.Bass && note.State == NoteState.Pending) n++;
            return n;
        }
    }

    public string Describe()
        => $"hit={Hits} miss={Misses} auto={AutoPlayed} combo={Combo} best={BestCombo} acc={Accuracy:P0}";
}
