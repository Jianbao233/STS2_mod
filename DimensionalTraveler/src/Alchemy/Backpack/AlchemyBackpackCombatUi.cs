using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace DimensionalTraveler.Alchemy.Backpack;

internal static class AlchemyBackpackUiLayout
{
    public static readonly Vector2 ButtonPosition = new(110f, -98f);
    public static readonly Vector2 ButtonSize = new(80f, 80f);
}

internal sealed partial class AlchemyBackpackButton : NButton
{
    private const string IconPath = "res://images/packed/combat_ui/draw_pile.png";

    private readonly Player _player;
    private readonly CardPile _pile;
    private readonly Label _countLabel;
    private readonly TextureRect _icon;
    private Tween? _pulseTween;

    public AlchemyBackpackButton(Player player)
    {
        _player = player;
        _pile = AlchemyBackpack.GetPile(player);
        Name = "DimensionalTravelerAlchemyBackpackButton";
        AnchorTop = 1f;
        AnchorBottom = 1f;
        Position = AlchemyBackpackUiLayout.ButtonPosition;
        Size = AlchemyBackpackUiLayout.ButtonSize;
        PivotOffset = Size * 0.5f;
        FocusMode = FocusModeEnum.All;
        TooltipText = string.Empty;

        _icon = new TextureRect
        {
            Name = "Icon",
            Texture = PreloadManager.Cache.GetAsset<Texture2D>(IconPath),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(_icon);

        _countLabel = new Label
        {
            Name = "Count",
            AnchorLeft = 0.55f,
            AnchorTop = 0.48f,
            AnchorRight = 1.2f,
            AnchorBottom = 1.15f,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _countLabel.AddThemeFontSizeOverride("font_size", 26);
        _countLabel.AddThemeColorOverride("font_color", StsColors.cream);
        _countLabel.AddThemeColorOverride("font_outline_color", new Color("6F251F"));
        _countLabel.AddThemeConstantOverride("outline_size", 8);
        AddChild(_countLabel);
    }

    public override void _Ready()
    {
        ConnectSignals();
        _pile.ContentsChanged += Refresh;
        CombatManager.Instance.TurnStarted += OnCombatStateChanged;
        CombatManager.Instance.PlayerEndedTurn += OnPlayerTurnChanged;
        CombatManager.Instance.PlayerUnendedTurn += OnPlayerTurnChanged;
        Refresh();
    }

    public override void _ExitTree()
    {
        _pile.ContentsChanged -= Refresh;
        CombatManager.Instance.TurnStarted -= OnCombatStateChanged;
        CombatManager.Instance.PlayerEndedTurn -= OnPlayerTurnChanged;
        CombatManager.Instance.PlayerUnendedTurn -= OnPlayerTurnChanged;
        _pulseTween?.Kill();
        base._ExitTree();
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        if (!CombatManager.Instance.IsInProgress)
            return;

        if (_pile.IsEmpty)
        {
            var message = new LocString(
                "static_hover_tips",
                "DIMENSIONAL_TRAVELER_CARDPILE_BACKPACK.empty").GetFormattedText();
            NCombatRoom.Instance?.CombatVfxContainer.AddChildSafely(
                NThoughtBubbleVfx.Create(message, _player.Creature, 2f));
            return;
        }

        AlchemyBackpackScreen.Show(_pile);
    }

    protected override void OnFocus()
    {
        base.OnFocus();
        NHoverTipSet.CreateAndShow(this, new HoverTip(
            new LocString("static_hover_tips", "DIMENSIONAL_TRAVELER_CARDPILE_BACKPACK.title"),
            new LocString("static_hover_tips", "DIMENSIONAL_TRAVELER_CARDPILE_BACKPACK.description")));
        _pulseTween?.Kill();
        _pulseTween = CreateTween();
        _pulseTween.TweenProperty(_icon, "scale", Vector2.One * 1.14f, 0.08f);
    }

    protected override void OnUnfocus()
    {
        base.OnUnfocus();
        NHoverTipSet.Remove(this);
        _pulseTween?.Kill();
        _pulseTween = CreateTween();
        _pulseTween.TweenProperty(_icon, "scale", Vector2.One, 0.2f)
            .SetEase(Tween.EaseType.Out)
            .SetTrans(Tween.TransitionType.Expo);
    }

    private void OnCombatStateChanged(CombatState _) => Refresh();

    private void OnPlayerTurnChanged(Player player, bool _) 
    {
        if (player == _player)
            Refresh();
    }

    private void OnPlayerTurnChanged(Player player)
    {
        if (player == _player)
            Refresh();
    }

    private void Refresh()
    {
        _countLabel.Text = _pile.Cards.Count.ToString();
        QueueRedraw();
    }
}

internal sealed partial class AlchemyBackpackScreen : Control, ICapstoneScreen, IScreenContext
{
    private const string CardGridScene = "res://scenes/cards/card_grid.tscn";
    private const string BackButtonScene = "res://scenes/ui/back_button.tscn";

    private readonly CardPile _pile;
    private NCardGrid? _grid;
    private NButton? _backButton;

    private AlchemyBackpackScreen(CardPile pile)
    {
        _pile = pile;
        Name = "DimensionalTravelerAlchemyBackpackScreen";
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }

    public NetScreenType ScreenType => NetScreenType.CardPile;

    public bool UseSharedBackstop => true;

    public Control? DefaultFocusedControl => _grid?.DefaultFocusedControl;

    public static void Show(CardPile pile)
    {
        var container = NCapstoneContainer.Instance;
        if (container is null)
            return;

        container.Open(new AlchemyBackpackScreen(pile));
    }

    public override void _Ready()
    {
        var background = new ColorRect
        {
            Name = "Background",
            Color = new Color(0f, 0f, 0f, 0.75f),
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(background);

        _grid = PreloadManager.Cache.GetScene(CardGridScene).Instantiate<NCardGrid>();
        _grid.Name = "PotionBackpackGrid";
        _grid.OffsetTop = 80f;
        AddChild(_grid);

        _backButton = PreloadManager.Cache.GetScene(BackButtonScene).Instantiate<NButton>();
        _backButton.Name = "BackButton";
        _backButton.Connect(NClickableControl.SignalName.Released, Callable.From<NClickableControl>(_ =>
            NCapstoneContainer.Instance?.Close()));
        AddChild(_backButton);
        _backButton.Enable();

        _pile.ContentsChanged += RefreshCards;
        RefreshCards();
    }

    public override void _ExitTree()
    {
        _pile.ContentsChanged -= RefreshCards;
        base._ExitTree();
    }

    public void AfterCapstoneOpened()
    {
        Visible = true;
        Callable.From(ActiveScreenContext.Instance.FocusOnDefaultControl).CallDeferred();
    }

    public void AfterCapstoneClosed()
    {
        Visible = false;
        this.QueueFreeSafely();
    }

    private void RefreshCards()
    {
        if (_grid is null || !GodotObject.IsInstanceValid(_grid))
            return;

        _grid.SetCards(_pile.Cards.ToList(), PileType.Draw, [SortingOrders.Ascending]);
    }
}

[HarmonyPatch(typeof(NCombatUi), nameof(NCombatUi.Activate))]
internal static class AlchemyBackpackCombatUiPatch
{
    private static void Postfix(NCombatUi __instance, CombatState state)
    {
        var player = LocalContext.GetMe(state);
        if (player is null
            || !State.AlchemyCombatState.IsTraveler(player)
            || __instance.GetNodeOrNull<AlchemyBackpackButton>("DimensionalTravelerAlchemyBackpackButton") is not null)
        {
            return;
        }

        __instance.AddChildSafely(new AlchemyBackpackButton(player));
    }
}