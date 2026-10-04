namespace SpireInstrument.Songs;

/// <summary>
/// 内置曲库。
///
/// **版权红线**：工坊发布的 mod 不能内置受版权保护的曲目。
/// 因此这里全部是公有领域民歌/练习曲，且每条都带 <see cref="SongDefinition.PublicDomain"/>
/// 与 <see cref="SongDefinition.Credit"/> 标注，发布审核时逐条核对。
/// 玩家想弹流行曲，请用「导入 MIDI」自己导入（文件不进 mod 包）。
/// </summary>
public static class SongLibrary
{
    private static readonly SongDefinition[] All =
    {
        new SongDefinition
        {
            Id = "twinkle", Name = "小星星", Bpm = 104, PublicDomain = true,
            Credit = "法国民谣《Ah! vous dirai-je, maman》，公有领域",
            Melody =
                "C4:1 C4:1 G4:1 G4:1 A4:1 A4:1 G4:2 " +
                "F4:1 F4:1 E4:1 E4:1 D4:1 D4:1 C4:2 " +
                "G4:1 G4:1 F4:1 F4:1 E4:1 E4:1 D4:2 " +
                "G4:1 G4:1 F4:1 F4:1 E4:1 E4:1 D4:2 " +
                "C4:1 C4:1 G4:1 G4:1 A4:1 A4:1 G4:2 " +
                "F4:1 F4:1 E4:1 E4:1 D4:1 D4:1 C4:2",
        },
        new SongDefinition
        {
            Id = "ode", Name = "欢乐颂", Bpm = 112, PublicDomain = true,
            Credit = "贝多芬第九交响曲第四乐章主题，公有领域",
            Melody =
                "E4:1 E4:1 F4:1 G4:1 G4:1 F4:1 E4:1 D4:1 " +
                "C4:1 C4:1 D4:1 E4:1 E4:1.5 D4:0.5 D4:2 " +
                "E4:1 E4:1 F4:1 G4:1 G4:1 F4:1 E4:1 D4:1 " +
                "C4:1 C4:1 D4:1 E4:1 D4:1.5 C4:0.5 C4:2",
        },
        new SongDefinition
        {
            Id = "jingle", Name = "铃儿响叮当", Bpm = 126, PublicDomain = true,
            Credit = "James Lord Pierpont (1857)，公有领域",
            Melody =
                "E4:1 E4:1 E4:2 E4:1 E4:1 E4:2 " +
                "E4:1 G4:1 C4:1 D4:1 E4:4 " +
                "F4:1 F4:1 F4:1.5 F4:0.5 F4:1 E4:1 E4:1 E4:0.5 E4:0.5 " +
                "E4:1 D4:1 D4:1 E4:1 D4:2 G4:2",
        },
        new SongDefinition
        {
            Id = "farewell", Name = "送别（片段）", Bpm = 76, PublicDomain = true,
            Credit = "李叔同《送别》（曲调为美国作曲家 J.P. Ordway《Dreaming of Home and Mother》，公有领域）",
            Melody =
                "G4:1 E4:1 G4:1 C5:2 B4:0.5 C5:0.5 A4:2 " +
                "G4:1 E4:1 G4:1 C5:2 B4:0.5 C5:0.5 A4:2 " +
                "A4:1 G4:1 E4:1 G4:2 C5:1 B4:1 C5:2 A4:4",
        },
        new SongDefinition
        {
            Id = "canon", Name = "卡农和声进行（练习）", Bpm = 96, PublicDomain = true,
            Credit = "Pachelbel 卡农常用和声进行的自编简化练习句，公有领域",
            Melody =
                "C5:1 G4:1 A4:1 E4:1 F4:1 C4:1 F4:1 G4:1 " +
                "C5:1 G4:1 A4:1 E4:1 F4:1 C4:1 F4:1 G4:1 " +
                "C5:1 G4:1 A4:1 E4:1 F4:1 C4:1 F4:1 G4:1 C4:4",
        },
        new SongDefinition
        {
            Id = "scale", Name = "音阶练习（五声）", Bpm = 100, PublicDomain = true,
            Credit = "自编练习句",
            Melody =
                "C4:1 D4:1 E4:1 G4:1 A4:1 C5:1 A4:1 G4:1 E4:1 D4:1 C4:2 " +
                "C4:1 E4:1 G4:1 C5:1 G4:1 E4:1 C4:2",
        },
    };

    public static System.Collections.Generic.IReadOnlyList<SongDefinition> Items => All;

    public static SongDefinition? Get(string id)
    {
        foreach (var d in All) if (d.Id == id) return d;
        return null;
    }
}
