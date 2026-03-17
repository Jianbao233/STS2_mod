using System.Collections.Generic;

namespace ControlPanel;

/// <summary>
/// 卡牌角色归属：用于按角色筛选。来自 CardPool 文件 + ID 后缀推断。
/// </summary>
public static class CardCharacterHelper
{
    private static readonly Dictionary<string, string> Map = new()
    {
        {"BASH","IRONCLAD"},{"BODY_SLAM","IRONCLAD"},{"IRON_WAVE","IRONCLAD"},{"POMMEL_STRIKE","IRONCLAD"},
        {"SUNDER","IRONCLAD"},{"WHIRLWIND","IRONCLAD"},{"UPPERCUT","IRONCLAD"},{"BLUDGEON","IRONCLAD"},
        {"TWIN_STRIKE","IRONCLAD"},{"BATTLE_TRANCE","IRONCLAD"},{"SHRUG_IT_OFF","IRONCLAD"},{"FLAME_BARRIER","IRONCLAD"},
        {"TRUE_GRIT","IRONCLAD"},{"ENTRENCH","IRONCLAD"},{"BARRICADE","IRONCLAD"},{"OFFERING","IRONCLAD"},
        {"REAPER_FORM","IRONCLAD"},{"DEMON_FORM","IRONCLAD"},{"CORRUPTION","IRONCLAD"},{"RAMPAGE","IRONCLAD"},
        {"NEUTRALIZE","SILENT"},{"DAGGER_THROW","SILENT"},{"DAGGER_SPRAY","SILENT"},{"BACKSTAB","SILENT"},
        {"POISONED_STAB","SILENT"},{"DEADLY_POISON","SILENT"},{"NOXIOUS_FUMES","SILENT"},{"FOOTWORK","SILENT"},
        {"BACKFLIP","SILENT"},{"LEG_SWEEP","SILENT"},{"DODGE_AND_ROLL","SILENT"},{"BLUR","SILENT"},{"CALTROPS","SILENT"},
        {"PIERCING_WAIL","SILENT"},{"DASH","SILENT"},{"SLICE","SILENT"},{"PRECISE_CUT","SILENT"},{"INFINITE_BLADES","SILENT"},{"BLADE_DANCE","SILENT"},
        {"ZAP","DEFECT"},{"DUALCAST","DEFECT"},{"BALL_LIGHTNING","DEFECT"},{"COLD_SNAP","DEFECT"},{"BEAM_CELL","DEFECT"},
        {"DEFRAGMENT","DEFECT"},{"COOLHEADED","DEFECT"},{"GLACIER","DEFECT"},{"LOOP","DEFECT"},{"BOOT_SEQUENCE","DEFECT"},
        {"TURBO","DEFECT"},{"CHARGE_BATTERY","DEFECT"},{"HELLO_WORLD","DEFECT"},{"CLAW","DEFECT"},{"COMPILE_DRIVER","DEFECT"},
        {"FUSION","DEFECT"},{"CONVERGENCE","DEFECT"},{"HYPERBEAM","DEFECT"},
        {"REANIMATE","NECROBINDER"},{"REAP","NECROBINDER"},{"SACRIFICE","NECROBINDER"},{"BLOODLETTING","NECROBINDER"},{"HEMOKINESIS","NECROBINDER"},
        {"FLASH_OF_STEEL","COLORLESS"},{"MASTER_OF_STRATEGY","COLORLESS"},{"APOTHEOSIS","COLORLESS"},{"DISCOVERY","COLORLESS"},
        {"DOUBT","COLORLESS"},{"VOID","COLORLESS"},{"PURITY","COLORLESS"},{"NORMALITY","COLORLESS"},
    };

    /// <summary>获取卡牌所属角色：IRONCLAD/SILENT/DEFECT/NECROBINDER/REGENT/COLORLESS</summary>
    public static string GetCharacter(string cardId)
    {
        if (string.IsNullOrEmpty(cardId)) return "COLORLESS";
        var u = cardId.ToUpperInvariant();
        if (u.EndsWith("_IRONCLAD")) return "IRONCLAD";
        if (u.EndsWith("_SILENT")) return "SILENT";
        if (u.EndsWith("_DEFECT")) return "DEFECT";
        if (u.EndsWith("_NECROBINDER")) return "NECROBINDER";
        if (u.EndsWith("_REGENT")) return "REGENT";
        return Map.TryGetValue(u, out var c) ? c : "COLORLESS";
    }

    /// <summary>角色筛选索引：0=全部,1=铁甲,2=寂静,3=故障,4=亡灵,5=储君</summary>
    public static bool MatchesCharacter(string cardId, int charIdx)
    {
        if (charIdx <= 0) return true;
        var c = GetCharacter(cardId);
        return charIdx switch { 1 => c == "IRONCLAD", 2 => c == "SILENT", 3 => c == "DEFECT", 4 => c == "NECROBINDER", 5 => c == "REGENT", _ => true };
    }
}
