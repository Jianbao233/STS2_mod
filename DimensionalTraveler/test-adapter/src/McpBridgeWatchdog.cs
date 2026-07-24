using System.Reflection;
using Godot;
using HarmonyLib;
using KitLib.Host;

namespace DimensionalTraveler.TestAdapter;

internal static class McpBridgeWatchdog
{
    private static int _installed;

    public static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0)
            return;

        Callable.From(EnsureBridgeStarted).CallDeferred();
    }

    private static void EnsureBridgeStarted()
    {
        try
        {
            KitLibHost.TryRunDevBootstrap();
            var contract = ResolveContract();
            contract.StartCore.Invoke(null, null);
            if (!ReadIsRunning(contract))
            {
                Bootstrap.Entry.Logger.Error(
                    "[DimensionalTraveler.TestAdapter] KitLib Dev bootstrap completed, but the MCP HTTP bridge did not start.");
                return;
            }

            Bootstrap.Entry.Logger.Info(
                "[DimensionalTraveler.TestAdapter] KitLib MCP bridge is ready for acceptance tests.");
        }
        catch (Exception exception)
        {
            Bootstrap.Entry.Logger.Error(
                $"[DimensionalTraveler.TestAdapter] KitLib MCP bridge bootstrap failed: {exception.GetBaseException().Message}");
        }
    }

    private static McpBridgeContract ResolveContract()
    {
        var bridgeType = AccessTools.TypeByName("KitLib.Mcp.McpBridge")
            ?? throw new TypeLoadException("KitLib.Dev is loaded without KitLib.Mcp.McpBridge.");
        var isRunning = bridgeType.GetProperty(
            "IsRunning",
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var startCore = AccessTools.Method(bridgeType, "StartCore", Type.EmptyTypes);

        if (isRunning?.PropertyType != typeof(bool) || isRunning.GetMethod is null)
            throw new MissingMemberException(bridgeType.FullName, "IsRunning");
        if (startCore?.ReturnType != typeof(void))
            throw new MissingMethodException(bridgeType.FullName, "StartCore()");

        return new McpBridgeContract(isRunning, startCore);
    }

    private static bool ReadIsRunning(McpBridgeContract contract) =>
        contract.IsRunning.GetValue(null) as bool? == true;

    private sealed record McpBridgeContract(PropertyInfo IsRunning, MethodInfo StartCore);
}