using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace MP_PlayerManager
{
	// Token: 0x0200000F RID: 15
	[HarmonyPatch(typeof(NInspectRelicScreen), "Open")]
	internal static class InspectRelicOpenPatch
	{
		// Token: 0x0600001F RID: 31 RVA: 0x00002D57 File Offset: 0x00000F57
		private static void Postfix(NInspectRelicScreen __instance)
		{
			if (LoadoutPanel.IsEmbeddedScreenActive)
			{
				InspectRelicEdit.Attach(__instance);
			}
		}
	}
}
