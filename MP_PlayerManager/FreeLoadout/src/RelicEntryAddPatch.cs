using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace MP_PlayerManager
{
	// Token: 0x02000006 RID: 6
	[HarmonyPatch(typeof(NRelicCollectionCategory), "OnRelicEntryPressed")]
	internal static class RelicEntryAddPatch
	{
		// Token: 0x06000014 RID: 20 RVA: 0x00002A78 File Offset: 0x00000C78
		private static bool Prefix(NRelicCollectionEntry entry)
		{
			if (!LoadoutPanel.IsEmbeddedScreenActive)
			{
				return true;
			}
			if (!Input.IsKeyPressed(Key.Ctrl))
			{
				return true;
			}
			Player player = LoadoutPanel.GetPlayer();
			if (player == null)
			{
				return true;
			}
			int relicBatchCount = LoadoutPanel.RelicBatchCount;
			for (int i = 0; i < relicBatchCount; i++)
			{
				TaskHelper.RunSafely(RelicCmd.Obtain(entry.relic.ToMutable(), player, -1));
			}
			UiHelper.FlashAcquired(entry);
			return false;
		}
	}
}
