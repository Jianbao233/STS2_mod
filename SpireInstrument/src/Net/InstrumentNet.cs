using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using SpireInstrument.Songs;

namespace SpireInstrument.Net;

/// <summary>
/// 联机层：注册/注销自定义消息、发送本机音符、把收到的音符交给 UI 播放。
///
/// 网络服务实例会在"进房间 / 断线重连 / 退出对局"时更换，所以每帧比对一次实例，
/// 变了就重新注册（否则会出现"第一局能听到、第二局听不到"这类幽灵问题）。
/// </summary>
public sealed class InstrumentNet
{
    private INetGameService? _service;
    private bool _registered;
    private ushort _seq;

    /// <summary>收到远端音符（消息, 发送者 NetId）。</summary>
    public event Action<NoteMessage, ulong>? NoteReceived;

    /// <summary>收到远端曲谱。</summary>
    public event Action<SongSyncMessage, ulong>? SongReceived;

    public int SentNotes { get; private set; }
    public int ReceivedNotes { get; private set; }
    public int SentSongs { get; private set; }
    public int ReceivedSongs { get; private set; }
    public string LastError { get; private set; } = "";

    public bool IsConnected => _service is { IsConnected: true };

    public string Describe()
        => $"connected={IsConnected} sent={SentNotes} recv={ReceivedNotes} songs={SentSongs}/{ReceivedSongs}";

    /// <summary>每帧调用：跟踪网络服务实例并维护处理器注册。</summary>
    public void Update()
    {
        INetGameService? current = null;
        try { current = RunManager.Instance?.NetService; }
        catch { /* 对局未开始 / 实例未就绪 */ }

        if (ReferenceEquals(current, _service)) return;

        Unregister();
        _service = current;
        Register();
    }

    private void Register()
    {
        if (_service == null) return;
        try
        {
            _service.RegisterMessageHandler<NoteMessage>(HandleNote);
            _service.RegisterMessageHandler<SongSyncMessage>(HandleSong);
            _registered = true;
            GD.Print($"{SpireInstrumentMod.LogTag} net handlers registered (connected={_service.IsConnected}).");
        }
        catch (Exception e)
        {
            LastError = e.Message;
            GD.PushWarning($"{SpireInstrumentMod.LogTag} net handler registration failed: {e.Message}");
        }
    }

    private void Unregister()
    {
        if (_service == null || !_registered) return;
        try
        {
            _service.UnregisterMessageHandler<NoteMessage>(HandleNote);
            _service.UnregisterMessageHandler<SongSyncMessage>(HandleSong);
        }
        catch { /* 实例已失效，忽略 */ }
        _registered = false;
    }

    private void HandleNote(NoteMessage message, ulong senderId)
    {
        ReceivedNotes++;
        try { NoteReceived?.Invoke(message, senderId); }
        catch (Exception e) { GD.PushWarning($"{SpireInstrumentMod.LogTag} note handler failed: {e.Message}"); }
    }

    private void HandleSong(SongSyncMessage message, ulong senderId)
    {
        ReceivedSongs++;
        try { SongReceived?.Invoke(message, senderId); }
        catch (Exception e) { GD.PushWarning($"{SpireInstrumentMod.LogTag} song handler failed: {e.Message}"); }
    }

    /// <summary>发送一个音符事件（未联机时静默忽略）。</summary>
    public void SendNote(bool on, int midi, float velocity)
    {
        if (!IsConnected || _service == null) return;

        var msg = new NoteMessage
        {
            Kind = on ? NoteMessage.KindOn : NoteMessage.KindOff,
            KeyIndex = (byte)Math.Clamp(midi, 0, 127),
            Velocity = (byte)Math.Clamp((int)Math.Round(velocity * 127f), 0, 127),
            Seq = unchecked(++_seq),
        };

        try
        {
            _service.SendMessage(msg);
            SentNotes++;
        }
        catch (Exception e)
        {
            LastError = e.Message;
        }
    }

    /// <summary>广播曲谱 + 统一起拍（只在开始自动演奏时调用一次）。</summary>
    public void SendSong(Song song, float leadSeconds)
    {
        if (!IsConnected || _service == null) return;

        var packets = new List<SongNotePacket>(song.Notes.Count);
        foreach (var n in song.Notes)
        {
            packets.Add(new SongNotePacket
            {
                Time = (float)n.Time,
                Midi = (byte)Math.Clamp(n.Midi, 0, 127),
                Duration = (float)n.Duration,
                Velocity = (byte)Math.Clamp((int)Math.Round(n.Velocity * 127f), 0, 127),
            });
        }

        var msg = new SongSyncMessage { Name = song.Name, LeadSeconds = leadSeconds, Notes = packets };
        try
        {
            _service.SendMessage(msg);
            SentSongs++;
            GD.Print($"{SpireInstrumentMod.LogTag} song broadcast: {msg} ({packets.Count} notes)");
        }
        catch (Exception e)
        {
            LastError = e.Message;
            GD.PushWarning($"{SpireInstrumentMod.LogTag} song broadcast failed: {e.Message}");
        }
    }

    /// <summary>把收到的曲谱还原成可播放的 <see cref="Song"/>。</summary>
    public static Song ToSong(SongSyncMessage message)
    {
        var song = new Song { Name = message.Name, Imported = true };
        double last = 0;
        foreach (var p in message.Notes)
        {
            song.Notes.Add(new SongNote
            {
                Time = p.Time,
                Midi = p.Midi,
                Duration = Math.Max(0.05, p.Duration),
                Velocity = p.Velocity / 127f,
            });
            if (p.Time > last) last = p.Time;
        }
        song.Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
        song.Duration = last + 1.2;
        return song;
    }

    /// <summary>仅供自检：序列化 → 反序列化往返，验证消息在游戏序列化器里可用。</summary>
    public static string SelfTestRoundTrip()
    {
        // 音符消息（按位打包）
        var note = new NoteMessage { Kind = NoteMessage.KindOn, KeyIndex = 67, Velocity = 100, Seq = 4242 };
        var writer = new PacketWriter();
        note.Serialize(writer);

        var reader = new PacketReader();
        var buffer = new byte[writer.BytePosition];
        Array.Copy(writer.Buffer, buffer, buffer.Length);
        reader.Reset(buffer);

        var back = new NoteMessage();
        back.Deserialize(reader);
        bool noteOk = back.Kind == note.Kind && back.KeyIndex == note.KeyIndex
                      && back.Velocity == note.Velocity && back.Seq == note.Seq;

        // 曲谱消息（含列表）
        var song = new SongSyncMessage
        {
            Name = "自检曲",
            LeadSeconds = 0.45f,
            Notes = new List<SongNotePacket>
            {
                new() { Time = 0f, Midi = 60, Duration = 0.5f, Velocity = 100 },
                new() { Time = 0.6f, Midi = 64, Duration = 0.5f, Velocity = 90 },
            },
        };
        var w2 = new PacketWriter();
        song.Serialize(w2);
        var buf2 = new byte[w2.BytePosition];
        Array.Copy(w2.Buffer, buf2, buf2.Length);
        var r2 = new PacketReader();
        r2.Reset(buf2);
        var back2 = new SongSyncMessage();
        back2.Deserialize(r2);

        bool songOk = back2.Name == song.Name
                      && Math.Abs(back2.LeadSeconds - song.LeadSeconds) < 1e-4
                      && back2.Notes is { Count: 2 }
                      && back2.Notes[0].Midi == 60 && back2.Notes[1].Midi == 64
                      && Math.Abs(back2.Notes[1].Time - 0.6f) < 1e-3;

        // 消息类型是否被游戏登记（mod 自定义 INetMessage 的关键一步）
        bool registered;
        try
        {
            MessageTypes.Initialize();
            int id = MessageTypes.TypeToId<NoteMessage>();
            int id2 = MessageTypes.TypeToId<SongSyncMessage>();
            registered = id >= 0 && id2 >= 0 && id != id2;
        }
        catch (Exception e)
        {
            registered = false;
            GD.PushWarning($"{SpireInstrumentMod.LogTag} message type lookup failed: {e.Message}");
        }

        return $"noteBytes={buffer.Length} noteRt={noteOk} songBytes={buf2.Length} songRt={songOk} registered={registered}";
    }
}
