using System;
using System.Collections.Generic;
using System.Linq;

using RunHistoryAnalyzer;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 【P1】单地图节点内 <c>relic_choices</c> 中 <c>was_picked=true</c> 的次数异常（控制台/修改器常重复写入）。
/// 真商店（含问号房内嵌 shop 房间）可多次选购，不计入。
/// </summary>
public sealed class RelicMultiPickRule : Models.IAnomalyRule
{
    public string Name => "RelicMultiPick";
    public string DisplayName => "单节点多遗物选取";

    public IReadOnlyList<Models.Anomaly> Check(Models.RunHistoryData history)
    {
        var result = new List<Models.Anomaly>();
        var floorIndex = 0;
        foreach (var act in history.MapPointHistory)
        {
            foreach (var node in act)
            {
                floorIndex++;
                if (MapNodeShopUtil.IsShopLikeMapNode(node) || MapNodeShopUtil.HasShopTransaction(node))
                    continue;

                foreach (var stat in node.PlayerStats)
                {
                    if (history.AnalysisPlayerId != 0 && stat.PlayerId != history.AnalysisPlayerId)
                        continue;

                    var picked = stat.RelicChoices.Count(c => c.WasPicked);
                    var firstRoom = node.Rooms.FirstOrDefault();
                    var modelId = firstRoom?.ModelId;
                    var roomType = firstRoom?.RoomType;
                    var maxLegit = MaxLegitRelicPicksForNodeType(node.MapPointType, modelId, roomType);
                    if (picked <= maxLegit)
                        continue;

                    result.Add(new Models.Anomaly(
                        Models.AnomalyLevel.High,
                        Name,
                        "单节点遗物选取次数异常",
                        $"第 {floorIndex} 个地图节点：relic_choices 中 was_picked=true 共 {picked} 次，该节点类型（{node.MapPointType}）通常至多 {maxLegit} 次。",
                        $"map_point_type={node.MapPointType}；model_id={modelId ?? "(null)"}；room_type={roomType ?? "(null)"}",
                        "可能原因：控制台添加遗物 / 修改器；正常流程同一节点不应多次确认遗物奖励。"
                    ));
                }
            }
        }

        return result;
    }

    /// <summary>非商店节点下，按地图点类型（+ model_id + room_type）返回合理上限。</summary>
    private static int MaxLegitRelicPicksForNodeType(string mapPointType, string? modelId, string? roomType)
    {
        return AncientRuleLoader.MaxLegitRelicPicks(mapPointType, modelId, roomType);
    }
}
