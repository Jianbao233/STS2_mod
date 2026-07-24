using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using STS2RitsuLib;
using STS2RitsuLib.Combat.SecondaryResources;
using STS2RitsuLib.Scaffolding.Godot.NodeAttachments;
using DimensionalTraveler.Bootstrap;
using DimensionalTraveler.Characters;

namespace DimensionalTraveler.Resources;

public static class AlchemyPrinciples
{
    public const string VitalityLocalId = "vitality";
    public const string VolatilityLocalId = "volatility";
    public const string CorruptionLocalId = "corruption";
    public const string CatalysisLocalId = "catalysis";
    public const string DiffusionLocalId = "diffusion";
    public const string EchoLocalId = "echo";

    public const string VitalityIconPath =
        "res://images/atlases/ui_atlas.sprites/card/energy_silent.tres";

    public const string VolatilityIconPath =
        "res://images/atlases/ui_atlas.sprites/card/energy_defect.tres";

    public const string CorruptionIconPath =
        "res://images/atlases/ui_atlas.sprites/card/energy_ironclad.tres";

    private static readonly SecondaryResourceCounterStyle CombatCounterStyle =
        SecondaryResourceCounterStyle.Default with
        {
            CounterSize = new(54f, 54f),
            IconSize = new(52f, 52f),
            FontSize = 30,
            RowSeparation = 10,
            IconStyle = SecondaryResourceIconStyle.Default with
            {
                Size = new(52f, 52f),
                HoverTip = SecondaryResourceHoverTipStyle.Default with
                {
                    ResolveGlobalPosition = ResolveCombatHoverTipPosition,
                },
            },
            GainFeedback = SecondaryResourceCounterGainFeedback.StarCounterLike,
        };

    public static SecondaryResourceDefinition Vitality { get; private set; } = null!;

    public static SecondaryResourceDefinition Volatility { get; private set; } = null!;

    public static SecondaryResourceDefinition Corruption { get; private set; } = null!;

    public static SecondaryResourceDefinition Catalysis { get; private set; } = null!;

    public static SecondaryResourceDefinition Diffusion { get; private set; } = null!;

    public static SecondaryResourceDefinition Echo { get; private set; } = null!;

    public static IReadOnlyList<SecondaryResourceDefinition> Basic { get; private set; } = [];

    public static IReadOnlyList<SecondaryResourceDefinition> Special { get; private set; } = [];

    public static IReadOnlyList<SecondaryResourceDefinition> All { get; private set; } = [];

    public static void Register()
    {
        if (All.Count > 0)
            return;

        var registry = RitsuLibFramework.GetSecondaryResourceRegistry(Entry.ModId);
        Vitality = RegisterPrinciple(registry, VitalityLocalId, VitalityIconPath, maxAmount: null);
        Volatility = RegisterPrinciple(registry, VolatilityLocalId, VolatilityIconPath, maxAmount: null);
        Corruption = RegisterPrinciple(registry, CorruptionLocalId, CorruptionIconPath, maxAmount: null);
        Catalysis = RegisterPrinciple(registry, CatalysisLocalId, VitalityIconPath, maxAmount: 3);
        Diffusion = RegisterPrinciple(registry, DiffusionLocalId, VolatilityIconPath, maxAmount: 3);
        Echo = RegisterPrinciple(registry, EchoLocalId, CorruptionIconPath, maxAmount: 3);
        Basic = [Vitality, Volatility, Corruption];
        Special = [Catalysis, Diffusion, Echo];
        All = [.. Basic, .. Special];

        registry.AlwaysShowInCombatUiForCharacter<Traveler>(VitalityLocalId);
        registry.AlwaysShowInCombatUiForCharacter<Traveler>(VolatilityLocalId);
        registry.AlwaysShowInCombatUiForCharacter<Traveler>(CorruptionLocalId);
        registry.AlwaysShowInCombatUiForCharacter<Traveler>(CatalysisLocalId);
        registry.AlwaysShowInCombatUiForCharacter<Traveler>(DiffusionLocalId);
        registry.AlwaysShowInCombatUiForCharacter<Traveler>(EchoLocalId);
        RegisterCombatUi(registry);
    }

    public static int Get(Player player, SecondaryResourceDefinition principle) =>
        SecondaryResourceCmd.Get(player, principle.Id);

    public static bool CanPay(Player player, SecondaryResourceDefinition principle, int amount) =>
        amount <= 0 || Get(player, principle) >= amount;

    public static Task<int> Gain(
        Player player,
        SecondaryResourceDefinition principle,
        int amount,
        AbstractModel? source = null) =>
        SecondaryResourceCmd.Gain(player, principle.Id, amount, source);

    public static Task<bool> Spend(
        Player player,
        SecondaryResourceDefinition principle,
        int amount,
        CardModel? card = null,
        AbstractModel? source = null) =>
        SecondaryResourceCmd.Spend(player, principle.Id, amount, card, source);

    private static SecondaryResourceDefinition RegisterPrinciple(
        ModSecondaryResourceRegistry registry,
        string localId,
        string iconPath,
        int? maxAmount) =>
        registry.Register(localId, new SecondaryResourceDefinition(
            defaultAmount: 0,
            baseMaxAmount: maxAmount,
            turnStartPolicy: SecondaryResourceTurnStartPolicy.None,
            persistencePolicy: SecondaryResourcePersistencePolicy.Combat,
            smallIconPath: iconPath,
            largeIconPath: iconPath));

    private static void RegisterCombatUi(ModSecondaryResourceRegistry registry)
    {
        registry.RegisterCombatUi(
            "alchemy_principles_counter_row",
            _ => CreateCombatCounterRow(),
            context => context.Node.Refresh(
                context.Player,
                All.Where(context.VisibleDefinitions.Contains).ToArray()),
            new NodeAttachmentOptions
            {
                Name = "DimensionalTravelerAlchemyPrinciples",
                DuplicatePolicy = NodeAttachmentDuplicatePolicy.ReuseExistingByName,
            });
    }

    private static Vector2 ResolveCombatHoverTipPosition(SecondaryResourceHoverTipPlacementContext context)
    {
        const float margin = 20f;
        var ownerRect = context.Owner.GetGlobalRect();
        var tipSize = context.TipSet.Size;
        if (tipSize.X < 1f || tipSize.Y < 1f)
            tipSize = context.TipSet.GetCombinedMinimumSize();

        var viewportSize = context.Owner.GetViewportRect().Size;
        var centeredX = ownerRect.Position.X + ownerRect.Size.X * 0.5f - tipSize.X * 0.5f;
        var maxX = Math.Max(margin, viewportSize.X - tipSize.X - margin);
        return new(
            Mathf.Clamp(centeredX, margin, maxX),
            Math.Max(margin, ownerRect.Position.Y - tipSize.Y - margin));
    }

    private static NSecondaryResourceCounterRow CreateCombatCounterRow()
    {
        var row = new NSecondaryResourceCounterRow
        {
            Name = "DimensionalTravelerAlchemyPrinciples",
            AnchorLeft = 0f,
            AnchorTop = 1f,
            AnchorRight = 0f,
            AnchorBottom = 1f,
            OffsetLeft = 42f,
            OffsetTop = -346f,
            OffsetRight = 246f,
            OffsetBottom = -292f,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        row.Configure(CombatCounterStyle);
        return row;
    }
}