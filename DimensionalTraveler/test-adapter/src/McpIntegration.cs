using System.Text.Json.Nodes;
using HarmonyLib;

namespace DimensionalTraveler.TestAdapter;

internal static class McpIntegration
{
    public const string ControlToolName = "dimensional_traveler_test_control";
    public const string TargetToolName = "dimensional_traveler_test_target";
    public const string SelectionToolName = "dimensional_traveler_test_selection";
    public const string SessionToolName = "dimensional_traveler_test_session";

    private const string HarmonyId = "DimensionalTraveler.TestAdapter.McpIntegration";
    private static bool _installed;

    public static void Install()
    {
        if (_installed)
            return;

        var contract = ResolveContract();
        var harmony = new Harmony(HarmonyId);
        harmony.Patch(
            contract.ListToolSchemas,
            postfix: new HarmonyMethod(AccessTools.Method(typeof(McpIntegration), nameof(AfterListTools))));
        harmony.Patch(
            contract.CallAsync,
            prefix: new HarmonyMethod(AccessTools.Method(typeof(McpIntegration), nameof(BeforeCallTool))));
        _installed = true;
    }

    private static KitLibCompatibility.McpRegistryContract ResolveContract() =>
        KitLibCompatibility.RequireMcpRegistry();

    private static void AfterListTools(ref JsonArray __result)
    {
        AddSchema(__result, ControlTool.Schema);
        AddSchema(__result, TargetingControl.Schema);
        AddSchema(__result, CardSelectionControl.Schema);
        AddSchema(__result, TestSessionTool.Schema);
    }

    private static bool BeforeCallTool(string name, JsonObject? args, ref Task<JsonNode> __result)
    {
        if (name == ControlTool.Schema.Name)
        {
            __result = Wrap(ControlTool.Execute(args ?? new JsonObject()));
            return false;
        }
        if (name == TargetingControl.Schema.Name)
        {
            __result = Wrap(Task.FromResult<JsonNode>(TargetingControl.Execute(args ?? new JsonObject())));
            return false;
        }
        if (name == CardSelectionControl.Schema.Name)
        {
            __result = Wrap(Task.FromResult<JsonNode>(CardSelectionControl.Execute(args ?? new JsonObject())));
            return false;
        }
        if (name == TestSessionTool.Schema.Name)
        {
            __result = Wrap(Task.FromResult<JsonNode>(TestSessionTool.Execute(args ?? new JsonObject())));
            return false;
        }
        return true;
    }

    private static async Task<JsonNode> Wrap(Task<JsonNode> operation)
    {
        JsonNode payload;
        try
        {
            payload = await operation;
        }
        catch (Exception exception)
        {
            payload = TestToolResult.Fail(exception.Message, exception.GetType().Name);
        }

        return new JsonObject
        {
            ["content"] = new JsonArray
            {
                new JsonObject
                {
                    ["type"] = "text",
                    ["text"] = payload.ToJsonString(),
                },
            },
        };
    }

    private static void AddSchema(JsonArray schemas, TestToolSchema tool)
    {
        if (schemas.OfType<JsonObject>().Any(schema => schema["name"]?.GetValue<string>() == tool.Name))
            return;

        schemas.Add(new JsonObject
        {
            ["name"] = tool.Name,
            ["description"] = tool.Description,
            ["inputSchema"] = JsonNode.Parse(tool.InputSchema),
        });
    }

}

internal sealed record TestToolSchema(string Name, string Description, string InputSchema);

internal static class TestToolResult
{
    public static JsonObject Ok(JsonObject? fields = null)
    {
        var result = fields ?? new JsonObject();
        result["ok"] = true;
        return result;
    }

    public static JsonObject Fail(string error, string? code = null)
    {
        var result = new JsonObject
        {
            ["ok"] = false,
            ["error"] = error,
        };
        if (!string.IsNullOrWhiteSpace(code))
            result["code"] = code;
        return result;
    }
}