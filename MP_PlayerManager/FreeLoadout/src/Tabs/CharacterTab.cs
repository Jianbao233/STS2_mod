using System;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;

namespace MP_PlayerManager.Tabs
{
	// Token: 0x02000031 RID: 49
	[NullableContext(1)]
	[Nullable(0)]
	internal static class CharacterTab
	{
		// Token: 0x0600014A RID: 330 RVA: 0x000125CC File Offset: 0x000107CC
		internal static void Build(VBoxContainer container, Player player)
		{
			container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("char.hp_section", null)), false, Node.InternalMode.Disabled);
			int num = (TrainerState.InfiniteHpEnabled ? TrainerState.CurrentHpLockValue : player.Creature.CurrentHp);
			int num2 = (TrainerState.InfiniteHpEnabled ? TrainerState.MaxHpLockValue : player.Creature.MaxHp);
			GridContainer gridContainer = CharacterTab.CreateInputGrid();
			container.AddChild(gridContainer, false, Node.InternalMode.Disabled);
			CharacterTab.AddGridLabel(gridContainer, Loc.Get("char.current_hp", null));
			SpinBox curHpSpin = CharacterTab.AddGridSpinBox(gridContainer, num, 1, 9999);
			CharacterTab.AddGridLabel(gridContainer, Loc.Get("char.max_hp", null));
			SpinBox maxHpSpin = CharacterTab.AddGridSpinBox(gridContainer, num2, 1, 9999);
			HBoxContainer hboxContainer = CharacterTab.CreateButtonRow();
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			Button button = LoadoutPanel.CreateActionButton(Loc.Get("char.set", null), null);
			button.Pressed += delegate
			{
				int num8 = (int)curHpSpin.Value;
				int num9 = (int)maxHpSpin.Value;
				TrainerState.CurrentHpLockValue = Math.Max(1, num8);
				TrainerState.MaxHpLockValue = Math.Max(1, num9);
				if (!TrainerState.InfiniteHpEnabled)
				{
					TrainerState.SetHpOnce(player, num8, num9);
				}
			};
			hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			Button lockHpBtn = LoadoutPanel.CreateToggleButton(Loc.Get("char.lock", null), TrainerState.InfiniteHpEnabled);
			lockHpBtn.Pressed += delegate
			{
				if (!TrainerState.InfiniteHpEnabled)
				{
					TrainerState.CurrentHpLockValue = Math.Max(1, (int)curHpSpin.Value);
					TrainerState.MaxHpLockValue = Math.Max(1, (int)maxHpSpin.Value);
				}
				TrainerState.ToggleInfiniteHp();
				LoadoutPanel.UpdateToggleButton(lockHpBtn, TrainerState.InfiniteHpEnabled);
			};
			hboxContainer.AddChild(lockHpBtn, false, Node.InternalMode.Disabled);
			CharacterTab.AddSeparator(container);
			container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("char.energy_section", null)), false, Node.InternalMode.Disabled);
			int num3;
			if (!TrainerState.InfiniteEnergyEnabled)
			{
				PlayerCombatState playerCombatState = player.PlayerCombatState;
				num3 = ((playerCombatState != null) ? playerCombatState.Energy : 0);
			}
			else
			{
				num3 = TrainerState.CurrentEnergyLockValue;
			}
			int num4 = num3;
			int num5 = (TrainerState.InfiniteEnergyEnabled ? TrainerState.MaxEnergyLockValue : player.MaxEnergy);
			GridContainer gridContainer2 = CharacterTab.CreateInputGrid();
			container.AddChild(gridContainer2, false, Node.InternalMode.Disabled);
			CharacterTab.AddGridLabel(gridContainer2, Loc.Get("char.current_energy", null));
			SpinBox curEnergySpin = CharacterTab.AddGridSpinBox(gridContainer2, num4, 0, 999);
			CharacterTab.AddGridLabel(gridContainer2, Loc.Get("char.max_energy", null));
			SpinBox maxEnergySpin = CharacterTab.AddGridSpinBox(gridContainer2, num5, 0, 999);
			HBoxContainer hboxContainer2 = CharacterTab.CreateButtonRow();
			container.AddChild(hboxContainer2, false, Node.InternalMode.Disabled);
			Button button2 = LoadoutPanel.CreateActionButton(Loc.Get("char.set", null), null);
			button2.Pressed += delegate
			{
				int num10 = (int)curEnergySpin.Value;
				int num11 = (int)maxEnergySpin.Value;
				TrainerState.CurrentEnergyLockValue = Math.Max(0, num10);
				TrainerState.MaxEnergyLockValue = Math.Max(0, num11);
				if (!TrainerState.InfiniteEnergyEnabled)
				{
					TrainerState.SetEnergyOnce(player, num10, num11);
				}
			};
			hboxContainer2.AddChild(button2, false, Node.InternalMode.Disabled);
			Button lockEnergyBtn = LoadoutPanel.CreateToggleButton(Loc.Get("char.lock", null), TrainerState.InfiniteEnergyEnabled);
			lockEnergyBtn.Pressed += delegate
			{
				if (!TrainerState.InfiniteEnergyEnabled)
				{
					TrainerState.CurrentEnergyLockValue = Math.Max(0, (int)curEnergySpin.Value);
					TrainerState.MaxEnergyLockValue = Math.Max(0, (int)maxEnergySpin.Value);
				}
				TrainerState.ToggleInfiniteEnergy();
				LoadoutPanel.UpdateToggleButton(lockEnergyBtn, TrainerState.InfiniteEnergyEnabled);
			};
			hboxContainer2.AddChild(lockEnergyBtn, false, Node.InternalMode.Disabled);
			if (player.PlayerCombatState != null)
			{
				CharacterTab.AddSeparator(container);
				container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("char.stars_section", null)), false, Node.InternalMode.Disabled);
				int num6 = (TrainerState.InfiniteStarsEnabled ? TrainerState.StarsLockValue : player.PlayerCombatState.Stars);
				GridContainer gridContainer3 = CharacterTab.CreateInputGrid();
				container.AddChild(gridContainer3, false, Node.InternalMode.Disabled);
				CharacterTab.AddGridLabel(gridContainer3, Loc.Get("char.stars", null));
				SpinBox starsSpin = CharacterTab.AddGridSpinBox(gridContainer3, num6, 0, 9999);
				HBoxContainer hboxContainer3 = CharacterTab.CreateButtonRow();
				container.AddChild(hboxContainer3, false, Node.InternalMode.Disabled);
				Button button3 = LoadoutPanel.CreateActionButton(Loc.Get("char.set", null), null);
				button3.Pressed += delegate
				{
					int num12 = (int)starsSpin.Value;
					TrainerState.StarsLockValue = Math.Max(0, num12);
					if (!TrainerState.InfiniteStarsEnabled)
					{
						TrainerState.SetStarsOnce(player, num12);
					}
				};
				hboxContainer3.AddChild(button3, false, Node.InternalMode.Disabled);
				Button lockStarsBtn = LoadoutPanel.CreateToggleButton(Loc.Get("char.lock", null), TrainerState.InfiniteStarsEnabled);
				lockStarsBtn.Pressed += delegate
				{
					if (!TrainerState.InfiniteStarsEnabled)
					{
						TrainerState.StarsLockValue = Math.Max(0, (int)starsSpin.Value);
					}
					TrainerState.ToggleInfiniteStars();
					LoadoutPanel.UpdateToggleButton(lockStarsBtn, TrainerState.InfiniteStarsEnabled);
				};
				hboxContainer3.AddChild(lockStarsBtn, false, Node.InternalMode.Disabled);
			}
			CharacterTab.AddSeparator(container);
			container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("char.gold_section", null)), false, Node.InternalMode.Disabled);
			int num7 = (TrainerState.InfiniteGoldEnabled ? TrainerState.GoldLockValue : player.Gold);
			GridContainer gridContainer4 = CharacterTab.CreateInputGrid();
			container.AddChild(gridContainer4, false, Node.InternalMode.Disabled);
			CharacterTab.AddGridLabel(gridContainer4, Loc.Get("char.gold", null));
			SpinBox goldSpin = CharacterTab.AddGridSpinBox(gridContainer4, num7, 0, 99999);
			HBoxContainer hboxContainer4 = CharacterTab.CreateButtonRow();
			container.AddChild(hboxContainer4, false, Node.InternalMode.Disabled);
			Button button4 = LoadoutPanel.CreateActionButton(Loc.Get("char.set", null), null);
			button4.Pressed += delegate
			{
				int num13 = (int)goldSpin.Value;
				TrainerState.GoldLockValue = Math.Max(0, num13);
				if (!TrainerState.InfiniteGoldEnabled)
				{
					TrainerState.SetGoldOnce(player, num13);
				}
			};
			hboxContainer4.AddChild(button4, false, Node.InternalMode.Disabled);
			Button lockGoldBtn = LoadoutPanel.CreateToggleButton(Loc.Get("char.lock", null), TrainerState.InfiniteGoldEnabled);
			lockGoldBtn.Pressed += delegate
			{
				if (!TrainerState.InfiniteGoldEnabled)
				{
					TrainerState.GoldLockValue = Math.Max(0, (int)goldSpin.Value);
				}
				TrainerState.ToggleInfiniteGold();
				LoadoutPanel.UpdateToggleButton(lockGoldBtn, TrainerState.InfiniteGoldEnabled);
			};
			hboxContainer4.AddChild(lockGoldBtn, false, Node.InternalMode.Disabled);
			CharacterTab.AddSeparator(container);
			container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("char.player_info", null)), false, Node.InternalMode.Disabled);
			GridContainer gridContainer5 = new GridContainer();
			gridContainer5.Columns = 2;
			gridContainer5.AddThemeConstantOverride("h_separation", 16);
			gridContainer5.AddThemeConstantOverride("v_separation", 6);
			container.AddChild(gridContainer5, false, Node.InternalMode.Disabled);
			GridContainer gridContainer6 = gridContainer5;
			string text = Loc.Get("char.hp", null);
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(3, 2);
			defaultInterpolatedStringHandler.AppendFormatted<int>(player.Creature.CurrentHp);
			defaultInterpolatedStringHandler.AppendLiteral(" / ");
			defaultInterpolatedStringHandler.AppendFormatted<int>(player.Creature.MaxHp);
			CharacterTab.AddInfoRow(gridContainer6, text, defaultInterpolatedStringHandler.ToStringAndClear());
			GridContainer gridContainer7 = gridContainer5;
			string text2 = Loc.Get("char.gold", null);
			defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
			defaultInterpolatedStringHandler.AppendFormatted<int>(player.Gold);
			CharacterTab.AddInfoRow(gridContainer7, text2, defaultInterpolatedStringHandler.ToStringAndClear());
			try
			{
				CharacterTab.AddInfoRow(gridContainer5, Loc.Get("char.character", null), player.Creature.Name);
			}
			catch
			{
			}
		}

		// Token: 0x0600014B RID: 331 RVA: 0x00012BD0 File Offset: 0x00010DD0
		private static GridContainer CreateInputGrid()
		{
			GridContainer gridContainer = new GridContainer();
			gridContainer.Columns = 2;
			gridContainer.AddThemeConstantOverride("h_separation", 12);
			gridContainer.AddThemeConstantOverride("v_separation", 8);
			return gridContainer;
		}

		// Token: 0x0600014C RID: 332 RVA: 0x00012C01 File Offset: 0x00010E01
		private static HBoxContainer CreateButtonRow()
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 8);
			return hboxContainer;
		}

		// Token: 0x0600014D RID: 333 RVA: 0x00012C1C File Offset: 0x00010E1C
		private static void AddSeparator(VBoxContainer container)
		{
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 1f),
				Color = new Color(0.91f, 0.86f, 0.75f, 0.1f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
		}

		// Token: 0x0600014E RID: 334 RVA: 0x00012C78 File Offset: 0x00010E78
		private static void AddGridLabel(GridContainer grid, string text)
		{
			Label label = new Label();
			label.Text = text;
			label.CustomMinimumSize = new Vector2(120f, 0f);
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			grid.AddChild(label, false, Node.InternalMode.Disabled);
		}

		// Token: 0x0600014F RID: 335 RVA: 0x00012CE0 File Offset: 0x00010EE0
		private static SpinBox AddGridSpinBox(GridContainer grid, int value, int min, int max)
		{
			SpinBox spinBox = new SpinBox();
			spinBox.MinValue = (double)min;
			spinBox.MaxValue = (double)max;
			spinBox.Value = (double)value;
			spinBox.Step = 1.0;
			spinBox.CustomMinimumSize = new Vector2(120f, 32f);
			spinBox.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			LineEdit lineEdit = spinBox.GetLineEdit();
			if (lineEdit != null)
			{
				lineEdit.AddThemeFontSizeOverride("font_size", 14);
				lineEdit.AddThemeColorOverride("font_color", StsColors.cream);
				StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
				styleBoxFlat.BgColor = new Color(0.15f, 0.12f, 0.18f, 0.9f);
				styleBoxFlat.BorderColor = new Color(0.35f, 0.3f, 0.25f, 0.6f);
				styleBoxFlat.SetBorderWidthAll(1);
				styleBoxFlat.SetCornerRadiusAll(4);
				styleBoxFlat.SetContentMarginAll(4f);
				lineEdit.AddThemeStyleboxOverride("normal", styleBoxFlat);
			}
			grid.AddChild(spinBox, false, Node.InternalMode.Disabled);
			return spinBox;
		}

		// Token: 0x06000150 RID: 336 RVA: 0x00012DE4 File Offset: 0x00010FE4
		private static void AddInfoRow(GridContainer grid, string label, string value)
		{
			Label label2 = new Label();
			label2.Text = label;
			label2.AddThemeFontSizeOverride("font_size", 16);
			label2.AddThemeColorOverride("font_color", StsColors.gold);
			grid.AddChild(label2, false, Node.InternalMode.Disabled);
			Label label3 = new Label();
			label3.Text = value;
			label3.AddThemeFontSizeOverride("font_size", 16);
			label3.AddThemeColorOverride("font_color", StsColors.cream);
			grid.AddChild(label3, false, Node.InternalMode.Disabled);
		}
	}
}
