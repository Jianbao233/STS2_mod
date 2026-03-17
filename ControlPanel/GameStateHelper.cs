using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ControlPanel;

/// <summary>
/// 通过反射获取游戏实时状态：牌堆卡牌、遗物、药水等。
/// 依赖 CombatManager、RunManager、NRun 等游戏类型。
/// </summary>
public static class GameStateHelper
{
    /// <summary>获取当前牌堆中的卡牌 ID 列表。非战斗或出错时返回空。</summary>
    /// <param name="pile">Hand/Draw/Discard/Deck</param>
    public static List<string> GetCardsInPile(string pile)
    {
        var result = new List<string>();
        try
        {
            var combatMgr = GetType("MegaCrit.Sts2.Core.Combat.CombatManager") ?? GetType("CombatManager");
            if (combatMgr == null) return result;
            var inst = combatMgr.GetProperty("Instance")?.GetValue(null);
            if (inst == null) return result;
            var inProgress = combatMgr.GetProperty("IsInProgress")?.GetValue(inst) as bool?;
            if (inProgress != true) return result;

            var state = combatMgr.GetMethod("DebugOnlyGetState")?.Invoke(inst, null)
                ?? combatMgr.GetProperty("State")?.GetValue(inst);
            if (state == null) return result;
            var stateType = state.GetType();

            var getMe = GetType("MegaCrit.Sts2.Core.Context.LocalContext")?.GetMethod("GetMe", new[] { stateType });
            if (getMe == null) return result;
            var player = getMe.Invoke(null, new[] { state });
            if (player == null) return result;

            var pileType = GetType("MegaCrit.Sts2.Core.Entities.Cards.PileType");
            if (pileType == null) return result;
            object pileVal;
            try { pileVal = Enum.Parse(pileType, pile); } catch { return result; }

            var cardPileType = GetType("MegaCrit.Sts2.Core.Entities.Cards.CardPile");
            var getPile = cardPileType?.GetMethod("Get", new[] { pileType, player.GetType() });
            if (getPile == null) return result;
            var cardPile = getPile.Invoke(null, new[] { pileVal, player });
            if (cardPile == null) return result;

            var cardsProp = cardPile.GetType().GetProperty("Cards");
            var cards = cardsProp?.GetValue(cardPile) as System.Collections.IEnumerable;
            if (cards == null) return result;

            var cardModelType = GetType("MegaCrit.Sts2.Core.Models.CardModel");
            foreach (var c in cards)
            {
                if (c == null) continue;
                var idProp = c.GetType().GetProperty("Id") ?? cardModelType?.GetProperty("Id");
                if (idProp == null) continue;
                var idObj = idProp.GetValue(c);
                var entryProp = idObj?.GetType().GetProperty("Entry");
                var id = entryProp?.GetValue(idObj) as string ?? idObj?.ToString();
                if (!string.IsNullOrEmpty(id)) result.Add(id);
            }
        }
        catch { }
        return result;
    }

    /// <summary>获取当前拥有的遗物 ID 列表。非跑图或出错时返回空。</summary>
    public static List<string> GetOwnedRelics()
    {
        var result = new List<string>();
        try
        {
            object run = null;
            var combatMgr = GetType("MegaCrit.Sts2.Core.Combat.CombatManager") ?? GetType("CombatManager");
            var combatInst = combatMgr?.GetProperty("Instance")?.GetValue(null);
            if (combatInst != null && (combatMgr?.GetProperty("IsInProgress")?.GetValue(combatInst) as bool? == true))
            {
                var state = combatMgr.GetMethod("DebugOnlyGetState")?.Invoke(combatInst, null);
                run = state?.GetType().GetProperty("RunState")?.GetValue(state);
            }
            if (run == null)
            {
                var nRun = GetType("MegaCrit.Sts2.Core.Nodes.NRun") ?? GetType("NRun");
                var nRunInst = nRun?.GetProperty("Instance")?.GetValue(null);
                if (nRunInst != null)
                    run = nRun.GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(nRunInst);
            }
            if (run == null) return result;

            var playersProp = run.GetType().GetProperty("Players");
            var players = playersProp?.GetValue(run) as System.Collections.IEnumerable;
            if (players == null) return result;

            foreach (var p in players)
            {
                if (p == null) continue;
                var relicsProp = p.GetType().GetProperty("Relics");
                var relics = relicsProp?.GetValue(p) as System.Collections.IEnumerable;
                if (relics == null) continue;
                foreach (var r in relics)
                {
                    if (r == null) continue;
                    var idProp = r.GetType().GetProperty("Id");
                    if (idProp == null) continue;
                    var idObj = idProp.GetValue(r);
                    var entryProp = idObj?.GetType().GetProperty("Entry");
                    var id = entryProp?.GetValue(idObj) as string ?? idObj?.ToString();
                    if (!string.IsNullOrEmpty(id)) result.Add(id);
                }
                break;
            }
        }
        catch { }
        return result;
    }

    /// <summary>获取当前药水腰带中的药水 ID 列表。</summary>
    public static List<string> GetCurrentPotions()
    {
        var result = new List<string>();
        try
        {
            object run = null;
            var combatMgr = GetType("MegaCrit.Sts2.Core.Combat.CombatManager") ?? GetType("CombatManager");
            var combatInst = combatMgr?.GetProperty("Instance")?.GetValue(null);
            if (combatInst != null && (combatMgr?.GetProperty("IsInProgress")?.GetValue(combatInst) as bool? == true))
            {
                var state = combatMgr.GetMethod("DebugOnlyGetState")?.Invoke(combatInst, null);
                run = state?.GetType().GetProperty("RunState")?.GetValue(state);
            }
            if (run == null)
            {
                var nRun = GetType("MegaCrit.Sts2.Core.Nodes.NRun") ?? GetType("NRun");
                var nRunInst = nRun?.GetProperty("Instance")?.GetValue(null);
                if (nRunInst != null)
                    run = nRun.GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(nRunInst);
            }
            if (run == null) return result;

            var playersProp = run.GetType().GetProperty("Players");
            var players = playersProp?.GetValue(run) as System.Collections.IEnumerable;
            if (players == null) return result;

            foreach (var p in players)
            {
                if (p == null) continue;
                var potionsProp = p.GetType().GetProperty("Potions");
                var potions = potionsProp?.GetValue(p) as System.Collections.IEnumerable;
                if (potions == null) continue;
                foreach (var model in potions)
                {
                    if (model == null) continue;
                    var idProp = model.GetType().GetProperty("Id");
                    if (idProp == null) continue;
                    var idObj = idProp.GetValue(model);
                    var entryProp = idObj?.GetType().GetProperty("Entry");
                    var id = entryProp?.GetValue(idObj) as string ?? idObj?.ToString();
                    if (!string.IsNullOrEmpty(id)) result.Add(id);
                }
                break;
            }
        }
        catch { }
        return result;
    }

    /// <summary>是否处于战斗中</summary>
    public static bool IsInCombat()
    {
        try
        {
            var t = GetType("MegaCrit.Sts2.Core.Combat.CombatManager") ?? GetType("CombatManager");
            var inst = t?.GetProperty("Instance")?.GetValue(null);
            return inst != null && (t?.GetProperty("IsInProgress")?.GetValue(inst) as bool? == true);
        }
        catch { }
        return false;
    }

    private static readonly Dictionary<string, string> _relicRarityCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>从游戏获取遗物稀有度，失败返回 Common。结果已缓存。</summary>
    public static string GetRelicRarityFromGame(string relicId)
    {
        if (string.IsNullOrEmpty(relicId)) return "Common";
        if (_relicRarityCache.TryGetValue(relicId, out var cached)) return cached;
        try
        {
            var modelDb = GetType("MegaCrit.Sts2.Core.Models.ModelDb");
            if (modelDb == null) return "Common";
            var getById = modelDb.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "GetById" && m.IsGenericMethodDefinition && m.GetParameters().Length == 1);
            if (getById == null) return "Common";
            var relicType = GetType("MegaCrit.Sts2.Core.Models.RelicModel");
            if (relicType == null) return "Common";
            var modelIdType = GetType("MegaCrit.Sts2.Core.Models.ModelId");
            if (modelIdType == null) return "Common";
            var ctor = modelIdType.GetConstructor(new[] { typeof(string), typeof(string) });
            var modelId = ctor?.Invoke(new object[] { "relic", relicId.ToLowerInvariant() });
            if (modelId == null) return "Common";
            var relic = getById.MakeGenericMethod(relicType).Invoke(null, new[] { modelId });
            if (relic == null) return "Common";
            var rarityProp = relic.GetType().GetProperty("Rarity");
            var rarity = rarityProp?.GetValue(relic);
            var r = rarity?.ToString() ?? "Common";
            _relicRarityCache[relicId] = r;
            return r;
        }
        catch { }
        _relicRarityCache[relicId] = "Common";
        return "Common";
    }

    /// <summary>是否在跑图中（有 run）</summary>
    public static bool HasRun()
    {
        try
        {
            var t = GetType("MegaCrit.Sts2.Core.RunManager") ?? GetType("RunManager");
            var inst = t?.GetProperty("Instance")?.GetValue(null);
            var run = inst != null ? t?.GetProperty("CurrentRun")?.GetValue(inst) : null;
            return run != null;
        }
        catch { }
        return false;
    }

    private static Type GetType(string name)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }
}
