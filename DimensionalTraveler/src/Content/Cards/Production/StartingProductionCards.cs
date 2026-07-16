using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Characters;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Content.Cards.Production;

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "PRODUCE_VITALITY")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class ProduceVitality : DirectedPrincipleProductionCard
{
    public ProduceVitality() : base(AlchemyPrinciples.Vitality)
    {
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "PRODUCE_VOLATILITY")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class ProduceVolatility : DirectedPrincipleProductionCard
{
    public ProduceVolatility() : base(AlchemyPrinciples.Volatility)
    {
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "PRODUCE_CORRUPTION")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class ProduceCorruption : DirectedPrincipleProductionCard
{
    public ProduceCorruption() : base(AlchemyPrinciples.Corruption)
    {
    }
}

[RegisterCard(typeof(TravelerCardPool), StableEntryStem = "BALANCED_PRODUCTION")]
[RegisterCharacterStarterCard(typeof(Traveler))]
public sealed class BalancedProduction : ModCardTemplate
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Gain", 1m)];

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    public BalancedProduction() : base(1, CardType.Skill, CardRarity.Basic, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var amount = DynamicVars["Gain"].IntValue;
        foreach (var principle in AlchemyPrinciples.All)
            await AlchemyPrinciples.Gain(Owner, principle, amount, this);
    }

    protected override void OnUpgrade() => DynamicVars["Gain"].UpgradeValueBy(1m);
}