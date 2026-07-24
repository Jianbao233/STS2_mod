using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Content.Pools;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Alchemy.Choices;

public abstract class PrincipleChoiceCard : ModCardTemplate
{
    protected PrincipleChoiceCard()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None, showInCardLibrary: false)
    {
    }

    public abstract SecondaryResourceDefinition Principle { get; }

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override HashSet<CardTag> CanonicalTags => [];
}

public abstract class PrincipleCategoryChoiceCard : ModCardTemplate
{
    protected PrincipleCategoryChoiceCard()
        : base(0, CardType.Skill, CardRarity.Token, TargetType.None, showInCardLibrary: false)
    {
    }

    public abstract bool SelectsSpecialPrinciples { get; }

    public override bool CanBeGeneratedInCombat => false;

    public override bool CanBeGeneratedByModifiers => false;

    public override string? CustomPortraitPath => CardModel.MissingPortraitPath;

    protected override HashSet<CardTag> CanonicalTags => [];
}

[RegisterCard(typeof(TravelerTokenCardPool), StableEntryStem = "CHOICE_VITALITY")]
public sealed class VitalityChoice : PrincipleChoiceCard
{
    public override SecondaryResourceDefinition Principle => AlchemyPrinciples.Vitality;
}

[RegisterCard(typeof(TravelerTokenCardPool), StableEntryStem = "CHOICE_VOLATILITY")]
public sealed class VolatilityChoice : PrincipleChoiceCard
{
    public override SecondaryResourceDefinition Principle => AlchemyPrinciples.Volatility;
}

[RegisterCard(typeof(TravelerTokenCardPool), StableEntryStem = "CHOICE_CORRUPTION")]
public sealed class CorruptionChoice : PrincipleChoiceCard
{
    public override SecondaryResourceDefinition Principle => AlchemyPrinciples.Corruption;
}

[RegisterCard(typeof(TravelerTokenCardPool), StableEntryStem = "CHOICE_CATALYSIS")]
public sealed class CatalysisChoice : PrincipleChoiceCard
{
    public override SecondaryResourceDefinition Principle => AlchemyPrinciples.Catalysis;
}

[RegisterCard(typeof(TravelerTokenCardPool), StableEntryStem = "CHOICE_DIFFUSION")]
public sealed class DiffusionChoice : PrincipleChoiceCard
{
    public override SecondaryResourceDefinition Principle => AlchemyPrinciples.Diffusion;
}

[RegisterCard(typeof(TravelerTokenCardPool), StableEntryStem = "CHOICE_ECHO")]
public sealed class EchoChoice : PrincipleChoiceCard
{
    public override SecondaryResourceDefinition Principle => AlchemyPrinciples.Echo;
}

[RegisterCard(typeof(TravelerTokenCardPool), StableEntryStem = "CHOICE_BASIC_PRINCIPLES")]
public sealed class BasicPrinciplesChoice : PrincipleCategoryChoiceCard
{
    public override bool SelectsSpecialPrinciples => false;
}

[RegisterCard(typeof(TravelerTokenCardPool), StableEntryStem = "CHOICE_SPECIAL_PRINCIPLES")]
public sealed class SpecialPrinciplesChoice : PrincipleCategoryChoiceCard
{
    public override bool SelectsSpecialPrinciples => true;
}