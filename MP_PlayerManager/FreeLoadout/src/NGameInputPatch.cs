using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager
{
	// Token: 0x0200001E RID: 30
	[HarmonyPatch(typeof(NGame), "_Input")]
	internal static class NGameInputPatch
	{
		// Token: 0x0600009B RID: 155 RVA: 0x00006F24 File Offset: 0x00005124
		private static bool Prefix(NGame __instance, InputEvent inputEvent)
		{
			if (TrainerState.TryTogglePanel(__instance, inputEvent))
			{
				return false;
			}
			if (TrainerState.IsTrainerHotkey(inputEvent, Key.Escape))
			{
				if (PotionEditPanel.IsOpen)
				{
					Viewport viewport = __instance.GetViewport();
					if (viewport != null)
					{
						viewport.SetInputAsHandled();
					}
					PotionEditPanel.Close();
					LoadoutPanel.RequestRefresh();
					return false;
				}
				if (CardEditPanel.IsOpen)
				{
					Viewport viewport2 = __instance.GetViewport();
					if (viewport2 != null)
					{
						viewport2.SetInputAsHandled();
					}
					CardEditPanel.Close();
					LoadoutPanel.RequestRefresh();
					return false;
				}
				if (LoadoutPanel.IsOpen)
				{
					Viewport viewport3 = __instance.GetViewport();
					if (viewport3 != null)
					{
						viewport3.SetInputAsHandled();
					}
					LoadoutPanel.Hide();
					return false;
				}
			}
			return !LoadoutPanel.IsOpen && !CardEditPanel.IsOpen && !PotionEditPanel.IsOpen;
		}
	}
}
