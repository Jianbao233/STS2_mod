using System;

namespace SpireInstrument.Audio;

/// <summary>渲染结果：采样数据 + 循环区间（延音类才有意义）。</summary>
public readonly struct RenderResult
{
    public RenderResult(float[] samples, int loopBegin, int loopEnd)
    {
        Samples = samples;
        LoopBegin = loopBegin;
        LoopEnd = loopEnd;
    }

    public float[] Samples { get; }

    /// <summary>循环起点（帧）；等于 0 且 LoopEnd 为 0 时表示不循环。</summary>
    public int LoopBegin { get; }

    public int LoopEnd { get; }

    public bool IsLooping => LoopEnd > LoopBegin;
}

/// <summary>
/// 纯 C# 的 PCM 渲染（不引用 Godot）—— 因此可以在**不启动游戏**的前提下做离线自检
/// （音高、包络、削波、直流、爆音、循环接缝），见 tests/SpireInstrument.Tests。
///
/// 三种算法：
///   Additive —— 谐波叠加，各泛音独立衰减（键盘/槌击/钟）
///   Pluck    —— Karplus-Strong 物理建模拨弦，小数延迟保证音准（竖琴/吉他）
///   Sustain  —— 可无缝循环的稳态波形，循环段取整数个基频周期（口风琴/管风琴/弦乐）
/// </summary>
public static class PcmRenderer
{
    /// <summary>兼容入口：只要采样数据。</summary>
    public static float[] Render(InstrumentDef def, int midi, int mixRate, float peakTarget = 0.85f)
        => RenderFull(def, midi, mixRate, peakTarget).Samples;

    /// <summary>完整渲染（含循环区间）。</summary>
    public static RenderResult RenderFull(InstrumentDef def, int midi, int mixRate, float peakTarget = 0.85f)
    {
        double freq = 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        switch (def.Synth)
        {
            case SynthKind.Pluck:
            {
                var buf = RenderPluck(def, midi, mixRate, freq);
                // 瞬态乐器按 RMS 归一（见 NormalizeRms 注释）。
                // 目标取得高于峰值上限允许值：拨弦的波峰因数高，追平持续音的 RMS 必然削顶，
                // 所以让它顶到峰值上限（0.98）为止 —— 稳态仍比持续音低约 5dB，这是拨弦的物理特性。
                NormalizeRms(buf, 0.45 * def.Gain, 0.98f);
                FadeTail(buf, 0.006, mixRate);
                return new RenderResult(buf, 0, 0);
            }
            case SynthKind.Percussion:
            {
                var buf = RenderPercussion(def, midi, mixRate, freq);
                Normalize(buf, peakTarget * (float)def.Gain);
                FadeTail(buf, 0.004, mixRate);
                return new RenderResult(buf, 0, 0);
            }
            case SynthKind.Sustain:
            {
                var (buf, loopBegin, loopEnd) = RenderSustain(def, midi, mixRate, freq);
                Normalize(buf, peakTarget * (float)def.Gain);
                // 延音类不做尾音淡出：尾巴在循环区间内
                return new RenderResult(buf, loopBegin, loopEnd);
            }
            default:
            {
                var buf = RenderAdditive(def, midi, mixRate, freq);
                Normalize(buf, peakTarget * (float)def.Gain);
                FadeTail(buf, 0.006, mixRate);
                return new RenderResult(buf, 0, 0);
            }
        }
    }

    /// <summary>16bit 小端 PCM 字节流（Godot AudioStreamWav 的 Data 格式）。</summary>
    public static byte[] ToPcm16(float[] buf)
    {
        var bytes = new byte[buf.Length * 2];
        for (int i = 0; i < buf.Length; i++)
        {
            float v = Math.Clamp(buf[i], -1f, 1f);
            short s = (short)(v * 32000f);
            bytes[i * 2] = (byte)(s & 0xFF);
            bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
        }
        return bytes;
    }

    public static byte[] RenderPcm16(InstrumentDef def, int midi, int mixRate)
        => ToPcm16(Render(def, midi, mixRate));

    // ==================================================================
    //  Additive：谐波叠加 + 各自衰减
    // ==================================================================
    private static float[] RenderAdditive(InstrumentDef def, int midi, int mixRate, double freq)
    {
        int len = Math.Max(64, (int)(def.Duration * mixRate));
        var buf = new float[len];

        for (int p = 0; p < def.Partials.Length; p++)
        {
            double h = def.Partials[p];
            double f = freq * h;
            if (f >= mixRate * 0.45) continue;              // 超过奈奎斯特频率，跳过以免混叠

            double gain = p < def.Gains.Length ? def.Gains[p] : 0.0;
            if (gain <= 0) continue;

            double amp = gain * (1.0 + def.Stretch * h * h * 0.0008);
            double decay = def.Decay / Math.Pow(h, def.DecayExp);
            double attack = def.Attack + p * 0.0012;
            double w = 2.0 * Math.PI * f;

            for (int i = 0; i < len; i++)
            {
                double t = (double)i / mixRate;
                double env;
                if (t < attack)
                {
                    env = attack <= 0 ? 1.0 : t / attack;
                }
                else
                {
                    env = Math.Exp(-(t - attack) / Math.Max(0.02, decay));
                    if (env < 1e-4) break;                  // 该泛音已低于 -80dB
                }
                buf[i] += (float)(amp * env * Math.Sin(w * t));
            }
        }

        AddClick(def, buf, mixRate, freq);
        return buf;
    }

    // ==================================================================
    //  Pluck：Karplus-Strong（小数延迟 + 一阶低通阻尼）
    // ==================================================================
    private static float[] RenderPluck(InstrumentDef def, int midi, int mixRate, double freq)
    {
        int len = Math.Max(64, (int)(def.Duration * mixRate));
        var buf = new float[len];

        // 延迟线长度取整+2，读指针用小数插值 → 音准不受采样率整除限制
        double delay = mixRate / freq;
        int ringLen = (int)Math.Ceiling(delay) + 2;
        if (ringLen < 4) ringLen = 4;
        var ring = new float[ringLen];

        // 初始激励：低通白噪声（Brightness 越小越暗、越"闷"）
        var rnd = new Random(unchecked(midi * 7919 + StableHash(def.Id)));
        double a = Math.Clamp(def.Brightness, 0.05, 0.95);
        double lp = 0;
        double mean = 0;
        for (int i = 0; i < ringLen; i++)
        {
            double n = rnd.NextDouble() * 2.0 - 1.0;
            lp += a * (n - lp);
            ring[i] = (float)lp;
            mean += lp;
        }
        // 去直流：低通环路会把激励的直流分量一直循环下去（实测能到 4%，浪费动态余量）
        mean /= ringLen;
        for (int i = 0; i < ringLen; i++) ring[i] -= (float)mean;

        double damp = Math.Clamp(def.Damping, 0.80, 0.9999);
        int wp = 0;
        float prev = 0f;

        for (int i = 0; i < len; i++)
        {
            double rp = wp - delay;
            while (rp < 0) rp += ringLen;
            while (rp >= ringLen) rp -= ringLen;

            int i0 = (int)rp;
            int i1 = (i0 + 1) % ringLen;
            double frac = rp - i0;
            float y = (float)(ring[i0] * (1.0 - frac) + ring[i1] * frac);

            buf[i] = y;

            // 环路：一阶低通（相邻样本平均）+ 阻尼，写回读指针位置
            float back = (float)(((y + prev) * 0.5) * damp);
            ring[wp] = back;
            prev = y;

            wp++;
            if (wp >= ringLen) wp -= ringLen;
        }

        // 起音短斜坡，避免噪声起点造成爆音
        int atk = Math.Max(2, (int)(Math.Max(def.Attack, 0.002) * mixRate));
        for (int i = 0; i < atk && i < len; i++) buf[i] *= (float)i / atk;

        // 输出侧再去一次直流（保证整段均值为零）
        RemoveDc(buf);

        AddClick(def, buf, mixRate, freq);
        return buf;
    }

    // ==================================================================
    //  Sustain：起音段 + 可无缝循环的稳态段
    // ==================================================================
    private static (float[] Buf, int LoopBegin, int LoopEnd) RenderSustain(
        InstrumentDef def, int midi, int mixRate, double freq)
    {
        int attackLen = Math.Max(16, (int)(Math.Max(def.Attack, 0.02) * mixRate));

        // 循环长度取整数个基频周期 → 所有整数倍泛音都相位连续，接缝无缝
        int periods = Math.Max(8, (int)Math.Round(def.SustainSeconds * freq));
        int sustainLen = Math.Max(16, (int)Math.Round(periods * mixRate / freq));

        int total = attackLen + sustainLen;
        var buf = new float[total];

        for (int p = 0; p < def.Partials.Length; p++)
        {
            double h = def.Partials[p];
            double f = freq * h;
            if (f >= mixRate * 0.45) continue;
            double gain = p < def.Gains.Length ? def.Gains[p] : 0.0;
            if (gain <= 0) continue;

            // 高次泛音稍暗一点，听感更像真实乐器
            double amp = gain / (1.0 + 0.06 * h);
            double w = 2.0 * Math.PI * f;

            for (int i = 0; i < total; i++)
            {
                double env = i < attackLen ? (double)i / attackLen : 1.0;   // 起音后恒定 → 可循环
                buf[i] += (float)(amp * env * Math.Sin(w * (double)i / mixRate));
            }
        }

        AddClick(def, buf, mixRate, freq);
        return (buf, attackLen, total);
    }

    // ==================================================================
    //  公共处理
    // ==================================================================
    private static void AddClick(InstrumentDef def, float[] buf, int mixRate, double freq)
    {
        if (def.ClickAmount <= 0) return;

        var rnd = new Random(unchecked((int)(freq * 13) + StableHash(def.Id)));
        int clickLen = Math.Min(buf.Length, (int)(0.035 * mixRate));
        double cut = 2.0 * Math.PI * Math.Min(mixRate * 0.45, freq * def.ClickFreqRatio);
        for (int i = 0; i < clickLen; i++)
        {
            double t = (double)i / mixRate;
            double env = Math.Exp(-t / 0.008);
            double noise = rnd.NextDouble() * 2.0 - 1.0;
            buf[i] += (float)(def.ClickAmount * env * (noise * 0.6 + Math.Sin(cut * t) * 0.4));
        }
    }

    /// <summary>减去整段均值（去直流）。</summary>
    /// <summary>
    /// 跨进程稳定的字符串哈希（FNV-1a）。
    ///
    /// **不要用 <c>string.GetHashCode()</c> 做种子**：.NET Core 起字符串哈希每进程随机化，
    /// 会导致同一音色每次启动游戏渲染出的噪声激励都不同 —— 既让音色不稳定，也让响度测试漂移。
    /// </summary>
    private static int StableHash(string s)
    {
        unchecked
        {
            uint h = 2166136261u;
            foreach (char ch in s)
            {
                h ^= ch;
                h *= 16777619u;
            }
            return (int)(h & 0x7FFFFFFF);
        }
    }

    /// <summary>
    /// 按 **RMS** 归一（瞬态乐器用）。
    ///
    /// 为什么拨弦不能用峰值归一：Karplus-Strong 的峰值来自起音瞬间的噪声激励，
    /// 把峰值拉到 0.85 之后，**琴体（稳态）音量就取决于那一次噪声实现** ——
    /// 实测同一音色在不同种子下稳态 RMS 相差 2 倍以上（听感上就是"忽大忽小"）。
    /// 改按 RMS 定标、峰值只作上限兜底，音量才与噪声实现无关。
    /// </summary>
    private static void NormalizeRms(float[] buf, double targetRms, float peakCeiling)
    {
        if (buf.Length == 0) return;

        double sum = 0;
        float peak = 0;
        for (int i = 0; i < buf.Length; i++)
        {
            double v = buf[i];
            sum += v * v;
            float a = Math.Abs(buf[i]);
            if (a > peak) peak = a;
        }
        if (sum <= 0 || peak <= 0) return;

        double rms = Math.Sqrt(sum / buf.Length);
        double scale = targetRms / rms;
        if (peak * scale > peakCeiling) scale = peakCeiling / peak;

        for (int i = 0; i < buf.Length; i++) buf[i] = (float)(buf[i] * scale);
    }

    /// <summary>
    /// 多声部余量系数（**纯逻辑，可离线自检**）。
    ///
    /// 为什么必须补偿：单音样本峰值 0.85–0.98 看着安全，但多个音在混音总线里是**线性相加**的。
    /// 实测 6 音和弦（C4 E4 G4 C5 E5 G5）求和峰值达 **3.2–4.7**，超限样本数千个 ——
    /// 也就是同时按几个键就会破音（五声吸附恰恰鼓励这么按）。
    ///
    /// 取线性补偿 <c>1/N</c>（下限 0.15）：实测求和几乎线性增长，
    /// 用 1/√N 这类等功率补偿仍会超限（3.5 × 1/√6 = 1.43 &gt; 1.0）。
    /// 代价是和弦整体听感略保守 —— 玩家可用音量滑块补回来，比破音好。
    /// </summary>
    public static float PolyphonyGain(int activeVoices)
        => Math.Max(0.15f, 1f / Math.Max(1, activeVoices));

    /// <summary>
    /// 打击乐渲染：噪声瞬态 + 音高下滑的衰减音。
    /// 音高决定鼓件（低音=底鼓、中音=通鼓、高音=军鼓/踩镲）；
    /// Decay=衰减时间常数、ClickAmount=噪声占比、Brightness=音高下滑程度。
    /// </summary>
    private static float[] RenderPercussion(InstrumentDef def, int midi, int mixRate, double freq)
    {
        int n = Math.Max(16, (int)(def.Duration * mixRate));
        var buf = new float[n];
        var rnd = new Random(unchecked(StableHash(def.Id) + midi * 131));

        double decay = Math.Max(0.01, def.Decay);
        double noiseMix = Math.Clamp(def.ClickAmount, 0, 1);
        double drop = Math.Clamp(def.Brightness, 0, 1);

        for (int i = 0; i < n; i++)
        {
            double t = (double)i / mixRate;
            double env = Math.Exp(-t / decay);
            // 鼓皮特征：起音瞬间音高偏高，随后迅速落回
            double f = freq * (1.0 + drop * Math.Exp(-t / (decay * 0.3)));
            double tone = Math.Sin(2 * Math.PI * f * t);
            double noise = rnd.NextDouble() * 2.0 - 1.0;
            buf[i] = (float)(env * ((1.0 - noiseMix) * tone + noiseMix * noise));
        }
        RemoveDc(buf);   // 噪声瞬态均值不为零 —— 自检实测直流偏移 4.09E-003，必须去掉
        return buf;
    }

    private static void RemoveDc(float[] buf)
    {
        double sum = 0;
        for (int i = 0; i < buf.Length; i++) sum += buf[i];
        float mean = (float)(sum / buf.Length);
        if (Math.Abs(mean) < 1e-9f) return;
        for (int i = 0; i < buf.Length; i++) buf[i] -= mean;
    }

    private static void Normalize(float[] buf, float peakTarget)    {
        float peak = 0f;
        for (int i = 0; i < buf.Length; i++)
        {
            float a = Math.Abs(buf[i]);
            if (a > peak) peak = a;
        }
        if (peak <= 1e-6f) return;
        float k = peakTarget / peak;
        for (int i = 0; i < buf.Length; i++) buf[i] *= k;
    }

    private static void FadeTail(float[] buf, double seconds, int mixRate)
    {
        int n = Math.Min(buf.Length, (int)(seconds * mixRate));
        for (int i = 0; i < n; i++)
        {
            int idx = buf.Length - 1 - i;
            if (idx < 0) break;
            buf[idx] *= (float)((double)i / n);
        }
    }
}
