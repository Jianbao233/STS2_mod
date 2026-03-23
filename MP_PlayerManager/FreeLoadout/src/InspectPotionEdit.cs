using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager
{
	// Token: 0x02000014 RID: 20
	[NullableContext(1)]
	[Nullable(0)]
	internal static class InspectPotionEdit
	{
		// Token: 0x17000003 RID: 3
		// (get) Token: 0x0600003D RID: 61 RVA: 0x000044B4 File Offset: 0x000026B4
		internal static bool IsOpen
		{
			get
			{
				CanvasLayer layer = InspectPotionEdit._layer;
				return layer != null && layer.Visible;
			}
		}

		// Token: 0x0600003E RID: 62 RVA: 0x000044D2 File Offset: 0x000026D2
		internal static void Open(PotionModel potion, [Nullable(2)] Control sourceControl = null)
		{
			InspectPotionEdit.Close();
			InspectPotionEdit._potion = potion;
			InspectPotionEdit._sourceControl = sourceControl;
			InspectPotionEdit.Build();
		}

		// Token: 0x0600003F RID: 63 RVA: 0x000044EA File Offset: 0x000026EA
		internal static void Close()
		{
			if (InspectPotionEdit._layer != null && GodotObject.IsInstanceValid(InspectPotionEdit._layer))
			{
				InspectPotionEdit._layer.QueueFree();
			}
			InspectPotionEdit._layer = null;
			InspectPotionEdit._potion = null;
			InspectPotionEdit._sourceControl = null;
		}

		// Token: 0x06000040 RID: 64 RVA: 0x0000451C File Offset: 0x0000271C
		private static void Refresh()
		{
			if (InspectPotionEdit._potion == null)
			{
				return;
			}
			PotionModel potion = InspectPotionEdit._potion;
			Control sourceControl = InspectPotionEdit._sourceControl;
			if (InspectPotionEdit._layer != null && GodotObject.IsInstanceValid(InspectPotionEdit._layer))
			{
				InspectPotionEdit._layer.QueueFree();
			}
			InspectPotionEdit._layer = null;
			InspectPotionEdit._potion = potion;
			InspectPotionEdit._sourceControl = sourceControl;
			InspectPotionEdit.Build();
		}

		// Token: 0x06000041 RID: 65 RVA: 0x00004570 File Offset: 0x00002770
		private static void Build()
		{
			if (InspectPotionEdit._potion == null)
			{
				return;
			}
			InspectPotionEdit._layer = new CanvasLayer();
			InspectPotionEdit._layer.Layer = 102;
			InspectPotionEdit._layer.Name = "InspectPotionEdit";
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
					InspectPotionEdit.Close();
					Viewport viewport = backstop.GetViewport();
					if (viewport == null)
					{
						return;
					}
					viewport.SetInputAsHandled();
				}
			};
			InspectPotionEdit._layer.AddChild(backstop, false, Node.InternalMode.Disabled);
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
			InspectPotionEdit._layer.AddChild(panelContainer, false, Node.InternalMode.Disabled);
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
				text = InspectPotionEdit._potion.Title.GetFormattedText();
			}
			catch
			{
				text = InspectPotionEdit._potion.Id.Entry;
			}
			Label label = new Label();
			label.Text = Loc.Fmt("potions.edit_header", new object[] { text });
			label.AddThemeFontSizeOverride("font_size", 20);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			Button button = LoadoutPanel.CreateActionButton(Loc.Get("close", null), new Color?(StsColors.red));
			button.CustomMinimumSize = new Vector2(60f, 32f);
			BaseButton baseButton = button;
			Action action;
			if ((action = InspectPotionEdit.<>O.<0>__Close) == null)
			{
				action = (InspectPotionEdit.<>O.<0>__Close = new Action(InspectPotionEdit.Close));
			}
			baseButton.Pressed += action;
			hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			InspectPotionEdit.AddDivider(vboxContainer, 0.25f);
			HBoxContainer hboxContainer2 = new HBoxContainer();
			hboxContainer2.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			hboxContainer2.AddThemeConstantOverride("separation", 16);
			vboxContainer.AddChild(hboxContainer2, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer2 = new VBoxContainer();
			vboxContainer2.CustomMinimumSize = new Vector2(160f, 0f);
			vboxContainer2.AddThemeConstantOverride("separation", 8);
			hboxContainer2.AddChild(vboxContainer2, false, Node.InternalMode.Disabled);
			InspectPotionEdit.BuildPotionPreview(vboxContainer2, InspectPotionEdit._potion);
			ScrollContainer scrollContainer = new ScrollContainer();
			scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			hboxContainer2.AddChild(scrollContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer3 = new VBoxContainer();
			vboxContainer3.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vboxContainer3.AddThemeConstantOverride("separation", 10);
			scrollContainer.AddChild(vboxContainer3, false, Node.InternalMode.Disabled);
			InspectPotionEdit.BuildInfoSection(vboxContainer3, InspectPotionEdit._potion);
			InspectPotionEdit.AddDivider(vboxContainer3, 0.1f);
			InspectPotionEdit.BuildValuesSection(vboxContainer3, InspectPotionEdit._potion);
			InspectPotionEdit.AddDivider(vboxContainer3, 0.1f);
			InspectPotionEdit.BuildActionsSection(vboxContainer3, InspectPotionEdit._potion);
			NGame instance = NGame.Instance;
			if (instance == null)
			{
				return;
			}
			instance.AddChild(InspectPotionEdit._layer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000042 RID: 66 RVA: 0x00004968 File Offset: 0x00002B68
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

		// Token: 0x06000043 RID: 67 RVA: 0x00004A50 File Offset: 0x00002C50
		private static void BuildInfoSection(VBoxContainer container, PotionModel potion)
		{
			container.AddChild(InspectPotionEdit.CreateSectionLabel(Loc.Get("potions.edit_info", null)), false, Node.InternalMode.Disabled);
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
						FieldInfo rarityField = InspectPotionEdit.RarityField;
						if (rarityField != null)
						{
							rarityField.SetValue(potion, capturedRarity);
						}
					}
					catch (Exception ex)
					{
						GD.PrintErr("[InspectPotionEdit] Rarity: " + ex.Message);
					}
					InspectPotionEdit.Refresh();
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
						FieldInfo usageField = InspectPotionEdit.UsageField;
						if (usageField != null)
						{
							usageField.SetValue(potion, capturedUsage);
						}
					}
					catch (Exception ex2)
					{
						GD.PrintErr("[InspectPotionEdit] Usage: " + ex2.Message);
					}
					InspectPotionEdit.Refresh();
				};
				hflowContainer2.AddChild(button2, false, Node.InternalMode.Disabled);
			}
			InspectPotionEdit.AddInfoRow(container, Loc.Get("potions.target", null), potion.TargetType.ToString());
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
					Label label3 = new Label();
					label3.Text = text2;
					label3.AddThemeFontSizeOverride("font_size", 14);
					label3.AddThemeColorOverride("font_color", StsColors.cream);
					label3.AutowrapMode = TextServer.AutowrapMode.WordSmart;
					label3.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
					container.AddChild(label3, false, Node.InternalMode.Disabled);
				}
			}
			catch
			{
			}
		}

		// Token: 0x06000044 RID: 68 RVA: 0x00004E48 File Offset: 0x00003048
		private static void BuildValuesSection(VBoxContainer container, PotionModel potion)
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 8);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			hboxContainer.AddChild(InspectPotionEdit.CreateSectionLabel(Loc.Get("potions.edit_values", null)), false, Node.InternalMode.Disabled);
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
					capturedVar.BaseValue -= InspectPotionEdit.GetModifierAmount();
					InspectPotionEdit.Refresh();
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
					capturedVar.BaseValue += InspectPotionEdit.GetModifierAmount();
					InspectPotionEdit.Refresh();
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

		// Token: 0x06000045 RID: 69 RVA: 0x00005160 File Offset: 0x00003360
		private static void BuildActionsSection(VBoxContainer container, PotionModel potion)
		{
			container.AddChild(InspectPotionEdit.CreateSectionLabel(Loc.Get("potions.edit_actions", null)), false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 6);
			hflowContainer.AddThemeConstantOverride("v_separation", 4);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			Button button = LoadoutPanel.CreateActionButton(Loc.Get("inspect.acquire", null), new Color?(StsColors.green));
			button.CustomMinimumSize = new Vector2(80f, 30f);
			button.Pressed += delegate
			{
				Player player = LoadoutPanel.GetPlayer();
				if (player == null)
				{
					return;
				}
				try
				{
					PotionModel potionModel = (PotionModel)potion.MutableClone();
					if (!player.AddPotionInternal(potionModel, -1, false).success)
					{
						FieldInfo potionSlotsField = InspectPotionEdit.PotionSlotsField;
						List<PotionModel> list = ((potionSlotsField != null) ? potionSlotsField.GetValue(player) : null) as List<PotionModel>;
						if (list != null)
						{
							int num = list.FindIndex((PotionModel s) => s != null);
							if (num >= 0)
							{
								TaskHelper.RunSafely(PotionCmd.Discard(list[num]));
								potionModel = (PotionModel)potion.MutableClone();
								player.AddPotionInternal(potionModel, num, false);
							}
						}
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr("[InspectPotionEdit] Acquire failed: " + ex.Message);
				}
				if (InspectPotionEdit._sourceControl != null && GodotObject.IsInstanceValid(InspectPotionEdit._sourceControl))
				{
					UiHelper.FlashAcquired(InspectPotionEdit._sourceControl);
				}
				InspectPotionEdit.Close();
			};
			hflowContainer.AddChild(button, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000046 RID: 70 RVA: 0x00005211 File Offset: 0x00003411
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

		// Token: 0x06000047 RID: 71 RVA: 0x00005233 File Offset: 0x00003433
		private static Label CreateSectionLabel(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 16);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			return label;
		}

		// Token: 0x06000048 RID: 72 RVA: 0x00005268 File Offset: 0x00003468
		private static void AddDivider(VBoxContainer container, float alpha = 0.1f)
		{
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 1f),
				Color = new Color(0.91f, 0.86f, 0.75f, alpha),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000049 RID: 73 RVA: 0x000052C0 File Offset: 0x000034C0
		private static void AddInfoRow(VBoxContainer container, string label, string value)
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 8);
			Label label2 = new Label();
			label2.Text = label;
			label2.AddThemeFontSizeOverride("font_size", 14);
			label2.AddThemeColorOverride("font_color", StsColors.cream);
			hboxContainer.AddChild(label2, false, Node.InternalMode.Disabled);
			Label label3 = new Label();
			label3.Text = value;
			label3.AddThemeFontSizeOverride("font_size", 14);
			label3.AddThemeColorOverride("font_color", StsColors.gold);
			hboxContainer.AddChild(label3, false, Node.InternalMode.Disabled);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x04000019 RID: 25
		[Nullable(2)]
		private static CanvasLayer _layer;

		// Token: 0x0400001A RID: 26
		[Nullable(2)]
		private static PotionModel _potion;

		// Token: 0x0400001B RID: 27
		[Nullable(2)]
		private static Control _sourceControl;

		// Token: 0x0400001C RID: 28
		[Nullable(2)]
		private static readonly FieldInfo PotionSlotsField = AccessTools.Field(typeof(Player), "_potionSlots");

		// Token: 0x0400001D RID: 29
		[Nullable(2)]
		private static readonly FieldInfo RarityField = AccessTools.Field(typeof(PotionModel), "<Rarity>k__BackingField") ?? AccessTools.Field(typeof(PotionModel), "_rarity");

		// Token: 0x0400001E RID: 30
		[Nullable(2)]
		private static readonly FieldInfo UsageField = AccessTools.Field(typeof(PotionModel), "<Usage>k__BackingField") ?? AccessTools.Field(typeof(PotionModel), "_usage");

		// Token: 0x02000044 RID: 68
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x040000A4 RID: 164
			[Nullable(0)]
			public static Action <0>__Close;
		}
	}
}
