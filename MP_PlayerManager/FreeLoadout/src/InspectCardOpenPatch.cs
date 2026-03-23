using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace MP_PlayerManager
{
	// Token: 0x0200000C RID: 12
	[HarmonyPatch(typeof(NInspectCardScreen), "Open")]
	internal static class InspectCardOpenPatch
	{
		// Token: 0x0600001C RID: 28 RVA: 0x00002D3A File Offset: 0x00000F3A
		private static void Postfix(NInspectCardScreen __instance)
		{
			if (LoadoutPanel.IsOpen)
			{
				InspectCardEdit.Attach(__instance);
			}
		}
	}
}
