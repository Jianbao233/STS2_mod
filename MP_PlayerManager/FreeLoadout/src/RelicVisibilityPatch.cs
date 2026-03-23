using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace MP_PlayerManager
{
	// Token: 0x0200000A RID: 10
	[HarmonyPatch(typeof(NRelicCollectionEntry), "Create")]
	internal static class RelicVisibilityPatch
	{
		// Token: 0x0600001A RID: 26 RVA: 0x00002D20 File Offset: 0x00000F20
		private static void Prefix(RelicModel relic, ref ModelVisibility visibility)
		{
			if (!LoadoutPanel.IsEmbeddedScreenActive)
			{
				return;
			}
			visibility = ModelVisibility.Visible;
		}
	}
}
