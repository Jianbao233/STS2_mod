namespace SpireInstrument.Audio;

/// <summary>把纯 C# 渲染出的 PCM 包装成 Godot 音频流（延音类带循环点）。</summary>
public static class ToneSynth
{
    /// <summary>混音率。22050Hz 单声道对这类音色足够，且省一半内存与生成耗时。</summary>
    public const int MixRate = 22050;

    public static Godot.AudioStreamWav Render(InstrumentDef def, int midi)
    {
        var result = PcmRenderer.RenderFull(def, midi, MixRate);
        var wav = new Godot.AudioStreamWav
        {
            Format = Godot.AudioStreamWav.FormatEnum.Format16Bits,
            MixRate = MixRate,
            Stereo = false,
            Data = PcmRenderer.ToPcm16(result.Samples),
        };

        if (result.IsLooping)
        {
            // 延音类：循环段取整数个基频周期，接缝无缝（离线自检会验证这一点）
            wav.LoopMode = Godot.AudioStreamWav.LoopModeEnum.Forward;
            wav.LoopBegin = result.LoopBegin;
            wav.LoopEnd = result.LoopEnd;
        }

        return wav;
    }
}
