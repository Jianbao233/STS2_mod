using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using FreeLoadout.Tabs;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.PotionLab;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager
{
	// Token: 0x02000028 RID: 40
	[NullableContext(1)]
	[Nullable(0)]
	internal static class LoadoutPanel
	{
		// Token: 0x17000019 RID: 25
		// (get) Token: 0x060000B4 RID: 180 RVA: 0x00007577 File Offset: 0x00005777
		// (set) Token: 0x060000B5 RID: 181 RVA: 0x0000757E File Offset: 0x0000577E
		internal static bool CardsShowUpgraded { get; set; }

		// Token: 0x1700001A RID: 26
		// (get) Token: 0x060000B6 RID: 182 RVA: 0x00007586 File Offset: 0x00005786
		// (set) Token: 0x060000B7 RID: 183 RVA: 0x0000758D File Offset: 0x0000578D
		internal static bool ShowMyCards { get; set; }

		// Token: 0x1700001B RID: 27
		// (get) Token: 0x060000B8 RID: 184 RVA: 0x00007595 File Offset: 0x00005795
		// (set) Token: 0x060000B9 RID: 185 RVA: 0x0000759C File Offset: 0x0000579C
		internal static int RelicBatchCount { get; set; } = 1;

		// Token: 0x1700001C RID: 28
		// (get) Token: 0x060000BA RID: 186 RVA: 0x000075A4 File Offset: 0x000057A4
		// (set) Token: 0x060000BB RID: 187 RVA: 0x000075AB File Offset: 0x000057AB
		internal static int PowerBatchCount { get; set; } = 1;

		// Token: 0x1700001D RID: 29
		// (get) Token: 0x060000BC RID: 188 RVA: 0x000075B4 File Offset: 0x000057B4
		internal static bool IsEmbeddedScreenActive
		{
			get
			{
				NRelicCollection relicScreen = LoadoutPanel._relicScreen;
				if (relicScreen == null || !relicScreen.Visible)
				{
					NCardLibrary cardScreen = LoadoutPanel._cardScreen;
					if (cardScreen == null || !cardScreen.Visible)
					{
						NPotionLab potionScreen = LoadoutPanel._potionScreen;
						return potionScreen != null && potionScreen.Visible;
					}
				}
				return true;
			}
		}

		// Token: 0x1700001E RID: 30
		// (get) Token: 0x060000BD RID: 189 RVA: 0x000075F8 File Offset: 0x000057F8
		internal static bool IsOpen
		{
			get
			{
				CanvasLayer layer = LoadoutPanel._layer;
				return layer != null && layer.Visible;
			}
		}

		// Token: 0x060000BE RID: 190 RVA: 0x00007618 File Offset: 0x00005818
		internal static void Toggle()
		{
			if (LoadoutPanel._layer == null || !GodotObject.IsInstanceValid(LoadoutPanel._layer))
			{
				LoadoutPanel.Build();
				return;
			}
			LoadoutPanel._layer.Visible = !LoadoutPanel._layer.Visible;
			if (LoadoutPanel._layer.Visible)
			{
				LoadoutPanel.RefreshContextBar();
				LoadoutPanel.RefreshCurrentTab();
			}
		}

		// Token: 0x060000BF RID: 191 RVA: 0x0000766B File Offset: 0x0000586B
		internal static void Show()
		{
			if (LoadoutPanel._layer == null || !GodotObject.IsInstanceValid(LoadoutPanel._layer))
			{
				LoadoutPanel.Build();
				return;
			}
			LoadoutPanel._layer.Visible = true;
			LoadoutPanel.RefreshContextBar();
			LoadoutPanel.RefreshCurrentTab();
		}

		// Token: 0x060000C0 RID: 192 RVA: 0x0000769B File Offset: 0x0000589B
		internal static void Hide()
		{
			if (LoadoutPanel._layer != null && GodotObject.IsInstanceValid(LoadoutPanel._layer))
			{
				LoadoutPanel._layer.Visible = false;
			}
		}

		// Token: 0x060000C1 RID: 193 RVA: 0x000076BB File Offset: 0x000058BB
		internal static void HideForInspect()
		{
			if (LoadoutPanel._layer != null && GodotObject.IsInstanceValid(LoadoutPanel._layer))
			{
				LoadoutPanel._layer.Visible = false;
			}
			if (LoadoutPanel._hoverTipLayer != null && GodotObject.IsInstanceValid(LoadoutPanel._hoverTipLayer))
			{
				LoadoutPanel._hoverTipLayer.Visible = false;
			}
		}

		// Token: 0x060000C2 RID: 194 RVA: 0x000076FC File Offset: 0x000058FC
		internal static void ShowAfterInspect()
		{
			if (LoadoutPanel._layer != null && GodotObject.IsInstanceValid(LoadoutPanel._layer))
			{
				LoadoutPanel._layer.Visible = true;
			}
			if (LoadoutPanel._hoverTipLayer != null && GodotObject.IsInstanceValid(LoadoutPanel._hoverTipLayer))
			{
				LoadoutPanel._hoverTipLayer.Visible = true;
			}
			LoadoutPanel.RefreshCurrentTab();
		}

		// Token: 0x060000C3 RID: 195 RVA: 0x0000774C File Offset: 0x0000594C
		internal static void ReparentToHoverTipLayer(Node tipSet)
		{
			if (LoadoutPanel._hoverTipLayer == null || !GodotObject.IsInstanceValid(LoadoutPanel._hoverTipLayer))
			{
				return;
			}
			if (tipSet.GetParent() == LoadoutPanel._hoverTipLayer)
			{
				return;
			}
			Node parent = tipSet.GetParent();
			if (parent != null)
			{
				parent.RemoveChild(tipSet);
			}
			LoadoutPanel._hoverTipLayer.AddChild(tipSet, false, Node.InternalMode.Disabled);
		}

		// Token: 0x060000C4 RID: 196 RVA: 0x0000779B File Offset: 0x0000599B
		internal static void ShowHoverTip(Control owner, IHoverTip tip, HoverTipAlignment alignment = HoverTipAlignment.Right)
		{
			LoadoutPanel.ShowHoverTips(owner, new IHoverTip[] { tip }, alignment);
		}

		// Token: 0x060000C5 RID: 197 RVA: 0x000077B0 File Offset: 0x000059B0
		internal static void ShowHoverTips(Control owner, IEnumerable<IHoverTip> tips, HoverTipAlignment alignment = HoverTipAlignment.Right)
		{
			try
			{
				NHoverTipSet nhoverTipSet = NHoverTipSet.CreateAndShow(owner, tips, alignment);
				if (LoadoutPanel._hoverTipLayer != null && GodotObject.IsInstanceValid(LoadoutPanel._hoverTipLayer) && nhoverTipSet != null && GodotObject.IsInstanceValid(nhoverTipSet))
				{
					Node parent = nhoverTipSet.GetParent();
					if (parent != null)
					{
						parent.RemoveChild(nhoverTipSet);
					}
					LoadoutPanel._hoverTipLayer.AddChild(nhoverTipSet, false, Node.InternalMode.Disabled);
				}
			}
			catch
			{
			}
		}

		// Token: 0x060000C6 RID: 198 RVA: 0x0000781C File Offset: 0x00005A1C
		[NullableContext(2)]
		internal static Player GetPlayer()
		{
			RunManager instance = RunManager.Instance;
			RunState runState = ((instance != null) ? instance.DebugOnlyGetState() : null);
			if (runState == null)
			{
				return null;
			}
			return LocalContext.GetMe(runState);
		}

		// Token: 0x060000C7 RID: 199 RVA: 0x00007846 File Offset: 0x00005A46
		internal static void RequestRefresh()
		{
			if (LoadoutPanel._layer != null && GodotObject.IsInstanceValid(LoadoutPanel._layer) && LoadoutPanel._layer.Visible)
			{
				LoadoutPanel.RefreshCurrentTab();
			}
		}

		// Token: 0x060000C8 RID: 200 RVA: 0x0000786C File Offset: 0x00005A6C
		private static void Build()
		{
			LoadoutPanel._layer = new CanvasLayer();
			LoadoutPanel._layer.Layer = 100;
			LoadoutPanel._layer.Name = "LoadoutPanel";
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
					LoadoutPanel.Hide();
					Viewport viewport = backstop.GetViewport();
					if (viewport == null)
					{
						return;
					}
					viewport.SetInputAsHandled();
				}
			};
			LoadoutPanel._layer.AddChild(backstop, false, Node.InternalMode.Disabled);
			PanelContainer panelContainer = new PanelContainer();
			panelContainer.AnchorLeft = 0.05f;
			panelContainer.AnchorRight = 0.95f;
			panelContainer.AnchorTop = 0.05f;
			panelContainer.AnchorBottom = 0.95f;
			panelContainer.GrowHorizontal = Control.GrowDirection.Both;
			panelContainer.GrowVertical = Control.GrowDirection.Both;
			panelContainer.MouseFilter = Control.MouseFilterEnum.Stop;
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.08f, 0.06f, 0.1f, 0.95f);
			styleBoxFlat.SetBorderWidthAll(0);
			styleBoxFlat.SetCornerRadiusAll(8);
			styleBoxFlat.SetContentMarginAll(12f);
			styleBoxFlat.ShadowSize = 0;
			styleBoxFlat.ShadowColor = Colors.Transparent;
			styleBoxFlat.SetExpandMarginAll(0f);
			panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
			LoadoutPanel._panel = panelContainer;
			LoadoutPanel._panelStyleNormal = styleBoxFlat;
			LoadoutPanel._panelStyleClear = new StyleBoxFlat();
			LoadoutPanel._panelStyleClear.BgColor = Colors.Transparent;
			LoadoutPanel._panelStyleClear.SetBorderWidthAll(0);
			LoadoutPanel._panelStyleClear.SetContentMarginAll(0f);
			LoadoutPanel._panelStyleClear.ShadowSize = 0;
			LoadoutPanel._panelStyleClear.ShadowColor = Colors.Transparent;
			LoadoutPanel._panelStyleClear.SetExpandMarginAll(0f);
			LoadoutPanel._layer.AddChild(panelContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.AnchorRight = 1f;
			vboxContainer.AnchorBottom = 1f;
			vboxContainer.AddThemeConstantOverride("separation", 8);
			panelContainer.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
			LoadoutPanel._mainVBox = vboxContainer;
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 4);
			vboxContainer.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			string[] array = new string[]
			{
				Loc.Get("tab.cards", null),
				Loc.Get("tab.relics", null),
				Loc.Get("tab.potions", null),
				Loc.Get("tab.powers", null),
				Loc.Get("tab.events", null),
				Loc.Get("tab.encounters", null),
				Loc.Get("tab.character", null)
			};
			LoadoutPanel._tabButtons = new Button[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				int tabIndex = i;
				Button button = LoadoutPanel.CreateTabButton(array[i]);
				button.Pressed += delegate
				{
					LoadoutPanel.SwitchTab(tabIndex);
				};
				hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
				LoadoutPanel._tabButtons[i] = button;
			}
			hboxContainer.AddChild(new Control
			{
				SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
			}, false, Node.InternalMode.Disabled);
			LoadoutPanel._contextBar = new HBoxContainer();
			LoadoutPanel._contextBar.AddThemeConstantOverride("separation", 4);
			hboxContainer.AddChild(LoadoutPanel._contextBar, false, Node.InternalMode.Disabled);
			Button button2 = LoadoutPanel.CreateTabButton("✕");
			button2.CustomMinimumSize = new Vector2(40f, 36f);
			BaseButton baseButton = button2;
			Action action;
			if ((action = LoadoutPanel.<>O.<0>__Hide) == null)
			{
				action = (LoadoutPanel.<>O.<0>__Hide = new Action(LoadoutPanel.Hide));
			}
			baseButton.Pressed += action;
			hboxContainer.AddChild(button2, false, Node.InternalMode.Disabled);
			LoadoutPanel._divider = new ColorRect();
			LoadoutPanel._divider.CustomMinimumSize = new Vector2(0f, 2f);
			LoadoutPanel._divider.Color = new Color(0.91f, 0.86f, 0.75f, 0.25f);
			LoadoutPanel._divider.MouseFilter = Control.MouseFilterEnum.Ignore;
			vboxContainer.AddChild(LoadoutPanel._divider, false, Node.InternalMode.Disabled);
			LoadoutPanel._scrollContainer = new ScrollContainer();
			LoadoutPanel._scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			LoadoutPanel._scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			vboxContainer.AddChild(LoadoutPanel._scrollContainer, false, Node.InternalMode.Disabled);
			LoadoutPanel._contentContainer = new VBoxContainer();
			LoadoutPanel._contentContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			LoadoutPanel._contentContainer.AddThemeConstantOverride("separation", 6);
			LoadoutPanel._scrollContainer.AddChild(LoadoutPanel._contentContainer, false, Node.InternalMode.Disabled);
			LoadoutPanel._hoverTipLayer = new CanvasLayer();
			LoadoutPanel._hoverTipLayer.Layer = 101;
			LoadoutPanel._hoverTipLayer.Name = "LoadoutHoverTips";
			NGame instance = NGame.Instance;
			if (instance != null)
			{
				instance.AddChild(LoadoutPanel._layer, false, Node.InternalMode.Disabled);
			}
			NGame instance2 = NGame.Instance;
			if (instance2 != null)
			{
				instance2.AddChild(LoadoutPanel._hoverTipLayer, false, Node.InternalMode.Disabled);
			}
			LoadoutPanel._activeTab = 0;
			LoadoutPanel.UpdateTabHighlights();
			LoadoutPanel.RefreshContextBar();
			LoadoutPanel.RefreshCurrentTab();
		}

		// Token: 0x060000C9 RID: 201 RVA: 0x00007D63 File Offset: 0x00005F63
		private static void SwitchTab(int index)
		{
			LoadoutPanel._activeTab = index;
			LoadoutPanel.UpdateTabHighlights();
			LoadoutPanel.RefreshContextBar();
			LoadoutPanel.RefreshCurrentTab();
		}

		// Token: 0x060000CA RID: 202 RVA: 0x00007D7C File Offset: 0x00005F7C
		private static void RefreshCurrentTab()
		{
			if (LoadoutPanel._contentContainer == null || LoadoutPanel._mainVBox == null)
			{
				return;
			}
			switch (LoadoutPanel._activeTab)
			{
			case 0:
				if (LoadoutPanel.ShowMyCards)
				{
					LoadoutPanel.HideEmbeddedScreens();
					LoadoutPanel.ClearChildren(LoadoutPanel._contentContainer);
					Player player = LoadoutPanel.GetPlayer();
					if (player != null)
					{
						LoadoutPanel.BuildMyCardsView(LoadoutPanel._contentContainer, player);
						return;
					}
				}
				else
				{
					Func<NCardLibrary> func;
					if ((func = LoadoutPanel.<>O.<1>__CreateCardScreen) == null)
					{
						func = (LoadoutPanel.<>O.<1>__CreateCardScreen = new Func<NCardLibrary>(LoadoutPanel.CreateCardScreen));
					}
					LoadoutPanel.ShowEmbeddedScreen<NCardLibrary>(ref LoadoutPanel._cardScreen, func);
				}
				return;
			case 1:
			{
				Func<NRelicCollection> func2;
				if ((func2 = LoadoutPanel.<>O.<2>__CreateRelicScreen) == null)
				{
					func2 = (LoadoutPanel.<>O.<2>__CreateRelicScreen = new Func<NRelicCollection>(LoadoutPanel.CreateRelicScreen));
				}
				LoadoutPanel.ShowEmbeddedScreen<NRelicCollection>(ref LoadoutPanel._relicScreen, func2);
				return;
			}
			case 2:
			{
				Func<NPotionLab> func3;
				if ((func3 = LoadoutPanel.<>O.<3>__CreatePotionScreen) == null)
				{
					func3 = (LoadoutPanel.<>O.<3>__CreatePotionScreen = new Func<NPotionLab>(LoadoutPanel.CreatePotionScreen));
				}
				LoadoutPanel.ShowEmbeddedScreen<NPotionLab>(ref LoadoutPanel._potionScreen, func3);
				return;
			}
			default:
			{
				LoadoutPanel.HideEmbeddedScreens();
				LoadoutPanel.ClearChildren(LoadoutPanel._contentContainer);
				Player player2 = LoadoutPanel.GetPlayer();
				if (player2 == null)
				{
					Label label = new Label();
					label.Text = Loc.Get("not_in_game", null);
					label.AddThemeFontSizeOverride("font_size", 20);
					label.AddThemeColorOverride("font_color", StsColors.cream);
					LoadoutPanel._contentContainer.AddChild(label, false, Node.InternalMode.Disabled);
					return;
				}
				switch (LoadoutPanel._activeTab)
				{
				case 3:
					PowersTab.Build(LoadoutPanel._contentContainer, player2);
					return;
				case 4:
					EventsTab.Build(LoadoutPanel._contentContainer, player2);
					return;
				case 5:
					EncountersTab.Build(LoadoutPanel._contentContainer, player2);
					return;
				case 6:
					CharacterTab.Build(LoadoutPanel._contentContainer, player2);
					return;
				default:
					return;
				}
				break;
			}
			}
		}

		// Token: 0x060000CB RID: 203 RVA: 0x00007F08 File Offset: 0x00006108
		private static void RefreshContextBar()
		{
			if (LoadoutPanel._contextBar == null)
			{
				return;
			}
			LoadoutPanel.ClearChildren(LoadoutPanel._contextBar);
			switch (LoadoutPanel._activeTab)
			{
			case 0:
				LoadoutPanel.BuildCardsPileContextBar();
				return;
			case 1:
				LoadoutPanel.BuildBatchContextBar("relics");
				return;
			case 2:
				break;
			case 3:
				LoadoutPanel.BuildBatchContextBar("powers");
				LoadoutPanel.BuildPowersPresetContextBar();
				break;
			default:
				return;
			}
		}

		// Token: 0x060000CC RID: 204 RVA: 0x00007F64 File Offset: 0x00006164
		private static void BuildCardsPileContextBar()
		{
			if (LoadoutPanel._contextBar == null)
			{
				return;
			}
			Button myCardsToggle = LoadoutPanel.CreateToggleButton(Loc.Get("pile.my_cards", null), LoadoutPanel.ShowMyCards);
			myCardsToggle.CustomMinimumSize = new Vector2(90f, 36f);
			myCardsToggle.Pressed += delegate
			{
				LoadoutPanel.ShowMyCards = !LoadoutPanel.ShowMyCards;
				LoadoutPanel.UpdateToggleButton(myCardsToggle, LoadoutPanel.ShowMyCards);
				LoadoutPanel.RefreshCurrentTab();
			};
			LoadoutPanel._contextBar.AddChild(myCardsToggle, false, Node.InternalMode.Disabled);
		}

		// Token: 0x060000CD RID: 205 RVA: 0x00007FE0 File Offset: 0x000061E0
		private static void BuildMyCardsView(VBoxContainer container, Player player)
		{
			CombatManager instance = CombatManager.Instance;
			if (instance != null && instance.IsInProgress)
			{
				LoadoutPanel.BuildMyCardsPileGroup(container, player, PileType.Hand, Loc.Get("pile.hand", null));
				LoadoutPanel.BuildMyCardsPileGroup(container, player, PileType.Draw, Loc.Get("pile.draw", null));
				LoadoutPanel.BuildMyCardsPileGroup(container, player, PileType.Discard, Loc.Get("pile.discard", null));
				LoadoutPanel.BuildMyCardsPileGroup(container, player, PileType.Exhaust, Loc.Get("pile.exhaust", null));
			}
			LoadoutPanel.BuildMyCardsPileGroup(container, player, PileType.Deck, Loc.Get("pile.deck", null));
		}

		// Token: 0x060000CE RID: 206 RVA: 0x00008060 File Offset: 0x00006260
		private static void BuildMyCardsPileGroup(VBoxContainer container, Player player, PileType pileType, string label)
		{
			CardPile cardPile = CardPile.Get(pileType, player);
			if (cardPile == null)
			{
				return;
			}
			IReadOnlyList<CardModel> cards = cardPile.Cards;
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(3, 2);
			defaultInterpolatedStringHandler.AppendFormatted(label);
			defaultInterpolatedStringHandler.AppendLiteral(" (");
			defaultInterpolatedStringHandler.AppendFormatted<int>(cards.Count);
			defaultInterpolatedStringHandler.AppendLiteral(")");
			container.AddChild(LoadoutPanel.CreateSectionHeader(defaultInterpolatedStringHandler.ToStringAndClear()), false, Node.InternalMode.Disabled);
			if (cards.Count == 0)
			{
				Label label2 = new Label();
				label2.Text = Loc.Get("empty", null);
				label2.AddThemeFontSizeOverride("font_size", 13);
				label2.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label2, false, Node.InternalMode.Disabled);
				return;
			}
			List<CardModel> list = cards.ToList<CardModel>();
			foreach (IGrouping<CardType, CardModel> grouping in from c in list
				group c by c.Type into g
				orderby g.Key
				select g)
			{
				defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(5, 1);
				defaultInterpolatedStringHandler.AppendLiteral("type.");
				defaultInterpolatedStringHandler.AppendFormatted<CardType>(grouping.Key);
				string text = Loc.Get(defaultInterpolatedStringHandler.ToStringAndClear(), grouping.Key.ToString());
				Color color;
				switch (grouping.Key)
				{
				case CardType.Attack:
					color = StsColors.red;
					break;
				case CardType.Skill:
					color = StsColors.blue;
					break;
				case CardType.Power:
					color = new Color("CC77FF");
					break;
				default:
					color = StsColors.gray;
					break;
				}
				Color color2 = color;
				Label label3 = new Label();
				Label label4 = label3;
				defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(5, 2);
				defaultInterpolatedStringHandler.AppendLiteral("  ");
				defaultInterpolatedStringHandler.AppendFormatted(text);
				defaultInterpolatedStringHandler.AppendLiteral(" (");
				defaultInterpolatedStringHandler.AppendFormatted<int>(grouping.Count<CardModel>());
				defaultInterpolatedStringHandler.AppendLiteral(")");
				label4.Text = defaultInterpolatedStringHandler.ToStringAndClear();
				label3.AddThemeFontSizeOverride("font_size", 14);
				label3.AddThemeColorOverride("font_color", color2);
				container.AddChild(label3, false, Node.InternalMode.Disabled);
				GridContainer gridContainer = new GridContainer
				{
					Columns = 6
				};
				gridContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				gridContainer.AddThemeConstantOverride("h_separation", 4);
				gridContainer.AddThemeConstantOverride("v_separation", 4);
				container.AddChild(gridContainer, false, Node.InternalMode.Disabled);
				foreach (CardModel cardModel in grouping.OrderBy(delegate(CardModel c)
				{
					string text2;
					try
					{
						text2 = c.Title;
					}
					catch
					{
						text2 = c.Id.Entry;
					}
					return text2;
				}))
				{
					CardModel capturedCard = cardModel;
					List<CardModel> capturedList = list;
					Control control = CardsTab.CreateNCardWrapperPublic(cardModel, pileType, list, delegate
					{
						int num = capturedList.IndexOf(capturedCard);
						if (num < 0)
						{
							num = 0;
						}
						NGame instance = NGame.Instance;
						NInspectCardScreen ninspectCardScreen = ((instance != null) ? instance.GetInspectCardScreen() : null);
						if (ninspectCardScreen == null)
						{
							return;
						}
						ninspectCardScreen.Open(capturedList, num, false);
						InspectCardEdit.Attach(ninspectCardScreen);
					});
					gridContainer.AddChild(control, false, Node.InternalMode.Disabled);
				}
			}
		}

		// Token: 0x060000CF RID: 207 RVA: 0x000083C0 File Offset: 0x000065C0
		private static void BuildBatchContextBar(string target)
		{
			if (LoadoutPanel._contextBar == null)
			{
				return;
			}
			int num = ((target == "relics") ? LoadoutPanel.RelicBatchCount : LoadoutPanel.PowerBatchCount);
			Label label = new Label();
			label.Text = Loc.Get("quantity", null);
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			LoadoutPanel._contextBar.AddChild(label, false, Node.InternalMode.Disabled);
			int[] array = new int[] { 1, 5, 10, 20 };
			for (int i = 0; i < array.Length; i++)
			{
				int num2 = array[i];
				int c = num2;
				bool flag = num == c;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 1);
				defaultInterpolatedStringHandler.AppendLiteral("×");
				defaultInterpolatedStringHandler.AppendFormatted<int>(c);
				Button button = LoadoutPanel.CreateToggleButton(defaultInterpolatedStringHandler.ToStringAndClear(), flag);
				button.CustomMinimumSize = new Vector2(50f, 36f);
				button.Pressed += delegate
				{
					if (target == "relics")
					{
						LoadoutPanel.RelicBatchCount = c;
					}
					else
					{
						LoadoutPanel.PowerBatchCount = c;
					}
					LoadoutPanel.RefreshContextBar();
					LoadoutPanel.RequestRefresh();
				};
				LoadoutPanel._contextBar.AddChild(button, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x060000D0 RID: 208 RVA: 0x00008518 File Offset: 0x00006718
		private static void BuildPowersPresetContextBar()
		{
			if (LoadoutPanel._contextBar == null)
			{
				return;
			}
			VSeparator vseparator = new VSeparator();
			vseparator.CustomMinimumSize = new Vector2(2f, 0f);
			LoadoutPanel._contextBar.AddChild(vseparator, false, Node.InternalMode.Disabled);
			Button presetToggle = LoadoutPanel.CreateToggleButton(Loc.Get("presets", null), PowerPresets.Enabled);
			presetToggle.CustomMinimumSize = new Vector2(70f, 36f);
			presetToggle.Pressed += delegate
			{
				PowerPresets.Enabled = !PowerPresets.Enabled;
				LoadoutPanel.UpdateToggleButton(presetToggle, PowerPresets.Enabled);
			};
			LoadoutPanel._contextBar.AddChild(presetToggle, false, Node.InternalMode.Disabled);
			Button button = LoadoutPanel.CreateToggleButton(Loc.Get("to_player", null), PowerPresets.PresetTarget == 0);
			button.CustomMinimumSize = new Vector2(80f, 36f);
			button.Pressed += delegate
			{
				PowerPresets.PresetTarget = 0;
				LoadoutPanel.RefreshContextBar();
				LoadoutPanel.RequestRefresh();
			};
			LoadoutPanel._contextBar.AddChild(button, false, Node.InternalMode.Disabled);
			Button button2 = LoadoutPanel.CreateToggleButton(Loc.Get("to_enemy", null), PowerPresets.PresetTarget == 1);
			button2.CustomMinimumSize = new Vector2(80f, 36f);
			button2.Pressed += delegate
			{
				PowerPresets.PresetTarget = 1;
				LoadoutPanel.RefreshContextBar();
				LoadoutPanel.RequestRefresh();
			};
			LoadoutPanel._contextBar.AddChild(button2, false, Node.InternalMode.Disabled);
		}

		// Token: 0x060000D1 RID: 209 RVA: 0x00008680 File Offset: 0x00006880
		private static void UpdateTabHighlights()
		{
			if (LoadoutPanel._tabButtons == null)
			{
				return;
			}
			for (int i = 0; i < LoadoutPanel._tabButtons.Length; i++)
			{
				Button button = LoadoutPanel._tabButtons[i];
				if (i == LoadoutPanel._activeTab)
				{
					button.AddThemeColorOverride("font_color", StsColors.gold);
					button.AddThemeColorOverride("font_hover_color", StsColors.gold);
				}
				else
				{
					button.AddThemeColorOverride("font_color", StsColors.cream);
					button.AddThemeColorOverride("font_hover_color", StsColors.gold);
				}
			}
		}

		// Token: 0x060000D2 RID: 210 RVA: 0x00008710 File Offset: 0x00006910
		private static void ShowEmbeddedScreen<[Nullable(0)] T>([Nullable(2)] ref T screen, Func<T> factory) where T : NSubmenu
		{
			LoadoutPanel.HideEmbeddedScreens();
			if (LoadoutPanel._scrollContainer != null)
			{
				LoadoutPanel._scrollContainer.Visible = false;
			}
			if (screen == null || !GodotObject.IsInstanceValid(screen))
			{
				screen = factory();
				screen.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
				screen.Visible = false;
				LoadoutPanel._mainVBox.AddChild(screen, false, Node.InternalMode.Disabled);
			}
			screen.Visible = true;
			screen.OnSubmenuOpened();
			NSubmenu capturedForBtn = screen;
			Callable.From(delegate
			{
				if (!GodotObject.IsInstanceValid(capturedForBtn))
				{
					return;
				}
				Control nodeOrNull = capturedForBtn.GetNodeOrNull<Control>("BackButton");
				if (nodeOrNull != null)
				{
					nodeOrNull.Hide();
				}
				LoadoutPanel.HideScreenShadows(capturedForBtn);
			}).CallDeferred(Array.Empty<Variant>());
			if (LoadoutPanel._panel != null && LoadoutPanel._panelStyleClear != null)
			{
				LoadoutPanel._panel.AddThemeStyleboxOverride("panel", LoadoutPanel._panelStyleClear);
			}
			ColorRect divider = LoadoutPanel._divider;
			if (divider != null)
			{
				divider.Hide();
			}
			LoadoutPanel.SetTabButtonsChrome(false);
		}

		// Token: 0x060000D3 RID: 211 RVA: 0x0000881B File Offset: 0x00006A1B
		private static NRelicCollection CreateRelicScreen()
		{
			return NRelicCollection.Create();
		}

		// Token: 0x060000D4 RID: 212 RVA: 0x00008822 File Offset: 0x00006A22
		private static NCardLibrary CreateCardScreen()
		{
			return NCardLibrary.Create();
		}

		// Token: 0x060000D5 RID: 213 RVA: 0x00008829 File Offset: 0x00006A29
		private static NPotionLab CreatePotionScreen()
		{
			return NPotionLab.Create();
		}

		// Token: 0x060000D6 RID: 214 RVA: 0x00008830 File Offset: 0x00006A30
		private static void HideEmbeddedScreens()
		{
			NRelicCollection relicScreen = LoadoutPanel._relicScreen;
			if (relicScreen != null && relicScreen.Visible)
			{
				try
				{
					LoadoutPanel._relicScreen.OnSubmenuClosed();
				}
				catch
				{
				}
				LoadoutPanel._relicScreen.Visible = false;
			}
			NCardLibrary cardScreen = LoadoutPanel._cardScreen;
			if (cardScreen != null && cardScreen.Visible)
			{
				try
				{
					LoadoutPanel._cardScreen.OnSubmenuClosed();
				}
				catch
				{
				}
				LoadoutPanel._cardScreen.Visible = false;
			}
			NPotionLab potionScreen = LoadoutPanel._potionScreen;
			if (potionScreen != null && potionScreen.Visible)
			{
				try
				{
					LoadoutPanel._potionScreen.OnSubmenuClosed();
				}
				catch
				{
				}
				LoadoutPanel._potionScreen.Visible = false;
			}
			if (LoadoutPanel._scrollContainer != null)
			{
				LoadoutPanel._scrollContainer.Visible = true;
			}
			if (LoadoutPanel._panel != null && LoadoutPanel._panelStyleNormal != null)
			{
				LoadoutPanel._panel.AddThemeStyleboxOverride("panel", LoadoutPanel._panelStyleNormal);
			}
			ColorRect divider = LoadoutPanel._divider;
			if (divider != null)
			{
				divider.Show();
			}
			LoadoutPanel.SetTabButtonsChrome(true);
		}

		// Token: 0x060000D7 RID: 215 RVA: 0x00008938 File Offset: 0x00006B38
		private static void SetTabButtonsChrome(bool visible)
		{
			if (LoadoutPanel._tabButtons == null)
			{
				return;
			}
			foreach (Button button in LoadoutPanel._tabButtons)
			{
				if (visible)
				{
					LoadoutPanel.ApplyFlatStyle(button);
					button.AddThemeConstantOverride("outline_size", 4);
				}
				else
				{
					StyleBoxEmpty styleBoxEmpty = new StyleBoxEmpty();
					button.AddThemeStyleboxOverride("normal", styleBoxEmpty);
					button.AddThemeStyleboxOverride("hover", styleBoxEmpty);
					button.AddThemeStyleboxOverride("pressed", styleBoxEmpty);
					button.AddThemeStyleboxOverride("focus", styleBoxEmpty);
					button.AddThemeConstantOverride("outline_size", 0);
				}
			}
			LoadoutPanel.UpdateTabHighlights();
		}

		// Token: 0x060000D8 RID: 216 RVA: 0x000089E4 File Offset: 0x00006BE4
		private static void HideScreenShadows(Node screen)
		{
			foreach (string text in new string[] { "*Shadow*", "*shadow*", "*Gradient*", "*gradient*", "*Fade*", "*fade*", "*Vignette*", "*Darkener*" })
			{
				Control control = screen.FindChild(text, true, false) as Control;
				if (control != null)
				{
					control.Visible = false;
				}
			}
			LoadoutPanel.HideBottomTextures(screen);
		}

		// Token: 0x060000D9 RID: 217 RVA: 0x00008A6C File Offset: 0x00006C6C
		private static void HideBottomTextures(Node parent)
		{
			foreach (Node node in parent.GetChildren(false))
			{
				TextureRect textureRect = node as TextureRect;
				if (textureRect != null && textureRect.AnchorTop >= 0.8f)
				{
					textureRect.Visible = false;
				}
				if (node is Control)
				{
					foreach (Node node2 in node.GetChildren(false))
					{
						TextureRect textureRect2 = node2 as TextureRect;
						if (textureRect2 != null && textureRect2.AnchorTop >= 0.8f)
						{
							textureRect2.Visible = false;
						}
					}
				}
			}
		}

		// Token: 0x060000DA RID: 218 RVA: 0x00008B30 File Offset: 0x00006D30
		internal static void ClearChildren(Node parent)
		{
			foreach (Node node in parent.GetChildren(false))
			{
				parent.RemoveChild(node);
				node.QueueFree();
			}
		}

		// Token: 0x060000DB RID: 219 RVA: 0x00008B84 File Offset: 0x00006D84
		internal static Label CreateSectionHeader(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 18);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			label.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.15f, 0.18f, 0.8f));
			label.AddThemeConstantOverride("outline_size", 4);
			Font font = GD.Load<Font>("res://themes/kreon_bold_glyph_space_two.tres");
			if (font != null)
			{
				label.AddThemeFontOverride("font", font);
			}
			return label;
		}

		// Token: 0x060000DC RID: 220 RVA: 0x00008C20 File Offset: 0x00006E20
		internal static Button CreateItemButton(string text, Vector2? minSize = null, int fontSize = 14)
		{
			Button button = new Button();
			button.Text = text;
			button.CustomMinimumSize = minSize ?? new Vector2(0f, 32f);
			button.AddThemeFontSizeOverride("font_size", fontSize);
			button.AddThemeColorOverride("font_color", StsColors.cream);
			button.AddThemeColorOverride("font_hover_color", StsColors.gold);
			button.AddThemeColorOverride("font_pressed_color", StsColors.gray);
			button.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.15f, 0.18f, 0.8f));
			button.AddThemeConstantOverride("outline_size", 4);
			LoadoutPanel.ApplyFlatStyle(button);
			return button;
		}

		// Token: 0x060000DD RID: 221 RVA: 0x00008CF4 File Offset: 0x00006EF4
		internal static Button CreateActionButton(string text, Color? fontColor = null)
		{
			Button button = new Button();
			button.Text = text;
			button.CustomMinimumSize = new Vector2(60f, 28f);
			button.AddThemeFontSizeOverride("font_size", 13);
			button.AddThemeColorOverride("font_color", fontColor ?? StsColors.cream);
			button.AddThemeColorOverride("font_hover_color", StsColors.gold);
			button.AddThemeColorOverride("font_pressed_color", StsColors.gray);
			LoadoutPanel.ApplyFlatStyle(button);
			return button;
		}

		// Token: 0x060000DE RID: 222 RVA: 0x00008D90 File Offset: 0x00006F90
		private static Button CreateTabButton(string text)
		{
			Button button = new Button();
			button.Text = text;
			button.CustomMinimumSize = new Vector2(80f, 36f);
			button.AddThemeFontSizeOverride("font_size", 16);
			button.AddThemeColorOverride("font_color", StsColors.cream);
			button.AddThemeColorOverride("font_hover_color", StsColors.gold);
			button.AddThemeColorOverride("font_pressed_color", StsColors.gray);
			button.AddThemeColorOverride("font_outline_color", new Color(0.1f, 0.15f, 0.18f, 0.8f));
			button.AddThemeConstantOverride("outline_size", 4);
			Font font = GD.Load<Font>("res://themes/kreon_bold_glyph_space_two.tres");
			if (font != null)
			{
				button.AddThemeFontOverride("font", font);
			}
			LoadoutPanel.ApplyFlatStyle(button);
			return button;
		}

		// Token: 0x060000DF RID: 223 RVA: 0x00008E70 File Offset: 0x00007070
		internal static Button CreateToggleButton(string text, bool enabled)
		{
			Button button = LoadoutPanel.CreateTabButton(text);
			button.CustomMinimumSize = new Vector2(100f, 36f);
			LoadoutPanel.UpdateToggleButton(button, enabled);
			return button;
		}

		// Token: 0x060000E0 RID: 224 RVA: 0x00008E94 File Offset: 0x00007094
		internal static void UpdateToggleButton(Button btn, bool enabled)
		{
			if (enabled)
			{
				btn.AddThemeStyleboxOverride("normal", LoadoutPanel.CreateStyleBox(new Color(0.15f, 0.25f, 0.15f, 0.9f), new Color(0.3f, 0.6f, 0.3f, 0.7f)));
				return;
			}
			btn.AddThemeStyleboxOverride("normal", LoadoutPanel.CreateStyleBox(new Color(0.12f, 0.1f, 0.15f, 0.85f), new Color(0.35f, 0.3f, 0.25f, 0.5f)));
		}

		// Token: 0x060000E1 RID: 225 RVA: 0x00008F34 File Offset: 0x00007134
		private static void ApplyFlatStyle(Button btn)
		{
			btn.AddThemeStyleboxOverride("normal", LoadoutPanel.CreateStyleBox(new Color(0.12f, 0.1f, 0.15f, 0.85f), new Color(0.35f, 0.3f, 0.25f, 0.5f)));
			btn.AddThemeStyleboxOverride("hover", LoadoutPanel.CreateStyleBox(new Color(0.18f, 0.15f, 0.22f, 0.92f), StsColors.gold));
			btn.AddThemeStyleboxOverride("pressed", LoadoutPanel.CreateStyleBox(new Color(0.08f, 0.06f, 0.1f, 0.95f), new Color("B89840")));
			btn.AddThemeStyleboxOverride("focus", LoadoutPanel.CreateStyleBox(new Color(0.18f, 0.15f, 0.22f, 0.92f), StsColors.gold));
		}

		// Token: 0x060000E2 RID: 226 RVA: 0x00009026 File Offset: 0x00007226
		private static StyleBoxFlat CreateStyleBox(Color bg, Color border)
		{
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = bg;
			styleBoxFlat.BorderColor = border;
			styleBoxFlat.SetBorderWidthAll(2);
			styleBoxFlat.SetCornerRadiusAll(6);
			styleBoxFlat.SetContentMarginAll(6f);
			return styleBoxFlat;
		}

		// Token: 0x04000046 RID: 70
		[Nullable(2)]
		private static CanvasLayer _layer;

		// Token: 0x04000047 RID: 71
		[Nullable(2)]
		private static CanvasLayer _hoverTipLayer;

		// Token: 0x04000048 RID: 72
		[Nullable(2)]
		private static VBoxContainer _contentContainer;

		// Token: 0x04000049 RID: 73
		[Nullable(2)]
		private static ScrollContainer _scrollContainer;

		// Token: 0x0400004A RID: 74
		[Nullable(2)]
		private static HBoxContainer _contextBar;

		// Token: 0x0400004B RID: 75
		[Nullable(2)]
		private static VBoxContainer _mainVBox;

		// Token: 0x0400004C RID: 76
		[Nullable(2)]
		private static PanelContainer _panel;

		// Token: 0x0400004D RID: 77
		[Nullable(2)]
		private static StyleBoxFlat _panelStyleNormal;

		// Token: 0x0400004E RID: 78
		[Nullable(2)]
		private static StyleBoxFlat _panelStyleClear;

		// Token: 0x0400004F RID: 79
		[Nullable(2)]
		private static ColorRect _divider;

		// Token: 0x04000050 RID: 80
		[Nullable(2)]
		private static NRelicCollection _relicScreen;

		// Token: 0x04000051 RID: 81
		[Nullable(2)]
		private static NCardLibrary _cardScreen;

		// Token: 0x04000052 RID: 82
		[Nullable(2)]
		private static NPotionLab _potionScreen;

		// Token: 0x04000053 RID: 83
		private static int _activeTab;

		// Token: 0x04000054 RID: 84
		[Nullable(new byte[] { 2, 1 })]
		private static Button[] _tabButtons;

		// Token: 0x02000057 RID: 87
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x040000BF RID: 191
			[Nullable(0)]
			public static Action <0>__Hide;

			// Token: 0x040000C0 RID: 192
			[Nullable(new byte[] { 0, 1 })]
			public static Func<NCardLibrary> <1>__CreateCardScreen;

			// Token: 0x040000C1 RID: 193
			[Nullable(new byte[] { 0, 1 })]
			public static Func<NRelicCollection> <2>__CreateRelicScreen;

			// Token: 0x040000C2 RID: 194
			[Nullable(new byte[] { 0, 1 })]
			public static Func<NPotionLab> <3>__CreatePotionScreen;
		}
	}
}
