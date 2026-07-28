using System.Reflection;
using System.Text.Json.Nodes;
using HarmonyLib;

namespace DimensionalTraveler.TestAdapter;

internal static class KitLibCompatibility
{
    public static McpRegistryContract RequireMcpRegistry()
    {
        var registryType = AccessTools.TypeByName("KitLib.Mcp.Tools.McpToolRegistry")
            ?? throw Incompatible("未加载 KitLib.Dev，找不到 KitLib.Mcp.Tools.McpToolRegistry");
        var listToolSchemas = AccessTools.Method(registryType, "ListToolSchemas", Type.EmptyTypes);
        var callAsync = AccessTools.Method(
            registryType,
            "CallAsync",
            [typeof(string), typeof(JsonObject)]);
        if (listToolSchemas?.ReturnType != typeof(JsonArray))
            throw Incompatible("ListToolSchemas() -> JsonArray 签名不存在");
        if (callAsync?.ReturnType != typeof(Task<JsonNode>))
            throw Incompatible("CallAsync(string, JsonObject) -> Task<JsonNode> 签名不存在");

        return new McpRegistryContract(listToolSchemas, callAsync);
    }

    public static McpBridgeContract RequireMcpBridge()
    {
        var bridgeType = AccessTools.TypeByName("KitLib.Mcp.McpBridge")
            ?? throw Incompatible("未加载 KitLib.Dev，找不到 KitLib.Mcp.McpBridge");
        var startCore = AccessTools.Method(bridgeType, "StartCore", Type.EmptyTypes);
        var isRunning = AccessTools.PropertyGetter(bridgeType, "IsRunning");
        if (startCore?.ReturnType != typeof(void))
            throw Incompatible("McpBridge.StartCore() -> void 签名不存在");
        if (isRunning?.ReturnType != typeof(bool))
            throw Incompatible("McpBridge.IsRunning: bool 签名不存在");

        return new McpBridgeContract(startCore, isRunning);
    }

    public static string GetKitLibVersion() =>
        AccessTools.TypeByName("KitLib.Host.KitLibHost")?.Assembly.GetName().Version?.ToString()
        ?? "unknown";

    private static NotSupportedException Incompatible(string detail) =>
        new($"DimensionalTraveler.TestAdapter 与当前 KitLib MCP 接口不兼容：{detail}；KitLib.Core={GetKitLibVersion()}。");

    internal sealed record McpRegistryContract(MethodInfo ListToolSchemas, MethodInfo CallAsync);

    internal sealed record McpBridgeContract(MethodInfo StartCore, MethodInfo IsRunning);
}