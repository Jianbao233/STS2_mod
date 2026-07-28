using Godot;
using KitLib.Host;

namespace DimensionalTraveler.TestAdapter;

internal static class McpBridgeWatchdog
{
    private const int MaxBootstrapAttempts = 120;

    private static readonly int BridgePort = ResolveBridgePort();
    private static int _installed;
    private static int _bootstrapAttempts;

    private static int ResolveBridgePort() =>
        int.TryParse(System.Environment.GetEnvironmentVariable("KITLIB_MCP_PORT"), out var port)
        && port is > 0 and <= ushort.MaxValue
            ? port
            : 9877;

    public static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0)
            return;

        var configuredPort = System.Environment.GetEnvironmentVariable("KITLIB_MCP_PORT") ?? "<unset>";
        Bootstrap.Entry.Logger.Info(
            $"[DimensionalTraveler.TestAdapter] MCP bootstrap scheduled: KITLIB_MCP_PORT={configuredPort}, port={BridgePort}.");
        Callable.From(RequestBridgeBootstrap).CallDeferred();
    }

    private static void RequestBridgeBootstrap()
    {
        try
        {
            var contract = KitLibCompatibility.RequireMcpBridge();
            contract.StartCore.Invoke(null, null);
            if (contract.IsRunning.Invoke(null, null) is true)
            {
                Bootstrap.Entry.Logger.Info(
                    $"[DimensionalTraveler.TestAdapter] KitLib MCP bridge is listening on port {BridgePort}.");
                return;
            }

            _bootstrapAttempts += 1;
            if (_bootstrapAttempts >= MaxBootstrapAttempts)
            {
                Bootstrap.Entry.Logger.Error(
                    $"[DimensionalTraveler.TestAdapter] KitLib MCP bridge did not enter listening state after {MaxBootstrapAttempts} main-thread attempts on port {BridgePort}.");
                return;
            }

            ScheduleRetry();
        }
        catch (Exception exception)
        {
            Bootstrap.Entry.Logger.Error(
                $"[DimensionalTraveler.TestAdapter] KitLib MCP bridge bootstrap failed: {exception.GetBaseException().Message}");
        }
    }

    private static void ScheduleRetry()
    {
        if (Engine.GetMainLoop() is not SceneTree tree)
        {
            Bootstrap.Entry.Logger.Error(
                "[DimensionalTraveler.TestAdapter] Cannot schedule KitLib MCP bridge retry because SceneTree is unavailable.");
            return;
        }

        var timer = tree.CreateTimer(0.25);
        timer.Timeout += RequestBridgeBootstrap;
    }

}