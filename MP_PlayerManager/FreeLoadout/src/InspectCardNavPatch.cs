using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace MP_PlayerManager
{
	// Token: 0x0200000E RID: 14
	[HarmonyPatch(typeof(NInspectCardScreen), "SetCard")]
	internal static class InspectCardNavPatch
	{
		// Token: 0x0600001E RID: 30 RVA: 0x00002D50 File Offset: 0x00000F50
		private static void Postfix()
		{
			InspectCardEdit.Refresh();
		}
	}
}
