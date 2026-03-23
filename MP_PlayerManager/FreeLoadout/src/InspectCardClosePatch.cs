using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace MP_PlayerManager
{
	// Token: 0x0200000D RID: 13
	[HarmonyPatch(typeof(NInspectCardScreen), "Close")]
	internal static class InspectCardClosePatch
	{
		// Token: 0x0600001D RID: 29 RVA: 0x00002D49 File Offset: 0x00000F49
		private static void Postfix()
		{
			InspectCardEdit.Detach();
		}
	}
}
