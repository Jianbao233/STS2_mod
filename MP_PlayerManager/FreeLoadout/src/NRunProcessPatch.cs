using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager
{
	// Token: 0x02000020 RID: 32
	[HarmonyPatch(typeof(NRun), "_Process")]
	internal static class NRunProcessPatch
	{
		// Token: 0x0600009D RID: 157 RVA: 0x00006FF3 File Offset: 0x000051F3
		private static void Postfix(NRun __instance)
		{
			TrainerState.Apply(__instance);
			PowerPresets.CheckAndApply();
		}
	}
}
