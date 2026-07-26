using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Saves;
using STS2RitsuLib.Unlocks;
using DimensionalTraveler.Characters;

namespace DimensionalTraveler.Progression;

public static class NarrativeProgression
{
    public const string DistortedLegacyEpochId = "DIMENSIONAL_TRAVELER_EPOCH_1";
    public const string ChoiceOfTheEmberEpochId = "DIMENSIONAL_TRAVELER_EPOCH_2";
    public const string ThoseWhoRemainedEpochId = "DIMENSIONAL_TRAVELER_EPOCH_3";

    private static readonly Lock Gate = new();
    private static bool _rulesRegistered;

    public static void RegisterRunUnlockRules(string modId)
    {
        lock (Gate)
        {
            if (_rulesRegistered)
                return;

            var unlocks = ModUnlockRegistry.For(modId);
            unlocks.RegisterPostRunRule(PostRunEpochUnlockRule.Create(
                ThoseWhoRemainedEpochId,
                "Unlock ThoseWhoRemainedEpoch after the second mapped traveler run",
                context =>
                    IsEligibleTravelerRun(context) &&
                    SaveManager.Instance.Progress.IsEpochObtained(ChoiceOfTheEmberEpochId)));
            unlocks.RegisterPostRunRule(PostRunEpochUnlockRule.Create(
                ChoiceOfTheEmberEpochId,
                "Unlock ChoiceOfTheEmberEpoch after the first mapped traveler run",
                context =>
                    IsEligibleTravelerRun(context) &&
                    SaveManager.Instance.Progress.IsEpochObtained(DistortedLegacyEpochId)));
            _rulesRegistered = true;
        }
    }

    public static bool TryGrantInitialEpoch(SaveManager saveManager)
    {
        ArgumentNullException.ThrowIfNull(saveManager);

        if (saveManager.Progress.IsEpochObtained(DistortedLegacyEpochId))
            return false;

        saveManager.ObtainEpoch(DistortedLegacyEpochId);
        saveManager.SaveProgressFile();
        return true;
    }

    private static bool IsEligibleTravelerRun(PostRunUnlockContext context) =>
        context.CharacterId == ModelDb.GetId<Traveler>() &&
        context.Run.FloorReached > 0;
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.InitProgressData))]
internal static class InitialNarrativeEpochPatch
{
    [HarmonyPostfix]
    private static void Postfix(SaveManager __instance)
    {
        NarrativeProgression.TryGrantInitialEpoch(__instance);
    }
}