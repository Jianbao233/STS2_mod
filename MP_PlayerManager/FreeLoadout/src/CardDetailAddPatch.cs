using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager
{
	// Token: 0x02000007 RID: 7
	[HarmonyPatch(typeof(NCardLibrary), "ShowCardDetail")]
	internal static class CardDetailAddPatch
	{
		// Token: 0x06000015 RID: 21 RVA: 0x00002ADC File Offset: 0x00000CDC
		private static bool Prefix(NCardLibrary __instance, NCardHolder holder)
		{
			if (!LoadoutPanel.IsEmbeddedScreenActive)
			{
				return true;
			}
			CardModel cardModel = holder.CardModel;
			if (cardModel == null)
			{
				return true;
			}
			if (!Input.IsKeyPressed(Key.Ctrl))
			{
				try
				{
					FieldInfo gridField = CardDetailAddPatch.GridField;
					object obj = ((gridField != null) ? gridField.GetValue(__instance) : null);
					PropertyInfo propertyInfo = ((obj != null) ? obj.GetType().GetProperty("VisibleCards") : null);
					IEnumerable<CardModel> enumerable = ((propertyInfo != null) ? propertyInfo.GetValue(obj) : null) as IEnumerable<CardModel>;
					List<CardModel> list;
					if ((list = ((enumerable != null) ? enumerable.ToList<CardModel>() : null)) == null)
					{
						(list = new List<CardModel>()).Add(cardModel);
					}
					List<CardModel> list2 = list;
					int num = list2.IndexOf(cardModel);
					if (num < 0)
					{
						list2 = new List<CardModel> { cardModel };
						num = 0;
					}
					bool flag = false;
					try
					{
						FieldInfo viewUpgradesField = CardDetailAddPatch.ViewUpgradesField;
						object obj2 = ((viewUpgradesField != null) ? viewUpgradesField.GetValue(__instance) : null);
						PropertyInfo propertyInfo2 = ((obj2 != null) ? obj2.GetType().GetProperty("IsTicked") : null);
						flag = (bool)(((propertyInfo2 != null) ? propertyInfo2.GetValue(obj2) : null) ?? false);
					}
					catch
					{
					}
					NGame instance = NGame.Instance;
					if (instance != null)
					{
						NInspectCardScreen inspectCardScreen = instance.GetInspectCardScreen();
						if (inspectCardScreen != null)
						{
							inspectCardScreen.Open(list2, num, flag);
						}
					}
				}
				catch
				{
				}
				return false;
			}
			Player player = LoadoutPanel.GetPlayer();
			if (player == null)
			{
				return true;
			}
			RunManager instance2 = RunManager.Instance;
			RunState runState = ((instance2 != null) ? instance2.DebugOnlyGetState() : null);
			if (runState == null)
			{
				return true;
			}
			TaskHelper.RunSafely(UiHelper.AcquireCardWithPreview(runState.CreateCard(cardModel, player), PileType.Deck));
			UiHelper.FlashAcquired(holder);
			return false;
		}

		// Token: 0x0400000B RID: 11
		private static readonly FieldInfo GridField = AccessTools.Field(typeof(NCardLibrary), "_grid");

		// Token: 0x0400000C RID: 12
		private static readonly FieldInfo ViewUpgradesField = AccessTools.Field(typeof(NCardLibrary), "_viewUpgrades");
	}
}
