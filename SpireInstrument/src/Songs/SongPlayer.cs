using System;
using System.Collections.Generic;
using Godot;
using SpireInstrument.Audio;

namespace SpireInstrument.Songs;

/// <summary>播放模式。</summary>
public enum PlayMode
{
    /// <summary>自由弹：不播放曲目。</summary>
    Free,

    /// <summary>自动演奏：mod 自己把整曲弹出来（联机时队友听到的就是这个）。</summary>
    Auto,

    /// <summary>跟弹引导：曲目不自动弹，等玩家照着亮键按，带辅助补音。</summary>
    Learn,
}

/// <summary>
/// 曲目播放器：用**前视调度**（每帧把未来 ~20ms 内该响的音排出去）驱动声部池。
///
/// 为什么不用"给每个音建一个定时器"：Godot 的定时器精度受帧率影响，
/// 密集音符会产生大量定时器与抖动；前视调度只在一个地方推进索引，
/// 抖动被限制在一帧内（自检会报告实测抖动）。
/// </summary>
public partial class SongPlayer : Node
{
    private const double MinLookaheadSeconds = 0.020;
    private const double MaxLookaheadSeconds = 0.150;
    private const double TailSeconds = 0.8;

    private readonly InstrumentAudio _audio;
    private readonly List<SongNote> _autoPlayBuffer = new();

    private Song? _song;
    private PlayMode _mode = PlayMode.Free;
    private bool _playing;
    private double _startSeconds;
    private int _nextIndex;
    private double _jitterSum;
    private int _jitterCount;
    private double _worstJitter;
    private double _lookahead = MinLookaheadSeconds;

    public SongPlayer(InstrumentAudio audio)
    {
        _audio = audio;
    }

    public bool IsPlaying => _playing;
    public Song? Current => _song;
    public LearnJudge? Judge { get; private set; }

    public double Position => _playing ? Now() - _startSeconds : 0;

    public double Progress
    {
        get
        {
            if (_song == null || _song.Duration <= 0) return 0;
            return Math.Clamp(Position / _song.Duration, 0, 1);
        }
    }

    /// <summary>实测平均调度误差（毫秒，取绝对值：早/晚都算误差），自检用。</summary>
    public double AverageJitterMs => _jitterCount == 0 ? 0 : _jitterSum / _jitterCount * 1000.0;

    /// <summary>实测最坏调度误差（毫秒）。</summary>
    public double WorstJitterMs => _worstJitter * 1000.0;

    public int ScheduledNotes { get; private set; }

    private static double Now() => Time.GetTicksMsec() / 1000.0;

    public void Start(Song song, PlayMode mode, string instrumentId, double leadSeconds = 0.35)
    {
        Stop();

        _song = song;
        _mode = mode;
        _playing = mode != PlayMode.Free;

        foreach (var n in song.Notes) n.State = NoteState.Pending;
        Judge = mode == PlayMode.Learn ? new LearnJudge(song) : null;

        _nextIndex = 0;
        _jitterSum = 0;
        _jitterCount = 0;
        _worstJitter = 0;
        _lookahead = MinLookaheadSeconds;
        ScheduledNotes = 0;
        // 起拍留白：本地 350ms；联机同步时由发送方指定（两端用同一个起拍时刻）
        _startSeconds = Now() + Math.Max(0.05, leadSeconds);

        GD.Print($"{SpireInstrumentMod.LogTag} song start: {song.Name} mode={mode} notes={song.MelodyCount} " +
                 $"dur={song.Duration:F1}s lead={leadSeconds:F2}s imported={song.Imported}");
    }

    public void Stop()
    {
        _playing = false;
        _song = null;
        Judge = null;
        _nextIndex = 0;
    }

    public override void _Process(double delta)
    {
        if (!_playing || _song == null) return;

        // 自适应前视：Godot 不能"预约未来时刻播放"，只能在到点时立刻播。
        // 因此前视至少要覆盖一帧，否则长帧（资源预载、切后台限帧）会让音符成串迟到。
        // 宁可稳定地早一点点（听感上是一致的偏移），也不要忽早忽晚。
        _lookahead = Math.Clamp(delta * 1.25, MinLookaheadSeconds, MaxLookaheadSeconds);

        double t = Now() - _startSeconds;

        // ---- 自动演奏：把到点的音符排出去 ----
        if (_mode == PlayMode.Auto)
        {
            var def = InstrumentLibrary.Get(Core.ModSettings.InstrumentId);
            while (_nextIndex < _song.Notes.Count && _song.Notes[_nextIndex].Time <= t + _lookahead)
            {
                var n = _song.Notes[_nextIndex++];
                _audio.PlayPreview(n.Midi, n.Velocity, def, n.Duration + 0.4);
                ScheduledNotes++;

                double error = Math.Abs(t - n.Time);      // 早/晚都算误差
                if (error < 0.5)
                {
                    _jitterSum += error;
                    _jitterCount++;
                    if (error > _worstJitter) _worstJitter = error;
                }
            }
        }
        // ---- 跟弹：只推进判定，不自动发声（辅助补音另算）----
        else if (_mode == PlayMode.Learn && Judge != null)
        {
            _autoPlayBuffer.Clear();
            Judge.Tick(t, Core.ModSettings.Assist, _autoPlayBuffer);
            if (_autoPlayBuffer.Count > 0)
            {
                var def = InstrumentLibrary.Get(Core.ModSettings.InstrumentId);
                foreach (var n in _autoPlayBuffer)
                {
                    _audio.PlayPreview(n.KeyMidi, n.Velocity * 0.8f, def, n.Duration + 0.4);
                }
            }
        }

        if (t > _song.Duration + TailSeconds)
        {
            Judge?.Finish(t);
            FinishedCount++;
            GD.Print($"{SpireInstrumentMod.LogTag} song finished: {_song.Name} scheduled={ScheduledNotes} " +
                     $"err={AverageJitterMs:F1}ms worst={WorstJitterMs:F1}ms {(Judge != null ? Judge.Describe() : "")}");
            Stop();
        }
    }

    /// <summary>整曲自然播完的次数（自检用：验证收尾路径确实跑到）。</summary>
    public int FinishedCount { get; private set; }

    /// <summary>跟弹模式：玩家按下一个键。</summary>
    public HitGrade Press(int midi)
    {
        if (!_playing || _mode != PlayMode.Learn || Judge == null) return HitGrade.None;
        return Judge.Press(midi, Now() - _startSeconds);
    }
}
