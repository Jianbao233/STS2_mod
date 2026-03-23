using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager
{
	// Token: 0x02000022 RID: 34
	[HarmonyPatch(typeof(NEventRoom), "Proceed")]
	internal static class EventProceedReturnPatch
	{
		// Token: 0x060000A9 RID: 169 RVA: 0x0000714C File Offset: 0x0000534C
		private static bool Prefix(ref Task __result)
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(40, 1);
			defaultInterpolatedStringHandler.AppendLiteral("[Trainer] Proceed Prefix: IsNestedEvent=");
			defaultInterpolatedStringHandler.AppendFormatted<bool>(TrainerEventState.IsNestedEvent);
			GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
			if (!TrainerEventState.IsNestedEvent)
			{
				return true;
			}
			TrainerEventState.IsNestedEvent = false;
			__result = EventProceedReturnPatch.ReturnToSavedRoom();
			return false;
		}

		// Token: 0x060000AA RID: 170 RVA: 0x000071A0 File Offset: 0x000053A0
		private static async Task ReturnToSavedRoom()
		{
			RoomType? savedRoomType = TrainerEventState.SavedRoomType;
			MapPointType savedPointType = TrainerEventState.SavedPointType;
			ModelId savedModelId = TrainerEventState.SavedModelId;
			TrainerEventState.SavedRoomType = null;
			TrainerEventState.SavedModelId = null;
			if (savedRoomType == null)
			{
				TrainerEventState.SuppressMapHistory = false;
				NMapScreen instance = NMapScreen.Instance;
				if (instance != null)
				{
					instance.SetTravelEnabled(true);
				}
				NMapScreen instance2 = NMapScreen.Instance;
				if (instance2 != null)
				{
					instance2.Open(false);
				}
			}
			else
			{
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(60, 3);
				defaultInterpolatedStringHandler.AppendLiteral("[Trainer] ReturnToSavedRoom: roomType=");
				defaultInterpolatedStringHandler.AppendFormatted<RoomType?>(savedRoomType);
				defaultInterpolatedStringHandler.AppendLiteral(", pointType=");
				defaultInterpolatedStringHandler.AppendFormatted<MapPointType>(savedPointType);
				defaultInterpolatedStringHandler.AppendLiteral(", modelId=");
				defaultInterpolatedStringHandler.AppendFormatted<ModelId>(savedModelId);
				GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
				try
				{
					switch (savedRoomType.Value)
					{
					case RoomType.Monster:
					case RoomType.Elite:
					case RoomType.Boss:
						if (savedModelId != null)
						{
							defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(30, 1);
							defaultInterpolatedStringHandler.AppendLiteral("[Trainer] Re-entering combat: ");
							defaultInterpolatedStringHandler.AppendFormatted<ModelId>(savedModelId);
							GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
							EncounterModel encounterModel = ModelDb.GetById<EncounterModel>(savedModelId).ToMutable();
							await RunManager.Instance.EnterRoomDebug(savedRoomType.Value, savedPointType, encounterModel, true);
							TrainerEventState.SuppressMapHistory = false;
							return;
						}
						break;
					case RoomType.Shop:
						GD.Print("[Trainer] Re-entering shop");
						await RunManager.Instance.EnterRoomDebug(RoomType.Shop, savedPointType, null, true);
						TrainerEventState.SuppressMapHistory = false;
						return;
					case RoomType.Event:
						if (savedModelId != null)
						{
							defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(29, 1);
							defaultInterpolatedStringHandler.AppendLiteral("[Trainer] Re-entering event: ");
							defaultInterpolatedStringHandler.AppendFormatted<ModelId>(savedModelId);
							GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
							EventModel byId = ModelDb.GetById<EventModel>(savedModelId);
							await RunManager.Instance.EnterRoomDebug(RoomType.Event, savedPointType, byId, true);
							TrainerEventState.SuppressMapHistory = false;
							return;
						}
						break;
					case RoomType.RestSite:
						GD.Print("[Trainer] Re-entering rest site");
						await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, savedPointType, null, true);
						TrainerEventState.SuppressMapHistory = false;
						return;
					}
					defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(47, 2);
					defaultInterpolatedStringHandler.AppendLiteral("[Trainer] No match for roomType=");
					defaultInterpolatedStringHandler.AppendFormatted<RoomType>(savedRoomType.Value);
					defaultInterpolatedStringHandler.AppendLiteral(", modelId null=");
					defaultInterpolatedStringHandler.AppendFormatted<bool>(savedModelId == null);
					GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
				}
				catch (Exception ex)
				{
					defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(35, 1);
					defaultInterpolatedStringHandler.AppendLiteral("[Trainer] ReturnToSavedRoom error: ");
					defaultInterpolatedStringHandler.AppendFormatted<Exception>(ex);
					GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
				}
				TrainerEventState.SuppressMapHistory = false;
				NMapScreen instance3 = NMapScreen.Instance;
				if (instance3 != null)
				{
					instance3.SetTravelEnabled(true);
				}
				NMapScreen instance4 = NMapScreen.Instance;
				if (instance4 != null)
				{
					instance4.Open(false);
				}
			}
		}
	}
}
