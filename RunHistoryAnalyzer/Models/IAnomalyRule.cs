using System.Collections.Generic;

namespace RunHistoryAnalyzer.Models;

public interface IAnomalyRule
{
    string Name { get; }
    string DisplayName { get; }

    /// <summary>
    /// 检查历史数据，返回所有命中的异常（无异常时返回空列表）。
    /// 每条命中的节点/卡牌/遗物等各生成一条 <see cref="Anomaly"/>。
    /// </summary>
    IReadOnlyList<Anomaly> Check(RunHistoryData history);
}
