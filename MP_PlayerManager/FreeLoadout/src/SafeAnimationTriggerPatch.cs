using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace MP_PlayerManager
{
	// Token: 0x02000024 RID: 36
	[HarmonyPatch(typeof(NCreature), "SetAnimationTrigger")]
	internal static class SafeAnimationTriggerPatch
	{
		// Token: 0x060000AC RID: 172 RVA: 0x000071E5 File Offset: 0x000053E5
		private static Exception Finalizer(Exception __exception)
		{
			if (__exception != null)
			{
				GD.Print("[Trainer] Animation trigger error suppressed: " + __exception.GetType().Name);
			}
			return null;
		}
	}
}
