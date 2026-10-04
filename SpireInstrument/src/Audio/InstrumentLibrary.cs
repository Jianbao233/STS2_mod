namespace SpireInstrument.Audio;

/// <summary>乐器的发声语义（决定按键/松键怎么处理）。</summary>
public enum InstrumentKind
{
    /// <summary>一次性：按下即响、自然衰减，松键不打断（竖琴、马林巴、钟琴…）。</summary>
    OneShot,

    /// <summary>延音型：按住持续发声（循环段），松键淡出释放（口风琴、管风琴、弦乐）。</summary>
    Sustain,
}

/// <summary>合成算法。</summary>
public enum SynthKind
{
    /// <summary>谐波叠加 + 各自衰减（键盘/槌击/钟类）。</summary>
    Additive,

    /// <summary>Karplus-Strong 物理建模拨弦（小数延迟保证音准）—— 竖琴/吉他类。</summary>
    Pluck,

    /// <summary>可无缝循环的稳态波形（延音类；循环段取整数个基频周期）。</summary>
    Sustain,

    /// <summary>打击乐：噪声瞬态 + 音高下滑的衰减音（鼓类）。音高映射鼓件。</summary>
    Percussion,
}

/// <summary>
/// 音色定义。
///
/// 全部由 <see cref="PcmRenderer"/> 在运行时程序化生成（零素材依赖、离线可构建、
/// 无版权风险）。参数按合成算法分组，新增音色＝加一条数据。
/// </summary>
public sealed class InstrumentDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required string Tag { get; init; }

    public InstrumentKind Kind { get; init; } = InstrumentKind.OneShot;
    public SynthKind Synth { get; init; } = SynthKind.Additive;

    /// <summary>泛音比（1.0 = 基频）。Additive / Sustain 使用。</summary>
    public double[] Partials { get; init; } = System.Array.Empty<double>();

    /// <summary>各泛音音量（与 Partials 对应）。</summary>
    public double[] Gains { get; init; } = System.Array.Empty<double>();

    /// <summary>基频衰减时间常数（秒）。</summary>
    public double Decay { get; init; } = 2.4;

    /// <summary>高次泛音衰减更快的指数（越大越"拨弦"）。</summary>
    public double DecayExp { get; init; } = 0.85;

    /// <summary>起音时间（秒）。Sustain 用它作为"循环点之前的起音段长度"。</summary>
    public double Attack { get; init; } = 0.003;

    /// <summary>泛音失谐程度（钢琴/钟这类非理想弦用）。</summary>
    public double Stretch { get; init; }

    /// <summary>起音噪声量（拨弦/槌击的"击弦感"）。</summary>
    public double ClickAmount { get; init; }

    /// <summary>起音噪声中心频率倍数（相对基频）。</summary>
    public double ClickFreqRatio { get; init; } = 7.0;

    /// <summary>采样总长（秒）。Additive / Pluck 使用。</summary>
    public double Duration { get; init; } = 2.2;

    // ---- Pluck（Karplus-Strong）专用 ----

    /// <summary>环路阻尼（越接近 1 衰减越慢、延音越长）。</summary>
    public double Damping { get; init; } = 0.997;

    /// <summary>初始激励的低通系数（越小越暗、越"闷"）。</summary>
    public double Brightness { get; init; } = 0.5;

    // ---- Sustain 专用 ----

    /// <summary>循环段目标时长（秒）；实际会取整到整数个基频周期以保证无缝。</summary>
    public double SustainSeconds { get; init; } = 0.7;

    /// <summary>
    /// 响度微调（1.0 = 不动）。
    /// 峰值已由 <c>Normalize</c> 统一到 0.85，但**峰值相同 ≠ 听感一样响**：
    /// 拨弦峰值高、RMS 低（听着轻），延音类持续输出、RMS 高（听着响）。
    /// 这里按实测 RMS 给每种音色一个配平系数，让 9 种音色切换时响度一致。
    /// </summary>
    public double Gain { get; init; } = 1.0;

    public bool IsSustain => Kind == InstrumentKind.Sustain;
}

/// <summary>内置乐器库：9 种音色（6 一次性 + 3 延音）。</summary>
public static class InstrumentLibrary
{
    private static readonly InstrumentDef[] All =
    {
        // ============ 一次性音色 ============
        new InstrumentDef
        {
            Id = "harp", Name = "竖琴 / 拨弦", Icon = "🪕", Tag = "Pluck",
            Synth = SynthKind.Pluck,
            // 物理建模拨弦：乱按也像在弹琴，因此设为默认音色
            Damping = 0.9985, Brightness = 0.62, Duration = 3.4,
            ClickAmount = 0.10, ClickFreqRatio = 9.0, Attack = 0.002,
        },
        new InstrumentDef
        {
            Id = "musicbox", Name = "八音盒", Icon = "🎼", Tag = "Box",
            Gain = 1.03,
            Partials = new[] { 1.0, 2.01, 5.43, 8.9, 13.1 },
            Gains    = new[] { 0.90, 0.40, 0.20, 0.10, 0.05 },
            Decay = 1.6, DecayExp = 0.95, Attack = 0.001,
            ClickAmount = 0.10, ClickFreqRatio = 12.0, Duration = 2.0,
        },
        new InstrumentDef
        {
            Id = "marimba", Name = "马林巴", Icon = "🪵", Tag = "Mallet",
            // 马林巴琴板是非谐和体：真实泛音比约 1 : 3.93 : 9.2
            Partials = new[] { 1.0, 3.93, 9.2, 1.99 },
            Gains    = new[] { 1.00, 0.30, 0.09, 0.05 },
            Decay = 0.9, DecayExp = 0.90, Attack = 0.002,
            ClickAmount = 0.14, ClickFreqRatio = 5.0, Duration = 1.2,
        },
        new InstrumentDef
        {
            Id = "piano", Name = "大钢琴", Icon = "🎹", Tag = "Piano",
            Gain = 0.7,
            Partials = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0 },
            Gains    = new[] { 0.95, 0.34, 0.16, 0.08, 0.04, 0.02 },
            Decay = 4.6, DecayExp = 0.72, Attack = 0.004, Stretch = 1.0,
            ClickAmount = 0.16, ClickFreqRatio = 7.0, Duration = 4.0,
        },
        new InstrumentDef
        {
            Id = "chip", Name = "芯片音", Icon = "👾", Tag = "8-bit",
            Gain = 1.11,
            Partials = new[] { 1.0, 2.0, 3.0 },
            Gains    = new[] { 0.50, 0.16, 0.07 },
            Decay = 0.30, DecayExp = 0.60, Attack = 0.001, Duration = 0.6,
        },
        new InstrumentDef
        {
            Id = "bell", Name = "钟琴", Icon = "🔔", Tag = "Bell",
            Gain = 1.09,
            Partials = new[] { 1.0, 2.76, 5.4, 8.93, 13.3 },
            Gains    = new[] { 0.90, 0.42, 0.24, 0.12, 0.06 },
            Decay = 5.0, DecayExp = 0.55, Attack = 0.004,
            ClickAmount = 0.08, ClickFreqRatio = 14.0, Duration = 4.5,
        },

        new InstrumentDef
        {
            Id = "epiano", Name = "电钢琴", Icon = "🎹", Tag = "EPiano",
            Gain = 0.92,
            // 电钢琴：基频 + 略带失谐的高次泛音（音叉感），衰减比大钢琴短
            Partials = new[] { 1.0, 2.0, 3.02, 4.05, 6.1 },
            Gains    = new[] { 0.95, 0.30, 0.22, 0.10, 0.04 },
            Decay = 2.6, DecayExp = 0.80, Attack = 0.003, Stretch = 1.0,
            ClickAmount = 0.12, ClickFreqRatio = 8.0, Duration = 2.6,
        },
        new InstrumentDef
        {
            Id = "guzheng", Name = "古筝", Icon = "🪕", Tag = "Pluck2",
            Gain = 1.15,
            // 拨弦变体：比竖琴更亮、衰减更快（短促清脆）
            Synth = SynthKind.Pluck,
            Damping = 0.9975, Brightness = 0.72, Duration = 2.4,
            ClickAmount = 0.16, ClickFreqRatio = 11.0, Attack = 0.001,
        },
        new InstrumentDef
        {
            Id = "drum", Name = "鼓组", Icon = "🥁", Tag = "Drum",
            // 打击乐：Decay=衰减时间常数；ClickAmount=噪声占比；Brightness=音高下滑程度
            Synth = SynthKind.Percussion,
            Decay = 0.14, ClickAmount = 0.42, Brightness = 0.9, Duration = 0.7,
        },
        new InstrumentDef
        {
            Id = "shaker", Name = "沙锤", Icon = "🪇", Tag = "Shaker",
            // 沙锤：几乎纯噪声、极短衰减
            Synth = SynthKind.Percussion,
            Decay = 0.06, ClickAmount = 0.95, Brightness = 0.1, Duration = 0.3,
        },

        new InstrumentDef
        {
            Id = "guitar", Name = "吉他", Icon = "🎸", Tag = "Guitar",
            // 拨弦变体：比竖琴更暖（低亮度）、余音更长
            Synth = SynthKind.Pluck,
            Damping = 0.9988, Brightness = 0.45, Duration = 3.0,
            ClickAmount = 0.12, ClickFreqRatio = 6.0, Attack = 0.002,
        },
        // ============ 延音型音色（按住持续、松键淡出）============
        new InstrumentDef
        {
            Id = "flute", Name = "长笛", Icon = "🪈", Tag = "Flute",
            Gain = 0.65,
            Kind = InstrumentKind.Sustain, Synth = SynthKind.Sustain,
            // 长笛：以基频为主、少量泛音，慢起音带气声感
            Partials = new[] { 1.0, 2.0, 3.0, 4.0 },
            Gains    = new[] { 0.88, 0.14, 0.05, 0.02 },
            Attack = 0.06, SustainSeconds = 0.7,
            ClickAmount = 0.06, ClickFreqRatio = 2.5,
        },

        new InstrumentDef
        {
            Id = "melodica", Name = "口风琴", Icon = "🎺", Tag = "Reed",
            Gain = 0.83,
            Kind = InstrumentKind.Sustain, Synth = SynthKind.Sustain,
            // 簧片：奇偶泛音都强、略带"吹气感"
            Partials = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0 },
            Gains    = new[] { 0.60, 0.34, 0.24, 0.16, 0.12, 0.08, 0.05, 0.03 },
            Attack = 0.035, SustainSeconds = 0.7,
            ClickAmount = 0.05, ClickFreqRatio = 3.0,
        },
        new InstrumentDef
        {
            Id = "organ", Name = "管风琴", Icon = "⛪", Tag = "Organ",
            Gain = 0.71,
            Kind = InstrumentKind.Sustain, Synth = SynthKind.Sustain,
            // 拉杆音栓：1' 2' 3' 4' 6' 8'（整数倍才能无缝循环）
            Partials = new[] { 1.0, 2.0, 3.0, 4.0, 6.0, 8.0 },
            Gains    = new[] { 0.62, 0.30, 0.18, 0.12, 0.07, 0.04 },
            Attack = 0.02, SustainSeconds = 0.8,
        },
        new InstrumentDef
        {
            Id = "strings", Name = "弦乐", Icon = "🎻", Tag = "Strings",
            Gain = 0.96,
            Kind = InstrumentKind.Sustain, Synth = SynthKind.Sustain,
            // 弦乐靠"多泛音 + 慢起音"表现厚度（循环段不能用失谐，否则接缝会响）
            Partials = new[] { 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 10.0, 12.0 },
            Gains    = new[] { 0.50, 0.30, 0.20, 0.14, 0.10, 0.08, 0.06, 0.05, 0.03, 0.02 },
            Attack = 0.16, SustainSeconds = 0.9,
            ClickAmount = 0.04, ClickFreqRatio = 4.0,
        },
    };

    public static IReadOnlyList<InstrumentDef> Items => All;

    public static InstrumentDef Get(string id)
    {
        foreach (var d in All)
        {
            if (d.Id == id) return d;
        }
        return All[0];
    }
}
