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
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager
{
	// Token: 0x02000013 RID: 19
	[NullableContext(1)]
	[Nullable(0)]
	internal static class InspectCardEdit
	{
		// Token: 0x17000002 RID: 2
		// (get) Token: 0x06000024 RID: 36 RVA: 0x00002DC7 File Offset: 0x00000FC7
		internal static bool IsAttached
		{
			get
			{
				return InspectCardEdit._screen != null;
			}
		}

		// Token: 0x06000025 RID: 37 RVA: 0x00002DD4 File Offset: 0x00000FD4
		internal static void Attach(NInspectCardScreen screen)
		{
			InspectCardEdit.Detach();
			InspectCardEdit._screen = screen;
			InspectCardEdit._panelWasHidden = true;
			LoadoutPanel.HideForInspect();
			if (InspectCardEdit.CardsField != null)
			{
				List<CardModel> list = InspectCardEdit.CardsField.GetValue(screen) as List<CardModel>;
				if (list != null)
				{
					for (int i = 0; i < list.Count; i++)
					{
						if (!list[i].IsMutable)
						{
							list[i] = list[i].ToMutable();
						}
					}
				}
			}
			Action action;
			if ((action = InspectCardEdit.<>O.<0>__BuildPanels) == null)
			{
				action = (InspectCardEdit.<>O.<0>__BuildPanels = new Action(InspectCardEdit.BuildPanels));
			}
			Callable.From(action).CallDeferred(Array.Empty<Variant>());
		}

		// Token: 0x06000026 RID: 38 RVA: 0x00002E77 File Offset: 0x00001077
		internal static void Detach()
		{
			InspectCardEdit.CleanupPanels();
			InspectCardEdit._screen = null;
			if (InspectCardEdit._panelWasHidden)
			{
				InspectCardEdit._panelWasHidden = false;
				LoadoutPanel.ShowAfterInspect();
			}
		}

		// Token: 0x06000027 RID: 39 RVA: 0x00002E98 File Offset: 0x00001098
		internal static void Refresh()
		{
			if (!InspectCardEdit.IsAttached || !GodotObject.IsInstanceValid(InspectCardEdit._screen))
			{
				return;
			}
			InspectCardEdit.CleanupPanels();
			Action action;
			if ((action = InspectCardEdit.<>O.<0>__BuildPanels) == null)
			{
				action = (InspectCardEdit.<>O.<0>__BuildPanels = new Action(InspectCardEdit.BuildPanels));
			}
			Callable.From(action).CallDeferred(Array.Empty<Variant>());
		}

		// Token: 0x06000028 RID: 40 RVA: 0x00002EEB File Offset: 0x000010EB
		private static void CleanupPanels()
		{
			if (InspectCardEdit._editLayer != null && GodotObject.IsInstanceValid(InspectCardEdit._editLayer))
			{
				InspectCardEdit._editLayer.QueueFree();
			}
			InspectCardEdit._editLayer = null;
			InspectCardEdit._leftPanel = null;
			InspectCardEdit._rightPanel = null;
		}

		// Token: 0x06000029 RID: 41 RVA: 0x00002F1C File Offset: 0x0000111C
		[NullableContext(2)]
		private static CardModel GetCurrentCard()
		{
			if (InspectCardEdit._screen == null || InspectCardEdit.CardsField == null || InspectCardEdit.IndexField == null)
			{
				return null;
			}
			List<CardModel> list = InspectCardEdit.CardsField.GetValue(InspectCardEdit._screen) as List<CardModel>;
			int num = (int)(InspectCardEdit.IndexField.GetValue(InspectCardEdit._screen) ?? 0);
			if (list == null || num < 0 || num >= list.Count)
			{
				return null;
			}
			return list[num];
		}

		// Token: 0x0600002A RID: 42 RVA: 0x00002F9C File Offset: 0x0000119C
		private static void RefreshCardDisplay()
		{
			if (InspectCardEdit._screen == null || !GodotObject.IsInstanceValid(InspectCardEdit._screen))
			{
				return;
			}
			try
			{
				InspectCardEdit._screen.Call("UpdateCardDisplay", Array.Empty<Variant>());
			}
			catch (Exception ex)
			{
				GD.PrintErr("[InspectCardEdit] UpdateCardDisplay: " + ex.Message);
			}
		}

		// Token: 0x0600002B RID: 43 RVA: 0x00003004 File Offset: 0x00001204
		private static void DoRefreshAll()
		{
			GD.Print("[InspectCardEdit] DoRefreshAll called");
			InspectCardEdit.RefreshCardDisplay();
			InspectCardEdit.RefreshGameCardVisuals();
			InspectCardEdit.NotifyPileChanged();
			if (!InspectCardEdit.IsAttached)
			{
				return;
			}
			InspectCardEdit.CleanupPanels();
			InspectCardEdit.BuildPanels();
		}

		// Token: 0x0600002C RID: 44 RVA: 0x00003034 File Offset: 0x00001234
		private static void NotifyPileChanged()
		{
			CardModel currentCard = InspectCardEdit.GetCurrentCard();
			CardPile cardPile = ((currentCard != null) ? currentCard.Pile : null);
			if (cardPile == null)
			{
				return;
			}
			try
			{
				cardPile.InvokeContentsChanged();
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(48, 1);
				defaultInterpolatedStringHandler.AppendLiteral("[InspectCardEdit] Notified pile ");
				defaultInterpolatedStringHandler.AppendFormatted<PileType>(cardPile.Type);
				defaultInterpolatedStringHandler.AppendLiteral(" ContentsChanged");
				GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
			}
			catch (Exception ex)
			{
				GD.PrintErr("[InspectCardEdit] InvokeContentsChanged failed: " + ex.Message);
			}
		}

		// Token: 0x0600002D RID: 45 RVA: 0x000030C4 File Offset: 0x000012C4
		private static void RefreshGameCardVisuals()
		{
			NGame instance = NGame.Instance;
			Window window;
			if (instance == null)
			{
				window = null;
			}
			else
			{
				SceneTree tree = instance.GetTree();
				window = ((tree != null) ? tree.Root : null);
			}
			Window window2 = window;
			if (window2 == null)
			{
				return;
			}
			int num = 0;
			InspectCardEdit.FindAndRefreshNCards(window2, ref num);
			if (num > 0)
			{
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(40, 1);
				defaultInterpolatedStringHandler.AppendLiteral("[InspectCardEdit] Refreshed ");
				defaultInterpolatedStringHandler.AppendFormatted<int>(num);
				defaultInterpolatedStringHandler.AppendLiteral(" NCard nodes");
				GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
			}
		}

		// Token: 0x0600002E RID: 46 RVA: 0x00003138 File Offset: 0x00001338
		private static void FindAndRefreshNCards(Node node, ref int count)
		{
			NCard ncard = node as NCard;
			if (ncard != null && GodotObject.IsInstanceValid(ncard))
			{
				try
				{
					ncard.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
					count++;
				}
				catch
				{
				}
			}
			foreach (Node node2 in node.GetChildren(false))
			{
				InspectCardEdit.FindAndRefreshNCards(node2, ref count);
			}
		}

		// Token: 0x0600002F RID: 47 RVA: 0x000031B8 File Offset: 0x000013B8
		private static void BuildPanels()
		{
			if (!InspectCardEdit.IsAttached || !GodotObject.IsInstanceValid(InspectCardEdit._screen))
			{
				return;
			}
			if (InspectCardEdit._editLayer != null && GodotObject.IsInstanceValid(InspectCardEdit._editLayer))
			{
				return;
			}
			CardModel currentCard = InspectCardEdit.GetCurrentCard();
			if (currentCard == null)
			{
				return;
			}
			InspectCardEdit._editLayer = new CanvasLayer
			{
				Layer = 101,
				Name = "InspectCardEditPanels"
			};
			NGame instance = NGame.Instance;
			if (instance != null)
			{
				instance.AddChild(InspectCardEdit._editLayer, false, Node.InternalMode.Disabled);
			}
			InspectCardEdit._leftPanel = InspectCardEdit.CreateSidePanel(0.02f, 0.2f);
			InspectCardEdit._rightPanel = InspectCardEdit.CreateSidePanel(0.8f, 0.98f);
			InspectCardEdit._editLayer.AddChild(InspectCardEdit._leftPanel, false, Node.InternalMode.Disabled);
			InspectCardEdit._editLayer.AddChild(InspectCardEdit._rightPanel, false, Node.InternalMode.Disabled);
			VBoxContainer panelVBox = InspectCardEdit.GetPanelVBox(InspectCardEdit._leftPanel);
			VBoxContainer panelVBox2 = InspectCardEdit.GetPanelVBox(InspectCardEdit._rightPanel);
			InspectCardEdit.BuildValuesSection(panelVBox, currentCard);
			InspectCardEdit.AddDivider(panelVBox);
			InspectCardEdit.BuildCostSection(panelVBox, currentCard);
			InspectCardEdit.BuildKeywordsSection(panelVBox2, currentCard);
			InspectCardEdit.AddDivider(panelVBox2);
			InspectCardEdit.BuildEnchantmentSection(panelVBox2, currentCard);
			InspectCardEdit.AddDivider(panelVBox2);
			InspectCardEdit.BuildActionsSection(panelVBox2, currentCard);
		}

		// Token: 0x06000030 RID: 48 RVA: 0x000032C8 File Offset: 0x000014C8
		private static PanelContainer CreateSidePanel(float anchorLeft, float anchorRight)
		{
			PanelContainer panelContainer = new PanelContainer();
			panelContainer.AnchorLeft = anchorLeft;
			panelContainer.AnchorRight = anchorRight;
			panelContainer.AnchorTop = 0.05f;
			panelContainer.AnchorBottom = 0.95f;
			panelContainer.MouseFilter = Control.MouseFilterEnum.Stop;
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.06f, 0.05f, 0.08f, 0.92f);
			styleBoxFlat.SetBorderWidthAll(0);
			styleBoxFlat.SetCornerRadiusAll(8);
			styleBoxFlat.SetContentMarginAll(10f);
			panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
			ScrollContainer scrollContainer = new ScrollContainer();
			scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			panelContainer.AddChild(scrollContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vboxContainer.AddThemeConstantOverride("separation", 8);
			scrollContainer.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
			return panelContainer;
		}

		// Token: 0x06000031 RID: 49 RVA: 0x000033A6 File Offset: 0x000015A6
		private static VBoxContainer GetPanelVBox(PanelContainer panel)
		{
			return panel.GetChild<ScrollContainer>(0, false).GetChild<VBoxContainer>(0, false);
		}

		// Token: 0x06000032 RID: 50 RVA: 0x000033B8 File Offset: 0x000015B8
		private static void BuildValuesSection(VBoxContainer container, CardModel card)
		{
			container.AddChild(InspectCardEdit.CreateSectionLabel(Loc.Get("edit.values", null)), false, Node.InternalMode.Disabled);
			Label label = new Label();
			label.Text = Loc.Get("modifier_hint", null);
			label.AddThemeFontSizeOverride("font_size", 11);
			label.AddThemeColorOverride("font_color", StsColors.gray);
			container.AddChild(label, false, Node.InternalMode.Disabled);
			HashSet<string> hashSet = new HashSet<string> { "Energy", "Stars" };
			bool flag = false;
			foreach (KeyValuePair<string, DynamicVar> keyValuePair in card.DynamicVars)
			{
				string text;
				DynamicVar dynamicVar;
				keyValuePair.Deconstruct(out text, out dynamicVar);
				string name = text;
				DynamicVar dynamicVar2 = dynamicVar;
				if (!hashSet.Contains(name))
				{
					flag = true;
					HBoxContainer hboxContainer = new HBoxContainer();
					hboxContainer.AddThemeConstantOverride("separation", 4);
					string text2 = Loc.Get("var." + name, name);
					Label label2 = new Label();
					label2.Text = text2 + ":";
					label2.CustomMinimumSize = new Vector2(70f, 0f);
					label2.AddThemeFontSizeOverride("font_size", 12);
					label2.AddThemeColorOverride("font_color", StsColors.cream);
					hboxContainer.AddChild(label2, false, Node.InternalMode.Disabled);
					DynamicVar capturedVar = dynamicVar2;
					Button button = LoadoutPanel.CreateActionButton("-", new Color?(StsColors.red));
					button.CustomMinimumSize = new Vector2(28f, 24f);
					button.Pressed += delegate
					{
						decimal baseValue = capturedVar.BaseValue;
						capturedVar.BaseValue -= InspectCardEdit.GetModifierAmount();
						DefaultInterpolatedStringHandler defaultInterpolatedStringHandler2 = new DefaultInterpolatedStringHandler(24, 3);
						defaultInterpolatedStringHandler2.AppendLiteral("[InspectCardEdit] ");
						defaultInterpolatedStringHandler2.AppendFormatted(name);
						defaultInterpolatedStringHandler2.AppendLiteral(": ");
						defaultInterpolatedStringHandler2.AppendFormatted<decimal>(baseValue);
						defaultInterpolatedStringHandler2.AppendLiteral(" -> ");
						defaultInterpolatedStringHandler2.AppendFormatted<decimal>(capturedVar.BaseValue);
						GD.Print(defaultInterpolatedStringHandler2.ToStringAndClear());
						InspectCardEdit.DoRefreshAll();
					};
					hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
					Label label3 = new Label();
					Label label4 = label3;
					DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
					defaultInterpolatedStringHandler.AppendFormatted<decimal>(dynamicVar2.BaseValue, "0");
					label4.Text = defaultInterpolatedStringHandler.ToStringAndClear();
					label3.CustomMinimumSize = new Vector2(36f, 0f);
					label3.AddThemeFontSizeOverride("font_size", 13);
					label3.AddThemeColorOverride("font_color", StsColors.gold);
					label3.HorizontalAlignment = HorizontalAlignment.Center;
					hboxContainer.AddChild(label3, false, Node.InternalMode.Disabled);
					Button button2 = LoadoutPanel.CreateActionButton("+", new Color?(StsColors.green));
					button2.CustomMinimumSize = new Vector2(28f, 24f);
					button2.Pressed += delegate
					{
						decimal baseValue2 = capturedVar.BaseValue;
						capturedVar.BaseValue += InspectCardEdit.GetModifierAmount();
						DefaultInterpolatedStringHandler defaultInterpolatedStringHandler3 = new DefaultInterpolatedStringHandler(24, 3);
						defaultInterpolatedStringHandler3.AppendLiteral("[InspectCardEdit] ");
						defaultInterpolatedStringHandler3.AppendFormatted(name);
						defaultInterpolatedStringHandler3.AppendLiteral(": ");
						defaultInterpolatedStringHandler3.AppendFormatted<decimal>(baseValue2);
						defaultInterpolatedStringHandler3.AppendLiteral(" -> ");
						defaultInterpolatedStringHandler3.AppendFormatted<decimal>(capturedVar.BaseValue);
						GD.Print(defaultInterpolatedStringHandler3.ToStringAndClear());
						InspectCardEdit.DoRefreshAll();
					};
					hboxContainer.AddChild(button2, false, Node.InternalMode.Disabled);
					container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
				}
			}
			if (!flag)
			{
				Label label5 = new Label();
				label5.Text = Loc.Get("edit.no_values", null);
				label5.AddThemeFontSizeOverride("font_size", 12);
				label5.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label5, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x06000033 RID: 51 RVA: 0x000036E4 File Offset: 0x000018E4
		private static void BuildCostSection(VBoxContainer container, CardModel card)
		{
			container.AddChild(InspectCardEdit.CreateSectionLabel(Loc.Get("edit.cost", null)), false, Node.InternalMode.Disabled);
			bool isX = card.EnergyCost.CostsX;
			Label label = new Label();
			label.Text = (isX ? Loc.Fmt("edit.cost_x_info", new object[] { card.EnergyCost.Canonical }) : Loc.Fmt("edit.cost_info", new object[] { card.EnergyCost.Canonical }));
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			container.AddChild(label, false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 3);
			hflowContainer.AddThemeConstantOverride("v_separation", 3);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			for (int i = 0; i <= 5; i++)
			{
				int cost = i;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
				defaultInterpolatedStringHandler.AppendFormatted<int>(cost);
				Button button = LoadoutPanel.CreateActionButton(defaultInterpolatedStringHandler.ToStringAndClear(), new Color?(StsColors.cream));
				button.CustomMinimumSize = new Vector2(32f, 24f);
				button.Pressed += delegate
				{
					try
					{
						if (card.EnergyCost.CostsX)
						{
							InspectCardEdit.SetEnergyCost(card, cost, false);
						}
						else
						{
							card.EnergyCost.SetCustomBaseCost(cost);
						}
					}
					catch
					{
					}
					InspectCardEdit.DoRefreshAll();
				};
				hflowContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			Button button2 = LoadoutPanel.CreateToggleButton("X", isX);
			button2.CustomMinimumSize = new Vector2(40f, 24f);
			button2.Pressed += delegate
			{
				try
				{
					if (isX)
					{
						InspectCardEdit.SetEnergyCost(card, 1, false);
					}
					else
					{
						InspectCardEdit.SetEnergyCost(card, 0, true);
					}
				}
				catch
				{
				}
				InspectCardEdit.DoRefreshAll();
			};
			hflowContainer.AddChild(button2, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000034 RID: 52 RVA: 0x000038CC File Offset: 0x00001ACC
		private static void SetEnergyCost(CardModel card, int canonicalCost, bool costsX)
		{
			if (InspectCardEdit.EnergyCostField == null)
			{
				return;
			}
			CardEnergyCost cardEnergyCost = new CardEnergyCost(card, canonicalCost, costsX);
			InspectCardEdit.EnergyCostField.SetValue(card, cardEnergyCost);
		}

		// Token: 0x06000035 RID: 53 RVA: 0x000038FC File Offset: 0x00001AFC
		private static void BuildKeywordsSection(VBoxContainer container, CardModel card)
		{
			container.AddChild(InspectCardEdit.CreateSectionLabel(Loc.Get("edit.keywords", null)), false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 4);
			hflowContainer.AddThemeConstantOverride("v_separation", 3);
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
				button.CustomMinimumSize = new Vector2(60f, 24f);
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
					catch
					{
					}
					InspectCardEdit.DoRefreshAll();
				};
				hflowContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x06000036 RID: 54 RVA: 0x00003AA8 File Offset: 0x00001CA8
		private static void BuildEnchantmentSection(VBoxContainer container, CardModel card)
		{
			container.AddChild(InspectCardEdit.CreateSectionLabel(Loc.Get("edit.enchantment", null)), false, Node.InternalMode.Disabled);
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
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 4);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			Label label = new Label();
			label.Text = Loc.Fmt("edit.current", new object[] { text });
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", (card.Enchantment != null) ? StsColors.green : StsColors.gray);
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			if (card.Enchantment != null)
			{
				Button button = LoadoutPanel.CreateActionButton(Loc.Get("clear", null), new Color?(StsColors.red));
				button.CustomMinimumSize = new Vector2(40f, 22f);
				button.Pressed += delegate
				{
					try
					{
						CardCmd.ClearEnchantment(card);
					}
					catch (Exception ex)
					{
						GD.PrintErr("[InspectCardEdit] ClearEnchantment failed: " + ex.Message);
					}
					InspectCardEdit.DoRefreshAll();
				};
				hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			HBoxContainer hboxContainer2 = new HBoxContainer();
			hboxContainer2.AddThemeConstantOverride("separation", 4);
			container.AddChild(hboxContainer2, false, Node.InternalMode.Disabled);
			Label label2 = new Label();
			label2.Text = Loc.Get("edit.ench_amount", null);
			label2.AddThemeFontSizeOverride("font_size", 11);
			label2.AddThemeColorOverride("font_color", StsColors.cream);
			hboxContainer2.AddChild(label2, false, Node.InternalMode.Disabled);
			Button button2 = LoadoutPanel.CreateActionButton("-", new Color?(StsColors.red));
			button2.CustomMinimumSize = new Vector2(24f, 22f);
			button2.Pressed += delegate
			{
				InspectCardEdit._enchantAmount = Math.Max(1, InspectCardEdit._enchantAmount - 1);
				InspectCardEdit.DoRefreshAll();
			};
			hboxContainer2.AddChild(button2, false, Node.InternalMode.Disabled);
			Label label3 = new Label();
			Label label4 = label3;
			defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
			defaultInterpolatedStringHandler.AppendFormatted<int>(InspectCardEdit._enchantAmount);
			label4.Text = defaultInterpolatedStringHandler.ToStringAndClear();
			label3.CustomMinimumSize = new Vector2(24f, 0f);
			label3.AddThemeFontSizeOverride("font_size", 12);
			label3.AddThemeColorOverride("font_color", StsColors.gold);
			label3.HorizontalAlignment = HorizontalAlignment.Center;
			hboxContainer2.AddChild(label3, false, Node.InternalMode.Disabled);
			Button button3 = LoadoutPanel.CreateActionButton("+", new Color?(StsColors.green));
			button3.CustomMinimumSize = new Vector2(24f, 22f);
			button3.Pressed += delegate
			{
				InspectCardEdit._enchantAmount++;
				InspectCardEdit.DoRefreshAll();
			};
			hboxContainer2.AddChild(button3, false, Node.InternalMode.Disabled);
			Label label5 = new Label();
			label5.Text = Loc.Get("edit.select_ench", null);
			label5.AddThemeFontSizeOverride("font_size", 11);
			label5.AddThemeColorOverride("font_color", StsColors.cream);
			container.AddChild(label5, false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 3);
			hflowContainer.AddThemeConstantOverride("v_separation", 3);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			try
			{
				if (InspectCardEdit._allEnchantments == null)
				{
					InspectCardEdit._allEnchantments = ModelDb.DebugEnchantments.Where((EnchantmentModel e) => e.GetType().Name != "DeprecatedEnchantment").OrderBy(delegate(EnchantmentModel e)
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
				foreach (EnchantmentModel enchantmentModel in InspectCardEdit._allEnchantments)
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
					button4.CustomMinimumSize = new Vector2(0f, 22f);
					if (flag)
					{
						EnchantmentModel capturedEnchant = enchantmentModel;
						button4.Pressed += delegate
						{
							try
							{
								DefaultInterpolatedStringHandler defaultInterpolatedStringHandler2 = new DefaultInterpolatedStringHandler(54, 4);
								defaultInterpolatedStringHandler2.AppendLiteral("[InspectCardEdit] Enchanting ");
								defaultInterpolatedStringHandler2.AppendFormatted<ModelId>(card.Id);
								defaultInterpolatedStringHandler2.AppendLiteral(" (pile=");
								CardPile pile = card.Pile;
								defaultInterpolatedStringHandler2.AppendFormatted<PileType?>((pile != null) ? new PileType?(pile.Type) : null);
								defaultInterpolatedStringHandler2.AppendLiteral(", mutable=");
								defaultInterpolatedStringHandler2.AppendFormatted<bool>(card.IsMutable);
								defaultInterpolatedStringHandler2.AppendLiteral(", hash=");
								defaultInterpolatedStringHandler2.AppendFormatted<int>(card.GetHashCode());
								defaultInterpolatedStringHandler2.AppendLiteral(")");
								GD.Print(defaultInterpolatedStringHandler2.ToStringAndClear());
								if (card.Enchantment != null)
								{
									CardCmd.ClearEnchantment(card);
								}
								CardCmd.Enchant((EnchantmentModel)capturedEnchant.MutableClone(), card, InspectCardEdit._enchantAmount);
								defaultInterpolatedStringHandler2 = new DefaultInterpolatedStringHandler(35, 2);
								defaultInterpolatedStringHandler2.AppendLiteral("[InspectCardEdit] Enchant OK: ");
								EnchantmentModel enchantment = card.Enchantment;
								defaultInterpolatedStringHandler2.AppendFormatted((enchantment != null) ? enchantment.GetType().Name : null);
								defaultInterpolatedStringHandler2.AppendLiteral(" amt=");
								EnchantmentModel enchantment2 = card.Enchantment;
								defaultInterpolatedStringHandler2.AppendFormatted<int?>((enchantment2 != null) ? new int?(enchantment2.Amount) : null);
								GD.Print(defaultInterpolatedStringHandler2.ToStringAndClear());
							}
							catch (Exception ex2)
							{
								GD.PrintErr("[InspectCardEdit] Enchant failed: " + ex2.Message);
							}
							InspectCardEdit.DoRefreshAll();
						};
					}
					hflowContainer.AddChild(button4, false, Node.InternalMode.Disabled);
				}
			}
			catch
			{
			}
		}

		// Token: 0x06000037 RID: 55 RVA: 0x000040A4 File Offset: 0x000022A4
		private static void BuildActionsSection(VBoxContainer container, CardModel card)
		{
			container.AddChild(InspectCardEdit.CreateSectionLabel(Loc.Get("edit.actions", null)), false, Node.InternalMode.Disabled);
			CombatManager instance = CombatManager.Instance;
			bool flag = instance != null && instance.IsInProgress;
			if (card.Pile == null)
			{
				HFlowContainer hflowContainer = new HFlowContainer();
				hflowContainer.AddThemeConstantOverride("h_separation", 4);
				hflowContainer.AddThemeConstantOverride("v_separation", 3);
				container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
				InspectCardEdit.AddPileButton(hflowContainer, card, Loc.Get("acquire", null), PileType.Deck, StsColors.green, false);
				if (flag)
				{
					InspectCardEdit.AddPileButton(hflowContainer, card, Loc.Get("to_hand", null), PileType.Hand, new Color("4499FF"), true);
					InspectCardEdit.AddPileButton(hflowContainer, card, Loc.Get("to_draw", null), PileType.Draw, new Color("4499FF"), true);
					InspectCardEdit.AddPileButton(hflowContainer, card, Loc.Get("pile.discard", null), PileType.Discard, StsColors.cream, true);
					InspectCardEdit.AddPileButton(hflowContainer, card, Loc.Get("pile.exhaust", null), PileType.Exhaust, StsColors.gray, true);
				}
			}
			HFlowContainer hflowContainer2 = new HFlowContainer();
			hflowContainer2.AddThemeConstantOverride("h_separation", 4);
			hflowContainer2.AddThemeConstantOverride("v_separation", 3);
			container.AddChild(hflowContainer2, false, Node.InternalMode.Disabled);
			if (card.IsUpgradable)
			{
				Button button = LoadoutPanel.CreateActionButton(Loc.Get("upgrade", null), new Color?(StsColors.green));
				button.CustomMinimumSize = new Vector2(50f, 26f);
				button.Pressed += delegate
				{
					try
					{
						CardCmd.Upgrade(card, CardPreviewStyle.HorizontalLayout);
					}
					catch
					{
					}
					InspectCardEdit.DoRefreshAll();
				};
				hflowContainer2.AddChild(button, false, Node.InternalMode.Disabled);
			}
			if (card.CurrentUpgradeLevel > 0)
			{
				Button button2 = LoadoutPanel.CreateActionButton(Loc.Get("downgrade", null), new Color?(new Color("FF9944")));
				button2.CustomMinimumSize = new Vector2(50f, 26f);
				button2.Pressed += delegate
				{
					try
					{
						CardCmd.Downgrade(card);
					}
					catch
					{
					}
					InspectCardEdit.DoRefreshAll();
				};
				hflowContainer2.AddChild(button2, false, Node.InternalMode.Disabled);
			}
			Label label = new Label();
			label.Text = Loc.Fmt("edit.level", new object[] { card.CurrentUpgradeLevel });
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			hflowContainer2.AddChild(label, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000038 RID: 56 RVA: 0x00004338 File Offset: 0x00002538
		private static void AddPileButton(HFlowContainer flow, CardModel card, string label, PileType pile, Color color, bool isCombatPile)
		{
			Button button = LoadoutPanel.CreateActionButton(label, new Color?(color));
			button.CustomMinimumSize = new Vector2(50f, 26f);
			button.Pressed += delegate
			{
				Player player = LoadoutPanel.GetPlayer();
				if (player == null)
				{
					return;
				}
				try
				{
					ICardScope cardScope2;
					if (!isCombatPile)
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
						CardModel byIdOrNull = ModelDb.GetByIdOrNull<CardModel>(card.Id);
						CardModel cardModel;
						if (byIdOrNull != null)
						{
							cardModel = cardScope3.CreateCard(byIdOrNull, player);
						}
						else
						{
							cardModel = cardScope3.CreateCard(card, player);
						}
						TaskHelper.RunSafely(UiHelper.AcquireCardWithPreview(cardModel, pile));
					}
				}
				catch (Exception ex)
				{
					GD.PrintErr("[InspectCardEdit] Acquire failed: " + ex.Message);
				}
			};
			flow.AddChild(button, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000039 RID: 57 RVA: 0x000043A0 File Offset: 0x000025A0
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

		// Token: 0x0600003A RID: 58 RVA: 0x000043C2 File Offset: 0x000025C2
		private static Label CreateSectionLabel(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			return label;
		}

		// Token: 0x0600003B RID: 59 RVA: 0x000043F8 File Offset: 0x000025F8
		private static void AddDivider(VBoxContainer container)
		{
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 1f),
				Color = new Color(0.91f, 0.86f, 0.75f, 0.1f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
		}

		// Token: 0x0400000F RID: 15
		[Nullable(2)]
		private static NInspectCardScreen _screen;

		// Token: 0x04000010 RID: 16
		[Nullable(2)]
		private static CanvasLayer _editLayer;

		// Token: 0x04000011 RID: 17
		[Nullable(2)]
		private static PanelContainer _leftPanel;

		// Token: 0x04000012 RID: 18
		[Nullable(2)]
		private static PanelContainer _rightPanel;

		// Token: 0x04000013 RID: 19
		private static int _enchantAmount = 3;

		// Token: 0x04000014 RID: 20
		private static bool _panelWasHidden;

		// Token: 0x04000015 RID: 21
		[Nullable(2)]
		private static readonly FieldInfo CardsField = AccessTools.Field(typeof(NInspectCardScreen), "_cards");

		// Token: 0x04000016 RID: 22
		[Nullable(2)]
		private static readonly FieldInfo IndexField = AccessTools.Field(typeof(NInspectCardScreen), "_index");

		// Token: 0x04000017 RID: 23
		[Nullable(2)]
		private static readonly FieldInfo EnergyCostField = AccessTools.Field(typeof(CardModel), "_energyCost");

		// Token: 0x04000018 RID: 24
		[Nullable(new byte[] { 2, 1 })]
		private static List<EnchantmentModel> _allEnchantments;

		// Token: 0x02000039 RID: 57
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x0400008E RID: 142
			[Nullable(0)]
			public static Action <0>__BuildPanels;
		}
	}
}
