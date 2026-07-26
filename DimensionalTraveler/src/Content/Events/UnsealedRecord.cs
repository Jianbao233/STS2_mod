using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.ValueProps;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Characters;
using DimensionalTraveler.Content.Relics;

namespace DimensionalTraveler.Content.Events;

[RegisterSharedEvent]
public sealed class UnsealedRecord : ModEventTemplate
{
    private const int ContainGold = 50;
    private const int TransferFallbackGold = 100;
    private const int ExploitHpLoss = 5;
    private const int ExploitGold = 150;
    private const int LeaveHeal = 12;
    private const int SalvageHpLoss = 8;
    private const int SalvageGold = 125;

    public override EventAssetProfile AssetProfile => ContentAssetProfiles.Event("AROMA_OF_CHAOS");

    public override LocString InitialDescription => PageDescription(IsTraveler ? "TRAVELER_INITIAL" : "OTHER_INITIAL");

    public override IEnumerable<LocString> GameInfoOptions =>
    [
        .. PageOptionKeys("TRAVELER_INITIAL", "CONTAIN", "TRANSFER", "EXPLOIT"),
        .. PageOptionKeys("OTHER_INITIAL", "LEAVE", "COPY", "SALVAGE"),
    ];

    protected override IReadOnlyList<EventOption> GenerateInitialOptions()
    {
        return IsTraveler ? GenerateTravelerOptions() : GenerateOtherOptions();
    }

    private bool IsTraveler => Owner!.Character.Id == ModelDb.GetId<Traveler>();

    private IReadOnlyList<EventOption> GenerateTravelerOptions()
    {
        return
        [
            Option("TRAVELER_INITIAL", "CONTAIN", Contain),
            Option("TRAVELER_INITIAL", "TRANSFER", Transfer, disableOnChosen: false),
            IsSafeToLose(ExploitHpLoss)
                ? Option("TRAVELER_INITIAL", "EXPLOIT", Exploit)
                : LockedOption("TRAVELER_INITIAL", "EXPLOIT"),
        ];
    }

    private IReadOnlyList<EventOption> GenerateOtherOptions()
    {
        return
        [
            Option("OTHER_INITIAL", "LEAVE", Leave),
            HasUpgradableCards()
                ? Option("OTHER_INITIAL", "COPY", Copy, disableOnChosen: false)
                : LockedOption("OTHER_INITIAL", "COPY"),
            IsSafeToLose(SalvageHpLoss)
                ? Option("OTHER_INITIAL", "SALVAGE", Salvage)
                : LockedOption("OTHER_INITIAL", "SALVAGE"),
        ];
    }

    private EventOption Option(string page, string name, Func<Task> onChosen, bool disableOnChosen = true)
    {
        return new EventOption(this, onChosen, ModOptionKey(page, name), disableOnChosen);
    }

    private EventOption LockedOption(string page, string name)
    {
        var optionKey = ModOptionKey(page, name);
        return new EventOption(
            this,
            null,
            GetOptionTitle(optionKey)!,
            L10NLookup($"{optionKey}.disabled"),
            optionKey,
            Array.Empty<IHoverTip>());
    }

    private IEnumerable<LocString> PageOptionKeys(string page, params string[] options)
    {
        return options.SelectMany(name =>
        {
            var optionKey = ModOptionKey(page, name);
            return new[]
            {
                L10NLookup($"{optionKey}.title"),
                L10NLookup($"{optionKey}.description"),
            };
        });
    }

    private bool IsSafeToLose(int hpLoss)
    {
        return Owner!.Creature.CurrentHp > hpLoss;
    }

    private bool HasUpgradableCards()
    {
        return PileType.Deck.GetPile(Owner!).Cards.Any(card => card.IsUpgradable);
    }

    private async Task Contain()
    {
        await PlayerCmd.GainGold(ContainGold, Owner!);
        SetEventFinished(PageDescription("TRAVELER_CONTAIN"));
    }

    private async Task Transfer()
    {
        var candidates = TransferCandidates();
        if (candidates.Count == 0)
        {
            await PlayerCmd.GainGold(TransferFallbackGold, Owner!);
            SetEventFinished(PageDescription("TRAVELER_TRANSFER"));
            return;
        }

        if (candidates.Count == 1)
        {
            await RelicCmd.Obtain(candidates[0], Owner!);
            SetEventFinished(PageDescription("TRAVELER_TRANSFER"));
            return;
        }

        var selected = await RelicSelectCmd.FromChooseARelicScreen(Owner!, candidates);
        if (selected == null)
            return;

        await RelicCmd.Obtain(selected, Owner!);
        SetEventFinished(PageDescription("TRAVELER_TRANSFER"));
    }

    private List<RelicModel> TransferCandidates()
    {
        var owner = Owner!;
        var candidates = new List<RelicModel>(2);

        if (!owner.Relics.Any(relic => relic is FirstFormulaPrincipleDiscount))
            candidates.Add(ModelDb.Relic<FirstFormulaPrincipleDiscount>().ToMutable());
        if (!owner.Relics.Any(relic => relic is PotionSatchelExpansion))
            candidates.Add(ModelDb.Relic<PotionSatchelExpansion>().ToMutable());

        foreach (var relic in candidates)
            relic.Owner = owner;

        return candidates;
    }

    private async Task Exploit()
    {
        if (!IsSafeToLose(ExploitHpLoss))
            return;

        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            ExploitHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
        await PlayerCmd.GainGold(ExploitGold, Owner!);
        SetEventFinished(PageDescription("TRAVELER_EXPLOIT"));
    }

    private async Task Leave()
    {
        await CreatureCmd.Heal(Owner!.Creature, LeaveHeal);
        SetEventFinished(PageDescription("OTHER_LEAVE"));
    }

    private async Task Copy()
    {
        if (!HasUpgradableCards())
            return;

        var prompt = L10NLookup($"{Id.Entry}.pages.OTHER_COPY.selectionScreenPrompt");
        var selected = (await CardSelectCmd.FromDeckForUpgrade(Owner!, new CardSelectorPrefs(prompt, 1))).FirstOrDefault();
        if (selected == null || !selected.IsUpgradable)
            return;

        CardCmd.Upgrade(selected, CardPreviewStyle.EventLayout);
        SetEventFinished(PageDescription("OTHER_COPY"));
    }

    private async Task Salvage()
    {
        if (!IsSafeToLose(SalvageHpLoss))
            return;

        await CreatureCmd.Damage(
            new ThrowingPlayerChoiceContext(),
            Owner!.Creature,
            SalvageHpLoss,
            ValueProp.Unblockable | ValueProp.Unpowered,
            null,
            null);
        await PlayerCmd.GainGold(SalvageGold, Owner!);
        SetEventFinished(PageDescription("OTHER_SALVAGE"));
    }
}