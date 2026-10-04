using System;
using System.Collections.Generic;

namespace SpireInstrument.Songs;

/// <summary>MIDI 解析结果。</summary>
public sealed class MidiParseResult
{
    public Song? Song { get; init; }
    public string? Error { get; init; }
    public int TrackCount { get; init; }
    public int RawNoteCount { get; init; }
    public int SkippedForDensity { get; init; }
    public bool Success => Song != null && Error == null;
}

/// <summary>
/// 标准 MIDI 文件（SMF）解析 —— **纯 C#，可离线自检**。
///
/// 覆盖：格式 0/1、多轨合并、running status、velocity=0 视作 note off、
/// tempo 元事件、未知事件跳过、超密音符限流、损坏文件不抛到调用方。
/// 已知取舍：**忽略中途变速**（用最后一个 tempo），对绝大多数曲子听感无影响。
/// </summary>
public static class MidiFileParser
{
    private const int MaxNotes = 6000;
    private const int MaxNotesPer40Ms = 6;   // 限流：避免导入密集钢琴曲时爆声部

    public static MidiParseResult Parse(byte[] data, string displayName)
    {
        try
        {
            return ParseInternal(data, displayName);
        }
        catch (Exception e)
        {
            return new MidiParseResult { Error = $"解析失败：{e.Message}" };
        }
    }

    private static MidiParseResult ParseInternal(byte[] data, string displayName)
    {
        if (data.Length < 14) return new MidiParseResult { Error = "文件太小，不是 MIDI" };

        int p = 0;
        if (ReadU32(data, ref p) != 0x4D546864) return new MidiParseResult { Error = "缺少 MThd 头（不是标准 MIDI 文件）" };

        int headerLen = ReadU32(data, ref p);
        ReadU16(data, ref p);                       // format（0/1/2 都按合并处理）
        int trackCount = ReadU16(data, ref p);
        int division = ReadU16(data, ref p);
        p += Math.Max(0, headerLen - 6);

        int ticksPerBeat = (division & 0x8000) != 0 ? 480 : (division == 0 ? 480 : division);
        int tempo = 500_000;                        // 微秒/四分音符 = 120 BPM
        var raw = new List<RawNote>();

        for (int track = 0; track < trackCount && p + 8 <= data.Length; track++)
        {
            if (ReadU32(data, ref p) != 0x4D54726B) break;   // 非 MTrk 就停
            int trackLen = ReadU32(data, ref p);
            int end = Math.Min(p + trackLen, data.Length);

            long tick = 0;
            int running = 0;
            var active = new Dictionary<int, (long Tick, int Vel)>();

            while (p < end)
            {
                tick += ReadVarLen(data, ref p);

                int status = data[p];
                if ((status & 0x80) != 0)
                {
                    p++;
                    if (status < 0xF0) running = status;     // meta/sysex 不覆盖 running status
                }
                else
                {
                    status = running;                        // running status：沿用上一个状态字节
                }

                int type = status & 0xF0;

                if (status == 0xFF)
                {
                    int metaType = data[p++];
                    int len = ReadVarLen(data, ref p);
                    if (metaType == 0x51 && len == 3 && p + 3 <= data.Length)
                    {
                        tempo = (data[p] << 16) | (data[p + 1] << 8) | data[p + 2];
                    }
                    p += len;
                }
                else if (status == 0xF0 || status == 0xF7)
                {
                    int len = ReadVarLen(data, ref p);
                    p += len;
                }
                else if (type == 0x90 || type == 0x80)
                {
                    int note = data[p++];
                    int vel = data[p++];
                    if (type == 0x90 && vel > 0)
                    {
                        active[note] = (tick, vel);
                    }
                    else if (active.TryGetValue(note, out var on))
                    {
                        raw.Add(new RawNote(on.Tick, Math.Max(1, tick - on.Tick), note, on.Vel / 127.0));
                        active.Remove(note);
                    }
                }
                else if (type == 0xA0 || type == 0xB0 || type == 0xE0)
                {
                    p += 2;
                }
                else if (type == 0xC0 || type == 0xD0)
                {
                    p += 1;
                }
                else
                {
                    break;                                   // 未知状态：跳出本轨
                }

                if (raw.Count > MaxNotes * 3) break;         // 防御超大文件
            }

            p = end;
        }

        if (raw.Count == 0) return new MidiParseResult { Error = "文件里没有音符事件", TrackCount = trackCount };

        double secPerTick = (tempo / 1_000_000.0) / ticksPerBeat;
        long first = long.MaxValue;
        foreach (var n in raw) if (n.Tick < first) first = n.Tick;

        var notes = new List<SongNote>(raw.Count);
        foreach (var n in raw)
        {
            notes.Add(new SongNote
            {
                Time = (n.Tick - first) * secPerTick,
                Midi = n.Note,
                Duration = Math.Max(0.08, n.DurTick * secPerTick * 0.95),
                Velocity = (float)Math.Clamp(n.Vel, 0.15, 1.0),
            });
        }
        notes.Sort((a, b) => a.Time.CompareTo(b.Time));

        // 限流：每 40ms 窗口最多 6 个同时发声，避免导入密集钢琴曲时爆声部
        var kept = new List<SongNote>(notes.Count);
        var windowCount = new Dictionary<long, int>();
        int skipped = 0;
        foreach (var n in notes)
        {
            long bucket = (long)Math.Round(n.Time / 0.04);
            windowCount.TryGetValue(bucket, out int c);
            if (c < MaxNotesPer40Ms)
            {
                kept.Add(n);
                windowCount[bucket] = c + 1;
            }
            else
            {
                skipped++;
            }
        }

        double duration = kept.Count > 0 ? kept[kept.Count - 1].Time + 1.2 : 1.0;
        var song = new Song
        {
            Name = "导入：" + displayName,
            Notes = kept,
            Duration = duration,
            Imported = true,
        };

        return new MidiParseResult
        {
            Song = song,
            TrackCount = trackCount,
            RawNoteCount = raw.Count,
            SkippedForDensity = skipped,
        };
    }

    private readonly record struct RawNote(long Tick, long DurTick, int Note, double Vel);

    private static int ReadVarLen(byte[] data, ref int p)
    {
        int value = 0;
        int b;
        do
        {
            if (p >= data.Length) throw new InvalidOperationException("文件在变长数值处被截断");
            b = data[p++];
            value = (value << 7) | (b & 0x7F);
        } while ((b & 0x80) != 0);
        return value;
    }

    private static int ReadU32(byte[] data, ref int p)
    {
        if (p + 4 > data.Length) throw new InvalidOperationException("文件被截断");
        int v = (data[p] << 24) | (data[p + 1] << 16) | (data[p + 2] << 8) | data[p + 3];
        p += 4;
        return v;
    }

    private static int ReadU16(byte[] data, ref int p)
    {
        if (p + 2 > data.Length) throw new InvalidOperationException("文件被截断");
        int v = (data[p] << 8) | data[p + 1];
        p += 2;
        return v;
    }
}
