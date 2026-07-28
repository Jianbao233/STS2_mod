using HarmonyLib;
using KitLib;
using KitLib.Host;
using MegaCrit.Sts2.Core.Nodes;

namespace DimensionalTraveler.TestAdapter;

internal static class LanAcceptanceAutoDriverGuard
{
    public static bool IsActive =>
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DT_ACCEPTANCE_RUN_ID")) &&
        !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DT_ACCEPTANCE_ROLE"));

    public static void Disable()
    {
        AiSessionSettings.AutoPlayEnabled = false;
        KitLibHost.StopAiPlayLoop?.Invoke();
        AiSessionSettings.MpAiTeammateEnabled = false;
        AiSessionSettings.MpAiTeammateDriveLiveEnet = false;
        AiSessionSettings.MpAiTeammateAfkClient = false;
        AiSessionSettings.SyncBotEnabled = false;
        AiSessionSettings.SyncBotSpawnPhantomPlayer = false;
    }
}

[HarmonyPatch(typeof(NRun), nameof(NRun._Process))]
internal static class LanAcceptanceAutoDriverGuardPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (LanAcceptanceAutoDriverGuard.IsActive)
            LanAcceptanceAutoDriverGuard.Disable();
    }
}