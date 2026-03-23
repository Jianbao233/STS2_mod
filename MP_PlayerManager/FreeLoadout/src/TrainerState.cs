using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager
{
	// Token: 0x0200001B RID: 27
	internal static class TrainerState
	{
		// Token: 0x17000008 RID: 8
		// (get) Token: 0x06000064 RID: 100 RVA: 0x000062E6 File Offset: 0x000044E6
		// (set) Token: 0x06000065 RID: 101 RVA: 0x000062ED File Offset: 0x000044ED
		internal static int CurrentHpLockValue { get; set; } = 99;

		// Token: 0x17000009 RID: 9
		// (get) Token: 0x06000066 RID: 102 RVA: 0x000062F5 File Offset: 0x000044F5
		// (set) Token: 0x06000067 RID: 103 RVA: 0x000062FC File Offset: 0x000044FC
		internal static int MaxHpLockValue { get; set; } = 99;

		// Token: 0x1700000A RID: 10
		// (get) Token: 0x06000068 RID: 104 RVA: 0x00006304 File Offset: 0x00004504
		// (set) Token: 0x06000069 RID: 105 RVA: 0x0000630B File Offset: 0x0000450B
		internal static int CurrentEnergyLockValue { get; set; } = 10;

		// Token: 0x1700000B RID: 11
		// (get) Token: 0x0600006A RID: 106 RVA: 0x00006313 File Offset: 0x00004513
		// (set) Token: 0x0600006B RID: 107 RVA: 0x0000631A File Offset: 0x0000451A
		internal static int MaxEnergyLockValue { get; set; } = 10;

		// Token: 0x1700000C RID: 12
		// (get) Token: 0x0600006C RID: 108 RVA: 0x00006322 File Offset: 0x00004522
		// (set) Token: 0x0600006D RID: 109 RVA: 0x00006329 File Offset: 0x00004529
		internal static int StarsLockValue { get; set; } = 99;

		// Token: 0x1700000D RID: 13
		// (get) Token: 0x0600006E RID: 110 RVA: 0x00006331 File Offset: 0x00004531
		// (set) Token: 0x0600006F RID: 111 RVA: 0x00006338 File Offset: 0x00004538
		internal static int GoldLockValue { get; set; } = 999;

		// Token: 0x1700000E RID: 14
		// (get) Token: 0x06000070 RID: 112 RVA: 0x00006340 File Offset: 0x00004540
		// (set) Token: 0x06000071 RID: 113 RVA: 0x00006347 File Offset: 0x00004547
		internal static bool InfiniteHpEnabled { get; private set; }

		// Token: 0x1700000F RID: 15
		// (get) Token: 0x06000072 RID: 114 RVA: 0x0000634F File Offset: 0x0000454F
		// (set) Token: 0x06000073 RID: 115 RVA: 0x00006356 File Offset: 0x00004556
		internal static bool InfiniteEnergyEnabled { get; private set; }

		// Token: 0x17000010 RID: 16
		// (get) Token: 0x06000074 RID: 116 RVA: 0x0000635E File Offset: 0x0000455E
		// (set) Token: 0x06000075 RID: 117 RVA: 0x00006365 File Offset: 0x00004565
		internal static bool InfiniteStarsEnabled { get; private set; }

		// Token: 0x17000011 RID: 17
		// (get) Token: 0x06000076 RID: 118 RVA: 0x0000636D File Offset: 0x0000456D
		// (set) Token: 0x06000077 RID: 119 RVA: 0x00006374 File Offset: 0x00004574
		internal static bool InfiniteGoldEnabled { get; private set; }

		// Token: 0x06000078 RID: 120 RVA: 0x0000637C File Offset: 0x0000457C
		internal static bool IsTrainerHotkey(InputEvent inputEvent, Key key)
		{
			InputEventKey inputEventKey = inputEvent as InputEventKey;
			if (inputEventKey != null && inputEventKey.Pressed && !inputEventKey.Echo)
			{
				Key keycode = inputEventKey.Keycode;
				return keycode == key;
			}
			return false;
		}

		// Token: 0x06000079 RID: 121 RVA: 0x000063B0 File Offset: 0x000045B0
		internal static bool IsTrainerHotkey(InputEvent inputEvent, HotkeyBinding binding)
		{
			InputEventKey inputEventKey = inputEvent as InputEventKey;
			return inputEventKey != null && inputEventKey.Pressed && !inputEventKey.Echo && binding.Matches(inputEventKey);
		}

		// Token: 0x0600007A RID: 122 RVA: 0x000063E4 File Offset: 0x000045E4
		internal static bool IsAnyTrainerHotkey(InputEvent inputEvent)
		{
			InputEventKey inputEventKey = inputEvent as InputEventKey;
			return inputEventKey != null && inputEventKey.Pressed && !inputEventKey.Echo && Config.MatchesAny(inputEventKey);
		}

		// Token: 0x0600007B RID: 123 RVA: 0x00006413 File Offset: 0x00004613
		internal static void ToggleInfiniteHp()
		{
			TrainerState.InfiniteHpEnabled = !TrainerState.InfiniteHpEnabled;
		}

		// Token: 0x0600007C RID: 124 RVA: 0x00006422 File Offset: 0x00004622
		internal static void ToggleInfiniteEnergy()
		{
			TrainerState.InfiniteEnergyEnabled = !TrainerState.InfiniteEnergyEnabled;
		}

		// Token: 0x0600007D RID: 125 RVA: 0x00006431 File Offset: 0x00004631
		internal static void ToggleInfiniteStars()
		{
			TrainerState.InfiniteStarsEnabled = !TrainerState.InfiniteStarsEnabled;
		}

		// Token: 0x0600007E RID: 126 RVA: 0x00006440 File Offset: 0x00004640
		internal static void ToggleInfiniteGold()
		{
			TrainerState.InfiniteGoldEnabled = !TrainerState.InfiniteGoldEnabled;
		}

		// Token: 0x0600007F RID: 127 RVA: 0x0000644F File Offset: 0x0000464F
		internal static void SetHpOnce(Player player, int currentHp, int maxHp)
		{
			maxHp = Math.Max(1, maxHp);
			currentHp = Math.Clamp(currentHp, 1, maxHp);
			player.Creature.SetMaxHpInternal(maxHp);
			player.Creature.SetCurrentHpInternal(currentHp);
		}

		// Token: 0x06000080 RID: 128 RVA: 0x00006486 File Offset: 0x00004686
		internal static void SetEnergyOnce(Player player, int currentEnergy, int maxEnergy)
		{
			player.MaxEnergy = Math.Max(0, maxEnergy);
			if (player.PlayerCombatState != null)
			{
				player.PlayerCombatState.Energy = Math.Max(0, currentEnergy);
			}
		}

		// Token: 0x06000081 RID: 129 RVA: 0x000064AF File Offset: 0x000046AF
		internal static void SetStarsOnce(Player player, int stars)
		{
			if (player.PlayerCombatState != null)
			{
				player.PlayerCombatState.Stars = Math.Max(0, stars);
			}
		}

		// Token: 0x06000082 RID: 130 RVA: 0x000064CB File Offset: 0x000046CB
		internal static void SetGoldOnce(Player player, int gold)
		{
			player.Gold = Math.Max(0, gold);
		}

		// Token: 0x06000083 RID: 131 RVA: 0x000064DA File Offset: 0x000046DA
		internal static bool TryTogglePanel(NGame game, InputEvent inputEvent)
		{
			if (!TrainerState.IsTrainerHotkey(inputEvent, Config.GetHotkey("toggle_panel")))
			{
				return false;
			}
			Viewport viewport = game.GetViewport();
			if (viewport != null)
			{
				viewport.SetInputAsHandled();
			}
			LoadoutPanel.Toggle();
			return true;
		}

		// Token: 0x06000084 RID: 132 RVA: 0x00006508 File Offset: 0x00004708
		internal static void Apply(NRun run)
		{
			if (!TrainerState.InfiniteHpEnabled && !TrainerState.InfiniteEnergyEnabled && !TrainerState.InfiniteStarsEnabled && !TrainerState.InfiniteGoldEnabled)
			{
				return;
			}
			RunState runState = TrainerState.RunStateField.GetValue(run) as RunState;
			if (runState == null)
			{
				return;
			}
			foreach (Player player in runState.Players)
			{
				if (TrainerState.InfiniteHpEnabled)
				{
					int num = Math.Max(1, TrainerState.MaxHpLockValue);
					if (player.Creature.MaxHp != num)
					{
						player.Creature.SetMaxHpInternal(num);
					}
					int num2 = Math.Clamp(TrainerState.CurrentHpLockValue, 1, num);
					if (player.Creature.CurrentHp != num2)
					{
						player.Creature.SetCurrentHpInternal(num2);
					}
				}
				if (TrainerState.InfiniteEnergyEnabled)
				{
					if (player.MaxEnergy != TrainerState.MaxEnergyLockValue)
					{
						player.MaxEnergy = TrainerState.MaxEnergyLockValue;
					}
					if (player.PlayerCombatState != null && player.PlayerCombatState.Energy != TrainerState.CurrentEnergyLockValue)
					{
						player.PlayerCombatState.Energy = TrainerState.CurrentEnergyLockValue;
					}
				}
				if (TrainerState.InfiniteStarsEnabled && player.PlayerCombatState != null && player.PlayerCombatState.Stars != TrainerState.StarsLockValue)
				{
					player.PlayerCombatState.Stars = TrainerState.StarsLockValue;
				}
				if (TrainerState.InfiniteGoldEnabled && player.Gold != TrainerState.GoldLockValue)
				{
					player.Gold = TrainerState.GoldLockValue;
				}
			}
			Player player2 = LocalContext.GetMe(runState) ?? runState.Players.FirstOrDefault<Player>();
			if (player2 != null)
			{
				TrainerBuffUi.Sync(player2);
			}
		}

		// Token: 0x06000085 RID: 133 RVA: 0x000066B0 File Offset: 0x000048B0
		// Note: this type is marked as 'beforefieldinit'.
		static TrainerState()
		{
			FieldInfo fieldInfo = AccessTools.Field(typeof(NRun), "_state");
			if (fieldInfo == null)
			{
				throw new MissingFieldException(typeof(NRun).FullName, "_state");
			}
			TrainerState.RunStateField = fieldInfo;
		}

		// Token: 0x0400002C RID: 44
		private static readonly FieldInfo RunStateField;
	}
}
