using System;
using System.Collections.Generic;
using Godot;

namespace SpireInstrument.Audio;

/// <summary>一次发声的句柄：延音类需要靠它在松键时淡出释放。</summary>
public sealed class VoiceHandle
{
    internal AudioStreamPlayer Player = null!;
    public bool Sustained { get; internal set; }
    public bool Finished { get; internal set; }
}

/// <summary>
/// 声部管理与播放。
///
/// 设计要点：
///   * 对象池 —— 预创建固定数量 AudioStreamPlayer，演奏时零即时分配；
///   * 走游戏原生 "SFX" 音频总线 —— 自动跟随玩家音效音量与切后台静音；
///   * 复音上限内抢占最旧声部，保证快速连弹/和弦不哑；
///   * 采样按需生成 + LRU 缓存；
///   * 延音类：循环采样 + 松键 Tween 淡出（Godot 的 AudioStreamPlayer 没有 ADSR，自己补）。
/// </summary>
public partial class InstrumentAudio : Node
{
    private const int Polyphony = 24;
    private const int SampleCacheLimit = 128;
    private const string BusName = "SFX";

    private readonly Queue<AudioStreamPlayer> _free = new();
    private readonly List<AudioStreamPlayer> _active = new();
    private readonly Dictionary<AudioStreamPlayer, VoiceHandle> _handleByPlayer = new();
    private readonly Dictionary<string, AudioStreamWav> _samples = new();
    private readonly LinkedList<string> _lru = new();

    private float _volume = 1f;
    private int _voicesUsedTotal;

    public override void _Ready()
    {
        for (int i = 0; i < Polyphony; i++)
        {
            var player = new AudioStreamPlayer
            {
                Name = $"Voice{i}",
                Bus = BusName,
                VolumeDb = 0f,
                PitchScale = 1f,
            };
            AddChild(player);
            player.Finished += () => OnVoiceFinished(player);
            _free.Enqueue(player);
        }

        GD.Print($"{SpireInstrumentMod.LogTag} audio ready: {Polyphony} voices on bus '{BusName}', mix {ToneSynth.MixRate} Hz.");
    }

    public void SetVolume(float volume) => _volume = Math.Clamp(volume, 0f, 1f);

    public int ActiveVoices => _active.Count;

    /// <summary>当前缓存的采样条数（自检用：应始终 ≤ <see cref="SampleCacheLimit"/>）。</summary>
    public int SampleCacheCount => _lru.Count;

    /// <summary>缓存上限（自检用）。</summary>
    public int SampleCacheLimitPublic => SampleCacheLimit;

    public int TotalNotesPlayed => _voicesUsedTotal;

    /// <summary>播放一个音；返回句柄（延音类松键时用它释放）。</summary>
    public VoiceHandle? Play(int midi, float velocity, InstrumentDef def)
    {
        if (!IsInsideTree()) return null;

        var player = Acquire();
        player.Stream = GetSample(def, midi);
        // 多声部余量：样本单音峰值 0.85，但 N 个音是**线性相加**的 ——
        // 实测 6 音和弦求和峰值达 3.2–4.7（削顶 10–13dB，数千样本超限）。
        // 按发声数做线性补偿（见 PcmRenderer.PolyphonyGain），保证和弦不破音。
        float poly = PcmRenderer.PolyphonyGain(_active.Count + 1);
        player.VolumeDb = Mathf.LinearToDb(Math.Clamp(velocity * _volume * poly, 0.02f, 1f));
        player.PitchScale = 1f;
        player.Play();

        _active.Add(player);
        _voicesUsedTotal++;

        var handle = new VoiceHandle { Player = player, Sustained = def.IsSustain };
        _handleByPlayer[player] = handle;
        return handle;
    }

    /// <summary>
    /// 试听/一次性播放：延音类在 <paramref name="seconds"/> 后自动释放（避免一直响）。
    /// 返回句柄，调用方也可提前释放（例如远端音符的松键）。
    /// </summary>
    public VoiceHandle? PlayPreview(int midi, float velocity, InstrumentDef def, double seconds = 0.7)
    {
        var handle = Play(midi, velocity, def);
        if (handle == null) return null;
        if (!def.IsSustain) return handle;

        var tree = GetTree();
        if (tree == null)
        {
            Release(handle);
            return null;
        }

        var timer = tree.CreateTimer(seconds);
        if (timer == null)
        {
            Release(handle);
            return null;
        }
        timer.Timeout += () => Release(handle);
        return handle;
    }

    /// <summary>淡出释放（延音类松键）。</summary>
    public void Release(VoiceHandle? handle, double fadeSeconds = 0.13)
    {
        if (handle == null || handle.Finished) return;
        handle.Finished = true;

        var player = handle.Player;
        if (!GodotObject.IsInstanceValid(player)) return;

        if (!player.IsInsideTree())
        {
            ReturnToPool(player);
            return;
        }

        var tween = player.CreateTween();
        tween.TweenProperty(player, "volume_db", -60.0f, fadeSeconds);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(player)) player.Stop();
            ReturnToPool(player);
        }));
    }

    /// <summary>立即停掉（可制音的一次性音色，松键时用）。</summary>
    public void StopNow(VoiceHandle? handle)
    {
        if (handle == null || handle.Finished) return;
        handle.Finished = true;
        if (GodotObject.IsInstanceValid(handle.Player)) handle.Player.Stop();
        ReturnToPool(handle.Player);
    }

    /// <summary>停掉所有在响声部（收起面板 / 切乐器）。</summary>
    public void ReleaseAll()
    {
        // 必须先快照：ReturnToPool 会修改 _active，边遍历边删会抛"集合已修改"
        var snapshot = _active.ToArray();
        foreach (var p in snapshot)
        {
            if (GodotObject.IsInstanceValid(p)) p.Stop();
            ReturnToPool(p);
        }
        _active.Clear();
    }

    private AudioStreamPlayer Acquire()
    {
        if (_free.Count > 0) return _free.Dequeue();

        // 池子空 → 抢占最旧的在响声部（宁可牺牲最旧的音，也不吞掉新按下的键）
        var victim = _active[0];
        _active.RemoveAt(0);
        if (GodotObject.IsInstanceValid(victim)) victim.Stop();
        return victim;
    }

    private void OnVoiceFinished(AudioStreamPlayer player)
    {
        if (_handleByPlayer.TryGetValue(player, out var handle)) handle.Finished = true;
        ReturnToPool(player);
    }

    private void ReturnToPool(AudioStreamPlayer player)
    {
        _active.Remove(player);
        if (!_free.Contains(player)) _free.Enqueue(player);
    }

    private AudioStreamWav GetSample(InstrumentDef def, int midi)
    {
        string key = def.Id + ":" + midi;
        if (_samples.TryGetValue(key, out var cached))
        {
            _lru.Remove(key);
            _lru.AddFirst(key);
            return cached;
        }

        var wav = ToneSynth.Render(def, midi);
        _samples[key] = wav;
        _lru.AddFirst(key);

        while (_lru.Count > SampleCacheLimit)
        {
            var last = _lru.Last;
            if (last == null) break;
            _samples.Remove(last.Value);
            _lru.RemoveLast();
        }
        return wav;
    }
}
