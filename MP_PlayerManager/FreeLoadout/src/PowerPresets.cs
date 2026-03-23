using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;

namespace MP_PlayerManager
{
	// Token: 0x0200001D RID: 29
	internal static class PowerPresets
	{
		// Token: 0x17000012 RID: 18
		// (get) Token: 0x06000093 RID: 147 RVA: 0x00006C9C File Offset: 0x00004E9C
		// (set) Token: 0x06000094 RID: 148 RVA: 0x00006CA3 File Offset: 0x00004EA3
		internal static bool Enabled { get; set; } = true;

		// Token: 0x17000013 RID: 19
		// (get) Token: 0x06000095 RID: 149 RVA: 0x00006CAB File Offset: 0x00004EAB
		// (set) Token: 0x06000096 RID: 150 RVA: 0x00006CB2 File Offset: 0x00004EB2
		internal static int PresetTarget { get; set; }

		// Token: 0x06000097 RID: 151 RVA: 0x00006CBA File Offset: 0x00004EBA
		internal static Dictionary<Type, int> GetTargetPresets()
		{
			if (PowerPresets.PresetTarget != 0)
			{
				return PowerPresets.EnemyPowers;
			}
			return PowerPresets.PlayerPowers;
		}

		// Token: 0x06000098 RID: 152 RVA: 0x00006CD0 File Offset: 0x00004ED0
		internal static void AddToPreset(Type powerType, int amount)
		{
			Dictionary<Type, int> targetPresets = PowerPresets.GetTargetPresets();
			int num;
			if (targetPresets.TryGetValue(powerType, out num))
			{
				targetPresets[powerType] = num + amount;
			}
			else
			{
				targetPresets[powerType] = amount;
			}
			if (targetPresets[powerType] <= 0)
			{
				targetPresets.Remove(powerType);
			}
		}

		// Token: 0x06000099 RID: 153 RVA: 0x00006D14 File Offset: 0x00004F14
		internal static void CheckAndApply()
		{
			if (!PowerPresets.Enabled)
			{
				return;
			}
			if (PowerPresets.PlayerPowers.Count == 0 && PowerPresets.EnemyPowers.Count == 0)
			{
				return;
			}
			CombatManager instance = CombatManager.Instance;
			if (instance == null || !instance.IsInProgress)
			{
				PowerPresets._lastAppliedRound = -1;
				return;
			}
			CombatState combatState = instance.DebugOnlyGetState();
			if (combatState == null)
			{
				return;
			}
			if (combatState.CurrentSide != CombatSide.Player)
			{
				return;
			}
			if (combatState.RoundNumber == PowerPresets._lastAppliedRound)
			{
				return;
			}
			PowerPresets._lastAppliedRound = combatState.RoundNumber;
			foreach (Creature creature in combatState.Creatures)
			{
				if (!creature.IsDead)
				{
					Dictionary<Type, int> dictionary = (creature.IsPlayer ? PowerPresets.PlayerPowers : PowerPresets.EnemyPowers);
					if (dictionary.Count != 0)
					{
						foreach (KeyValuePair<Type, int> keyValuePair in dictionary.ToList<KeyValuePair<Type, int>>())
						{
							Type type;
							int num;
							keyValuePair.Deconstruct(out type, out num);
							Type powerType = type;
							int num2 = num;
							try
							{
								PowerModel powerModel = ModelDb.DebugPower(powerType).ToMutable(0);
								PowerModel powerModel2 = creature.Powers.FirstOrDefault((PowerModel p) => p.GetType() == powerType);
								if (!powerModel.IsInstanced && powerModel2 != null)
								{
									TaskHelper.RunSafely(PowerCmd.ModifyAmount(powerModel2, num2, null, null, false));
								}
								else
								{
									TaskHelper.RunSafely(PowerCmd.Apply(powerModel, creature, num2, null, null, false));
								}
							}
							catch
							{
							}
						}
					}
				}
			}
		}

		// Token: 0x0400003A RID: 58
		internal static readonly Dictionary<Type, int> PlayerPowers = new Dictionary<Type, int>();

		// Token: 0x0400003B RID: 59
		internal static readonly Dictionary<Type, int> EnemyPowers = new Dictionary<Type, int>();

		// Token: 0x0400003E RID: 62
		private static int _lastAppliedRound = -1;
	}
}
