using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace MP_PlayerManager
{
	// Token: 0x02000012 RID: 18
	[HarmonyPatch(typeof(NHoverTipSet))]
	internal static class HoverTipReparentPatch
	{
		// Token: 0x06000022 RID: 34 RVA: 0x00002D74 File Offset: 0x00000F74
		[HarmonyTargetMethods]
		private static IEnumerable<MethodBase> TargetMethods()
		{
			return from m in typeof(NHoverTipSet).GetMethods(BindingFlags.Static | BindingFlags.Public)
				where m.Name == "CreateAndShow" && m.ReturnType == typeof(NHoverTipSet)
				select m;
		}

		// Token: 0x06000023 RID: 35 RVA: 0x00002DAB File Offset: 0x00000FAB
		private static void Postfix(NHoverTipSet __result)
		{
			if (__result == null || !GodotObject.IsInstanceValid(__result))
			{
				return;
			}
			if (!LoadoutPanel.IsOpen)
			{
				return;
			}
			LoadoutPanel.ReparentToHoverTipLayer(__result);
		}
	}
}
