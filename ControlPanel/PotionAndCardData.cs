using System;

namespace ControlPanel;

/// <summary>遭遇战类型：普通/精英/Boss</summary>
public enum EncounterType { Normal, Elite, Boss }

/// <summary>
/// 控制面板静态数据：药水分类、卡牌子集、药水完整列表、遭遇战列表、遗物、事件。
/// </summary>
public static class PotionAndCardData
{
    /// <summary>牌堆类型（对应 PileType）</summary>
    public static readonly (string name, string cmd)[] PileTypes =
    {
        ("手牌", "Hand"), ("抽牌堆", "Draw"), ("弃牌堆", "Discard"), ("牌组", "Deck"),
    };

    /// <summary>遗物稀有度</summary>
    public static readonly string[] RelicRarities = { "全部", "普通", "罕见", "稀有", "远古", "初始", "事件", "商店" };
    public static readonly string[] PotionCategories = { "全部", "伤害", "格挡", "增益", "减益", "治疗", "能量", "抽牌/技能", "其他" };

    /// <summary>约 80 张常用卡牌 ID</summary>
    public static readonly string[] CardIds =
    {
        "BODY_SLAM", "STRIKE_IRONCLAD", "STRIKE_SILENT", "STRIKE_DEFECT", "STRIKE_NECROBINDER", "STRIKE_REGENT",
        "DEFEND_IRONCLAD", "DEFEND_SILENT", "DEFEND_DEFECT", "DEFEND_NECROBINDER", "DEFEND_REGENT",
        "BASH", "IRON_WAVE", "POMMEL_STRIKE", "SUNDER", "WHIRLWIND", "UPPERCUT", "BLUDGEON", "TWIN_STRIKE",
        "BATTLE_TRANCE", "SHRUG_IT_OFF", "FLAME_BARRIER", "TRUE_GRIT", "ENTRENCH", "BARRICADE",
        "OFFERING", "REAPER_FORM", "DEMON_FORM", "CORRUPTION", "RAMPAGE",
        "NEUTRALIZE", "DAGGER_THROW", "DAGGER_SPRAY", "BACKSTAB", "POISONED_STAB", "DEADLY_POISON",
        "NOXIOUS_FUMES", "FOOTWORK", "BACKFLIP", "LEG_SWEEP", "DODGE_AND_ROLL", "BLUR", "CALTROPS",
        "PIERCING_WAIL", "DASH", "SLICE", "PRECISE_CUT", "INFINITE_BLADES", "BLADE_DANCE",
        "ZAP", "DUALCAST", "BALL_LIGHTNING", "COLD_SNAP", "BEAM_CELL", "DEFRAGMENT", "COOLHEADED",
        "GLACIER", "LOOP", "BOOT_SEQUENCE", "TURBO", "CHARGE_BATTERY", "HELLO_WORLD",
        "CLAW", "COMPILE_DRIVER", "FUSION", "CONVERGENCE", "HYPERBEAM",
        "REANIMATE", "REAP", "SACRIFICE", "BLOODLETTING", "HEMOKINESIS",
        "FLASH_OF_STEEL", "MASTER_OF_STRATEGY", "APOTHEOSIS", "DISCOVERY",
        "DOUBT", "VOID", "PURITY", "NORMALITY"
    };

    /// <summary>与 CardIds 对应中文名</summary>
    public static readonly string[] CardZhs =
    {
        "全身撞击", "打击", "打击", "打击", "打击", "打击",
        "防御", "防御", "防御", "防御", "防御",
        "痛击", "铁斩波", "剑柄打击", "分离", "旋风斩", "上勾拳", "重锤", "双重打击",
        "战斗专注", "耸肩无视", "火焰屏障", "坚毅", "巩固", "壁垒",
        "祭品", "死神形态", "恶魔形态", "腐化", "暴走",
        "中和", "投掷匕首", "匕首雨", "背刺", "带毒刺击", "致命毒药",
        "毒雾", "灵动步法", "后空翻", "扫腿", "闪躲翻滚", "残影", "铁蒺藜",
        "尖啸", "冲刺", "切割", "精确切击", "无尽刀刃", "刀刃之舞",
        "电击", "双重释放", "球状闪电", "寒流", "光束射线", "碎片整理", "冷静头脑",
        "冰川", "循环", "启动流程", "内核加速", "充电", "你好世界",
        "爪击", "编译冲击", "聚变", "汇流", "超能光束",
        "死者苏生", "收割", "牺牲", "放血", "御血术",
        "亮剑", "战略大师", "神化", "发现",
        "疑虑", "虚空", "净化", "凡庸"
    };

    /// <summary>全部 63 个药水 (id, zh, 分类)</summary>
    public static readonly (string id, string zh, string cat)[] PotionData =
    {
        ("ASHWATER", "灰水", "其他"),
        ("ATTACK_POTION", "攻击药水", "伤害"),
        ("BEETLE_JUICE", "甲虫汁", "其他"),
        ("BLESSING_OF_THE_FORGE", "熔炉的祝福", "其他"),
        ("BLOCK_POTION", "格挡药水", "格挡"),
        ("BLOOD_POTION", "鲜血药水", "治疗"),
        ("BONE_BREW", "骨头酿", "伤害"),
        ("BOTTLED_POTENTIAL", "瓶装潜能", "增益"),
        ("CLARITY", "明晰提取物", "增益"),
        ("COLORLESS_POTION", "无色药水", "抽牌/技能"),
        ("COSMIC_CONCOCTION", "宇宙药剂", "其他"),
        ("CUNNING_POTION", "狡诈药水", "增益"),
        ("CURE_ALL", "痊愈药水", "治疗"),
        ("DEXTERITY_POTION", "敏捷药水", "增益"),
        ("DISTILLED_CHAOS", "精炼混沌", "其他"),
        ("DROPLET_OF_PRECOGNITION", "预知之滴", "其他"),
        ("DUPLICATOR", "复制药水", "其他"),
        ("ENERGY_POTION", "能量药水", "能量"),
        ("ENTROPIC_BREW", "混沌药水", "其他"),
        ("ESSENCE_OF_DARKNESS", "黑暗精华", "伤害"),
        ("EXPLOSIVE_AMPOULE", "爆炸安瓿", "伤害"),
        ("FAIRY_IN_ABOTTLE", "瓶中精灵", "治疗"),  // 游戏内拼写为 ABOTTLE
        ("FIRE_POTION", "火焰药水", "伤害"),
        ("FLEX_POTION", "肌肉药水", "增益"),
        ("FOCUS_POTION", "集中药水", "增益"),
        ("FORTIFIER", "固化药水", "格挡"),
        ("FOUL_POTION", "污浊药水", "其他"),
        ("FRUIT_JUICE", "果汁", "其他"),
        ("FYSH_OIL", "异鱼之油", "其他"),
        ("GAMBLERS_BREW", "赌徒特酿", "其他"),
        ("GHOST_IN_AJAR", "罐中幽灵", "其他"),
        ("GIGANTIFICATION_POTION", "超巨化药水", "增益"),
        ("GLOWWATER_POTION", "发光水", "其他"),
        ("HEART_OF_IRON", "铁心药水", "其他"),
        ("KINGS_COURAGE", "王之勇气", "增益"),
        ("LIQUID_BRONZE", "流动铜液", "能量"),
        ("LIQUID_MEMORIES", "液态记忆", "其他"),
        ("LUCKY_TONIC", "幸运补剂", "增益"),
        ("MAZALETHS_GIFT", "马萨雷斯的赠礼", "其他"),
        ("OROBIC_ACID", "欧洛巴斯之酸", "其他"),
        ("POISON_POTION", "毒药水", "伤害"),
        ("POTION_OF_BINDING", "缚魂药水", "其他"),
        ("POTION_OF_CAPACITY", "扩容药水", "其他"),
        ("POTION_OF_DOOM", "灾厄药水", "其他"),
        ("POTION_SHAPED_ROCK", "药水形状的石头", "其他"),
        ("POT_OF_GHOULS", "尸鬼瓮", "其他"),
        ("POWDERED_DEMISE", "消亡粉末", "伤害"),
        ("POWER_POTION", "能力药水", "增益"),
        ("RADIANT_TINCTURE", "明耀酊剂", "增益"),
        ("REGEN_POTION", "再生药水", "治疗"),
        ("SHACKLING_POTION", "镣铐药水", "减益"),
        ("SHIP_IN_ABOTTLE", "瓶中船", "其他"),  // 游戏内拼写为 ABOTTLE
        ("SKILL_POTION", "技能药水", "抽牌/技能"),
        ("SNECKO_OIL", "异蛇之油", "其他"),
        ("SOLDIERS_STEW", "士兵炖汤", "其他"),
        ("SPEED_POTION", "速度药水", "其他"),
        ("STABLE_SERUM", "稳定血清", "增益"),
        ("STAR_POTION", "星星药水", "其他"),
        ("STRENGTH_POTION", "力量药水", "增益"),
        ("SWIFT_POTION", "迅捷药水", "其他"),
        ("TOUCH_OF_INSANITY", "癫狂之触", "其他"),
        ("VULNERABLE_POTION", "易伤药水", "减益"),
        ("WEAK_POTION", "虚弱药水", "减益"),
    };

    /// <summary>约 30 个遭遇战 (id, zh)</summary>
    public static readonly (string id, string zh)[] FightData =
    {
        ("AXEBOTS_NORMAL", "斧头机器人（普通）"),
        ("SLIMES_NORMAL", "史莱姆（普通）"),
        ("BOWLBUGS_NORMAL", "碗虫（普通）"),
        ("CULTISTS_NORMAL", "邪教徒（普通）"),
        ("EXOSKELETONS_NORMAL", "外骨骼（普通）"),
        ("LOUSE_PROGENITOR_NORMAL", "虱母（普通）"),
        ("CHOMPERS_NORMAL", "啃噬者（普通）"),
        ("GREMLIN_MERC_NORMAL", "地精佣兵（普通）"),
        ("FOGMOG_NORMAL", "雾怪（普通）"),
        ("PUNCH_CONSTRUCT_NORMAL", "拳击构体（普通）"),
        ("CUBEX_CONSTRUCT_NORMAL", "立方体构体（普通）"),
        ("FABRICATOR_NORMAL", "制造机（普通）"),
        ("OWL_MAGISTRATE_NORMAL", "猫头鹰法官（普通）"),
        ("SEWER_CLAM_NORMAL", "下水道蚌（普通）"),
        ("INKLETS_NORMAL", "墨仔（普通）"),
        ("HAUNTED_SHIP_NORMAL", "幽灵船（普通）"),
        ("LIVING_FOG_NORMAL", "活雾（普通）"),
        ("FOSSIL_STALKER_NORMAL", "化石潜行者（普通）"),
        ("KNIGHTS_ELITE", "骑士（精英）"),
        ("BYRDONIS_ELITE", "多尼斯异鸟（精英）"),
        ("PHANTASMAL_GARDENERS_ELITE", "幻影园丁（精英）"),
        ("ENTOMANCER_ELITE", "昆虫法师（精英）"),
        ("MECHA_KNIGHT_ELITE", "机甲骑士（精英）"),
        ("QUEEN_BOSS", "女王Boss"),
        ("KAISER_CRAB_BOSS", "帝王蟹Boss"),
        ("CEREMONIAL_BEAST_BOSS", "仪式巨兽Boss"),
        ("DOORMAKER_BOSS", "门匠Boss"),
        ("SOUL_FYSH_BOSS", "灵魂异鱼Boss"),
        ("KNOWLEDGE_DEMON_BOSS", "知识恶魔Boss"),
        ("LAGAVULIN_MATRIARCH_BOSS", "拉格维林母体Boss"),
    };

    /// <summary>从遭遇 ID 推断类型</summary>
    public static EncounterType GetEncounterType(string id)
    {
        if (string.IsNullOrEmpty(id)) return EncounterType.Normal;
        var u = id.ToUpperInvariant();
        if (u.Contains("_BOSS")) return EncounterType.Boss;
        if (u.Contains("_ELITE")) return EncounterType.Elite;
        return EncounterType.Normal;
    }

    /// <summary>遗物数据 (id, zh, rarity)。rarity: Common/Uncommon/Rare/Ancient/Starter/Event/Shop</summary>
    public static readonly (string id, string zh, string rarity)[] RelicData =
    {
        ("VAJRA", "金刚杵", "Common"), ("BAG_OF_MARBLES", "弹珠袋", "Common"), ("WARPED_TONGS", "扭曲钳", "Common"),
        ("ANCHOR", "锚", "Common"), ("PRESERVED_INSECT", "保存的昆虫", "Common"), ("BRONZE_SCALES", "青铜鳞片", "Common"),
        ("SELF_FORMING_CLAY", "自塑粘土", "Uncommon"), ("ORICHALCUM", "山铜", "Uncommon"), ("PAPER_PHROG", "纸蛙", "Uncommon"),
        ("TOXIC_EGG", "毒蛋", "Rare"), ("INCENSE_BURNER", "香炉", "Rare"), ("WHITE_BEAST_STATUE", "白兽雕像", "Rare"),
        ("THE_BOOT", "靴子", "Event"), ("GOLDEN_IDOL", "黄金偶像", "Rare"), ("NUNCHAKU", "双节棍", "Uncommon"),
        ("KUNAI", "苦无", "Uncommon"), ("SHURIKEN", "手里剑", "Uncommon"), ("ORNAMENTAL_FAN", "装饰扇", "Uncommon"),
        ("STRIKE_DUMMY", "打击假人", "Common"), ("RED_MASK", "红面具", "Common"), ("TWISTED_FUNNEL", "扭曲漏斗", "Uncommon"),
        ("MEMENTO", "纪念品", "Starter"), ("CRACKED_CORE", "裂隙核心", "Starter"), ("RING_OF_THE_SERPENT", "蛇戒", "Starter"),
    };

    /// <summary>常用能力/Buff (id, zh)</summary>
    public static readonly (string id, string zh)[] PowerData =
    {
        ("STRENGTH_POWER", "力量"), ("DEXTERITY_POWER", "敏捷"), ("VULNERABLE_POWER", "易伤"), ("WEAK_POWER", "虚弱"),
        ("PLATED_ARMOR_POWER", "覆甲"), ("BLUR_POWER", "残影"), ("ARTIFACT_POWER", "人工制品"), ("POISON_POWER", "中毒"),
        ("FOCUS_POWER", "集中"), ("REGEN_POWER", "再生"), ("INTANGIBLE_POWER", "无实体"), ("RITUAL_POWER", "仪式"),
        ("NOXIOUS_FUMES_POWER", "毒雾"), ("ENVENOM_POWER", "涂毒"), ("THORNS_POWER", "荆棘"), ("FLAME_BARRIER_POWER", "火焰屏障"),
        ("JUGGERNAUT_POWER", "势不可当"), ("BARRICADE_POWER", "壁垒"), ("DARK_EMBRACE_POWER", "黑暗之拥"), ("CORRUPTION_POWER", "腐化"),
        ("MACHINE_LEARNING_POWER", "机器学习"), ("LOOP_POWER", "循环"), ("ECHO_FORM_POWER", "回响形态"), ("CREATIVE_AI_POWER", "创造性AI"),
    };

    /// <summary>事件数据 (id, zh)，ID 需与 events.json 及游戏 EventModel 一致</summary>
    public static readonly (string id, string zh)[] EventData =
    {
        ("ABYSSAL_BATHS", "深渊浴场"), ("AMALGAMATOR", "熔合者"), ("AROMA_OF_CHAOS", "混沌芳香"),
        ("BATTLEWORN_DUMMY", "战痕累累的训练假人"), ("BRAIN_LEECH", "脑蛭"), ("BUGSLAYER", "害虫杀手"),
        ("BYRDONIS_NEST", "多尼斯异鸟巢"), ("COLORFUL_PHILOSOPHERS", "色彩哲学家"),
        ("RELIC_TRADER", "遗物商人"), ("POTION_COURIER", "药水信使"), ("PUNCH_OFF", "拳击赛"),
        ("THE_ARCHITECT", "建筑师"), ("MORPHIC_GROVE", "变形树丛"), ("WELCOME_TO_WONGOS", "欢迎来到翁戈斯"),
        ("NEOW", "尼奥"), ("SUNKEN_TREASURY", "沉没宝库"), ("RANWID_THE_ELDER", "长者兰维德"),
    };
}
