using System.Collections.Generic;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Multiplayer.Transport;

namespace SpireInstrument.Net;

/// <summary>
/// 实时音符消息（**mod 自定义的 INetMessage**）。
///
/// 关键选择：
///   * <see cref="NetTransferMode.Unreliable"/> —— 音频实时性优先：丢一两个音无所谓，
///     但绝不能因为重传把后续音符堆在后面（那会听起来像"卡带"）；
///   * <see cref="ShouldBroadcast"/> = true —— 客机发出后由主机转发给所有其他客机，
///     所以 3 人以上房间里每个人都能听到彼此；
///   * <see cref="ShouldBuffer"/> = false —— 实时事件，进缓冲队列只会迟到。
///
/// 体积：1 + 7 + 7 + 16 = 31 bit ≈ **4 字节**（按位打包），密集演奏也不怕。
/// </summary>
public struct NoteMessage : INetMessage, IPacketSerializable
{
    public const byte KindOn = 0;
    public const byte KindOff = 1;

    /// <summary>按下 / 松开。</summary>
    public byte Kind;

    /// <summary>音高 0..127。</summary>
    public byte KeyIndex;

    /// <summary>力度 0..127。</summary>
    public byte Velocity;

    /// <summary>序号（诊断丢包/乱序用）。</summary>
    public ushort Seq;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Unreliable;

    public LogLevel LogLevel => LogLevel.VeryDebug;

    public bool ShouldBuffer => false;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteByte(Kind, 1);
        writer.WriteByte(KeyIndex, 7);
        writer.WriteByte(Velocity, 7);
        writer.WriteUShort(Seq, 16);
    }

    public void Deserialize(PacketReader reader)
    {
        Kind = reader.ReadByte(1);
        KeyIndex = reader.ReadByte(7);
        Velocity = reader.ReadByte(7);
        Seq = reader.ReadUShort(16);
    }

    public override string ToString() => $"Note({(Kind == KindOn ? "on" : "off")} {KeyIndex} v{Velocity} #{Seq})";
}

/// <summary>曲谱里的一个音符（用于曲谱同步）。</summary>
public struct SongNotePacket : IPacketSerializable
{
    public float Time;
    public byte Midi;
    public float Duration;
    public byte Velocity;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteFloat(Time);
        writer.WriteByte(Midi, 7);
        writer.WriteFloat(Duration);
        writer.WriteByte(Velocity, 7);
    }

    public void Deserialize(PacketReader reader)
    {
        Time = reader.ReadFloat();
        Midi = reader.ReadByte(7);
        Duration = reader.ReadFloat();
        Velocity = reader.ReadByte(7);
    }
}

/// <summary>
/// 曲谱同步消息：**发曲谱 + 统一起拍时间**，而不是逐音符广播。
///
/// 为什么：逐音符同步必然受各自网络延迟影响，多人合奏会"散"；
/// 把整张曲谱发过去、约定同一个起拍时刻，各端本机合成 —— 才能真正齐奏。
/// 走 Reliable（曲谱丢了整首就废了），且只在开始播放时发一次。
/// </summary>
public struct SongSyncMessage : INetMessage, IPacketSerializable
{
    /// <summary>曲名（UI 提示用）。</summary>
    public string Name;

    /// <summary>统一延迟起拍（秒）：接收端在收到后这么多秒开始播放。</summary>
    public float LeadSeconds;

    /// <summary>曲谱。</summary>
    public List<SongNotePacket> Notes;

    public bool ShouldBroadcast => true;

    public NetTransferMode Mode => NetTransferMode.Reliable;

    public LogLevel LogLevel => LogLevel.Debug;

    public bool ShouldBuffer => false;

    public void Serialize(PacketWriter writer)
    {
        writer.WriteString(Name ?? string.Empty);
        writer.WriteFloat(LeadSeconds);
        writer.WriteList(Notes ?? new List<SongNotePacket>());
    }

    public void Deserialize(PacketReader reader)
    {
        Name = reader.ReadString();
        LeadSeconds = reader.ReadFloat();
        Notes = reader.ReadList<SongNotePacket>();
    }

    public override string ToString() => $"SongSync({Name} notes={Notes?.Count ?? 0} lead={LeadSeconds:F2}s)";
}
