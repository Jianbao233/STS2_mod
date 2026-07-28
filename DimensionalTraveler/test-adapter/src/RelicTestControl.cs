using System.Text.Json.Nodes;
using DimensionalTraveler.Content.Pools;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace DimensionalTraveler.TestAdapter;

internal static class RelicTestControl
{
    public static async Task<JsonNode> Grant(Player player, JsonObject args)
    {
        var relicId = args["relic_id"]?.GetValue<string>()?.Trim();
        if (string.IsNullOrWhiteSpace(relicId))
            return TestToolResult.Fail("grant_relic 需要 relic_id。", "missing_relic_id");

        var candidate = ModelDb.AllRelics.FirstOrDefault(relic =>
            string.Equals(relic.Id.Entry, relicId, StringComparison.OrdinalIgnoreCase));
        if (candidate is null || candidate.GetType().Assembly != typeof(TravelerRelicPool).Assembly)
            return TestToolResult.Fail($"找不到次元旅人遗物 {relicId}。", "relic_not_found");

        if (!candidate.IsStackable && player.Relics.Any(relic => relic.Id == candidate.Id))
            return TestToolResult.Fail($"NetId={player.NetId} 已拥有不可叠加遗物 {candidate.Id.Entry}。", "relic_already_owned");

        var obtained = await RelicCmd.Obtain(candidate.ToMutable(), player);
        return TestToolResult.Ok(new JsonObject
        {
            ["playerNetId"] = player.NetId.ToString(),
            ["relicId"] = obtained.Id.Entry,
            ["relicType"] = obtained.GetType().Name,
            ["rarity"] = obtained.Rarity.ToString(),
        });
    }
}