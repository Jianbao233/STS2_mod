using System;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager
{
	// Token: 0x02000023 RID: 35
	[HarmonyPatch(typeof(RunState), "AppendToMapPointHistory")]
	internal static class SuppressMapHistoryPatch
	{
		// Token: 0x060000AB RID: 171 RVA: 0x000071DB File Offset: 0x000053DB
		private static bool Prefix()
		{
			return !TrainerEventState.SuppressMapHistory;
		}
	}
}
