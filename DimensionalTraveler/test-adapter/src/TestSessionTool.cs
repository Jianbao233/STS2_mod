using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using HarmonyLib;

namespace DimensionalTraveler.TestAdapter;

internal static class TestSessionTool
{
    public static TestToolSchema Schema { get; } = new(
        McpIntegration.SessionToolName,
        "Return the identity and compatibility facts for the active DimensionalTraveler acceptance session.",
        """
        {
          "type": "object",
          "properties": {
            "action": { "type": "string", "enum": ["handshake"] }
          },
          "required": ["action"]
        }
        """);

    public static JsonNode Execute(JsonObject args)
    {
        var action = args["action"]?.GetValue<string>()?.Trim().ToLowerInvariant();
        return action == "handshake"
            ? Handshake()
            : TestToolResult.Fail($"未知 session action：{action ?? "<null>"}。", "invalid_action");
    }

    private static JsonNode Handshake()
    {
        var gameAssembly = AccessTools.TypeByName("KitLib.Host.KitLibHost")?.Assembly;
        var travelerAssembly = typeof(Bootstrap.Entry).Assembly;
        var adapterAssembly = Assembly.GetExecutingAssembly();
        var tools = new JsonArray
        {
            McpIntegration.ControlToolName,
            McpIntegration.TargetToolName,
            McpIntegration.SelectionToolName,
            McpIntegration.SessionToolName,
        };

        return TestToolResult.Ok(new JsonObject
        {
            ["runId"] = Environment.GetEnvironmentVariable("DT_ACCEPTANCE_RUN_ID") ?? string.Empty,
            ["processId"] = Process.GetCurrentProcess().Id,
            ["mcpPort"] = ParsePort(Environment.GetEnvironmentVariable("KITLIB_MCP_PORT")),
            ["kitLibVersion"] = gameAssembly?.GetName().Version?.ToString() ?? "unknown",
            ["travelerVersion"] = travelerAssembly.GetName().Version?.ToString() ?? "unknown",
            ["adapterVersion"] = adapterAssembly.GetName().Version?.ToString() ?? "unknown",
            ["travelerAssembly"] = travelerAssembly.Location,
            ["adapterAssembly"] = adapterAssembly.Location,
            ["tools"] = tools,
        });
    }

    private static int ParsePort(string? configured) =>
        int.TryParse(configured, out var port) && port is > 0 and <= ushort.MaxValue
            ? port
            : 9877;
}