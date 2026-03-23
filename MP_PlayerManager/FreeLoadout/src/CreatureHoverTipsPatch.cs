using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;

namespace MP_PlayerManager
{
	// Token: 0x0200001F RID: 31
	[HarmonyPatch(typeof(Creature), "get_HoverTips")]
	internal static class CreatureHoverTipsPatch
	{
		// Token: 0x0600009C RID: 156 RVA: 0x00006FCC File Offset: 0x000051CC
		private static void Postfix(Creature __instance, ref IEnumerable<IHoverTip> __result)
		{
			IEnumerable<IHoverTip> trainerHoverTips = TrainerBuffUi.GetTrainerHoverTips(__instance);
			if (!trainerHoverTips.Any<IHoverTip>())
			{
				return;
			}
			__result = __result.Concat(trainerHoverTips);
		}
	}
}
