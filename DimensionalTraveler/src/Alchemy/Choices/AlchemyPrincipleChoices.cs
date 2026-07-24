using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Combat.SecondaryResources;

namespace DimensionalTraveler.Alchemy.Choices;

public static class AlchemyPrincipleChoices
{
    private static readonly Type[] BasicChoiceTypes =
    [
        typeof(VitalityChoice),
        typeof(VolatilityChoice),
        typeof(CorruptionChoice),
    ];

    private static readonly Type[] SpecialChoiceTypes =
    [
        typeof(CatalysisChoice),
        typeof(DiffusionChoice),
        typeof(EchoChoice),
    ];

    private static readonly Type[] CategoryChoiceTypes =
    [
        typeof(BasicPrinciplesChoice),
        typeof(SpecialPrinciplesChoice),
    ];

    public static Task<SecondaryResourceDefinition> ChooseBasic(
        PlayerChoiceContext choiceContext,
        Player player) =>
        ChoosePrinciple(choiceContext, player, BasicChoiceTypes);

    public static Task<SecondaryResourceDefinition> ChooseSpecial(
        PlayerChoiceContext choiceContext,
        Player player) =>
        ChoosePrinciple(choiceContext, player, SpecialChoiceTypes);

    public static async Task<SecondaryResourceDefinition> ChooseAny(
        PlayerChoiceContext choiceContext,
        Player player)
    {
        var category = await ChooseCard<PrincipleCategoryChoiceCard>(
            choiceContext,
            player,
            CategoryChoiceTypes);
        return category.SelectsSpecialPrinciples
            ? await ChooseSpecial(choiceContext, player)
            : await ChooseBasic(choiceContext, player);
    }

    private static async Task<SecondaryResourceDefinition> ChoosePrinciple(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyList<Type> candidateTypes)
    {
        var selected = await ChooseCard<PrincipleChoiceCard>(
            choiceContext,
            player,
            candidateTypes);
        return selected.Principle;
    }

    private static async Task<TChoice> ChooseCard<TChoice>(
        PlayerChoiceContext choiceContext,
        Player player,
        IReadOnlyList<Type> candidateTypes)
        where TChoice : CardModel
    {
        var combatState = player.Creature.CombatState
            ?? throw new InvalidOperationException("原理选择只能在战斗中执行。");
        var candidates = candidateTypes
            .Select(type => combatState.CreateCard(ModelDb.GetById<CardModel>(ModelDb.GetId(type)), player))
            .ToArray();
        var selected = await CardSelectCmd.FromChooseACardScreen(
            choiceContext,
            candidates,
            player,
            canSkip: false);
        return selected as TChoice
            ?? throw new InvalidOperationException("必要的原理选择没有返回合法候选。");
    }
}