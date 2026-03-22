namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P0 - HP边界异常】
/// 当前HP不得超过MaxHp（数学上不可能）。
/// </summary>
public class HpBoundaryRule : Models.IAnomalyRule
{
    public string Name => "HpBoundary";
    public string DisplayName => "HP边界异常";

    public Models.Anomaly? Check(Models.RunHistoryData history)
    {
        foreach (var act in history.MapPointHistory)
        foreach (var node in act)
        foreach (var stat in node.PlayerStats)
        {
            // 如果指定了分析目标玩家，跳过其他玩家的节点
            if (history.AnalysisPlayerId != 0 && stat.PlayerId != history.AnalysisPlayerId) continue;

            if (stat.CurrentHp <= 0 || stat.MaxHp <= 0) continue;

            if (stat.CurrentHp > stat.MaxHp)
            {
                return new Models.Anomaly(
                    Models.AnomalyLevel.Low,
                    Name,
                    "HP超出最大上限",
                    $"最大HP：{stat.MaxHp}，当前HP：{stat.CurrentHp}，超出：{stat.CurrentHp - stat.MaxHp}",
                    $"在任何节点，当前HP都不应超过MaxHP",
                    "可能原因：内存修改 / heal 控制台作弊"
                );
            }
        }

        return null;
    }
}
