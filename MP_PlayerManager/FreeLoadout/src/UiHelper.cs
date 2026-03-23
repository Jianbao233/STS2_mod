using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace MP_PlayerManager
{
	// Token: 0x02000004 RID: 4
	internal static class UiHelper
	{
		// Token: 0x06000010 RID: 16 RVA: 0x000027E0 File Offset: 0x000009E0
		internal static void FlashAcquired(Control node)
		{
			if (!GodotObject.IsInstanceValid(node))
			{
				return;
			}
			Tween tween = node.CreateTween();
			tween.TweenProperty(node, "modulate", new Color(0.5f, 1f, 0.5f, 1f), 0.10000000149011612);
			tween.TweenProperty(node, "modulate", Colors.White, 0.30000001192092896);
		}

		// Token: 0x06000011 RID: 17 RVA: 0x0000285C File Offset: 0x00000A5C
		internal static async Task AcquireCardWithPreview(CardModel card, PileType pile)
		{
			CardCmd.PreviewCardPileAdd(await CardPileCmd.Add(card, pile, CardPilePosition.Bottom, null, false), 0.8f, CardPreviewStyle.HorizontalLayout);
		}
	}
}
