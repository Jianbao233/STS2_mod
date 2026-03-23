using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager.Tabs
{
	// Token: 0x0200002A RID: 42
	[NullableContext(1)]
	[Nullable(0)]
	internal static class CardEditPanel
	{
		// Token: 0x1700001F RID: 31
		// (get) Token: 0x060000FD RID: 253 RVA: 0x0000B234 File Offset: 0x00009434
		internal static bool IsOpen
		{
			get
			{
				CanvasLayer layer = CardEditPanel._layer;
				return layer != null && layer.Visible;
			}
		}

		// Token: 0x060000FE RID: 254 RVA: 0x0000B252 File Offset: 0x00009452
		internal static void Open(CardModel card, PileType pileType, [Nullable(2)] Player player = null)
		{
			CardEditPanel.Close();
			CardEditPanel._editingCard = card;
			CardEditPanel._editingPileType = pileType;
			CardEditPanel._editingPlayer = player;
			CardEditPanel.Build();
		}

		// Token: 0x060000FF RID: 255 RVA: 0x0000B270 File Offset: 0x00009470
		internal static void Close()
		{
			if (CardEditPanel._layer != null && GodotObject.IsInstanceValid(CardEditPanel._layer))
			{
				CardEditPanel._layer.QueueFree();
			}
			CardEditPanel._layer = null;
			CardEditPanel._editingCard = null;
			CardEditPanel._editingPlayer = null;
		}

		// Token: 0x06000100 RID: 256 RVA: 0x0000B2A4 File Offset: 0x000094A4
		private static void Refresh()
		{
			if (CardEditPanel._editingCard == null)
			{
				return;
			}
			CardModel editingCard = CardEditPanel._editingCard;
			PileType editingPileType = CardEditPanel._editingPileType;
			Player editingPlayer = CardEditPanel._editingPlayer;
			if (CardEditPanel._layer != null && GodotObject.IsInstanceValid(CardEditPanel._layer))
			{
				CardEditPanel._layer.QueueFree();
			}
			CardEditPanel._layer = null;
			CardEditPanel._editingCard = editingCard;
			CardEditPanel._editingPileType = editingPileType;
			CardEditPanel._editingPlayer = editingPlayer;
			CardEditPanel.Build();
		}

		// Token: 0x06000101 RID: 257 RVA: 0x0000B304 File Offset: 0x00009504
		private static void Build()
		{
			if (CardEditPanel._editingCard == null)
			{
				return;
			}
			CardEditPanel._layer = new CanvasLayer();
			CardEditPanel._layer.Layer = 102;
			CardEditPanel._layer.Name = "CardEditPanel";
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
					CardEditPanel.Close();
					Viewport viewport = backstop.GetViewport();
					if (viewport != null)
					{
						viewport.SetInputAsHandled();
					}
					LoadoutPanel.RequestRefresh();
				}
			};
			CardEditPanel._layer.AddChild(backstop, false, Node.InternalMode.Disabled);
			PanelContainer panelContainer = new PanelContainer();
			panelContainer.AnchorLeft = 0.12f;
			panelContainer.AnchorRight = 0.88f;
			panelContainer.AnchorTop = 0.08f;
			panelContainer.AnchorBottom = 0.92f;
			panelContainer.GrowHorizontal = Control.GrowDirection.Both;
			panelContainer.GrowVertical = Control.GrowDirection.Both;
			panelContainer.MouseFilter = Control.MouseFilterEnum.Stop;
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.06f, 0.05f, 0.08f, 0.97f);
			styleBoxFlat.SetBorderWidthAll(0);
			styleBoxFlat.SetCornerRadiusAll(10);
			styleBoxFlat.SetContentMarginAll(16f);
			panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
			CardEditPanel._layer.AddChild(panelContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.AnchorRight = 1f;
			vboxContainer.AnchorBottom = 1f;
			vboxContainer.AddThemeConstantOverride("separation", 8);
			panelContainer.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 8);
			vboxContainer.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			string text;
			try
			{
				text = CardEditPanel._editingCard.Title;
			}
			catch
			{
				text = CardEditPanel._editingCard.Id.Entry;
			}
			Label label = new Label();
			label.Text = Loc.Fmt("edit.header", new object[] { text });
			label.AddThemeFontSizeOverride("font_size", 20);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			Button button = LoadoutPanel.CreateActionButton(Loc.Get("close", null), new Color?(StsColors.red));
			button.CustomMinimumSize = new Vector2(60f, 32f);
			button.Pressed += delegate
			{
				CardEditPanel.Close();
				LoadoutPanel.RequestRefresh();
			};
			hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			CardEditPanel.AddDivider(vboxContainer, 0.25f);
			HBoxContainer hboxContainer2 = new HBoxContainer();
			hboxContainer2.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			hboxContainer2.AddThemeConstantOverride("separation", 16);
			vboxContainer.AddChild(hboxContainer2, false, Node.InternalMode.Disabled);
			Control control = new Control();
			control.CustomMinimumSize = new Vector2(300f, 0f);
			control.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			hboxContainer2.AddChild(control, false, Node.InternalMode.Disabled);
			CardEditPanel.BuildCardPreview(control, CardEditPanel._editingCard);
			ScrollContainer scrollContainer = new ScrollContainer();
			scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			hboxContainer2.AddChild(scrollContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer2 = new VBoxContainer();
			vboxContainer2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vboxContainer2.AddThemeConstantOverride("separation", 10);
			scrollContainer.AddChild(vboxContainer2, false, Node.InternalMode.Disabled);
			CardEditPanel.BuildKeywordsSection(vboxContainer2, CardEditPanel._editingCard);
			CardEditPanel.AddDivider(vboxContainer2, 0.1f);
			CardEditPanel.BuildValuesSection(vboxContainer2, CardEditPanel._editingCard);
			CardEditPanel.AddDivider(vboxContainer2, 0.1f);
			CardEditPanel.BuildCostSection(vboxContainer2, CardEditPanel._editingCard);
			CardEditPanel.AddDivider(vboxContainer2, 0.1f);
			CardEditPanel.BuildEnchantmentSection(vboxContainer2, CardEditPanel._editingCard);
			CardEditPanel.AddDivider(vboxContainer2, 0.1f);
			CardEditPanel.BuildActionsSection(vboxContainer2, CardEditPanel._editingCard);
			NGame instance = NGame.Instance;
			if (instance == null)
			{
				return;
			}
			instance.AddChild(CardEditPanel._layer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000102 RID: 258 RVA: 0x0000B720 File Offset: 0x00009920
		private static void BuildCardPreview(Control container, CardModel card)
		{
			NCard ncard = null;
			try
			{
				ncard = NCard.Create(card, ModelVisibility.Visible);
				if (ncard != null)
				{
					ncard.Scale = new Vector2(0.9f, 0.9f);
					ncard.MouseFilter = Control.MouseFilterEnum.Ignore;
					container.AddChild(ncard, false, Node.InternalMode.Disabled);
					NCard cardRef = ncard;
					PileType pt = CardEditPanel._editingPileType;
					cardRef.Ready += delegate
					{
						if (GodotObject.IsInstanceValid(cardRef) && GodotObject.IsInstanceValid(container))
						{
							cardRef.UpdateVisuals(pt, CardPreviewMode.Normal);
							cardRef.Position = new Vector2(container.Size.X / 2f, container.Size.Y / 2f);
						}
					};
				}
			}
			catch
			{
			}
			NCard captured = ncard;
			container.Resized += delegate
			{
				if (captured != null && GodotObject.IsInstanceValid(captured) && GodotObject.IsInstanceValid(container))
				{
					captured.Position = new Vector2(container.Size.X / 2f, container.Size.Y / 2f);
				}
			};
		}

		// Token: 0x06000103 RID: 259 RVA: 0x0000B7E4 File Offset: 0x000099E4
		private static void BuildKeywordsSection(VBoxContainer container, CardModel card)
		{
			container.AddChild(CardEditPanel.CreateSectionLabel(Loc.Get("edit.keywords", null)), false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 6);
			hflowContainer.AddThemeConstantOverride("v_separation", 4);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			ValueTuple<CardKeyword, string>[] array = new ValueTuple<CardKeyword, string>[]
			{
				new ValueTuple<CardKeyword, string>(CardKeyword.Exhaust, Loc.Get("kw.exhaust", null)),
				new ValueTuple<CardKeyword, string>(CardKeyword.Ethereal, Loc.Get("kw.ethereal", null)),
				new ValueTuple<CardKeyword, string>(CardKeyword.Innate, Loc.Get("kw.innate", null)),
				new ValueTuple<CardKeyword, string>(CardKeyword.Retain, Loc.Get("kw.retain", null)),
				new ValueTuple<CardKeyword, string>(CardKeyword.Sly, Loc.Get("kw.sly", null)),
				new ValueTuple<CardKeyword, string>(CardKeyword.Eternal, Loc.Get("kw.eternal", null)),
				new ValueTuple<CardKeyword, string>(CardKeyword.Unplayable, Loc.Get("kw.unplayable", null))
			};
			for (int i = 0; i < array.Length; i++)
			{
				ValueTuple<CardKeyword, string> valueTuple = array[i];
				CardKeyword item = valueTuple.Item1;
				string item2 = valueTuple.Item2;
				bool flag = card.Keywords.Contains(item);
				Button button = LoadoutPanel.CreateToggleButton(item2, flag);
				button.CustomMinimumSize = new Vector2(80f, 30f);
				CardKeyword capturedKw = item;
				button.Pressed += delegate
				{
					try
					{
						if (card.Keywords.Contains(capturedKw))
						{
							card.RemoveKeyword(capturedKw);
						}
						else
						{
							card.AddKeyword(capturedKw);
						}
					}
					catch (Exception ex)
					{
						GD.PrintErr("[CardEdit] Keyword toggle failed: " + ex.Message);
					}
					CardEditPanel.Refresh();
				};
				hflowContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x06000104 RID: 260 RVA: 0x0000B990 File Offset: 0x00009B90
		private static void BuildValuesSection(VBoxContainer container, CardModel card)
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 8);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			hboxContainer.AddChild(CardEditPanel.CreateSectionLabel(Loc.Get("edit.values", null)), false, Node.InternalMode.Disabled);
			Label label = new Label();
			label.Text = Loc.Get("modifier_hint", null);
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", StsColors.gray);
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			HashSet<string> hashSet = new HashSet<string> { "Energy", "Stars" };
			bool flag = false;
			foreach (KeyValuePair<string, DynamicVar> keyValuePair in card.DynamicVars)
			{
				string text;
				DynamicVar dynamicVar;
				keyValuePair.Deconstruct(out text, out dynamicVar);
				string text2 = text;
				DynamicVar dynamicVar2 = dynamicVar;
				if (!hashSet.Contains(text2))
				{
					flag = true;
					HBoxContainer hboxContainer2 = new HBoxContainer();
					hboxContainer2.AddThemeConstantOverride("separation", 6);
					string varDisplayName = CardEditPanel.GetVarDisplayName(text2);
					Label label2 = new Label();
					label2.Text = varDisplayName + ":";
					label2.CustomMinimumSize = new Vector2(100f, 0f);
					label2.AddThemeFontSizeOverride("font_size", 14);
					label2.AddThemeColorOverride("font_color", StsColors.cream);
					hboxContainer2.AddChild(label2, false, Node.InternalMode.Disabled);
					DynamicVar capturedVar = dynamicVar2;
					Button button = LoadoutPanel.CreateActionButton("-", new Color?(StsColors.red));
					button.CustomMinimumSize = new Vector2(36f, 28f);
					button.Pressed += delegate
					{
						capturedVar.BaseValue -= CardEditPanel.GetModifierAmount();
						CardEditPanel.Refresh();
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
						capturedVar.BaseValue += CardEditPanel.GetModifierAmount();
						CardEditPanel.Refresh();
					};
					hboxContainer2.AddChild(button2, false, Node.InternalMode.Disabled);
					container.AddChild(hboxContainer2, false, Node.InternalMode.Disabled);
				}
			}
			if (!flag)
			{
				Label label5 = new Label();
				label5.Text = Loc.Get("edit.no_values", null);
				label5.AddThemeFontSizeOverride("font_size", 13);
				label5.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label5, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x06000105 RID: 261 RVA: 0x0000BCCC File Offset: 0x00009ECC
		private static void BuildCostSection(VBoxContainer container, CardModel card)
		{
			container.AddChild(CardEditPanel.CreateSectionLabel(Loc.Get("edit.cost", null)), false, Node.InternalMode.Disabled);
			bool isX = card.EnergyCost.CostsX;
			Label label = new Label();
			label.Text = (isX ? Loc.Fmt("edit.cost_x_info", new object[] { card.EnergyCost.Canonical }) : Loc.Fmt("edit.cost_info", new object[] { card.EnergyCost.Canonical }));
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			container.AddChild(label, false, Node.InternalMode.Disabled);
			Label label2 = new Label();
			label2.Text = Loc.Get("edit.set_base_cost", null);
			label2.AddThemeFontSizeOverride("font_size", 13);
			label2.AddThemeColorOverride("font_color", StsColors.cream);
			container.AddChild(label2, false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 4);
			hflowContainer.AddThemeConstantOverride("v_separation", 4);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			for (int i = 0; i <= 5; i++)
			{
				int cost2 = i;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
				defaultInterpolatedStringHandler.AppendFormatted<int>(cost2);
				Button button = LoadoutPanel.CreateActionButton(defaultInterpolatedStringHandler.ToStringAndClear(), new Color?(StsColors.cream));
				button.CustomMinimumSize = new Vector2(40f, 28f);
				button.Pressed += delegate
				{
					try
					{
						if (card.EnergyCost.CostsX)
						{
							CardEditPanel.SetEnergyCost(card, cost2, false);
						}
						else
						{
							card.EnergyCost.SetCustomBaseCost(cost2);
						}
					}
					catch (Exception ex)
					{
						GD.PrintErr("[CardEdit] Set cost failed: " + ex.Message);
					}
					CardEditPanel.Refresh();
				};
				hflowContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			Button button2 = LoadoutPanel.CreateToggleButton("X", isX);
			button2.CustomMinimumSize = new Vector2(50f, 28f);
			button2.Pressed += delegate
			{
				try
				{
					if (isX)
					{
						CardEditPanel.SetEnergyCost(card, 1, false);
					}
					else
					{
						CardEditPanel.SetEnergyCost(card, 0, true);
					}
				}
				catch (Exception ex2)
				{
					GD.PrintErr("[CardEdit] Toggle X cost failed: " + ex2.Message);
				}
				CardEditPanel.Refresh();
			};
			hflowContainer.AddChild(button2, false, Node.InternalMode.Disabled);
			CombatManager instance = CombatManager.Instance;
			if (instance != null && instance.IsInProgress && !isX)
			{
				Label label3 = new Label();
				label3.Text = Loc.Get("edit.temp_cost", null);
				label3.AddThemeFontSizeOverride("font_size", 13);
				label3.AddThemeColorOverride("font_color", StsColors.cream);
				container.AddChild(label3, false, Node.InternalMode.Disabled);
				HFlowContainer hflowContainer2 = new HFlowContainer();
				hflowContainer2.AddThemeConstantOverride("h_separation", 4);
				hflowContainer2.AddThemeConstantOverride("v_separation", 4);
				container.AddChild(hflowContainer2, false, Node.InternalMode.Disabled);
				for (int j = 0; j <= 3; j++)
				{
					int cost = j;
					Button button3 = LoadoutPanel.CreateActionButton(Loc.Fmt("edit.set_to", new object[] { cost }), new Color?(new Color("4499FF")));
					button3.CustomMinimumSize = new Vector2(50f, 28f);
					button3.Pressed += delegate
					{
						try
						{
							card.EnergyCost.SetThisCombat(cost, false);
						}
						catch
						{
						}
						CardEditPanel.Refresh();
					};
					hflowContainer2.AddChild(button3, false, Node.InternalMode.Disabled);
				}
			}
		}

		// Token: 0x06000106 RID: 262 RVA: 0x0000C03C File Offset: 0x0000A23C
		private static void SetEnergyCost(CardModel card, int canonicalCost, bool costsX)
		{
			if (CardEditPanel.EnergyCostField == null)
			{
				GD.PrintErr("[CardEdit] Cannot find _energyCost field");
				return;
			}
			CardEnergyCost cardEnergyCost = new CardEnergyCost(card, canonicalCost, costsX);
			CardEditPanel.EnergyCostField.SetValue(card, cardEnergyCost);
		}

		// Token: 0x06000107 RID: 263 RVA: 0x0000C078 File Offset: 0x0000A278
		private static void BuildEnchantmentSection(VBoxContainer container, CardModel card)
		{
			container.AddChild(CardEditPanel.CreateSectionLabel(Loc.Get("edit.enchantment", null)), false, Node.InternalMode.Disabled);
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 6);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			string text = Loc.Get("none", null);
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler;
			if (card.Enchantment != null)
			{
				try
				{
					LocString title = card.Enchantment.Title;
					text = ((title != null) ? title.GetFormattedText() : null) ?? card.Enchantment.GetType().Name;
				}
				catch
				{
					text = card.Enchantment.GetType().Name;
				}
				if (card.Enchantment.ShowAmount)
				{
					string text2 = text;
					defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(3, 1);
					defaultInterpolatedStringHandler.AppendLiteral(" (");
					defaultInterpolatedStringHandler.AppendFormatted<int>(card.Enchantment.Amount);
					defaultInterpolatedStringHandler.AppendLiteral(")");
					text = text2 + defaultInterpolatedStringHandler.ToStringAndClear();
				}
			}
			Label label = new Label();
			label.Text = Loc.Fmt("edit.current", new object[] { text });
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", (card.Enchantment != null) ? StsColors.green : StsColors.gray);
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			if (card.Enchantment != null)
			{
				Button button = LoadoutPanel.CreateActionButton(Loc.Get("clear", null), new Color?(StsColors.red));
				button.CustomMinimumSize = new Vector2(50f, 26f);
				button.Pressed += delegate
				{
					try
					{
						CardCmd.ClearEnchantment(card);
					}
					catch
					{
					}
					CardEditPanel.Refresh();
				};
				hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			HBoxContainer hboxContainer2 = new HBoxContainer();
			hboxContainer2.AddThemeConstantOverride("separation", 6);
			container.AddChild(hboxContainer2, false, Node.InternalMode.Disabled);
			Label label2 = new Label();
			label2.Text = Loc.Get("edit.ench_amount", null);
			label2.AddThemeFontSizeOverride("font_size", 13);
			label2.AddThemeColorOverride("font_color", StsColors.cream);
			hboxContainer2.AddChild(label2, false, Node.InternalMode.Disabled);
			Button button2 = LoadoutPanel.CreateActionButton("-", new Color?(StsColors.red));
			button2.CustomMinimumSize = new Vector2(30f, 26f);
			button2.Pressed += delegate
			{
				CardEditPanel._enchantAmount = Math.Max(1, CardEditPanel._enchantAmount - 1);
				CardEditPanel.Refresh();
			};
			hboxContainer2.AddChild(button2, false, Node.InternalMode.Disabled);
			Label label3 = new Label();
			Label label4 = label3;
			defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
			defaultInterpolatedStringHandler.AppendFormatted<int>(CardEditPanel._enchantAmount);
			label4.Text = defaultInterpolatedStringHandler.ToStringAndClear();
			label3.CustomMinimumSize = new Vector2(30f, 0f);
			label3.AddThemeFontSizeOverride("font_size", 14);
			label3.AddThemeColorOverride("font_color", StsColors.gold);
			label3.HorizontalAlignment = HorizontalAlignment.Center;
			hboxContainer2.AddChild(label3, false, Node.InternalMode.Disabled);
			Button button3 = LoadoutPanel.CreateActionButton("+", new Color?(StsColors.green));
			button3.CustomMinimumSize = new Vector2(30f, 26f);
			button3.Pressed += delegate
			{
				CardEditPanel._enchantAmount++;
				CardEditPanel.Refresh();
			};
			hboxContainer2.AddChild(button3, false, Node.InternalMode.Disabled);
			Label label5 = new Label();
			label5.Text = Loc.Get("edit.select_ench", null);
			label5.AddThemeFontSizeOverride("font_size", 13);
			label5.AddThemeColorOverride("font_color", StsColors.cream);
			container.AddChild(label5, false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 4);
			hflowContainer.AddThemeConstantOverride("v_separation", 4);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			try
			{
				if (CardEditPanel._allEnchantments == null)
				{
					CardEditPanel._allEnchantments = ModelDb.DebugEnchantments.Where((EnchantmentModel e) => e.GetType().Name != "DeprecatedEnchantment").OrderBy(delegate(EnchantmentModel e)
					{
						string text4;
						try
						{
							LocString title3 = e.Title;
							text4 = ((title3 != null) ? title3.GetFormattedText() : null) ?? e.GetType().Name;
						}
						catch
						{
							text4 = e.GetType().Name;
						}
						return text4;
					}).ToList<EnchantmentModel>();
				}
				foreach (EnchantmentModel enchantmentModel in CardEditPanel._allEnchantments)
				{
					string text3;
					try
					{
						LocString title2 = enchantmentModel.Title;
						text3 = ((title2 != null) ? title2.GetFormattedText() : null) ?? enchantmentModel.GetType().Name;
					}
					catch
					{
						text3 = enchantmentModel.GetType().Name;
					}
					bool flag = false;
					try
					{
						flag = enchantmentModel.CanEnchantCardType(card.Type);
					}
					catch
					{
					}
					Color color = (flag ? StsColors.cream : new Color(0.5f, 0.45f, 0.4f, 1f));
					Button button4 = LoadoutPanel.CreateActionButton(text3, new Color?(color));
					button4.CustomMinimumSize = new Vector2(0f, 26f);
					if (flag)
					{
						EnchantmentModel capturedEnchant = enchantmentModel;
						button4.Pressed += delegate
						{
							try
							{
								if (card.Enchantment != null)
								{
									CardCmd.ClearEnchantment(card);
								}
								CardCmd.Enchant(capturedEnchant.ToMutable(), card, CardEditPanel._enchantAmount);
							}
							catch (Exception ex2)
							{
								GD.PrintErr("[CardEdit] Enchant failed: " + ex2.Message);
							}
							CardEditPanel.Refresh();
						};
					}
					hflowContainer.AddChild(button4, false, Node.InternalMode.Disabled);
				}
			}
			catch (Exception ex)
			{
				GD.PrintErr("[CardEdit] Failed to list enchantments: " + ex.Message);
			}
		}

		// Token: 0x06000108 RID: 264 RVA: 0x0000C68C File Offset: 0x0000A88C
		private static void BuildActionsSection(VBoxContainer container, CardModel card)
		{
			container.AddChild(CardEditPanel.CreateSectionLabel(Loc.Get("edit.actions", null)), false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 6);
			hflowContainer.AddThemeConstantOverride("v_separation", 4);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			if (card.IsUpgradable)
			{
				Button button = LoadoutPanel.CreateActionButton(Loc.Get("upgrade", null), new Color?(StsColors.green));
				button.CustomMinimumSize = new Vector2(60f, 30f);
				button.Pressed += delegate
				{
					try
					{
						CardCmd.Upgrade(card, CardPreviewStyle.HorizontalLayout);
					}
					catch
					{
					}
					CardEditPanel.Refresh();
				};
				hflowContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			if (card.CurrentUpgradeLevel > 0)
			{
				Button button2 = LoadoutPanel.CreateActionButton(Loc.Get("downgrade", null), new Color?(new Color("FF9944")));
				button2.CustomMinimumSize = new Vector2(60f, 30f);
				button2.Pressed += delegate
				{
					try
					{
						CardCmd.Downgrade(card);
					}
					catch
					{
					}
					CardEditPanel.Refresh();
				};
				hflowContainer.AddChild(button2, false, Node.InternalMode.Disabled);
			}
			if (CardEditPanel._editingPlayer != null)
			{
				Button button3 = LoadoutPanel.CreateActionButton(Loc.Get("copy", null), new Color?(StsColors.blue));
				button3.CustomMinimumSize = new Vector2(60f, 30f);
				button3.Pressed += delegate
				{
					try
					{
						ICardScope cardScope2;
						if (!CardEditPanel._editingPileType.IsCombatPile())
						{
							RunManager instance = RunManager.Instance;
							ICardScope cardScope = ((instance != null) ? instance.DebugOnlyGetState() : null);
							cardScope2 = cardScope;
						}
						else
						{
							CombatManager instance2 = CombatManager.Instance;
							ICardScope cardScope = ((instance2 != null) ? instance2.DebugOnlyGetState() : null);
							cardScope2 = cardScope;
						}
						ICardScope cardScope3 = cardScope2;
						if (cardScope3 != null)
						{
							TaskHelper.RunSafely(CardPileCmd.Add(cardScope3.CreateCard(card, CardEditPanel._editingPlayer), CardEditPanel._editingPileType, CardPilePosition.Bottom, null, false));
						}
					}
					catch
					{
					}
					CardEditPanel.Close();
					LoadoutPanel.RequestRefresh();
				};
				hflowContainer.AddChild(button3, false, Node.InternalMode.Disabled);
			}
			Button button4 = LoadoutPanel.CreateActionButton(Loc.Get("remove", null), new Color?(StsColors.red));
			button4.CustomMinimumSize = new Vector2(60f, 30f);
			button4.Pressed += delegate
			{
				try
				{
					if (CardEditPanel._editingPileType == PileType.Deck)
					{
						TaskHelper.RunSafely(CardPileCmd.RemoveFromDeck(card, false));
					}
					else
					{
						CardModel deckVersion = card.DeckVersion;
						if (deckVersion != null)
						{
							CardPile pile = deckVersion.Pile;
							if (pile != null && pile.Type == PileType.Deck)
							{
								TaskHelper.RunSafely(CardPileCmd.RemoveFromDeck(deckVersion, false));
							}
						}
						TaskHelper.RunSafely(CardPileCmd.RemoveFromCombat(card, false));
					}
				}
				catch
				{
				}
				CardEditPanel.Close();
				LoadoutPanel.RequestRefresh();
			};
			hflowContainer.AddChild(button4, false, Node.InternalMode.Disabled);
			Label label = new Label();
			label.Text = Loc.Fmt("edit.level", new object[] { card.CurrentUpgradeLevel });
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			hflowContainer.AddChild(label, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000109 RID: 265 RVA: 0x0000C8BC File Offset: 0x0000AABC
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

		// Token: 0x0600010A RID: 266 RVA: 0x0000C8DE File Offset: 0x0000AADE
		private static Label CreateSectionLabel(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 16);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			return label;
		}

		// Token: 0x0600010B RID: 267 RVA: 0x0000C914 File Offset: 0x0000AB14
		private static void AddDivider(VBoxContainer container, float alpha = 0.1f)
		{
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 1f),
				Color = new Color(0.91f, 0.86f, 0.75f, alpha),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
		}

		// Token: 0x0600010C RID: 268 RVA: 0x0000C969 File Offset: 0x0000AB69
		private static string GetVarDisplayName(string name)
		{
			return Loc.Get("var." + name, name);
		}

		// Token: 0x04000065 RID: 101
		[Nullable(2)]
		private static CanvasLayer _layer;

		// Token: 0x04000066 RID: 102
		[Nullable(2)]
		private static CardModel _editingCard;

		// Token: 0x04000067 RID: 103
		private static PileType _editingPileType;

		// Token: 0x04000068 RID: 104
		[Nullable(2)]
		private static Player _editingPlayer;

		// Token: 0x04000069 RID: 105
		private static int _enchantAmount = 3;

		// Token: 0x0400006A RID: 106
		[Nullable(2)]
		private static readonly FieldInfo EnergyCostField = AccessTools.Field(typeof(CardModel), "_energyCost");

		// Token: 0x0400006B RID: 107
		[Nullable(new byte[] { 2, 1 })]
		private static List<EnchantmentModel> _allEnchantments;
	}
}
