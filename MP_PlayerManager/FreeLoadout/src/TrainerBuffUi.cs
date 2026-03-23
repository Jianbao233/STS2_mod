using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace MP_PlayerManager
{
	// Token: 0x0200001C RID: 28
	internal static class TrainerBuffUi
	{
		// Token: 0x06000086 RID: 134 RVA: 0x00006724 File Offset: 0x00004924
		internal static void Sync(Player player)
		{
			NPowerContainer npowerContainer = TrainerBuffUi.ResolvePowerContainer(player.Creature);
			if (TrainerBuffUi._activeContainer != npowerContainer)
			{
				TrainerBuffUi.DetachAllIndicators();
				TrainerBuffUi._activeContainer = npowerContainer;
				TrainerBuffUi._trackedCreature = player.Creature;
			}
			if (TrainerBuffUi._activeContainer == null)
			{
				return;
			}
			TrainerBuffUi._trackedCreature = player.Creature;
			TrainerBuffUi.SyncIndicator<TrainerHpIndicatorPower>(TrainerState.InfiniteHpEnabled);
			TrainerBuffUi.SyncIndicator<TrainerEnergyIndicatorPower>(TrainerState.InfiniteEnergyEnabled);
			TrainerBuffUi.SyncIndicator<TrainerGoldIndicatorPower>(TrainerState.InfiniteGoldEnabled);
		}

		// Token: 0x06000087 RID: 135 RVA: 0x0000678C File Offset: 0x0000498C
		private static void SyncIndicator<TPower>(bool enabled) where TPower : PowerModel
		{
			if (TrainerBuffUi._activeContainer == null)
			{
				return;
			}
			Type typeFromHandle = typeof(TPower);
			if (!enabled)
			{
				TrainerBuffUi.RemoveIndicator(typeFromHandle);
				return;
			}
			if (TrainerBuffUi._indicatorNodes.ContainsKey(typeFromHandle))
			{
				return;
			}
			Creature trackedCreature = TrainerBuffUi._trackedCreature;
			if (trackedCreature == null)
			{
				return;
			}
			NPower npower = TrainerBuffUi.CreateIndicatorNode<TPower>(trackedCreature);
			TrainerBuffUi.ApplyBuiltinArt(npower, typeFromHandle);
			TrainerBuffUi.GetPowerNodes(TrainerBuffUi._activeContainer).Add(npower);
			TrainerBuffUi._activeContainer.AddChildSafely(npower);
			TrainerBuffUi.RefreshLayout(TrainerBuffUi._activeContainer);
			TrainerBuffUi._indicatorNodes[typeFromHandle] = npower;
		}

		// Token: 0x06000088 RID: 136 RVA: 0x00006810 File Offset: 0x00004A10
		private static NPower CreateIndicatorNode<[Nullable(0)] TPower>(Creature creature) where TPower : PowerModel
		{
			PowerModel powerModel = ModelDb.Power<TPower>().ToMutable(0);
			TrainerBuffUi.PowerOwnerField.SetValue(powerModel, creature);
			TrainerBuffUi.PowerAmountField.SetValue(powerModel, 1);
			NPower npower = NPower.Create(powerModel);
			if (TrainerBuffUi._activeContainer != null)
			{
				npower.Container = TrainerBuffUi._activeContainer;
			}
			return npower;
		}

		// Token: 0x06000089 RID: 137 RVA: 0x00006868 File Offset: 0x00004A68
		[return: Nullable(2)]
		private static NPowerContainer ResolvePowerContainer(Creature creature)
		{
			NCombatRoom instance = NCombatRoom.Instance;
			NCreature ncreature = ((instance != null) ? instance.GetCreatureNode(creature) : null);
			if (ncreature == null)
			{
				return null;
			}
			NCreatureStateDisplay ncreatureStateDisplay = TrainerBuffUi.CreatureStateDisplayField.GetValue(ncreature) as NCreatureStateDisplay;
			if (ncreatureStateDisplay == null)
			{
				return null;
			}
			return TrainerBuffUi.PowerContainerField.GetValue(ncreatureStateDisplay) as NPowerContainer;
		}

		// Token: 0x0600008A RID: 138 RVA: 0x000068B3 File Offset: 0x00004AB3
		private static List<NPower> GetPowerNodes(NPowerContainer container)
		{
			object value = TrainerBuffUi.PowerNodesField.GetValue(container);
			if (value == null)
			{
				throw new InvalidOperationException("Power container node list is null.");
			}
			return (List<NPower>)value;
		}

		// Token: 0x0600008B RID: 139 RVA: 0x000068D4 File Offset: 0x00004AD4
		private static void RemoveIndicator(Type powerType)
		{
			NPower npower;
			if (TrainerBuffUi._activeContainer == null || !TrainerBuffUi._indicatorNodes.Remove(powerType, out npower))
			{
				return;
			}
			TrainerBuffUi.GetPowerNodes(TrainerBuffUi._activeContainer).Remove(npower);
			TrainerBuffUi._activeContainer.RemoveChildSafely(npower);
			npower.QueueFreeSafely();
			TrainerBuffUi.RefreshLayout(TrainerBuffUi._activeContainer);
		}

		// Token: 0x0600008C RID: 140 RVA: 0x00006924 File Offset: 0x00004B24
		private static void DetachAllIndicators()
		{
			if (TrainerBuffUi._activeContainer != null && GodotObject.IsInstanceValid(TrainerBuffUi._activeContainer))
			{
				foreach (Type type in TrainerBuffUi._indicatorNodes.Keys.ToList<Type>())
				{
					TrainerBuffUi.RemoveIndicator(type);
				}
			}
			TrainerBuffUi._indicatorNodes.Clear();
			TrainerBuffUi._activeContainer = null;
			TrainerBuffUi._trackedCreature = null;
		}

		// Token: 0x0600008D RID: 141 RVA: 0x000069A8 File Offset: 0x00004BA8
		private static void RefreshLayout(NPowerContainer container)
		{
			container.Call("UpdatePositions", Array.Empty<Variant>());
		}

		// Token: 0x0600008E RID: 142 RVA: 0x000069C0 File Offset: 0x00004BC0
		internal static IEnumerable<IHoverTip> GetTrainerHoverTips(Creature creature)
		{
			if (!LocalContext.IsMe(creature))
			{
				return Array.Empty<IHoverTip>();
			}
			List<IHoverTip> list = new List<IHoverTip>();
			TrainerBuffUi.AddHoverTipIfEnabled<TrainerHpIndicatorPower>(list, TrainerState.InfiniteHpEnabled);
			TrainerBuffUi.AddHoverTipIfEnabled<TrainerEnergyIndicatorPower>(list, TrainerState.InfiniteEnergyEnabled);
			TrainerBuffUi.AddHoverTipIfEnabled<TrainerGoldIndicatorPower>(list, TrainerState.InfiniteGoldEnabled);
			return list;
		}

		// Token: 0x0600008F RID: 143 RVA: 0x000069F8 File Offset: 0x00004BF8
		private static void AddHoverTipIfEnabled<[Nullable(0)] TPower>(List<IHoverTip> tips, bool enabled) where TPower : PowerModel
		{
			if (!enabled)
			{
				return;
			}
			Type typeFromHandle = typeof(TPower);
			string entry = ModelDb.GetId(typeFromHandle).Entry;
			LocString locString = new LocString("powers", entry + ".title");
			LocString locString2 = new LocString("powers", entry + ".description");
			tips.Add(new HoverTip(locString, locString2, TrainerBuffUi.GetBuiltinIcon(typeFromHandle).Item1));
		}

		// Token: 0x06000090 RID: 144 RVA: 0x00006A6C File Offset: 0x00004C6C
		private static void ApplyBuiltinArt(NPower node, Type trainerPowerType)
		{
			ValueTuple<Texture2D, Texture2D> builtinIcon = TrainerBuffUi.GetBuiltinIcon(trainerPowerType);
			Texture2D item = builtinIcon.Item1;
			Texture2D item2 = builtinIcon.Item2;
			TextureRect nodeOrNull = node.GetNodeOrNull<TextureRect>("%Icon");
			if (nodeOrNull != null)
			{
				nodeOrNull.Texture = item;
			}
			CpuParticles2D nodeOrNull2 = node.GetNodeOrNull<CpuParticles2D>("%PowerFlash");
			if (nodeOrNull2 != null)
			{
				nodeOrNull2.Texture = item2;
			}
		}

		// Token: 0x06000091 RID: 145 RVA: 0x00006AC4 File Offset: 0x00004CC4
		[return: Nullable(new byte[] { 0, 1, 1 })]
		private static ValueTuple<Texture2D, Texture2D> GetBuiltinIcon(Type trainerPowerType)
		{
			Type type;
			if (!TrainerBuffUi._iconPowerMap.TryGetValue(trainerPowerType, out type))
			{
				throw new InvalidOperationException("No builtin icon mapping configured for " + trainerPowerType.Name + ".");
			}
			PowerModel powerModel = ModelDb.DebugPower(type);
			return new ValueTuple<Texture2D, Texture2D>(powerModel.Icon, powerModel.BigIcon);
		}

		// Token: 0x06000092 RID: 146 RVA: 0x00006B14 File Offset: 0x00004D14
		// Note: this type is marked as 'beforefieldinit'.
		static TrainerBuffUi()
		{
			FieldInfo fieldInfo = AccessTools.Field(typeof(NCreature), "_stateDisplay");
			if (fieldInfo == null)
			{
				throw new MissingFieldException(typeof(NCreature).FullName, "_stateDisplay");
			}
			TrainerBuffUi.CreatureStateDisplayField = fieldInfo;
			FieldInfo fieldInfo2 = AccessTools.Field(typeof(NCreatureStateDisplay), "_powerContainer");
			if (fieldInfo2 == null)
			{
				throw new MissingFieldException(typeof(NCreatureStateDisplay).FullName, "_powerContainer");
			}
			TrainerBuffUi.PowerContainerField = fieldInfo2;
			FieldInfo fieldInfo3 = AccessTools.Field(typeof(NPowerContainer), "_powerNodes");
			if (fieldInfo3 == null)
			{
				throw new MissingFieldException(typeof(NPowerContainer).FullName, "_powerNodes");
			}
			TrainerBuffUi.PowerNodesField = fieldInfo3;
			FieldInfo fieldInfo4 = AccessTools.Field(typeof(PowerModel), "_owner");
			if (fieldInfo4 == null)
			{
				throw new MissingFieldException(typeof(PowerModel).FullName, "_owner");
			}
			TrainerBuffUi.PowerOwnerField = fieldInfo4;
			FieldInfo fieldInfo5 = AccessTools.Field(typeof(PowerModel), "_amount");
			if (fieldInfo5 == null)
			{
				throw new MissingFieldException(typeof(PowerModel).FullName, "_amount");
			}
			TrainerBuffUi.PowerAmountField = fieldInfo5;
			TrainerBuffUi._indicatorNodes = new Dictionary<Type, NPower>();
			Dictionary<Type, Type> dictionary = new Dictionary<Type, Type>();
			Type typeFromHandle = typeof(TrainerHpIndicatorPower);
			dictionary[typeFromHandle] = typeof(RegenPower);
			Type typeFromHandle2 = typeof(TrainerEnergyIndicatorPower);
			dictionary[typeFromHandle2] = typeof(EnergyNextTurnPower);
			Type typeFromHandle3 = typeof(TrainerGoldIndicatorPower);
			dictionary[typeFromHandle3] = typeof(TrashToTreasurePower);
			TrainerBuffUi._iconPowerMap = dictionary;
		}

		// Token: 0x04000031 RID: 49
		private static readonly FieldInfo CreatureStateDisplayField;

		// Token: 0x04000032 RID: 50
		private static readonly FieldInfo PowerContainerField;

		// Token: 0x04000033 RID: 51
		private static readonly FieldInfo PowerNodesField;

		// Token: 0x04000034 RID: 52
		private static readonly FieldInfo PowerOwnerField;

		// Token: 0x04000035 RID: 53
		private static readonly FieldInfo PowerAmountField;

		// Token: 0x04000036 RID: 54
		private static NPowerContainer _activeContainer;

		// Token: 0x04000037 RID: 55
		private static Creature _trackedCreature;

		// Token: 0x04000038 RID: 56
		private static readonly Dictionary<Type, NPower> _indicatorNodes;

		// Token: 0x04000039 RID: 57
		private static readonly Dictionary<Type, Type> _iconPowerMap;
	}
}
