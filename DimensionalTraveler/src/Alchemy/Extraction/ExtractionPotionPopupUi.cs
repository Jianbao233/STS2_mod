using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Potions;
using DimensionalTraveler.Content.Cards.Potions;

namespace DimensionalTraveler.Alchemy.Extraction;

[HarmonyPatch(typeof(NPotionPopup), nameof(NPotionPopup._Ready))]
internal static class ExtractionPotionPopupPatch
{
    private const string ButtonName = "DimensionalTravelerExtractButton";
    private static readonly AccessTools.FieldRef<NPotionPopup, NPotionHolder> HolderRef =
        AccessTools.FieldRefAccess<NPotionPopup, NPotionHolder>("_holder");

    private static void Postfix(NPotionPopup __instance)
    {
        if (__instance.GetNodeOrNull<NPotionPopupButton>(ButtonName) is not null)
            return;

        var holder = HolderRef(__instance);
        var potion = holder.Potion?.Model;
        if (potion is null || !State.AlchemyCombatState.IsTraveler(potion.Owner))
            return;
        if (!PotionExtractionCatalog.TryGet(potion.Id.Entry, out var plan))
            return;

        var discardButton = __instance.GetNode<NPotionPopupButton>("%DiscardButton");
        var container = __instance.GetNode<Control>("%Container");
        var button = (NPotionPopupButton)discardButton.Duplicate(
            (int)(Node.DuplicateFlags.Groups
                | Node.DuplicateFlags.Scripts
                | Node.DuplicateFlags.UseInstantiation));
        button.Name = ButtonName;
        button.Position += Vector2.Down * 93f;
        button.GetNode<Label>("Label").Text = new LocString(
            "static_hover_tips",
            "DIMENSIONAL_TRAVELER_EXTRACTION.title").GetFormattedText();
        container.OffsetBottom += 93f;
        __instance.OffsetBottom += 93f;
        container.AddChild(button);

        button.Connect(NClickableControl.SignalName.Focused,
            Callable.From<NClickableControl>(_ => ShowPreview(button, potion, plan)));
        button.Connect(NClickableControl.SignalName.Unfocused,
            Callable.From<NClickableControl>(_ => NHoverTipSet.Remove(button)));
        button.Connect(NClickableControl.SignalName.Released,
            Callable.From<NClickableControl>(_ => TryEnqueue(__instance, potion, button)));

        var slotIndex = FindSlotIndex(potion.Owner, potion);
        button.SetVisible(slotIndex >= 0);
        button.SetEnabled(slotIndex >= 0 && ExtractionFlow.CanEnqueue(potion.Owner, slotIndex, out _));
    }

    private static int FindSlotIndex(Player player, PotionModel potion)
    {
        for (var index = 0; index < player.PotionSlots.Count; index++)
        {
            if (ReferenceEquals(player.PotionSlots[index], potion))
                return index;
        }

        return -1;
    }

    private static void TryEnqueue(
        NPotionPopup popup,
        PotionModel potion,
        NPotionPopupButton button)
    {
        var slotIndex = FindSlotIndex(potion.Owner, potion);
        if (slotIndex < 0 || !ExtractionFlow.Enqueue(potion.Owner, slotIndex, out _))
        {
            button.Disable();
            return;
        }

        popup.Remove();
    }

    private static void ShowPreview(
        Control button,
        PotionModel potion,
        ExtractionPlan plan)
    {
        var title = new LocString("static_hover_tips", "DIMENSIONAL_TRAVELER_EXTRACTION.preview.title");
        title.Add("Potion", potion.Title);
        var description = new LocString("static_hover_tips", "DIMENSIONAL_TRAVELER_EXTRACTION.preview.description");
        description.Add("Special", GetPrincipleName(plan.SpecialPrinciple));
        description.Add("Basic", GetPrincipleName(plan.BasicPrinciple));
        description.Add("Amount", plan.BasicAmount);
        description.Add("Rewards", GetRewardText(plan));
        description.Add("Gold", plan.Gold);
        description.Add("MaxHp", plan.MaxHp);
        NHoverTipSet.CreateAndShow(button, new HoverTip(title, description));
    }

    private static string GetPrincipleName(AlchemyPrincipleKind principle) =>
        new LocString("static_hover_tips", $"DIMENSIONAL_TRAVELER_EXTRACTION.principle.{principle}")
            .GetFormattedText();

    private static string GetRewardText(ExtractionPlan plan)
    {
        if (plan.ChoiceMode == ExtractionChoiceMode.AttackPotion)
        {
            return new LocString(
                "static_hover_tips",
                "DIMENSIONAL_TRAVELER_EXTRACTION.reward.attack_choice").GetFormattedText();
        }

        if (plan.PotionRewards.Count == 0)
            return new LocString("static_hover_tips", "DIMENSIONAL_TRAVELER_EXTRACTION.reward.none").GetFormattedText();

        var rewards = plan.PotionRewards
            .Select(static reward => FormatReward(reward))
            .ToArray();
        var rewardText = new LocString("static_hover_tips", "DIMENSIONAL_TRAVELER_EXTRACTION.reward.list");
        rewardText.Add("Items", string.Join(", ", rewards));
        return rewardText.GetFormattedText();
    }

    private static string FormatReward(ExtractionPotionReward reward)
    {
        var card = PotionCatalog.GetCanonical(reward.Family, reward.Quality);
        return reward.IsUpgraded ? $"{card.Title}+" : card.Title;
    }
}