using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;
using KitLib.Host;

namespace DimensionalTraveler.TestAdapter;

[ModInitializer(nameof(Initialize))]
public static class Entry
{
    public const string ModId = "DimensionalTraveler.TestAdapter";

    public static void Initialize()
    {
        KitLibHost.RegisterSnapshotContributor(DimensionalTravelerSnapshotContributor.Instance);
        PaymentAudit.Install();
        ChoiceAudit.Install();
        TestPotionGrantAction.Register();
        new Harmony("DimensionalTraveler.TestAdapter.LanAcceptanceAutoDriverGuard")
            .CreateClassProcessor(typeof(LanAcceptanceAutoDriverGuardPatch))
            .Patch();
        McpIntegration.Install();
        McpBridgeWatchdog.Install();
        Bootstrap.Entry.Logger.Info("[DimensionalTraveler.TestAdapter] 测试快照、支付审计与 MCP 控制已启用。");
    }
}