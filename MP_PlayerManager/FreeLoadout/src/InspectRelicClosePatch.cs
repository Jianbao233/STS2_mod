using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace MP_PlayerManager
{
	// Token: 0x02000010 RID: 16
	[HarmonyPatch(typeof(NInspectRelicScreen), "Close")]
	internal static class InspectRelicClosePatch
	{
		// Token: 0x06000020 RID: 32 RVA: 0x00002D66 File Offset: 0x00000F66
		private static void Postfix()
		{
			InspectRelicEdit.Detach();
		}
	}
}
