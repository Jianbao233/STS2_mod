using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.PotionLab;

namespace MP_PlayerManager
{
	// Token: 0x0200000B RID: 11
	[HarmonyPatch(typeof(NLabPotionHolder), "Create")]
	internal static class PotionVisibilityPatch
	{
		// Token: 0x0600001B RID: 27 RVA: 0x00002D2D File Offset: 0x00000F2D
		private static void Prefix(PotionModel potion, ref ModelVisibility visibility)
		{
			if (!LoadoutPanel.IsEmbeddedScreenActive)
			{
				return;
			}
			visibility = ModelVisibility.Visible;
		}
	}
}
