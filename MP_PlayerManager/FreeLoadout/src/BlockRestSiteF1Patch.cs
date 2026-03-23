using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MP_PlayerManager
{
	// Token: 0x02000025 RID: 37
	[HarmonyPatch(typeof(NRestSiteRoom), "_Input")]
	internal static class BlockRestSiteF1Patch
	{
		// Token: 0x060000AD RID: 173 RVA: 0x00007205 File Offset: 0x00005405
		private static bool Prefix(InputEvent inputEvent)
		{
			return !TrainerState.IsAnyTrainerHotkey(inputEvent);
		}
	}
}
