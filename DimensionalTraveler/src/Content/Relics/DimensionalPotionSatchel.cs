using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Alchemy.State;
using DimensionalTraveler.Characters;
using DimensionalTraveler.Content.Cards.System;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Relics;

[RegisterRelic(typeof(TravelerRelicPool), StableEntryStem = "DIMENSIONAL_POTION_SATCHEL")]
[RegisterCharacterStarterRelic(typeof(Traveler))]
public sealed class DimensionalPotionSatchel : ModRelicTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    public override RelicAssetProfile AssetProfile => ContentAssetProfiles.Relic("RING_OF_THE_SNAKE");

    public override RelicRarity Rarity => RelicRarity.Starter;

    public override decimal ModifyHandDraw(Player player, decimal count)
    {
        if (player != Owner)
            return count;

        var turnNumber = player.PlayerCombatState?.TurnNumber ?? 1;
        return count + DynamicVars.Cards.BaseValue + (turnNumber == 1 ? 1 : 0);
    }

    public override async Task BeforeCombatStart()
    {
        Flash();
        await AlchemyCombatState.Attach(Owner);
        foreach (var principle in AlchemyPrinciples.Basic)
            await AlchemyPrinciples.Gain(Owner, principle, 1, this);

        var combatState = Owner.Creature.CombatState;
        if (combatState is null)
            return;

        var satchel = combatState.CreateCard<PotionSatchel>(Owner);
        await CardPileCmd.AddGeneratedCardToCombat(satchel, PileType.Hand, Owner);
    }

    public override async Task BeforeSideTurnStart(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IReadOnlyList<Creature> participants,
        ICombatState combatState)
    {
        if (!participants.Contains(Owner.Creature))
            return;

        Flash();
        foreach (var principle in AlchemyPrinciples.Basic)
            await AlchemyPrinciples.Gain(Owner, principle, 1, this);
    }
}