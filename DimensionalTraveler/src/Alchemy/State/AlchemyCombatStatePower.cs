using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib.Interop.AutoRegistration;
using STS2RitsuLib.Scaffolding.Content;
using DimensionalTraveler.Content.Cards.Formulas;

namespace DimensionalTraveler.Alchemy.State;

[RegisterPower]
public sealed class AlchemyCombatStatePower : ModPowerTemplate
{
    private AlchemyTurnState _turn = new();

    public override PowerType Type => PowerType.None;

    public override PowerStackType StackType => PowerStackType.Single;

    protected override bool IsVisibleInternal => false;

    public override bool ShouldPlayVfx => false;

    public ulong PlayerNetId => Owner.Player?.NetId
        ?? throw new InvalidOperationException("炼金战斗状态只能附加到玩家生物。");

    public AlchemyTurnState Snapshot
    {
        get
        {
            AssertDigest();
            return _turn.Copy();
        }
    }

    public void Update(Action<AlchemyTurnState> update)
    {
        ArgumentNullException.ThrowIfNull(update);
        AssertDigest();
        update(_turn);
        RefreshDigest();
    }

    internal void InitializeDigest() => RefreshDigest();

    private void RefreshDigest() =>
        SetAmount(_turn.CalculateStableDigest(), silent: true);

    public void AssertDigest()
    {
        var expected = _turn.CalculateStableDigest();
        if (Amount != expected)
        {
            throw new InvalidOperationException(
                $"玩家 {PlayerNetId} 的炼金隐藏状态摘要不一致：Power={Amount}，State={expected}。为避免错误结算，已停止当前动作。");
        }
    }

    public override bool TryModifyEnergyCostInCombat(
        CardModel card,
        decimal originalCost,
        out decimal modifiedCost)
    {
        AssertDigest();
        if (card.Owner == Owner.Player
            && card is IAlchemyFormulaCard
            && _turn.NextFormulaEnergyDiscount > 0)
        {
            modifiedCost = Math.Max(0m, originalCost - _turn.NextFormulaEnergyDiscount);
            return modifiedCost != originalCost;
        }

        modifiedCost = originalCost;
        return false;
    }

    public override Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        if (cardPlay.IsLastInSeries
            && cardPlay.Card.Owner == Owner.Player
            && cardPlay.Card is IAlchemyFormulaCard
            && _turn.NextFormulaEnergyDiscount > 0)
        {
            Update(static turn => turn.NextFormulaEnergyDiscount = 0);
        }

        return Task.CompletedTask;
    }

    public override Task AfterSideTurnEnd(
        PlayerChoiceContext choiceContext,
        CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (participants.Contains(Owner))
            Update(static turn => turn.Reset());

        return Task.CompletedTask;
    }

    protected override void DeepCloneFields()
    {
        base.DeepCloneFields();
        _turn = _turn.Copy();
    }
}