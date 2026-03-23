using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;

namespace MP_PlayerManager
{
	// Token: 0x02000009 RID: 9
	[HarmonyPatch(typeof(NCardLibraryGrid), "GetCardVisibility")]
	internal static class CardVisibilityPatch
	{
		// Token: 0x06000019 RID: 25 RVA: 0x00002D11 File Offset: 0x00000F11
		private static bool Prefix(ref ModelVisibility __result)
		{
			if (!LoadoutPanel.IsEmbeddedScreenActive)
			{
				return true;
			}
			__result = ModelVisibility.Visible;
			return false;
		}
	}
}
