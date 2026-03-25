using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace MP_PlayerManager
{
    internal static class UiHelper
    {
        internal static void FlashAcquired(Control node)
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            var tween = node.CreateTween();
            tween.TweenProperty(node, "modulate", new Color(0.5f, 1f, 0.5f, 1f), 0.1f);
            tween.TweenProperty(node, "modulate", Colors.White, 0.3f);
        }

        internal static async Task AcquireCardWithPreview(CardModel card, PileType pile)
        {
            CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, pile, CardPilePosition.Bottom, null, false), 0.8f, CardPreviewStyle.HorizontalLayout);
        }
    }
}
