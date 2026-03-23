using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager.Tabs
{
	// Token: 0x0200002D RID: 45
	[NullableContext(1)]
	[Nullable(0)]
	internal static class PotionEditPanel
	{
		// Token: 0x17000020 RID: 32
		// (get) Token: 0x06000117 RID: 279 RVA: 0x0000DCEC File Offset: 0x0000BEEC
		internal static bool IsOpen
		{
			get
			{
				CanvasLayer layer = PotionEditPanel._layer;
				return layer != null && layer.Visible;
			}
		}

		// Token: 0x06000118 RID: 280 RVA: 0x0000DD0A File Offset: 0x0000BF0A
		internal static void Open(PotionModel potion, Player player)
		{
			PotionEditPanel.Close();
			PotionEditPanel._editingPotion = potion;
			PotionEditPanel._editingPlayer = player;
			PotionEditPanel.Build();
		}

		// Token: 0x06000119 RID: 281 RVA: 0x0000DD22 File Offset: 0x0000BF22
		internal static void Close()
		{
			if (PotionEditPanel._layer != null && GodotObject.IsInstanceValid(PotionEditPanel._layer))
			{
				PotionEditPanel._layer.QueueFree();
			}
			PotionEditPanel._layer = null;
			PotionEditPanel._editingPotion = null;
			PotionEditPanel._editingPlayer = null;
		}

		// Token: 0x0600011A RID: 282 RVA: 0x0000DD54 File Offset: 0x0000BF54
		private static void Refresh()
		{
			if (PotionEditPanel._editingPotion == null)
			{
				return;
			}
			PotionModel editingPotion = PotionEditPanel._editingPotion;
			Player editingPlayer = PotionEditPanel._editingPlayer;
			if (PotionEditPanel._layer != null && GodotObject.IsInstanceValid(PotionEditPanel._layer))
			{
				PotionEditPanel._layer.QueueFree();
			}
			PotionEditPanel._layer = null;
			PotionEditPanel._editingPotion = editingPotion;
			PotionEditPanel._editingPlayer = editingPlayer;
			PotionEditPanel.Build();
		}

		// Token: 0x0600011B RID: 283 RVA: 0x0000DDA8 File Offset: 0x0000BFA8
		private static void Build()
		{
			if (PotionEditPanel._editingPotion == null)
			{
				return;
			}
			PotionEditPanel._layer = new CanvasLayer();
			PotionEditPanel._layer.Layer = 102;
			PotionEditPanel._layer.Name = "PotionEditPanel";
			ColorRect backstop = new ColorRect();
			backstop.Color = new Color(0f, 0f, 0f, 0.5f);
			backstop.AnchorRight = 1f;
			backstop.AnchorBottom = 1f;
			backstop.MouseFilter = Control.MouseFilterEnum.Stop;
			backstop.GuiInput += delegate(InputEvent ev)
			{
				InputEventMouseButton inputEventMouseButton = ev as InputEventMouseButton;
				if (inputEventMouseButton != null && inputEventMouseButton.Pressed && inputEventMouseButton.ButtonIndex == MouseButton.Left)
				{
					PotionEditPanel.Close();
					Viewport viewport = backstop.GetViewport();
					if (viewport != null)
					{
						viewport.SetInputAsHandled();
					}
					LoadoutPanel.RequestRefresh();
				}
			};
			PotionEditPanel._layer.AddChild(backstop, false, Node.InternalMode.Disabled);
			PanelContainer panelContainer = new PanelContainer();
			panelContainer.AnchorLeft = 0.2f;
			panelContainer.AnchorRight = 0.8f;
			panelContainer.AnchorTop = 0.12f;
			panelContainer.AnchorBottom = 0.88f;
			panelContainer.GrowHorizontal = Control.GrowDirection.Both;
			panelContainer.GrowVertical = Control.GrowDirection.Both;
			panelContainer.MouseFilter = Control.MouseFilterEnum.Stop;
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.06f, 0.05f, 0.08f, 0.97f);
			styleBoxFlat.SetBorderWidthAll(0);
			styleBoxFlat.SetCornerRadiusAll(10);
			styleBoxFlat.SetContentMarginAll(16f);
			panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
			PotionEditPanel._layer.AddChild(panelContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.AnchorRight = 1f;
			vboxContainer.AnchorBottom = 1f;
			vboxContainer.AddThemeConstantOverride("separation", 10);
			panelContainer.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 8);
			vboxContainer.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			string text;
			try
			{
				text = PotionEditPanel._editingPotion.Title.GetFormattedText();
			}
			catch
			{
				text = PotionEditPanel._editingPotion.Id.Entry;
			}
			Label label = new Label();
			label.Text = Loc.Fmt("potions.edit_header", new object[] { text });
			label.AddThemeFontSizeOverride("font_size", 20);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			Button button = LoadoutPanel.CreateActionButton(Loc.Get("close", null), new Color?(StsColors.red));
			button.CustomMinimumSize = new Vector2(60f, 32f);
			button.Pressed += delegate
			{
				PotionEditPanel.Close();
				LoadoutPanel.RequestRefresh();
			};
			hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			PotionEditPanel.AddDivider(vboxContainer, 0.25f);
			HBoxContainer hboxContainer2 = new HBoxContainer();
			hboxContainer2.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			hboxContainer2.AddThemeConstantOverride("separation", 16);
			vboxContainer.AddChild(hboxContainer2, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer2 = new VBoxContainer();
			vboxContainer2.CustomMinimumSize = new Vector2(160f, 0f);
			vboxContainer2.AddThemeConstantOverride("separation", 8);
			hboxContainer2.AddChild(vboxContainer2, false, Node.InternalMode.Disabled);
			PotionEditPanel.BuildPotionPreview(vboxContainer2, PotionEditPanel._editingPotion);
			ScrollContainer scrollContainer = new ScrollContainer();
			scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			hboxContainer2.AddChild(scrollContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer3 = new VBoxContainer();
			vboxContainer3.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vboxContainer3.AddThemeConstantOverride("separation", 10);
			scrollContainer.AddChild(vboxContainer3, false, Node.InternalMode.Disabled);
			PotionEditPanel.BuildInfoSection(vboxContainer3, PotionEditPanel._editingPotion);
			PotionEditPanel.AddDivider(vboxContainer3, 0.1f);
			PotionEditPanel.BuildValuesSection(vboxContainer3, PotionEditPanel._editingPotion);
			PotionEditPanel.AddDivider(vboxContainer3, 0.1f);
			PotionEditPanel.BuildActionsSection(vboxContainer3, PotionEditPanel._editingPotion, PotionEditPanel._editingPlayer);
			NGame instance = NGame.Instance;
			if (instance == null)
			{
				return;
			}
			instance.AddChild(PotionEditPanel._layer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x0600011C RID: 284 RVA: 0x0000E1A8 File Offset: 0x0000C3A8
		private static void BuildPotionPreview(VBoxContainer container, PotionModel potion)
		{
			TextureRect textureRect = new TextureRect();
			textureRect.CustomMinimumSize = new Vector2(128f, 128f);
			textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			textureRect.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			try
			{
				Texture2D image = potion.Image;
				if (image != null)
				{
					textureRect.Texture = image;
				}
			}
			catch
			{
			}
			container.AddChild(textureRect, false, Node.InternalMode.Disabled);
			string text;
			try
			{
				text = potion.Title.GetFormattedText();
			}
			catch
			{
				text = potion.Id.Entry;
			}
			Label label = new Label();
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 16);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			container.AddChild(label, false, Node.InternalMode.Disabled);
		}

		// Token: 0x0600011D RID: 285 RVA: 0x0000E290 File Offset: 0x0000C490
		private static void BuildInfoSection(VBoxContainer container, PotionModel potion)
		{
			container.AddChild(PotionEditPanel.CreateSectionLabel(Loc.Get("potions.edit_info", null)), false, Node.InternalMode.Disabled);
			Label label = new Label();
			label.Text = Loc.Get("potions.rarity", null);
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			container.AddChild(label, false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 3);
			hflowContainer.AddThemeConstantOverride("v_separation", 3);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			ValueTuple<PotionRarity, string>[] array = new ValueTuple<PotionRarity, string>[]
			{
				new ValueTuple<PotionRarity, string>(PotionRarity.Common, Loc.Get("rarity.common", null)),
				new ValueTuple<PotionRarity, string>(PotionRarity.Uncommon, Loc.Get("rarity.uncommon", null)),
				new ValueTuple<PotionRarity, string>(PotionRarity.Rare, Loc.Get("rarity.rare", null)),
				new ValueTuple<PotionRarity, string>(PotionRarity.Event, Loc.Get("rarity.event", null))
			};
			for (int i = 0; i < array.Length; i++)
			{
				ValueTuple<PotionRarity, string> valueTuple = array[i];
				PotionRarity item = valueTuple.Item1;
				string item2 = valueTuple.Item2;
				bool flag = potion.Rarity == item;
				Button button = LoadoutPanel.CreateToggleButton(item2, flag);
				button.CustomMinimumSize = new Vector2(65f, 24f);
				PotionRarity capturedRarity = item;
				button.Pressed += delegate
				{
					try
					{
						FieldInfo rarityField = PotionEditPanel.RarityField;
						if (rarityField != null)
						{
							rarityField.SetValue(potion, capturedRarity);
						}
					}
					catch (Exception ex)
					{
						GD.PrintErr("[PotionEditPanel] Rarity: " + ex.Message);
					}
					PotionEditPanel.Refresh();
				};
				hflowContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			Label label2 = new Label();
			label2.Text = Loc.Get("potions.usage", null);
			label2.AddThemeFontSizeOverride("font_size", 14);
			label2.AddThemeColorOverride("font_color", StsColors.cream);
			container.AddChild(label2, false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer2 = new HFlowContainer();
			hflowContainer2.AddThemeConstantOverride("h_separation", 3);
			hflowContainer2.AddThemeConstantOverride("v_separation", 3);
			container.AddChild(hflowContainer2, false, Node.InternalMode.Disabled);
			ValueTuple<PotionUsage, string>[] array2 = new ValueTuple<PotionUsage, string>[]
			{
				new ValueTuple<PotionUsage, string>(PotionUsage.CombatOnly, Loc.Get("potions.usage_combat", null)),
				new ValueTuple<PotionUsage, string>(PotionUsage.AnyTime, Loc.Get("potions.usage_anytime", null)),
				new ValueTuple<PotionUsage, string>(PotionUsage.Automatic, Loc.Get("potions.usage_automatic", null))
			};
			for (int i = 0; i < array2.Length; i++)
			{
				ValueTuple<PotionUsage, string> valueTuple2 = array2[i];
				PotionUsage item3 = valueTuple2.Item1;
				string item4 = valueTuple2.Item2;
				bool flag2 = potion.Usage == item3;
				Button button2 = LoadoutPanel.CreateToggleButton(item4, flag2);
				button2.CustomMinimumSize = new Vector2(80f, 24f);
				PotionUsage capturedUsage = item3;
				button2.Pressed += delegate
				{
					try
					{
						FieldInfo usageField = PotionEditPanel.UsageField;
						if (usageField != null)
						{
							usageField.SetValue(potion, capturedUsage);
						}
					}
					catch (Exception ex2)
					{
						GD.PrintErr("[PotionEditPanel] Usage: " + ex2.Message);
					}
					PotionEditPanel.Refresh();
				};
				hflowContainer2.AddChild(button2, false, Node.InternalMode.Disabled);
			}
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 8);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			Label label3 = new Label();
			label3.Text = Loc.Get("potions.target", null);
			label3.AddThemeFontSizeOverride("font_size", 14);
			label3.AddThemeColorOverride("font_color", StsColors.cream);
			hboxContainer.AddChild(label3, false, Node.InternalMode.Disabled);
			Label label4 = new Label();
			label4.Text = potion.TargetType.ToString();
			label4.AddThemeFontSizeOverride("font_size", 14);
			label4.AddThemeColorOverride("font_color", StsColors.gold);
			hboxContainer.AddChild(label4, false, Node.InternalMode.Disabled);
			try
			{
				LocString dynamicDescription = potion.DynamicDescription;
				string text;
				if ((text = ((dynamicDescription != null) ? dynamicDescription.GetFormattedText() : null)) == null)
				{
					LocString staticDescription = potion.StaticDescription;
					text = ((staticDescription != null) ? staticDescription.GetFormattedText() : null) ?? "";
				}
				string text2 = text;
				if (!string.IsNullOrEmpty(text2))
				{
					Label label5 = new Label();
					label5.Text = text2;
					label5.AddThemeFontSizeOverride("font_size", 14);
					label5.AddThemeColorOverride("font_color", StsColors.cream);
					label5.AutowrapMode = TextServer.AutowrapMode.WordSmart;
					label5.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
					container.AddChild(label5, false, Node.InternalMode.Disabled);
				}
			}
			catch
			{
			}
		}

		// Token: 0x0600011E RID: 286 RVA: 0x0000E72C File Offset: 0x0000C92C
		private static void BuildValuesSection(VBoxContainer container, PotionModel potion)
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 8);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			hboxContainer.AddChild(PotionEditPanel.CreateSectionLabel(Loc.Get("potions.edit_values", null)), false, Node.InternalMode.Disabled);
			Label label = new Label();
			label.Text = Loc.Get("modifier_hint", null);
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", StsColors.gray);
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			bool flag = false;
			foreach (KeyValuePair<string, DynamicVar> keyValuePair in potion.DynamicVars)
			{
				string text;
				DynamicVar dynamicVar;
				keyValuePair.Deconstruct(out text, out dynamicVar);
				string text2 = text;
				DynamicVar dynamicVar2 = dynamicVar;
				flag = true;
				HBoxContainer hboxContainer2 = new HBoxContainer();
				hboxContainer2.AddThemeConstantOverride("separation", 6);
				string text3 = Loc.Get("var." + text2, text2);
				Label label2 = new Label();
				label2.Text = text3 + ":";
				label2.CustomMinimumSize = new Vector2(100f, 0f);
				label2.AddThemeFontSizeOverride("font_size", 14);
				label2.AddThemeColorOverride("font_color", StsColors.cream);
				hboxContainer2.AddChild(label2, false, Node.InternalMode.Disabled);
				DynamicVar capturedVar = dynamicVar2;
				Button button = LoadoutPanel.CreateActionButton("-", new Color?(StsColors.red));
				button.CustomMinimumSize = new Vector2(36f, 28f);
				button.Pressed += delegate
				{
					capturedVar.BaseValue -= PotionEditPanel.GetModifierAmount();
					PotionEditPanel.Refresh();
				};
				hboxContainer2.AddChild(button, false, Node.InternalMode.Disabled);
				Label label3 = new Label();
				Label label4 = label3;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
				defaultInterpolatedStringHandler.AppendFormatted<decimal>(dynamicVar2.BaseValue, "0");
				label4.Text = defaultInterpolatedStringHandler.ToStringAndClear();
				label3.CustomMinimumSize = new Vector2(50f, 0f);
				label3.AddThemeFontSizeOverride("font_size", 15);
				label3.AddThemeColorOverride("font_color", StsColors.gold);
				label3.HorizontalAlignment = HorizontalAlignment.Center;
				hboxContainer2.AddChild(label3, false, Node.InternalMode.Disabled);
				Button button2 = LoadoutPanel.CreateActionButton("+", new Color?(StsColors.green));
				button2.CustomMinimumSize = new Vector2(36f, 28f);
				button2.Pressed += delegate
				{
					capturedVar.BaseValue += PotionEditPanel.GetModifierAmount();
					PotionEditPanel.Refresh();
				};
				hboxContainer2.AddChild(button2, false, Node.InternalMode.Disabled);
				container.AddChild(hboxContainer2, false, Node.InternalMode.Disabled);
			}
			if (!flag)
			{
				Label label5 = new Label();
				label5.Text = Loc.Get("potions.no_values", null);
				label5.AddThemeFontSizeOverride("font_size", 13);
				label5.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label5, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x0600011F RID: 287 RVA: 0x0000EA44 File Offset: 0x0000CC44
		private static void BuildActionsSection(VBoxContainer container, PotionModel potion, Player player)
		{
			container.AddChild(PotionEditPanel.CreateSectionLabel(Loc.Get("potions.edit_actions", null)), false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 6);
			hflowContainer.AddThemeConstantOverride("v_separation", 4);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			CombatManager instance = CombatManager.Instance;
			bool flag = instance != null && instance.IsInProgress;
			if (potion.Usage == PotionUsage.AnyTime || (potion.Usage == PotionUsage.CombatOnly && flag))
			{
				Button button = LoadoutPanel.CreateActionButton(Loc.Get("potions.use", null), new Color?(StsColors.green));
				button.CustomMinimumSize = new Vector2(60f, 30f);
				button.Pressed += delegate
				{
					try
					{
						potion.EnqueueManualUse(null);
					}
					catch (Exception ex)
					{
						GD.PrintErr("[PotionEdit] Use failed: " + ex.Message);
					}
					PotionEditPanel.Close();
					LoadoutPanel.RequestRefresh();
				};
				hflowContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			Button button2 = LoadoutPanel.CreateActionButton(Loc.Get("potions.discard", null), new Color?(StsColors.red));
			button2.CustomMinimumSize = new Vector2(60f, 30f);
			button2.Pressed += delegate
			{
				try
				{
					TaskHelper.RunSafely(PotionCmd.Discard(potion));
				}
				catch
				{
				}
				PotionEditPanel.Close();
				LoadoutPanel.RequestRefresh();
			};
			hflowContainer.AddChild(button2, false, Node.InternalMode.Disabled);
			if (player.HasOpenPotionSlots)
			{
				Button button3 = LoadoutPanel.CreateActionButton(Loc.Get("copy", null), new Color?(StsColors.blue));
				button3.CustomMinimumSize = new Vector2(60f, 30f);
				button3.Pressed += delegate
				{
					try
					{
						TaskHelper.RunSafely(PotionCmd.TryToProcure(potion.CanonicalInstance.ToMutable(), player, -1));
					}
					catch
					{
					}
					PotionEditPanel.Close();
					LoadoutPanel.RequestRefresh();
				};
				hflowContainer.AddChild(button3, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x06000120 RID: 288 RVA: 0x0000EBDE File Offset: 0x0000CDDE
		private static int GetModifierAmount()
		{
			if (Input.IsKeyPressed(Key.Shift))
			{
				return 10;
			}
			if (Input.IsKeyPressed(Key.Ctrl))
			{
				return 5;
			}
			return 1;
		}

		// Token: 0x06000121 RID: 289 RVA: 0x0000EC00 File Offset: 0x0000CE00
		private static Label CreateSectionLabel(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 16);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			return label;
		}

		// Token: 0x06000122 RID: 290 RVA: 0x0000EC38 File Offset: 0x0000CE38
		private static void AddDivider(VBoxContainer container, float alpha = 0.1f)
		{
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 1f),
				Color = new Color(0.91f, 0.86f, 0.75f, alpha),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
		}

		// Token: 0x0400006E RID: 110
		[Nullable(2)]
		private static CanvasLayer _layer;

		// Token: 0x0400006F RID: 111
		[Nullable(2)]
		private static PotionModel _editingPotion;

		// Token: 0x04000070 RID: 112
		[Nullable(2)]
		private static Player _editingPlayer;

		// Token: 0x04000071 RID: 113
		[Nullable(2)]
		private static readonly FieldInfo RarityField = AccessTools.Field(typeof(PotionModel), "<Rarity>k__BackingField") ?? AccessTools.Field(typeof(PotionModel), "_rarity");

		// Token: 0x04000072 RID: 114
		[Nullable(2)]
		private static readonly FieldInfo UsageField = AccessTools.Field(typeof(PotionModel), "<Usage>k__BackingField") ?? AccessTools.Field(typeof(PotionModel), "_usage");
	}
}
