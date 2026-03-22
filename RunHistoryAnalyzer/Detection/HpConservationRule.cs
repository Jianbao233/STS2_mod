using System;
using System.Collections.Generic;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P0 - 生命守恒】
/// MaxHP：全局 初始 + Σ获得 − Σ失去 = 最终 MaxHP（±1）。
/// CurrentHP：按地图时间顺序逐节点递推并与快照比对（±2）。
/// 第 1 个地图节点不校验当前 HP：进阶开局最大血不满、涅奥/先古同节点先伤后疗或先疗后伤的记录顺序与推测不一致时会误报。
/// 第 2 个节点起尝试多种「伤/疗/上限提升」组合以兼容遗物扣血删牌等与治疗同节点的情况。
/// 瓶中精灵：自动在致死时触发治疗（约 30% MaxHP），但亡灵契约师的「灾厄」在回合末仍可按 DoomPower 逻辑处决；
/// 处决不经由通常受伤统计，故仅按伤/疗字段无法对齐最终 HP=0，需在存在瓶中精灵使用记录时放行。
/// </summary>
public class HpConservationRule : Models.IAnomalyRule
{
    public string Name => "HpConservation";
    public string DisplayName => "HP守恒定律";

    private const int MaxHpTolerance = 1;
    private const int CurrentHpTolerance = 2;

    public Models.Anomaly? Check(Models.RunHistoryData history)
    {
        foreach (var player in history.Players)
        {
            if (history.AnalysisPlayerId != 0 && player.Id != history.AnalysisPlayerId) continue;

            string character = player.Character;
            int initialMaxHp = Models.RunHistoryPlayerData.GetStartingMaxHp(character);

            var timeline = new List<Models.PlayerMapPointHistoryEntry>();
            foreach (var act in history.MapPointHistory)
            foreach (var node in act)
            foreach (var stat in node.PlayerStats)
            {
                if (stat.PlayerId == player.Id)
                    timeline.Add(stat);
            }

            if (timeline.Count == 0)
                continue;

            int totalMaxHpGained = 0;
            int totalMaxHpLost = 0;
            foreach (var s in timeline)
            {
                totalMaxHpGained += s.MaxHpGained;
                totalMaxHpLost += s.MaxHpLost;
            }

            var last = timeline[^1];
            // 极端快照（如 0/0），多为灾厄处决后或损坏存档；用户明确可忽略此类误报时不阻塞整局检视
            if (last.MaxHp <= 0 && last.CurrentHp <= 0)
                return null;

            int expectedMaxHp = initialMaxHp + totalMaxHpGained - totalMaxHpLost;
            int maxHpDeviation = Math.Abs(expectedMaxHp - last.MaxHp);
            if (maxHpDeviation > MaxHpTolerance)
            {
                return new Models.Anomaly(
                    Models.AnomalyLevel.High,
                    Name,
                    "MaxHP不守恒",
                    $"预期最大HP：{expectedMaxHp}，实际最大HP：{last.MaxHp}，偏差：{maxHpDeviation}",
                    $"初始={initialMaxHp}  + 获得={totalMaxHpGained}  - 失去={totalMaxHpLost}",
                    "可能原因：内存修改 / 存档直接编辑"
                );
            }

            // 逐节点递推（含 CurrentHp=0 的死亡节点，不再跳过）
            int prevMax = initialMaxHp;
            int prevCur = initialMaxHp;

            for (var i = 0; i < timeline.Count; i++)
            {
                var stat = timeline[i];
                int newMax = prevMax + stat.MaxHpGained - stat.MaxHpLost;

                int maxDev = Math.Abs(stat.MaxHp - newMax);
                bool curOk = i == 0 || CurrentHpMatches(prevCur, prevMax, newMax, stat.DamageTaken, stat.HpHealed, stat.CurrentHp, CurrentHpTolerance)
                    || ShouldSkipHpMismatchForFairyAndDoom(stat)
                    || ShouldSkipHpMismatchForNecromancerCalamity(character, prevCur, prevMax, stat);

                if (maxDev > MaxHpTolerance)
                {
                    return new Models.Anomaly(
                        Models.AnomalyLevel.High,
                        Name,
                        "MaxHP节点不一致",
                        $"第 {i + 1} 个地图节点：预期 MaxHP={newMax}，记录 MaxHP={stat.MaxHp}，偏差={maxDev}",
                        $"上一节点 Max={prevMax}，本节点 +获得{stat.MaxHpGained} -失去{stat.MaxHpLost}",
                        "可能原因：存档与地图流水不一致 / 联机同步异常"
                    );
                }

                if (!curOk)
                {
                    return new Models.Anomaly(
                        Models.AnomalyLevel.High,
                        Name,
                        "HP不守恒",
                        $"第 {i + 1} 个地图节点：无法将当前HP 与多种伤/疗顺序下的预期对齐，记录={stat.CurrentHp}",
                        $"上一节点 HP={prevCur}/{prevMax}；本节点 受伤={stat.DamageTaken} 治疗={stat.HpHealed}",
                        "可能原因：治疗/扣血与上限变化顺序与推测不符时为误报；若持续出现再怀疑作弊"
                    );
                }

                prevMax = stat.MaxHp;
                prevCur = stat.CurrentHp;
            }
        }

        return null;
    }

    /// <summary>
    /// 与快照比对：尝试多种「上限提升增量 + 伤/疗」顺序（含忽略部分治疗/伤害的记录噪声）。
    /// </summary>
    private static bool CurrentHpMatches(int prevCur, int prevMax, int newMax, int dmg, int heal, int actualCur, int tol)
    {
        int capRaise = newMax > prevMax ? newMax - prevMax : 0;
        // 与旧版主路径一致：先伤后疗，再加 capRaise，最后夹到 [0,newMax]
        int[] raw =
        {
            prevCur - dmg + heal + capRaise,
            prevCur + heal - dmg + capRaise,
            prevCur - dmg + capRaise,
            prevCur + heal + capRaise,
            prevCur + capRaise - dmg + heal // cap 先于伤疗（少数节点）
        };
        foreach (var r in raw)
        {
            int v = Math.Clamp(r, 0, newMax);
            if (Math.Abs(v - actualCur) <= tol)
                return true;
        }

        // 仅提升 MaxHP、当前生命不加上限差值（如草莓等 +MaxHp 遗物）：伤疗后仍用 prevCur 递推，再夹到 [0,newMax]
        if (capRaise > 0)
        {
            int[] noCapRaise =
            {
                prevCur - dmg + heal,
                prevCur + heal - dmg,
                prevCur - dmg,
                prevCur + heal
            };
            foreach (var r in noCapRaise)
            {
                int v = Math.Clamp(r, 0, newMax);
                if (Math.Abs(v - actualCur) <= tol)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 本节点使用了瓶中精灵且最终 HP 为 0：可能为先被精灵救起再被灾厄（DoomPower）回合末处决，流水无法还原。
    /// 存档 id 多为 <c>POTION.FAIRY_IN_A_BOTTLE</c>；部分资料写作 FAIRY_IN_ABOTTLE，两种均认。
    /// </summary>
    private static bool ShouldSkipHpMismatchForFairyAndDoom(Models.PlayerMapPointHistoryEntry stat)
    {
        if (stat.CurrentHp != 0)
            return false;
        foreach (var id in stat.PotionUsed)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            if (IsFairyPotionHistoryId(id))
                return true;
        }

        return false;
    }

    private static bool IsFairyPotionHistoryId(string id)
    {
        return id.Contains("FAIRY_IN_A_BOTTLE", StringComparison.OrdinalIgnoreCase)
            || id.Contains("FAIRY_IN_ABOTTLE", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 亡灵契约：灾厄（Doom）等回合末处决不经由 <c>damage_taken</c> 记录，会出现「上一节点仍有血、本节点伤疗为 0 却 HP=0」。
    /// </summary>
    private static bool ShouldSkipHpMismatchForNecromancerCalamity(string character, int prevCur, int prevMax, Models.PlayerMapPointHistoryEntry stat)
    {
        if (!IsNecromancerCharacter(character))
            return false;
        if (prevCur <= 0 || stat.CurrentHp != 0)
            return false;
        if (stat.DamageTaken != 0 || stat.HpHealed != 0)
            return false;
        // 本节点最大生命不应变化；若有获得/失去上限则交给常规递推
        if (stat.MaxHpGained != 0 || stat.MaxHpLost != 0 || stat.MaxHp != prevMax)
            return false;

        return true;
    }

    private static bool IsNecromancerCharacter(string characterId) =>
        characterId.Contains("NECROMANCER", StringComparison.OrdinalIgnoreCase)
        || characterId.Contains("NECROBINDER", StringComparison.OrdinalIgnoreCase);
}
