using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace MP_PlayerManager.Tabs
{
	// Token: 0x0200002E RID: 46
	[NullableContext(1)]
	[Nullable(0)]
	internal static class PowersTab
	{
		// Token: 0x17000021 RID: 33
		// (get) Token: 0x06000124 RID: 292 RVA: 0x0000ED00 File Offset: 0x0000CF00
		private static IEnumerable<PowerModel> AllPowers
		{
			get
			{
				if (PowersTab._allPowers == null)
				{
					PowersTab._allPowers = (from PowerModel p in from p in ModelDb.AllAbstractModelSubtypes.Where((Type t) => t.IsSubclassOf(typeof(PowerModel)) && !t.IsAbstract).Select(delegate(Type t)
							{
								PowerModel powerModel;
								try
								{
									powerModel = ModelDb.DebugPower(t);
								}
								catch
								{
									powerModel = null;
								}
								return powerModel;
							})
							where p != null
							select p
						where !PowersTab.IsMockOrNope(p)
						orderby p.Id.Entry
						select p).ToList<PowerModel>();
				}
				return PowersTab._allPowers;
			}
		}

		// Token: 0x06000125 RID: 293 RVA: 0x0000EDE4 File Offset: 0x0000CFE4
		private static bool IsMockOrNope(PowerModel power)
		{
			if ((power.GetType().Namespace ?? "").Contains("Mock", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			string entry = power.Id.Entry;
			if (entry.Contains("NOPE", StringComparison.OrdinalIgnoreCase) || entry.Contains("MOCK", StringComparison.OrdinalIgnoreCase))
			{
				return true;
			}
			string name = power.GetType().Name;
			return name.Contains("Mock", StringComparison.OrdinalIgnoreCase) || name.Contains("Nope", StringComparison.OrdinalIgnoreCase);
		}

		// Token: 0x06000126 RID: 294 RVA: 0x0000EE6C File Offset: 0x0000D06C
		internal static void Build(VBoxContainer container, Player player)
		{
			CombatManager instance = CombatManager.Instance;
			bool flag = instance != null && instance.IsInProgress;
			PowersTab.BuildPresetsDisplay(container);
			PowersTab.AddDivider(container);
			if (!flag)
			{
				Label label = new Label();
				label.Text = Loc.Get("powers.preset_hint", null);
				label.AddThemeFontSizeOverride("font_size", 12);
				label.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label, false, Node.InternalMode.Disabled);
				List<PowerModel> list = PowersTab.AllPowers.Where((PowerModel p) => p.Type == PowerType.Buff).ToList<PowerModel>();
				List<PowerModel> list2 = PowersTab.AllPowers.Where((PowerModel p) => p.Type != PowerType.Buff).ToList<PowerModel>();
				PowersTab.BuildPowerGroup(container, null, Loc.Get("powers.buffs", null), list, StsColors.green, false);
				PowersTab.BuildPowerGroup(container, null, Loc.Get("powers.debuffs", null), list2, StsColors.red, false);
				return;
			}
			CombatState combatState = CombatManager.Instance.DebugOnlyGetState();
			if (combatState == null)
			{
				return;
			}
			IReadOnlyList<Creature> creatures = combatState.Creatures;
			if (PowersTab._selectedTargetIndex >= creatures.Count)
			{
				PowersTab._selectedTargetIndex = 0;
			}
			container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("powers.target_select", null)), false, Node.InternalMode.Disabled);
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 6);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			bool flag2 = PowersTab._selectedTargetIndex == -1;
			Button button = LoadoutPanel.CreateItemButton(Loc.Get("powers.all_enemies", null), new Vector2?(new Vector2(120f, 32f)), 13);
			if (flag2)
			{
				button.AddThemeColorOverride("font_color", StsColors.gold);
				button.AddThemeStyleboxOverride("normal", PowersTab.CreateSelectedTargetStyle());
			}
			else
			{
				button.AddThemeColorOverride("font_color", StsColors.red);
			}
			button.Pressed += delegate
			{
				PowersTab._selectedTargetIndex = -1;
				LoadoutPanel.RequestRefresh();
			};
			hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			for (int i = 0; i < creatures.Count; i++)
			{
				int idx = i;
				Creature creature = creatures[i];
				Button button2 = LoadoutPanel.CreateItemButton((creature.IsPlayer ? Loc.Get("powers.player_prefix", null) : Loc.Get("powers.enemy_prefix", null)) + creature.Name, new Vector2?(new Vector2(160f, 32f)), 13);
				if (i == PowersTab._selectedTargetIndex)
				{
					button2.AddThemeColorOverride("font_color", StsColors.gold);
					button2.AddThemeStyleboxOverride("normal", PowersTab.CreateSelectedTargetStyle());
				}
				else
				{
					button2.AddThemeColorOverride("font_color", creature.IsPlayer ? StsColors.green : StsColors.red);
				}
				button2.Pressed += delegate
				{
					PowersTab._selectedTargetIndex = idx;
					LoadoutPanel.RequestRefresh();
				};
				hboxContainer.AddChild(button2, false, Node.InternalMode.Disabled);
			}
			List<Creature> list3;
			if (flag2)
			{
				list3 = creatures.Where((Creature c) => !c.IsPlayer).ToList<Creature>();
				Loc.Get("powers.all_enemies", null);
			}
			else
			{
				if (PowersTab._selectedTargetIndex >= creatures.Count)
				{
					PowersTab._selectedTargetIndex = 0;
				}
				Creature creature2 = creatures[PowersTab._selectedTargetIndex];
				list3 = new List<Creature> { creature2 };
				string name = creature2.Name;
			}
			Label label2 = new Label();
			label2.Text = Loc.Get("powers.combat_hint", null);
			label2.AddThemeFontSizeOverride("font_size", 12);
			label2.AddThemeColorOverride("font_color", StsColors.gray);
			container.AddChild(label2, false, Node.InternalMode.Disabled);
			foreach (Creature creature3 in list3)
			{
				List<PowerModel> list4 = creature3.Powers.ToList<PowerModel>();
				container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Fmt("powers.current_status", new object[] { creature3.Name, list4.Count })), false, Node.InternalMode.Disabled);
				if (list4.Count > 0)
				{
					FlowContainer flowContainer = new FlowContainer();
					flowContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
					flowContainer.AddThemeConstantOverride("h_separation", 4);
					flowContainer.AddThemeConstantOverride("v_separation", 4);
					container.AddChild(flowContainer, false, Node.InternalMode.Disabled);
					using (List<PowerModel>.Enumerator enumerator2 = list4.GetEnumerator())
					{
						while (enumerator2.MoveNext())
						{
							PowerModel powerModel = enumerator2.Current;
							flowContainer.AddChild(PowersTab.CreatePowerEntry(powerModel, creature3, true), false, Node.InternalMode.Disabled);
						}
						continue;
					}
				}
				Label label3 = new Label();
				label3.Text = Loc.Get("powers.no_powers", null);
				label3.AddThemeFontSizeOverride("font_size", 14);
				label3.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label3, false, Node.InternalMode.Disabled);
			}
			PowersTab.AddDivider(container);
			Label label4 = new Label();
			label4.Text = Loc.Get("powers.apply_hint", null);
			label4.AddThemeFontSizeOverride("font_size", 12);
			label4.AddThemeColorOverride("font_color", StsColors.gray);
			container.AddChild(label4, false, Node.InternalMode.Disabled);
			List<PowerModel> list5 = PowersTab.AllPowers.Where((PowerModel p) => p.Type == PowerType.Buff).ToList<PowerModel>();
			List<PowerModel> list6 = PowersTab.AllPowers.Where((PowerModel p) => p.Type != PowerType.Buff).ToList<PowerModel>();
			PowersTab.BuildPowerGroup(container, list3, Loc.Get("powers.buffs", null), list5, StsColors.green, true);
			PowersTab.BuildPowerGroup(container, list3, Loc.Get("powers.debuffs", null), list6, StsColors.red, true);
		}

		// Token: 0x06000127 RID: 295 RVA: 0x0000F4B4 File Offset: 0x0000D6B4
		private static void BuildPresetsDisplay(VBoxContainer container)
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 8);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			hboxContainer.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("powers.presets_header", null)), false, Node.InternalMode.Disabled);
			string text = ((PowerPresets.PresetTarget == 0) ? Loc.Get("to_player", null) : Loc.Get("to_enemy", null));
			Label label = new Label();
			label.Text = Loc.Fmt("powers.preset_toggle_hint", new object[] { text });
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", StsColors.gray);
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			if (PowerPresets.PlayerPowers.Count > 0 || PowerPresets.EnemyPowers.Count > 0)
			{
				Button button = LoadoutPanel.CreateItemButton(Loc.Get("clear_all", null), new Vector2?(new Vector2(80f, 28f)), 12);
				button.AddThemeColorOverride("font_color", StsColors.red);
				button.Pressed += delegate
				{
					PowerPresets.PlayerPowers.Clear();
					PowerPresets.EnemyPowers.Clear();
					LoadoutPanel.RequestRefresh();
				};
				hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			PowersTab.BuildPresetList(container, Loc.Get("powers.player_presets", null), StsColors.green, PowerPresets.PlayerPowers);
			PowersTab.BuildPresetList(container, Loc.Get("powers.enemy_presets", null), StsColors.red, PowerPresets.EnemyPowers);
		}

		// Token: 0x06000128 RID: 296 RVA: 0x0000F630 File Offset: 0x0000D830
		private static void BuildPresetList(VBoxContainer container, string title, Color color, Dictionary<Type, int> presets)
		{
			Label label = new Label();
			label.Text = title;
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", color);
			container.AddChild(label, false, Node.InternalMode.Disabled);
			if (presets.Count > 0)
			{
				FlowContainer flowContainer = new FlowContainer();
				flowContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				flowContainer.AddThemeConstantOverride("h_separation", 4);
				flowContainer.AddThemeConstantOverride("v_separation", 4);
				container.AddChild(flowContainer, false, Node.InternalMode.Disabled);
				using (List<KeyValuePair<Type, int>>.Enumerator enumerator = presets.ToList<KeyValuePair<Type, int>>().GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						KeyValuePair<Type, int> keyValuePair = enumerator.Current;
						Type type;
						int num;
						keyValuePair.Deconstruct(out type, out num);
						Type type2 = type;
						int num2 = num;
						flowContainer.AddChild(PowersTab.CreatePresetEntry(type2, num2, presets), false, Node.InternalMode.Disabled);
					}
					return;
				}
			}
			Label label2 = new Label();
			label2.Text = "    " + Loc.Get("empty", null);
			label2.AddThemeFontSizeOverride("font_size", 12);
			label2.AddThemeColorOverride("font_color", StsColors.gray);
			container.AddChild(label2, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000129 RID: 297 RVA: 0x0000F778 File Offset: 0x0000D978
		private static Control CreatePresetEntry(Type powerType, int amount, Dictionary<Type, int> targetDict)
		{
			PowerModel power;
			try
			{
				power = ModelDb.DebugPower(powerType);
			}
			catch
			{
				return new Control();
			}
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 2);
			string text;
			try
			{
				text = power.Title.GetFormattedText();
			}
			catch
			{
				text = power.Id.Entry;
			}
			Color color = ((power.Type == PowerType.Buff) ? StsColors.green : StsColors.red);
			Button btn = new Button();
			Button btn2 = btn;
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 2);
			defaultInterpolatedStringHandler.AppendFormatted(text);
			defaultInterpolatedStringHandler.AppendLiteral(" ×");
			defaultInterpolatedStringHandler.AppendFormatted<int>(amount);
			btn2.Text = defaultInterpolatedStringHandler.ToStringAndClear();
			try
			{
				if (power.Icon != null)
				{
					btn.Icon = power.Icon;
				}
			}
			catch
			{
			}
			btn.ExpandIcon = true;
			btn.CustomMinimumSize = new Vector2(140f, 28f);
			btn.AddThemeFontSizeOverride("font_size", 11);
			btn.AddThemeConstantOverride("icon_max_width", 22);
			btn.AddThemeColorOverride("font_color", color);
			btn.Flat = true;
			btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			btn.GuiInput += delegate(InputEvent ev)
			{
				InputEventMouseButton inputEventMouseButton = ev as InputEventMouseButton;
				if (inputEventMouseButton == null || !inputEventMouseButton.Pressed)
				{
					return;
				}
				if (inputEventMouseButton.ButtonIndex != MouseButton.Left && inputEventMouseButton.ButtonIndex != MouseButton.Right)
				{
					return;
				}
				Viewport viewport = btn.GetViewport();
				if (viewport != null)
				{
					viewport.SetInputAsHandled();
				}
				int num = LoadoutPanel.PowerBatchCount;
				if (inputEventMouseButton.CtrlPressed)
				{
					num *= 5;
				}
				else if (inputEventMouseButton.ShiftPressed)
				{
					num *= 10;
				}
				if (inputEventMouseButton.ButtonIndex == MouseButton.Left)
				{
					targetDict[powerType] = amount + num;
				}
				else
				{
					int num2 = amount - num;
					if (num2 <= 0)
					{
						targetDict.Remove(powerType);
					}
					else
					{
						targetDict[powerType] = num2;
					}
				}
				LoadoutPanel.RequestRefresh();
			};
			hboxContainer.AddChild(btn, false, Node.InternalMode.Disabled);
			btn.MouseEntered += delegate
			{
				try
				{
					IHoverTip hoverTip = HoverTipFactory.FromPower(power);
					LoadoutPanel.ShowHoverTip(btn, hoverTip, HoverTipAlignment.Right);
				}
				catch
				{
				}
			};
			btn.MouseExited += delegate
			{
				NHoverTipSet.Remove(btn);
			};
			Button button = new Button();
			button.Text = "✕";
			button.CustomMinimumSize = new Vector2(20f, 20f);
			button.AddThemeFontSizeOverride("font_size", 10);
			button.AddThemeColorOverride("font_color", StsColors.red);
			button.Flat = true;
			button.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			button.Pressed += delegate
			{
				targetDict.Remove(powerType);
				LoadoutPanel.RequestRefresh();
			};
			hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			return hboxContainer;
		}

		// Token: 0x0600012A RID: 298 RVA: 0x0000FA0C File Offset: 0x0000DC0C
		private static void BuildPowerGroup(VBoxContainer container, [Nullable(new byte[] { 2, 1 })] List<Creature> creatures, string groupName, List<PowerModel> powers, Color color, bool inCombat)
		{
			if (powers.Count == 0)
			{
				return;
			}
			Label label = LoadoutPanel.CreateSectionHeader(groupName);
			label.AddThemeColorOverride("font_color", color);
			container.AddChild(label, false, Node.InternalMode.Disabled);
			FlowContainer flowContainer = new FlowContainer();
			flowContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			flowContainer.AddThemeConstantOverride("h_separation", 4);
			flowContainer.AddThemeConstantOverride("v_separation", 4);
			container.AddChild(flowContainer, false, Node.InternalMode.Disabled);
			foreach (PowerModel powerModel in powers)
			{
				flowContainer.AddChild(PowersTab.CreatePowerEntry(powerModel, creatures, false, inCombat), false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x0600012B RID: 299 RVA: 0x0000FACC File Offset: 0x0000DCCC
		private static Control CreatePowerEntry(PowerModel power, Creature creature, bool isCurrentPower)
		{
			return PowersTab.CreatePowerEntry(power, new List<Creature> { creature }, isCurrentPower, true);
		}

		// Token: 0x0600012C RID: 300 RVA: 0x0000FAE4 File Offset: 0x0000DCE4
		private static Control CreatePowerEntry(PowerModel power, [Nullable(new byte[] { 2, 1 })] List<Creature> creatures, bool isCurrentPower, bool inCombat = true)
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 2);
			string text;
			try
			{
				text = power.Title.GetFormattedText();
			}
			catch
			{
				text = power.Id.Entry;
			}
			Color color = ((power.Type == PowerType.Buff) ? StsColors.green : StsColors.red);
			string text2 = "";
			if (!isCurrentPower)
			{
				Type type = power.GetType();
				bool flag = PowerPresets.PlayerPowers.ContainsKey(type);
				bool flag2 = PowerPresets.EnemyPowers.ContainsKey(type);
				if (flag && flag2)
				{
					text2 = Loc.Get("powers.indicator_both", null);
				}
				else if (flag)
				{
					text2 = Loc.Get("powers.indicator_player", null);
				}
				else if (flag2)
				{
					text2 = Loc.Get("powers.indicator_enemy", null);
				}
			}
			string text3;
			if (!isCurrentPower)
			{
				text3 = text + text2;
			}
			else
			{
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(2, 2);
				defaultInterpolatedStringHandler.AppendFormatted(text);
				defaultInterpolatedStringHandler.AppendLiteral(" ×");
				defaultInterpolatedStringHandler.AppendFormatted<int>(power.Amount);
				text3 = defaultInterpolatedStringHandler.ToStringAndClear();
			}
			string text4 = text3;
			Button btn = new Button();
			btn.Text = text4;
			try
			{
				if (power.Icon != null)
				{
					btn.Icon = power.Icon;
				}
			}
			catch
			{
			}
			btn.ExpandIcon = true;
			btn.AddThemeConstantOverride("icon_max_width", 24);
			btn.CustomMinimumSize = new Vector2((float)(isCurrentPower ? 150 : 130), 30f);
			btn.AddThemeFontSizeOverride("font_size", 12);
			btn.AddThemeColorOverride("font_color", color);
			btn.Flat = true;
			btn.MouseFilter = Control.MouseFilterEnum.Stop;
			btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			Func<PowerModel, bool> <>9__3;
			Func<PowerModel, bool> <>9__4;
			btn.GuiInput += delegate(InputEvent ev)
			{
				InputEventMouseButton inputEventMouseButton = ev as InputEventMouseButton;
				if (inputEventMouseButton == null || !inputEventMouseButton.Pressed)
				{
					return;
				}
				if (inputEventMouseButton.ButtonIndex != MouseButton.Left && inputEventMouseButton.ButtonIndex != MouseButton.Right)
				{
					return;
				}
				Viewport viewport = btn.GetViewport();
				if (viewport != null)
				{
					viewport.SetInputAsHandled();
				}
				int num = LoadoutPanel.PowerBatchCount;
				if (inputEventMouseButton.CtrlPressed)
				{
					num *= 5;
				}
				else if (inputEventMouseButton.ShiftPressed)
				{
					num *= 10;
				}
				bool flag3 = inputEventMouseButton.ButtonIndex == MouseButton.Left;
				if (inCombat && creatures != null && creatures.Count > 0)
				{
					if (flag3)
					{
						if (isCurrentPower && creatures.Count == 1)
						{
							TaskHelper.RunSafely(PowerCmd.ModifyAmount(power, num, null, null, false));
							goto IL_02B8;
						}
						using (List<Creature>.Enumerator enumerator = creatures.GetEnumerator())
						{
							while (enumerator.MoveNext())
							{
								Creature creature = enumerator.Current;
								IEnumerable<PowerModel> powers = creature.Powers;
								Func<PowerModel, bool> func;
								if ((func = <>9__3) == null)
								{
									func = (<>9__3 = (PowerModel p) => p.GetType() == power.GetType());
								}
								PowerModel powerModel = powers.FirstOrDefault(func);
								if (!power.IsInstanced && powerModel != null)
								{
									TaskHelper.RunSafely(PowerCmd.ModifyAmount(powerModel, num, null, null, false));
								}
								else
								{
									TaskHelper.RunSafely(PowerCmd.Apply(power.ToMutable(0), creature, num, null, null, false));
								}
							}
							goto IL_02B8;
						}
					}
					if (isCurrentPower && creatures.Count == 1)
					{
						if (power.Amount <= num)
						{
							TaskHelper.RunSafely(PowerCmd.Remove(power));
							goto IL_02B8;
						}
						TaskHelper.RunSafely(PowerCmd.ModifyAmount(power, -num, null, null, false));
						goto IL_02B8;
					}
					else
					{
						using (List<Creature>.Enumerator enumerator = creatures.GetEnumerator())
						{
							while (enumerator.MoveNext())
							{
								Creature creature2 = enumerator.Current;
								IEnumerable<PowerModel> powers2 = creature2.Powers;
								Func<PowerModel, bool> func2;
								if ((func2 = <>9__4) == null)
								{
									func2 = (<>9__4 = (PowerModel p) => p.GetType() == power.GetType());
								}
								PowerModel powerModel2 = powers2.FirstOrDefault(func2);
								if (powerModel2 != null)
								{
									if (powerModel2.Amount <= num)
									{
										TaskHelper.RunSafely(PowerCmd.Remove(powerModel2));
									}
									else
									{
										TaskHelper.RunSafely(PowerCmd.ModifyAmount(powerModel2, -num, null, null, false));
									}
								}
							}
							goto IL_02B8;
						}
					}
				}
				Dictionary<Type, int> targetPresets = PowerPresets.GetTargetPresets();
				Type type2 = power.GetType();
				int num2;
				if (flag3)
				{
					PowerPresets.AddToPreset(type2, num);
				}
				else if (targetPresets.TryGetValue(type2, out num2))
				{
					int num3 = num2 - num;
					if (num3 <= 0)
					{
						targetPresets.Remove(type2);
					}
					else
					{
						targetPresets[type2] = num3;
					}
				}
				IL_02B8:
				PowersTab.DelayedRefresh(btn);
			};
			btn.MouseEntered += delegate
			{
				try
				{
					IHoverTip hoverTip = HoverTipFactory.FromPower(power);
					LoadoutPanel.ShowHoverTip(btn, hoverTip, HoverTipAlignment.Right);
				}
				catch
				{
				}
			};
			btn.MouseExited += delegate
			{
				NHoverTipSet.Remove(btn);
			};
			hboxContainer.AddChild(btn, false, Node.InternalMode.Disabled);
			if ((isCurrentPower & inCombat) && creatures != null)
			{
				Button removeBtn = new Button();
				removeBtn.Text = "✕";
				removeBtn.CustomMinimumSize = new Vector2(24f, 24f);
				removeBtn.AddThemeFontSizeOverride("font_size", 11);
				removeBtn.AddThemeColorOverride("font_color", StsColors.red);
				removeBtn.Flat = true;
				removeBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
				Func<PowerModel, bool> <>9__6;
				removeBtn.Pressed += delegate
				{
					if (creatures.Count == 1)
					{
						TaskHelper.RunSafely(PowerCmd.Remove(power));
					}
					else
					{
						foreach (Creature creature3 in creatures)
						{
							IEnumerable<PowerModel> powers3 = creature3.Powers;
							Func<PowerModel, bool> func3;
							if ((func3 = <>9__6) == null)
							{
								func3 = (<>9__6 = (PowerModel p) => p.GetType() == power.GetType());
							}
							PowerModel powerModel3 = powers3.FirstOrDefault(func3);
							if (powerModel3 != null)
							{
								TaskHelper.RunSafely(PowerCmd.Remove(powerModel3));
							}
						}
					}
					PowersTab.DelayedRefresh(removeBtn);
				};
				hboxContainer.AddChild(removeBtn, false, Node.InternalMode.Disabled);
			}
			return hboxContainer;
		}

		// Token: 0x0600012D RID: 301 RVA: 0x0000FE54 File Offset: 0x0000E054
		private static StyleBoxFlat CreateSelectedTargetStyle()
		{
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.2f, 0.18f, 0.1f, 0.9f);
			styleBoxFlat.BorderColor = StsColors.gold;
			styleBoxFlat.SetBorderWidthAll(2);
			styleBoxFlat.SetCornerRadiusAll(6);
			styleBoxFlat.SetContentMarginAll(6f);
			return styleBoxFlat;
		}

		// Token: 0x0600012E RID: 302 RVA: 0x0000FEAC File Offset: 0x0000E0AC
		private static void AddDivider(VBoxContainer container)
		{
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 2f),
				Color = new Color(0.91f, 0.86f, 0.75f, 0.15f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
		}

		// Token: 0x0600012F RID: 303 RVA: 0x0000FF05 File Offset: 0x0000E105
		private static void DelayedRefresh(Control node)
		{
			node.GetTree().CreateTimer(0.2, true, false, false).Timeout += delegate
			{
				LoadoutPanel.RequestRefresh();
			};
		}

		// Token: 0x04000073 RID: 115
		[Nullable(new byte[] { 2, 1 })]
		private static List<PowerModel> _allPowers;

		// Token: 0x04000074 RID: 116
		private static int _selectedTargetIndex;
	}
}
