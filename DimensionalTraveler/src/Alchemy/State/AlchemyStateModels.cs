using DimensionalTraveler.Content.Cards.Potions;

namespace DimensionalTraveler.Alchemy.State;

[Flags]
public enum ExperimentRecord
{
    None = 0,
    BrewedOriginalPotion = 1 << 0,
    UpgradedExistingPotion = 1 << 1,
    UsedOriginalPotion = 1 << 2,
}

public enum PotionOrigin
{
    Original,
    EchoDerived,
}

public enum DiffusionMode
{
    None,
    AdditionalTarget,
    WholeSide,
}

public readonly record struct ResourceProduction(string ResourceId, int Amount);

public sealed record ProductionSnapshot(
    IReadOnlyList<ResourceProduction> Resources,
    int Energy)
{
    public static readonly ProductionSnapshot Empty = new([], 0);

    public bool IsEmpty => Energy <= 0 && Resources.All(static item => item.Amount <= 0);

    public ProductionSnapshot Copy() => new(Resources.ToArray(), Energy);
}

public readonly record struct PotionDescriptor(
    PotionFamily Family,
    PotionQuality Quality,
    bool IsUpgraded,
    PotionOrigin Origin);

public sealed record TargetSnapshot(IReadOnlyList<uint> CombatIds)
{
    public TargetSnapshot Copy() => new(CombatIds.ToArray());
}

public sealed record PotionResolutionSnapshot(
    PotionDescriptor Descriptor,
    TargetSnapshot Targets)
{
    public PotionResolutionSnapshot Copy() => new(Descriptor, Targets.Copy());
}

public sealed class AlchemyTurnState
{
    public ExperimentRecord Experiments { get; private set; }

    public int NextFormulaEnergyDiscount { get; set; }

    public int PrePurificationCharges { get; set; }

    public int? ProductionBoostCatalysisSnapshot { get; set; }

    public ProductionSnapshot? LatestProduction { get; set; }

    public DiffusionMode PendingDiffusion { get; set; }

    public PotionResolutionSnapshot? LatestOriginalPotion { get; set; }

    public bool ProductionFormulaFetchTriggered { get; set; }

    public bool DiffusionRewardTriggered { get; set; }

    public bool HasBrewedOrUsedOriginalPotion =>
        (Experiments & (ExperimentRecord.BrewedOriginalPotion | ExperimentRecord.UsedOriginalPotion)) != 0;

    public int ExperimentCount =>
        (Experiments.HasFlag(ExperimentRecord.BrewedOriginalPotion) ? 1 : 0)
        + (Experiments.HasFlag(ExperimentRecord.UpgradedExistingPotion) ? 1 : 0)
        + (Experiments.HasFlag(ExperimentRecord.UsedOriginalPotion) ? 1 : 0);

    public void Record(ExperimentRecord record) => Experiments |= record;

    public void Reset()
    {
        Experiments = ExperimentRecord.None;
        NextFormulaEnergyDiscount = 0;
        PrePurificationCharges = 0;
        ProductionBoostCatalysisSnapshot = null;
        LatestProduction = null;
        PendingDiffusion = DiffusionMode.None;
        LatestOriginalPotion = null;
        ProductionFormulaFetchTriggered = false;
        DiffusionRewardTriggered = false;
    }

    public AlchemyTurnState Copy() => new()
    {
        Experiments = Experiments,
        NextFormulaEnergyDiscount = NextFormulaEnergyDiscount,
        PrePurificationCharges = PrePurificationCharges,
        ProductionBoostCatalysisSnapshot = ProductionBoostCatalysisSnapshot,
        LatestProduction = LatestProduction?.Copy(),
        PendingDiffusion = PendingDiffusion,
        LatestOriginalPotion = LatestOriginalPotion?.Copy(),
        ProductionFormulaFetchTriggered = ProductionFormulaFetchTriggered,
        DiffusionRewardTriggered = DiffusionRewardTriggered,
    };

    public int CalculateStableDigest()
    {
        var hash = new StableDigest();
        hash.Add((int)Experiments);
        hash.Add(NextFormulaEnergyDiscount);
        hash.Add(PrePurificationCharges);
        hash.Add(ProductionBoostCatalysisSnapshot);
        hash.Add(PendingDiffusion);
        hash.Add(ProductionFormulaFetchTriggered);
        hash.Add(DiffusionRewardTriggered);
        hash.Add(LatestProduction);
        hash.Add(LatestOriginalPotion);
        return hash.Value;
    }

    private struct StableDigest
    {
        private const uint Offset = 2166136261u;
        private const uint Prime = 16777619u;
        private uint _hash;

        public int Value
        {
            get
            {
                const uint maxPowerAmount = 999_999_999u;
                var bounded = (_hash == 0 ? Offset : _hash) % maxPowerAmount;
                return (int)(bounded == 0 ? 1u : bounded);
            }
        }

        public void Add(bool value) => Add(value ? 1 : 0);

        public void Add<TEnum>(TEnum value) where TEnum : struct, Enum =>
            Add(Convert.ToInt32(value));

        public void Add(int? value)
        {
            Add(value.HasValue);
            if (value.HasValue)
                Add(value.Value);
        }

        public void Add(int value)
        {
            EnsureInitialized();
            unchecked
            {
                _hash ^= (uint)value;
                _hash *= Prime;
            }
        }

        public void Add(uint value)
        {
            EnsureInitialized();
            unchecked
            {
                _hash ^= value;
                _hash *= Prime;
            }
        }

        public void Add(string value)
        {
            EnsureInitialized();
            foreach (var character in value)
            {
                unchecked
                {
                    _hash ^= character;
                    _hash *= Prime;
                }
            }
            Add(0xffu);
        }

        public void Add(ProductionSnapshot? snapshot)
        {
            Add(snapshot is not null);
            if (snapshot is null)
                return;

            Add(snapshot.Energy);
            Add(snapshot.Resources.Count);
            foreach (var resource in snapshot.Resources)
            {
                Add(resource.ResourceId);
                Add(resource.Amount);
            }
        }

        public void Add(PotionResolutionSnapshot? snapshot)
        {
            Add(snapshot is not null);
            if (snapshot is null)
                return;

            Add(snapshot.Descriptor.Family);
            Add(snapshot.Descriptor.Quality);
            Add(snapshot.Descriptor.IsUpgraded);
            Add(snapshot.Descriptor.Origin);
            Add(snapshot.Targets.CombatIds.Count);
            foreach (var combatId in snapshot.Targets.CombatIds)
                Add(combatId);
        }

        private void EnsureInitialized()
        {
            if (_hash == 0)
                _hash = Offset;
        }
    }
}