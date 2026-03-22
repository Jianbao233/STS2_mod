using System;
using RunHistoryAnalyzer.Models;

namespace RunHistoryAnalyzer.Detection;

/// <summary>
/// 判断地图节点是否处于「商店」语义：<c>map_point_type=shop</c>，
/// 或问号房等内嵌 <c>room_type=shop</c>（真商人），
/// 或玩家统计中存在商店购买行为（bought_relics / bought_colorless）即便 map_point_type 标记为 unknown/event。
/// </summary>
public static class MapNodeShopUtil
{
    public static bool IsShopLikeMapNode(MapPointHistoryEntry node)
    {
        if (node.MapPointType.Equals("shop", StringComparison.OrdinalIgnoreCase))
            return true;
        foreach (var r in node.Rooms)
        {
            if (r.RoomType != null && r.RoomType.Equals("shop", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>节点中任意玩家存在商店购买行为（遗物/无色卡）时，也视为商店节点。</summary>
    public static bool HasShopTransaction(MapPointHistoryEntry node)
    {
        foreach (var stat in node.PlayerStats)
        {
            if (stat.BoughtRelics.Count > 0 || stat.BoughtColorless.Count > 0)
                return true;
        }

        return false;
    }
}
