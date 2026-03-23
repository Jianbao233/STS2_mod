using System;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace MP_PlayerManager
{
	// Token: 0x02000021 RID: 33
	internal static class TrainerEventState
	{
		// Token: 0x17000014 RID: 20
		// (get) Token: 0x0600009E RID: 158 RVA: 0x00007000 File Offset: 0x00005200
		// (set) Token: 0x0600009F RID: 159 RVA: 0x00007007 File Offset: 0x00005207
		internal static bool IsNestedEvent { get; set; }

		// Token: 0x17000015 RID: 21
		// (get) Token: 0x060000A0 RID: 160 RVA: 0x0000700F File Offset: 0x0000520F
		// (set) Token: 0x060000A1 RID: 161 RVA: 0x00007016 File Offset: 0x00005216
		internal static bool SuppressMapHistory { get; set; }

		// Token: 0x17000016 RID: 22
		// (get) Token: 0x060000A2 RID: 162 RVA: 0x0000701E File Offset: 0x0000521E
		// (set) Token: 0x060000A3 RID: 163 RVA: 0x00007025 File Offset: 0x00005225
		internal static RoomType? SavedRoomType { get; set; }

		// Token: 0x17000017 RID: 23
		// (get) Token: 0x060000A4 RID: 164 RVA: 0x0000702D File Offset: 0x0000522D
		// (set) Token: 0x060000A5 RID: 165 RVA: 0x00007034 File Offset: 0x00005234
		internal static MapPointType SavedPointType { get; set; }

		// Token: 0x17000018 RID: 24
		// (get) Token: 0x060000A6 RID: 166 RVA: 0x0000703C File Offset: 0x0000523C
		// (set) Token: 0x060000A7 RID: 167 RVA: 0x00007043 File Offset: 0x00005243
		internal static ModelId SavedModelId { get; set; }

		// Token: 0x060000A8 RID: 168 RVA: 0x0000704C File Offset: 0x0000524C
		internal static void SaveCurrentRoom()
		{
			RunManager instance = RunManager.Instance;
			RunState runState = ((instance != null) ? instance.DebugOnlyGetState() : null);
			AbstractRoom abstractRoom = ((runState != null) ? runState.CurrentRoom : null);
			if (abstractRoom != null)
			{
				TrainerEventState.SavedRoomType = new RoomType?(abstractRoom.RoomType);
				MapPointHistoryEntry currentMapPointHistoryEntry = runState.CurrentMapPointHistoryEntry;
				TrainerEventState.SavedPointType = ((currentMapPointHistoryEntry != null) ? currentMapPointHistoryEntry.MapPointType : MapPointType.Unknown);
				TrainerEventState.SavedModelId = abstractRoom.ModelId;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(57, 4);
				defaultInterpolatedStringHandler.AppendLiteral("[Trainer] SaveCurrentRoom: type=");
				defaultInterpolatedStringHandler.AppendFormatted<RoomType?>(TrainerEventState.SavedRoomType);
				defaultInterpolatedStringHandler.AppendLiteral(", point=");
				defaultInterpolatedStringHandler.AppendFormatted<MapPointType>(TrainerEventState.SavedPointType);
				defaultInterpolatedStringHandler.AppendLiteral(", modelId=");
				defaultInterpolatedStringHandler.AppendFormatted<ModelId>(TrainerEventState.SavedModelId);
				defaultInterpolatedStringHandler.AppendLiteral(", room=");
				defaultInterpolatedStringHandler.AppendFormatted(abstractRoom.GetType().Name);
				GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
				return;
			}
			TrainerEventState.SavedRoomType = null;
			TrainerEventState.SavedModelId = null;
			GD.Print("[Trainer] SaveCurrentRoom: room is null");
		}
	}
}
