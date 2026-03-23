using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.PotionLab;

namespace MP_PlayerManager
{
	// Token: 0x02000008 RID: 8
	[HarmonyPatch(typeof(NLabPotionHolder), "_Ready")]
	internal static class PotionLabClickPatch
	{
		// Token: 0x06000017 RID: 23 RVA: 0x00002C90 File Offset: 0x00000E90
		private static void Postfix(NLabPotionHolder __instance)
		{
			if (__instance.MouseFilter == Control.MouseFilterEnum.Ignore)
			{
				__instance.MouseFilter = Control.MouseFilterEnum.Pass;
			}
			__instance.GuiInput += delegate(InputEvent ev)
			{
				if (!LoadoutPanel.IsEmbeddedScreenActive)
				{
					return;
				}
				InputEventMouseButton inputEventMouseButton = ev as InputEventMouseButton;
				if (inputEventMouseButton == null || !inputEventMouseButton.Pressed || inputEventMouseButton.ButtonIndex != MouseButton.Left)
				{
					return;
				}
				Viewport viewport = __instance.GetViewport();
				if (viewport != null)
				{
					viewport.SetInputAsHandled();
				}
				Player player = LoadoutPanel.GetPlayer();
				if (player == null)
				{
					return;
				}
				if (PotionLabClickPatch.ModelField == null)
				{
					return;
				}
				PotionModel potionModel = PotionLabClickPatch.ModelField.GetValue(__instance) as PotionModel;
				if (potionModel == null)
				{
					return;
				}
				if (Input.IsKeyPressed(Key.Ctrl))
				{
					try
					{
						PotionModel potionModel2 = (PotionModel)potionModel.MutableClone();
						if (!player.AddPotionInternal(potionModel2, -1, false).success)
						{
							FieldInfo potionSlotsField = PotionLabClickPatch.PotionSlotsField;
							List<PotionModel> list = ((potionSlotsField != null) ? potionSlotsField.GetValue(player) : null) as List<PotionModel>;
							if (list != null)
							{
								int num = list.FindIndex((PotionModel p) => p != null);
								if (num >= 0)
								{
									TaskHelper.RunSafely(PotionCmd.Discard(list[num]));
									potionModel2 = (PotionModel)potionModel.MutableClone();
									player.AddPotionInternal(potionModel2, num, false);
								}
							}
						}
					}
					catch (Exception ex)
					{
						GD.PrintErr("[FreeLoadout] Potion obtain failed: " + ex.Message);
					}
					UiHelper.FlashAcquired(__instance);
					return;
				}
				InspectPotionEdit.Open(potionModel, __instance);
			};
		}

		// Token: 0x0400000D RID: 13
		private static readonly FieldInfo ModelField = AccessTools.Field(typeof(NLabPotionHolder), "_model");

		// Token: 0x0400000E RID: 14
		private static readonly FieldInfo PotionSlotsField = AccessTools.Field(typeof(Player), "_potionSlots");
	}
}
