using System.Reflection;
using System.Text.Json.Nodes;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace DimensionalTraveler.TestAdapter;

internal static class TargetingControl
{
    public static TestToolSchema Schema { get; } = new(
        McpIntegration.TargetToolName,
        "Inspect or complete an active NTargetManager selection.",
        """
        {
          "type": "object",
          "properties": {
            "action": { "type": "string", "enum": ["get", "select"] },
            "candidate_index": { "type": "integer" },
            "combat_id": { "type": "integer" }
          },
          "required": ["action"]
        }
        """);

    private static readonly MethodInfo FinishTargetingMethod =
        AccessTools.Method(typeof(NTargetManager), "FinishTargeting", [typeof(bool)])
        ?? throw new MissingMethodException(typeof(NTargetManager).FullName, "FinishTargeting");

    public static JsonObject Execute(JsonObject args)
    {
        var action = args["action"]?.GetValue<string>()?.Trim().ToLowerInvariant();
        if (action == "get")
            return TestToolResult.Ok(new JsonObject { ["targeting"] = Capture() });
        if (action != "select")
            return TestToolResult.Fail($"未知 action：{action ?? "<null>"}。", "invalid_action");

        var manager = TryGetManager();
        if (manager is null || !manager.IsInSelection)
            return TestToolResult.Fail("当前没有活动的 NTargetManager 选择。", "targeting_inactive");

        var candidates = GetCandidates(manager);
        NCreature? selected;
        if (args["candidate_index"] is JsonNode indexNode)
        {
            var index = indexNode.GetValue<int>();
            if (index < 0 || index >= candidates.Count)
                return TestToolResult.Fail(
                    $"candidate_index {index} 超出范围，候选数为 {candidates.Count}。",
                    "candidate_out_of_range");
            selected = candidates[index];
        }
        else if (args["combat_id"] is JsonNode combatIdNode)
        {
            var combatId = combatIdNode.GetValue<uint>();
            selected = candidates.FirstOrDefault(node => node.Entity.CombatId == combatId);
            if (selected is null)
                return TestToolResult.Fail($"候选中不存在 combat_id {combatId}。", "candidate_not_found");
        }
        else
        {
            return TestToolResult.Fail("select 需要 candidate_index 或 combat_id。", "missing_candidate");
        }

        manager.OnNodeHovered(selected);
        FinishTargetingMethod.Invoke(manager, [false]);
        return TestToolResult.Ok(new JsonObject { ["combatId"] = selected.Entity.CombatId });
    }

    public static JsonObject Capture()
    {
        var manager = TryGetManager();
        if (manager is null || !manager.IsInSelection)
            return new JsonObject { ["active"] = false, ["candidates"] = new JsonArray() };

        var candidates = GetCandidates(manager);
        var serialized = new JsonArray();
        for (var index = 0; index < candidates.Count; index++)
        {
            var creature = candidates[index].Entity;
            serialized.Add(new JsonObject
            {
                ["index"] = index,
                ["combatId"] = creature.CombatId,
                ["side"] = creature.Side.ToString(),
                ["currentHp"] = creature.CurrentHp,
                ["maxHp"] = creature.MaxHp,
                ["isPlayer"] = creature.IsPlayer,
                ["modelId"] = TryGetModelId(creature),
            });
        }

        return new JsonObject
        {
            ["active"] = true,
            ["candidates"] = serialized,
        };
    }

    private static NTargetManager? TryGetManager()
    {
        try
        {
            return NTargetManager.Instance;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<NCreature> GetCandidates(NTargetManager manager) =>
        (NCombatRoom.Instance?.CreatureNodes ?? [])
            .Where(node => node.Entity.CombatId.HasValue && manager.AllowedToTargetNode(node))
            .OrderBy(node => node.Entity.CombatId)
            .ToArray();

    private static string? TryGetModelId(Creature creature)
    {
        try
        {
            return creature.ModelId.Entry;
        }
        catch
        {
            return null;
        }
    }
}