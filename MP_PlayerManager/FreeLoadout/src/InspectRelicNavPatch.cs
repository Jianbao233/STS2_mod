using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace MP_PlayerManager
{
	// Token: 0x02000011 RID: 17
	[HarmonyPatch(typeof(NInspectRelicScreen), "SetRelic")]
	internal static class InspectRelicNavPatch
	{
		// Token: 0x06000021 RID: 33 RVA: 0x00002D6D File Offset: 0x00000F6D
		private static void Postfix()
		{
			InspectRelicEdit.Refresh();
		}
	}
}
