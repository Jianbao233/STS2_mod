using MegaCrit.Sts2.Core.Nodes.Screens.Timeline;
using MegaCrit.Sts2.Core.Timeline;
using STS2RitsuLib.Timeline;
using STS2RitsuLib.Timeline.Scaffolding;

namespace DimensionalTraveler.Progression;

public static class NarrativeTimelineRegistration
{
    private static readonly Lock Gate = new();
    private const int NarrativeEra = 11_000;
    private static bool _registered;

    public static void Register(string modId)
    {
        lock (Gate)
        {
            if (_registered)
                return;

            var era = (EpochEra)NarrativeEra;
            ModTimelineLayoutRegistry.RegisterTimelineSlot(typeof(DistortedLegacyEpoch), era, 0, modId);
            ModTimelineLayoutRegistry.RegisterTimelineSlot(typeof(ChoiceOfTheEmberEpoch), era, 1, modId);
            ModTimelineLayoutRegistry.RegisterTimelineSlot(typeof(ThoseWhoRemainedEpoch), era, 2, modId);

            var timeline = ModTimelineRegistry.For(modId);
            timeline.RegisterStory<DimensionalTravelerStory>();
            timeline.RegisterStoryEpoch<DimensionalTravelerStory, DistortedLegacyEpoch>();
            timeline.RegisterStoryEpoch<DimensionalTravelerStory, ChoiceOfTheEmberEpoch>();
            timeline.RegisterStoryEpoch<DimensionalTravelerStory, ThoseWhoRemainedEpoch>();
            _registered = true;
        }
    }
}

public sealed class DimensionalTravelerStory : ModStoryTemplate
{
    protected override string StoryKey => "DIMENSIONAL_TRAVELER_TRAVEL_RECORD";
}

public sealed class DistortedLegacyEpoch : ModEpochTemplate
{
    public override string Id => "DIMENSIONAL_TRAVELER_EPOCH_1";

    public override string StoryId => "DIMENSIONAL_TRAVELER_TRAVEL_RECORD";

    public override EpochModel[] GetTimelineExpansion() => [new ChoiceOfTheEmberEpoch()];

    public override void QueueUnlocks()
    {
        NTimelineScreen.Instance.QueueMiscUnlock(UnlockText);
        QueueTimelineExpansion(GetTimelineExpansion());
    }
}

public sealed class ChoiceOfTheEmberEpoch : ModEpochTemplate
{
    public override string Id => "DIMENSIONAL_TRAVELER_EPOCH_2";

    public override string StoryId => "DIMENSIONAL_TRAVELER_TRAVEL_RECORD";

    public override EpochModel[] GetTimelineExpansion() => [new ThoseWhoRemainedEpoch()];

    public override void QueueUnlocks()
    {
        NTimelineScreen.Instance.QueueMiscUnlock(UnlockText);
        QueueTimelineExpansion(GetTimelineExpansion());
    }
}

public sealed class ThoseWhoRemainedEpoch : ModEpochTemplate
{
    public override string Id => "DIMENSIONAL_TRAVELER_EPOCH_3";

    public override string StoryId => "DIMENSIONAL_TRAVELER_TRAVEL_RECORD";

    public override void QueueUnlocks()
    {
        NTimelineScreen.Instance.QueueMiscUnlock(UnlockText);
    }
}