using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager.Tabs
{
	// Token: 0x02000029 RID: 41
	[NullableContext(1)]
	[Nullable(0)]
	internal static class CardsTab
	{
		// Token: 0x060000E4 RID: 228 RVA: 0x00009064 File Offset: 0x00007264
		internal static void Build(VBoxContainer container, Player player)
		{
			CombatManager instance = CombatManager.Instance;
			bool flag = instance != null && instance.IsInProgress;
			CardsTab.BuildSearchBar(container);
			CardsTab.BuildSortBar(container);
			if (flag)
			{
				CardsTab.BuildPileSection(container, player, PileType.Hand, Loc.Get("pile.hand", null), flag);
				CardsTab.BuildPileSection(container, player, PileType.Draw, Loc.Get("pile.draw", null), flag);
				CardsTab.BuildPileSection(container, player, PileType.Discard, Loc.Get("pile.discard", null), flag);
				CardsTab.BuildPileSection(container, player, PileType.Exhaust, Loc.Get("pile.exhaust", null), flag);
			}
			CardsTab.BuildPileSection(container, player, PileType.Deck, Loc.Get("pile.deck", null), flag);
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 2f),
				Color = new Color(0.91f, 0.86f, 0.75f, 0.15f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
			container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("add_cards_header", null)), false, Node.InternalMode.Disabled);
			CardsTab.BuildAddCardSection(container, player, flag);
		}

		// Token: 0x060000E5 RID: 229 RVA: 0x00009164 File Offset: 0x00007364
		private static void BuildSearchBar(VBoxContainer container)
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 6);
			Label label = new Label();
			label.Text = Loc.Get("search", null);
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			LineEdit searchInput = new LineEdit();
			searchInput.PlaceholderText = Loc.Get("search_cards_placeholder", null);
			searchInput.Text = CardsTab._searchText;
			searchInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			searchInput.CustomMinimumSize = new Vector2(200f, 32f);
			searchInput.AddThemeFontSizeOverride("font_size", 14);
			searchInput.TextChanged += delegate(string newText)
			{
				CardsTab._searchText = newText;
				CardsTab._pageIndex.Clear();
				LoadoutPanel.RequestRefresh();
			};
			hboxContainer.AddChild(searchInput, false, Node.InternalMode.Disabled);
			if (!string.IsNullOrEmpty(CardsTab._searchText))
			{
				Button button = LoadoutPanel.CreateActionButton(Loc.Get("clear", null), new Color?(StsColors.red));
				button.Pressed += delegate
				{
					CardsTab._searchText = "";
					CardsTab._pageIndex.Clear();
					LoadoutPanel.RequestRefresh();
				};
				hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
				Callable.From(delegate
				{
					if (GodotObject.IsInstanceValid(searchInput))
					{
						searchInput.GrabFocus();
						searchInput.CaretColumn = searchInput.Text.Length;
					}
				}).CallDeferred(Array.Empty<Variant>());
			}
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x060000E6 RID: 230 RVA: 0x00009300 File Offset: 0x00007500
		private static bool MatchesSearch(CardModel card)
		{
			if (string.IsNullOrEmpty(CardsTab._searchText))
			{
				return true;
			}
			string text = CardsTab._searchText.ToLowerInvariant();
			try
			{
				if (card.Title.ToLowerInvariant().Contains(text))
				{
					return true;
				}
			}
			catch
			{
			}
			return card.Id.Entry.ToLowerInvariant().Contains(text);
		}

		// Token: 0x060000E7 RID: 231 RVA: 0x00009370 File Offset: 0x00007570
		private static void BuildSortBar(VBoxContainer container)
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 4);
			Label label = new Label();
			label.Text = Loc.Get("sort", null);
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			ValueTuple<CardsTab.SortMode, string>[] array = new ValueTuple<CardsTab.SortMode, string>[]
			{
				new ValueTuple<CardsTab.SortMode, string>(CardsTab.SortMode.Type, "sort.type"),
				new ValueTuple<CardsTab.SortMode, string>(CardsTab.SortMode.Cost, "sort.cost"),
				new ValueTuple<CardsTab.SortMode, string>(CardsTab.SortMode.Pinyin, "sort.pinyin"),
				new ValueTuple<CardsTab.SortMode, string>(CardsTab.SortMode.Rarity, "sort.rarity")
			};
			for (int i = 0; i < array.Length; i++)
			{
				ValueTuple<CardsTab.SortMode, string> valueTuple = array[i];
				CardsTab.SortMode item = valueTuple.Item1;
				string item2 = valueTuple.Item2;
				CardsTab.SortMode m = item;
				Button button = LoadoutPanel.CreateToggleButton(Loc.Get(item2, null), CardsTab._sortMode == m);
				button.CustomMinimumSize = new Vector2(80f, 30f);
				button.Pressed += delegate
				{
					CardsTab._sortMode = ((CardsTab._sortMode == m) ? CardsTab.SortMode.None : m);
					CardsTab._pageIndex.Clear();
					LoadoutPanel.RequestRefresh();
				};
				hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x060000E8 RID: 232 RVA: 0x000094C0 File Offset: 0x000076C0
		private static void BuildPileSection(VBoxContainer container, Player player, PileType pileType, string label, bool inCombat)
		{
			CardsTab.<>c__DisplayClass13_0 CS$<>8__locals1 = new CardsTab.<>c__DisplayClass13_0();
			CardPile cardPile = CardPile.Get(pileType, player);
			if (cardPile == null)
			{
				return;
			}
			IReadOnlyList<CardModel> cards = cardPile.Cards;
			IEnumerable<CardModel> enumerable = cards;
			Func<CardModel, bool> func;
			if ((func = CardsTab.<>O.<0>__MatchesSearch) == null)
			{
				func = (CardsTab.<>O.<0>__MatchesSearch = new Func<CardModel, bool>(CardsTab.MatchesSearch));
			}
			List<CardModel> list = enumerable.Where(func).ToList<CardModel>();
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler;
			string text;
			if (!string.IsNullOrEmpty(CardsTab._searchText))
			{
				defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(4, 3);
				defaultInterpolatedStringHandler.AppendFormatted(label);
				defaultInterpolatedStringHandler.AppendLiteral(" (");
				defaultInterpolatedStringHandler.AppendFormatted<int>(list.Count);
				defaultInterpolatedStringHandler.AppendLiteral("/");
				defaultInterpolatedStringHandler.AppendFormatted<int>(cards.Count);
				defaultInterpolatedStringHandler.AppendLiteral(")");
				text = defaultInterpolatedStringHandler.ToStringAndClear();
			}
			else
			{
				defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(3, 2);
				defaultInterpolatedStringHandler.AppendFormatted(label);
				defaultInterpolatedStringHandler.AppendLiteral(" (");
				defaultInterpolatedStringHandler.AppendFormatted<int>(cards.Count);
				defaultInterpolatedStringHandler.AppendLiteral(")");
				text = defaultInterpolatedStringHandler.ToStringAndClear();
			}
			string text2 = text;
			CardsTab.<>c__DisplayClass13_0 CS$<>8__locals2 = CS$<>8__locals1;
			defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(5, 1);
			defaultInterpolatedStringHandler.AppendLiteral("pile_");
			defaultInterpolatedStringHandler.AppendFormatted<PileType>(pileType);
			CS$<>8__locals2.groupKey = defaultInterpolatedStringHandler.ToStringAndClear();
			bool flag = CardsTab._collapsedGroups.Contains(CS$<>8__locals1.groupKey);
			Button button = LoadoutPanel.CreateItemButton((flag ? "▶" : "▼") + " " + text2, new Vector2?(new Vector2(400f, 32f)), 16);
			button.AddThemeColorOverride("font_color", StsColors.gold);
			button.Alignment = HorizontalAlignment.Left;
			button.Pressed += delegate
			{
				if (CardsTab._collapsedGroups.Contains(CS$<>8__locals1.groupKey))
				{
					CardsTab._collapsedGroups.Remove(CS$<>8__locals1.groupKey);
				}
				else
				{
					CardsTab._collapsedGroups.Add(CS$<>8__locals1.groupKey);
				}
				LoadoutPanel.RequestRefresh();
			};
			container.AddChild(button, false, Node.InternalMode.Disabled);
			if (flag)
			{
				return;
			}
			if (list.Count == 0)
			{
				Label label2 = new Label();
				label2.Text = (string.IsNullOrEmpty(CardsTab._searchText) ? ("  " + Loc.Get("empty", null)) : ("  " + Loc.Get("no_match", null)));
				label2.AddThemeFontSizeOverride("font_size", 14);
				label2.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label2, false, Node.InternalMode.Disabled);
				return;
			}
			int num = CardsTab._pageIndex.GetValueOrDefault(CS$<>8__locals1.groupKey, 0);
			int num2 = (list.Count + 24 - 1) / 24;
			num = Math.Clamp(num, 0, num2 - 1);
			CardsTab._pageIndex[CS$<>8__locals1.groupKey] = num;
			List<CardModel> list2 = list.Skip(num * 24).Take(24).ToList<CardModel>();
			GridContainer gridContainer = new GridContainer();
			gridContainer.Columns = 6;
			gridContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			gridContainer.AddThemeConstantOverride("h_separation", 8);
			gridContainer.AddThemeConstantOverride("v_separation", 8);
			container.AddChild(gridContainer, false, Node.InternalMode.Disabled);
			foreach (CardModel cardModel in list2)
			{
				CardsTab.AddPileCardCell(gridContainer, player, cardModel, pileType, list, inCombat);
			}
			if (num2 > 1)
			{
				CardsTab.BuildPageNav(container, CS$<>8__locals1.groupKey, num, num2, list.Count);
			}
		}

		// Token: 0x060000E9 RID: 233 RVA: 0x000097EC File Offset: 0x000079EC
		private static void AddPileCardCell(GridContainer grid, Player player, CardModel card, PileType pileType, List<CardModel> allCardsInPile, bool inCombat)
		{
			Control control = CardsTab.CreateNCardWrapper(card, pileType, allCardsInPile, delegate
			{
				CardEditPanel.Open(card, pileType, player);
			});
			grid.AddChild(control, false, Node.InternalMode.Disabled);
		}

		// Token: 0x060000EA RID: 234 RVA: 0x00009840 File Offset: 0x00007A40
		private static void BuildAddCardSection(VBoxContainer container, Player player, bool inCombat)
		{
			if (CardsTab._sortMode != CardsTab.SortMode.None)
			{
				CardsTab.BuildSortedAddCards(container, player, inCombat);
				return;
			}
			if (CardsTab._allCardsByPool == null)
			{
				List<CardModel> list = ModelDb.AllCards.OrderBy((CardModel c) => c.Id.Entry).ToList<CardModel>();
				Dictionary<string, List<CardModel>> dictionary = new Dictionary<string, List<CardModel>>();
				foreach (CardModel cardModel in list)
				{
					string text;
					try
					{
						CardPoolModel pool = cardModel.Pool;
						text = CardsTab.GetLocalizedPoolName(((pool != null) ? pool.Title : null) ?? Loc.Get("pool.other", null));
					}
					catch
					{
						text = Loc.Get("pool.other", null);
					}
					if (!dictionary.ContainsKey(text))
					{
						dictionary[text] = new List<CardModel>();
					}
					dictionary[text].Add(cardModel);
				}
				CardsTab._allCardsByPool = (from k in dictionary
					orderby k.Key
					select new ValueTuple<string, List<CardModel>>(k.Key, k.Value)).ToList<ValueTuple<string, List<CardModel>>>();
			}
			foreach (ValueTuple<string, List<CardModel>> valueTuple in CardsTab._allCardsByPool)
			{
				string item = valueTuple.Item1;
				List<CardModel> item2 = valueTuple.Item2;
				IEnumerable<CardModel> enumerable = item2;
				Func<CardModel, bool> func;
				if ((func = CardsTab.<>O.<0>__MatchesSearch) == null)
				{
					func = (CardsTab.<>O.<0>__MatchesSearch = new Func<CardModel, bool>(CardsTab.MatchesSearch));
				}
				List<CardModel> list2 = enumerable.Where(func).ToList<CardModel>();
				if (list2.Count != 0 || string.IsNullOrEmpty(CardsTab._searchText))
				{
					string groupKey = "add_" + item;
					bool isCollapsed = CardsTab._collapsedGroups.Contains(groupKey);
					if (!string.IsNullOrEmpty(CardsTab._searchText) && list2.Count > 0)
					{
						isCollapsed = false;
					}
					else if (string.IsNullOrEmpty(CardsTab._searchText) && !CardsTab._collapsedGroups.Contains(groupKey) && !CardsTab._collapsedGroups.Contains("_opened_" + groupKey))
					{
						isCollapsed = true;
					}
					string text2 = (isCollapsed ? "▶" : "▼");
					int num = (string.IsNullOrEmpty(CardsTab._searchText) ? item2.Count : list2.Count);
					DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(5, 3);
					defaultInterpolatedStringHandler.AppendFormatted(text2);
					defaultInterpolatedStringHandler.AppendLiteral("  ");
					defaultInterpolatedStringHandler.AppendFormatted(item);
					defaultInterpolatedStringHandler.AppendLiteral(" (");
					defaultInterpolatedStringHandler.AppendFormatted<int>(num);
					defaultInterpolatedStringHandler.AppendLiteral(")");
					Button button = LoadoutPanel.CreateItemButton(defaultInterpolatedStringHandler.ToStringAndClear(), new Vector2?(new Vector2(400f, 32f)), 15);
					button.AddThemeColorOverride("font_color", StsColors.gold);
					button.Alignment = HorizontalAlignment.Left;
					button.Pressed += delegate
					{
						if (isCollapsed)
						{
							CardsTab._collapsedGroups.Remove(groupKey);
							CardsTab._collapsedGroups.Add("_opened_" + groupKey);
						}
						else
						{
							CardsTab._collapsedGroups.Add(groupKey);
							CardsTab._collapsedGroups.Remove("_opened_" + groupKey);
						}
						LoadoutPanel.RequestRefresh();
					};
					container.AddChild(button, false, Node.InternalMode.Disabled);
					if (!isCollapsed)
					{
						List<CardModel> list3 = (string.IsNullOrEmpty(CardsTab._searchText) ? item2 : list2);
						int num2 = CardsTab._pageIndex.GetValueOrDefault(groupKey, 0);
						int num3 = (list3.Count + 24 - 1) / 24;
						num2 = Math.Clamp(num2, 0, Math.Max(0, num3 - 1));
						CardsTab._pageIndex[groupKey] = num2;
						List<CardModel> list4 = list3.Skip(num2 * 24).Take(24).ToList<CardModel>();
						GridContainer gridContainer = new GridContainer();
						gridContainer.Columns = 6;
						gridContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
						gridContainer.AddThemeConstantOverride("h_separation", 8);
						gridContainer.AddThemeConstantOverride("v_separation", 8);
						container.AddChild(gridContainer, false, Node.InternalMode.Disabled);
						foreach (CardModel cardModel2 in list4)
						{
							CardsTab.AddAddCardCell(gridContainer, player, cardModel2, inCombat);
						}
						if (num3 > 1)
						{
							CardsTab.BuildPageNav(container, groupKey, num2, num3, list3.Count);
						}
					}
				}
			}
		}

		// Token: 0x060000EB RID: 235 RVA: 0x00009CEC File Offset: 0x00007EEC
		private static void AddAddCardCell(GridContainer grid, Player player, CardModel card, bool inCombat)
		{
			bool showUpgraded = LoadoutPanel.CardsShowUpgraded && card.IsUpgradable;
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vboxContainer.AddThemeConstantOverride("separation", 2);
			Control control = CardsTab.CreateNCardWrapperForAdd(card, player, inCombat, showUpgraded);
			vboxContainer.AddChild(control, false, Node.InternalMode.Disabled);
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			hboxContainer.AddThemeConstantOverride("separation", 2);
			if (inCombat)
			{
				Button toHandBtn = LoadoutPanel.CreateActionButton(Loc.Get("to_hand", null), new Color?(StsColors.cream));
				toHandBtn.CustomMinimumSize = new Vector2(46f, 24f);
				toHandBtn.Pressed += delegate
				{
					CardsTab.AddCardToPlayer(card, player, PileType.Hand, showUpgraded, toHandBtn);
				};
				hboxContainer.AddChild(toHandBtn, false, Node.InternalMode.Disabled);
				Button toDrawBtn = LoadoutPanel.CreateActionButton(Loc.Get("to_draw", null), new Color?(StsColors.blue));
				toDrawBtn.CustomMinimumSize = new Vector2(46f, 24f);
				toDrawBtn.Pressed += delegate
				{
					CardsTab.AddCardToPlayer(card, player, PileType.Draw, showUpgraded, toDrawBtn);
				};
				hboxContainer.AddChild(toDrawBtn, false, Node.InternalMode.Disabled);
				if (!showUpgraded && card.IsUpgradable)
				{
					Button toHandUpgBtn = LoadoutPanel.CreateActionButton(Loc.Get("to_hand_up", null), new Color?(StsColors.green));
					toHandUpgBtn.CustomMinimumSize = new Vector2(52f, 24f);
					toHandUpgBtn.Pressed += delegate
					{
						CardsTab.AddCardToPlayer(card, player, PileType.Hand, true, toHandUpgBtn);
					};
					hboxContainer.AddChild(toHandUpgBtn, false, Node.InternalMode.Disabled);
					Button toDrawUpgBtn = LoadoutPanel.CreateActionButton(Loc.Get("to_draw_up", null), new Color?(new Color("4499FF")));
					toDrawUpgBtn.CustomMinimumSize = new Vector2(52f, 24f);
					toDrawUpgBtn.Pressed += delegate
					{
						CardsTab.AddCardToPlayer(card, player, PileType.Draw, true, toDrawUpgBtn);
					};
					hboxContainer.AddChild(toDrawUpgBtn, false, Node.InternalMode.Disabled);
				}
			}
			else
			{
				Button addBtn = LoadoutPanel.CreateActionButton(Loc.Get("acquire", null), new Color?(StsColors.cream));
				addBtn.CustomMinimumSize = new Vector2(40f, 24f);
				addBtn.Pressed += delegate
				{
					CardsTab.AddCardToPlayer(card, player, PileType.Deck, showUpgraded, addBtn);
				};
				hboxContainer.AddChild(addBtn, false, Node.InternalMode.Disabled);
				if (!showUpgraded && card.IsUpgradable)
				{
					Button addUpgBtn = LoadoutPanel.CreateActionButton(Loc.Get("acquire_up", null), new Color?(StsColors.green));
					addUpgBtn.CustomMinimumSize = new Vector2(46f, 24f);
					addUpgBtn.Pressed += delegate
					{
						CardsTab.AddCardToPlayer(card, player, PileType.Deck, true, addUpgBtn);
					};
					hboxContainer.AddChild(addUpgBtn, false, Node.InternalMode.Disabled);
				}
			}
			vboxContainer.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			grid.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x060000EC RID: 236 RVA: 0x0000A094 File Offset: 0x00008294
		internal static Control CreateNCardWrapperPublic(CardModel card, PileType pileType, [Nullable(new byte[] { 2, 1 })] List<CardModel> inspectList, [Nullable(2)] Action onClick = null)
		{
			return CardsTab.CreateNCardWrapper(card, pileType, inspectList, onClick);
		}

		// Token: 0x060000ED RID: 237 RVA: 0x0000A0A0 File Offset: 0x000082A0
		private static Control CreateNCardWrapper(CardModel card, PileType pileType, [Nullable(new byte[] { 2, 1 })] List<CardModel> inspectList, [Nullable(2)] Action onClick = null)
		{
			Control clip = new Control();
			clip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			clip.CustomMinimumSize = new Vector2(0f, CardsTab.NCardMinHeight);
			clip.ClipContents = true;
			clip.MouseFilter = Control.MouseFilterEnum.Stop;
			clip.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			NCard ncard = null;
			try
			{
				ncard = NCard.Create(card.IsMutable ? card : card.ToMutable(), ModelVisibility.Visible);
				if (ncard != null)
				{
					ncard.Scale = new Vector2(CardsTab.NCardScale, CardsTab.NCardScale);
					ncard.MouseFilter = Control.MouseFilterEnum.Ignore;
					clip.AddChild(ncard, false, Node.InternalMode.Disabled);
					PileType pt = pileType;
					NCard cardRef = ncard;
					cardRef.Ready += delegate
					{
						if (GodotObject.IsInstanceValid(cardRef))
						{
							cardRef.UpdateVisuals(pt, CardPreviewMode.Normal);
						}
					};
				}
			}
			catch
			{
			}
			NCard capturedCard = ncard;
			clip.Resized += delegate
			{
				if (capturedCard != null && GodotObject.IsInstanceValid(capturedCard))
				{
					capturedCard.Position = new Vector2(clip.Size.X / 2f, clip.Size.Y / 2f);
				}
			};
			clip.MouseEntered += delegate
			{
				try
				{
					LoadoutPanel.ShowHoverTips(clip, card.HoverTips, HoverTipAlignment.Left);
				}
				catch
				{
				}
			};
			clip.MouseExited += delegate
			{
				NHoverTipSet.Remove(clip);
			};
			clip.GuiInput += delegate(InputEvent ev)
			{
				InputEventMouseButton inputEventMouseButton = ev as InputEventMouseButton;
				if (inputEventMouseButton == null || !inputEventMouseButton.Pressed || inputEventMouseButton.ButtonIndex != MouseButton.Left)
				{
					return;
				}
				Viewport viewport = clip.GetViewport();
				if (viewport != null)
				{
					viewport.SetInputAsHandled();
				}
				NHoverTipSet.Remove(clip);
				if (onClick != null)
				{
					onClick();
					return;
				}
				try
				{
					List<CardModel> list;
					if ((list = inspectList) == null)
					{
						(list = new List<CardModel>()).Add(card);
					}
					List<CardModel> list2 = list;
					NGame instance = NGame.Instance;
					if (instance != null)
					{
						NInspectCardScreen inspectCardScreen = instance.GetInspectCardScreen();
						if (inspectCardScreen != null)
						{
							inspectCardScreen.Open(list2, list2.IndexOf(card), false);
						}
					}
				}
				catch
				{
				}
			};
			return clip;
		}

		// Token: 0x060000EE RID: 238 RVA: 0x0000A224 File Offset: 0x00008424
		private static Control CreateNCardWrapperForAdd(CardModel card, Player player, bool inCombat, bool upgraded = false)
		{
			Control clip = new Control();
			clip.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			clip.CustomMinimumSize = new Vector2(0f, CardsTab.NCardMinHeight);
			clip.ClipContents = true;
			clip.MouseFilter = Control.MouseFilterEnum.Stop;
			clip.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			NCard ncard = null;
			try
			{
				CardModel cardModel = (card.IsMutable ? card : card.ToMutable());
				if (upgraded && cardModel.IsUpgradable)
				{
					CardCmd.Upgrade(cardModel, CardPreviewStyle.HorizontalLayout);
				}
				ncard = NCard.Create(cardModel, ModelVisibility.Visible);
				if (ncard != null)
				{
					ncard.Scale = new Vector2(CardsTab.NCardScale, CardsTab.NCardScale);
					ncard.MouseFilter = Control.MouseFilterEnum.Ignore;
					clip.AddChild(ncard, false, Node.InternalMode.Disabled);
					NCard cardRef = ncard;
					cardRef.Ready += delegate
					{
						if (GodotObject.IsInstanceValid(cardRef))
						{
							cardRef.UpdateVisuals(PileType.None, CardPreviewMode.Normal);
						}
					};
				}
			}
			catch
			{
			}
			NCard capturedCard = ncard;
			clip.Resized += delegate
			{
				if (capturedCard != null && GodotObject.IsInstanceValid(capturedCard))
				{
					capturedCard.Position = new Vector2(clip.Size.X / 2f, clip.Size.Y / 2f);
				}
			};
			clip.MouseEntered += delegate
			{
				try
				{
					LoadoutPanel.ShowHoverTips(clip, card.HoverTips, HoverTipAlignment.Left);
				}
				catch
				{
				}
			};
			clip.MouseExited += delegate
			{
				NHoverTipSet.Remove(clip);
			};
			clip.GuiInput += delegate(InputEvent ev)
			{
				InputEventMouseButton inputEventMouseButton = ev as InputEventMouseButton;
				if (inputEventMouseButton == null || !inputEventMouseButton.Pressed || inputEventMouseButton.ButtonIndex != MouseButton.Left)
				{
					return;
				}
				Viewport viewport = clip.GetViewport();
				if (viewport != null)
				{
					viewport.SetInputAsHandled();
				}
				PileType pileType = (inCombat ? PileType.Hand : PileType.Deck);
				CardsTab.AddCardToPlayer(card, player, pileType, upgraded, clip);
			};
			return clip;
		}

		// Token: 0x060000EF RID: 239 RVA: 0x0000A3C0 File Offset: 0x000085C0
		private static void BuildPageNav(VBoxContainer container, string groupKey, int page, int totalPages, int totalCards)
		{
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 6);
			if (page > 0)
			{
				Button button = LoadoutPanel.CreateActionButton(Loc.Get("page_prev", null), new Color?(StsColors.cream));
				button.Pressed += delegate
				{
					CardsTab._pageIndex[groupKey] = page - 1;
					LoadoutPanel.RequestRefresh();
				};
				hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			Label label = new Label();
			label.Text = Loc.Fmt("page_info", new object[]
			{
				page + 1,
				totalPages,
				totalCards
			});
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			hboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			if (page < totalPages - 1)
			{
				Button button2 = LoadoutPanel.CreateActionButton(Loc.Get("page_next", null), new Color?(StsColors.cream));
				button2.Pressed += delegate
				{
					CardsTab._pageIndex[groupKey] = page + 1;
					LoadoutPanel.RequestRefresh();
				};
				hboxContainer.AddChild(button2, false, Node.InternalMode.Disabled);
			}
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x060000F0 RID: 240 RVA: 0x0000A4F8 File Offset: 0x000086F8
		private static void BuildSortedAddCards(VBoxContainer container, Player player, bool inCombat)
		{
			IEnumerable<CardModel> allCards = ModelDb.AllCards;
			Func<CardModel, bool> func;
			if ((func = CardsTab.<>O.<0>__MatchesSearch) == null)
			{
				func = (CardsTab.<>O.<0>__MatchesSearch = new Func<CardModel, bool>(CardsTab.MatchesSearch));
			}
			List<CardModel> list = allCards.Where(func).ToList<CardModel>();
			List<ValueTuple<string, List<CardModel>>> list2 = new List<ValueTuple<string, List<CardModel>>>();
			if (CardsTab._sortMode == CardsTab.SortMode.Type)
			{
				list2 = (from c in list
					group c by c.Type into g
					orderby CardsTab.GetTypeOrder(g.Key)
					select new ValueTuple<string, List<CardModel>>(CardsTab.GetTypeName(g.Key), g.OrderBy((CardModel c) => CardsTab.SafeTitle(c), StringComparer.CurrentCulture).ToList<CardModel>())).ToList<ValueTuple<string, List<CardModel>>>();
			}
			else if (CardsTab._sortMode == CardsTab.SortMode.Cost)
			{
				list2 = (from c in list
					group c by CardsTab.GetCostGroup(c) into g
					orderby g.Key.Item2
					select new ValueTuple<string, List<CardModel>>(g.Key.Item1, g.OrderBy((CardModel c) => CardsTab.SafeTitle(c), StringComparer.CurrentCulture).ToList<CardModel>())).ToList<ValueTuple<string, List<CardModel>>>();
			}
			else if (CardsTab._sortMode == CardsTab.SortMode.Pinyin)
			{
				StringComparer comparer;
				try
				{
					comparer = StringComparer.Create(new CultureInfo("zh-CN"), false);
				}
				catch
				{
					comparer = StringComparer.CurrentCulture;
				}
				list2 = (from g in (from c in list
						group c by CardsTab.GetAlphaGroup(CardsTab.SafeTitle(c))).OrderBy(delegate(IGrouping<string, CardModel> g)
					{
						if (!(g.Key == "#"))
						{
							return g.Key;
						}
						return "\uffff";
					}, StringComparer.Ordinal)
					select new ValueTuple<string, List<CardModel>>(g.Key, g.OrderBy((CardModel c) => CardsTab.SafeTitle(c), comparer).ToList<CardModel>())).ToList<ValueTuple<string, List<CardModel>>>();
			}
			else if (CardsTab._sortMode == CardsTab.SortMode.Rarity)
			{
				list2 = (from c in list
					group c by c.Rarity into g
					orderby CardsTab.GetRarityOrder(g.Key)
					select new ValueTuple<string, List<CardModel>>(CardsTab.GetRarityName(g.Key), g.OrderBy((CardModel c) => CardsTab.SafeTitle(c), StringComparer.CurrentCulture).ToList<CardModel>())).ToList<ValueTuple<string, List<CardModel>>>();
			}
			if (list2.Count != 0)
			{
				if (!list2.All(([TupleElementNames(new string[] { "Name", "Cards" })] ValueTuple<string, List<CardModel>> g) => g.Item2.Count == 0))
				{
					foreach (ValueTuple<string, List<CardModel>> valueTuple in list2)
					{
						string item = valueTuple.Item1;
						List<CardModel> item2 = valueTuple.Item2;
						CardsTab.<>c__DisplayClass23_1 CS$<>8__locals2 = new CardsTab.<>c__DisplayClass23_1();
						if (item2.Count != 0)
						{
							CardsTab.<>c__DisplayClass23_1 CS$<>8__locals3 = CS$<>8__locals2;
							DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(6, 2);
							defaultInterpolatedStringHandler.AppendLiteral("sort_");
							defaultInterpolatedStringHandler.AppendFormatted<CardsTab.SortMode>(CardsTab._sortMode);
							defaultInterpolatedStringHandler.AppendLiteral("_");
							defaultInterpolatedStringHandler.AppendFormatted(item);
							CS$<>8__locals3.groupKey = defaultInterpolatedStringHandler.ToStringAndClear();
							if (!string.IsNullOrEmpty(item))
							{
								bool flag = CardsTab._collapsedGroups.Contains(CS$<>8__locals2.groupKey);
								string text = (flag ? "▶" : "▼");
								defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(5, 3);
								defaultInterpolatedStringHandler.AppendFormatted(text);
								defaultInterpolatedStringHandler.AppendLiteral("  ");
								defaultInterpolatedStringHandler.AppendFormatted(item);
								defaultInterpolatedStringHandler.AppendLiteral(" (");
								defaultInterpolatedStringHandler.AppendFormatted<int>(item2.Count);
								defaultInterpolatedStringHandler.AppendLiteral(")");
								Button button = LoadoutPanel.CreateItemButton(defaultInterpolatedStringHandler.ToStringAndClear(), new Vector2?(new Vector2(400f, 32f)), 15);
								button.AddThemeColorOverride("font_color", StsColors.gold);
								button.Alignment = HorizontalAlignment.Left;
								CS$<>8__locals2.collapsed = flag;
								button.Pressed += delegate
								{
									if (CS$<>8__locals2.collapsed)
									{
										CardsTab._collapsedGroups.Remove(CS$<>8__locals2.groupKey);
									}
									else
									{
										CardsTab._collapsedGroups.Add(CS$<>8__locals2.groupKey);
									}
									LoadoutPanel.RequestRefresh();
								};
								container.AddChild(button, false, Node.InternalMode.Disabled);
								if (flag)
								{
									continue;
								}
							}
							int num = CardsTab._pageIndex.GetValueOrDefault(CS$<>8__locals2.groupKey, 0);
							int num2 = (item2.Count + 24 - 1) / 24;
							num = Math.Clamp(num, 0, Math.Max(0, num2 - 1));
							CardsTab._pageIndex[CS$<>8__locals2.groupKey] = num;
							List<CardModel> list3 = item2.Skip(num * 24).Take(24).ToList<CardModel>();
							GridContainer gridContainer = new GridContainer();
							gridContainer.Columns = 6;
							gridContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
							gridContainer.AddThemeConstantOverride("h_separation", 8);
							gridContainer.AddThemeConstantOverride("v_separation", 8);
							container.AddChild(gridContainer, false, Node.InternalMode.Disabled);
							foreach (CardModel cardModel in list3)
							{
								CardsTab.AddAddCardCell(gridContainer, player, cardModel, inCombat);
							}
							if (num2 > 1)
							{
								CardsTab.BuildPageNav(container, CS$<>8__locals2.groupKey, num, num2, item2.Count);
							}
						}
					}
					return;
				}
			}
			Label label = new Label();
			label.Text = Loc.Get("no_match", null);
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", StsColors.gray);
			container.AddChild(label, false, Node.InternalMode.Disabled);
		}

		// Token: 0x060000F1 RID: 241 RVA: 0x0000AA98 File Offset: 0x00008C98
		private static string SafeTitle(CardModel card)
		{
			string text;
			try
			{
				text = card.Title;
			}
			catch
			{
				text = card.Id.Entry;
			}
			return text;
		}

		// Token: 0x060000F2 RID: 242 RVA: 0x0000AAD0 File Offset: 0x00008CD0
		private static int GetTypeOrder(CardType type)
		{
			int num;
			switch (type)
			{
			case CardType.Attack:
				num = 0;
				break;
			case CardType.Skill:
				num = 1;
				break;
			case CardType.Power:
				num = 2;
				break;
			case CardType.Status:
				num = 3;
				break;
			case CardType.Curse:
				num = 4;
				break;
			case CardType.Quest:
				num = 5;
				break;
			default:
				num = 9;
				break;
			}
			return num;
		}

		// Token: 0x060000F3 RID: 243 RVA: 0x0000AB1C File Offset: 0x00008D1C
		private static string GetTypeName(CardType type)
		{
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(5, 1);
			defaultInterpolatedStringHandler.AppendLiteral("type.");
			defaultInterpolatedStringHandler.AppendFormatted<CardType>(type);
			return Loc.Get(defaultInterpolatedStringHandler.ToStringAndClear(), type.ToString());
		}

		// Token: 0x060000F4 RID: 244 RVA: 0x0000AB60 File Offset: 0x00008D60
		private static int GetRarityOrder(CardRarity rarity)
		{
			int num;
			switch (rarity)
			{
			case CardRarity.Basic:
				num = 0;
				break;
			case CardRarity.Common:
				num = 1;
				break;
			case CardRarity.Uncommon:
				num = 2;
				break;
			case CardRarity.Rare:
				num = 3;
				break;
			case CardRarity.Ancient:
				num = 4;
				break;
			case CardRarity.Event:
				num = 5;
				break;
			case CardRarity.Token:
				num = 6;
				break;
			case CardRarity.Status:
				num = 7;
				break;
			case CardRarity.Curse:
				num = 8;
				break;
			case CardRarity.Quest:
				num = 9;
				break;
			default:
				num = 10;
				break;
			}
			return num;
		}

		// Token: 0x060000F5 RID: 245 RVA: 0x0000ABCC File Offset: 0x00008DCC
		private static string GetRarityName(CardRarity rarity)
		{
			string text;
			switch (rarity)
			{
			case CardRarity.Basic:
				text = Loc.Get("rarity.starter", null);
				break;
			case CardRarity.Common:
				text = Loc.Get("rarity.common", null);
				break;
			case CardRarity.Uncommon:
				text = Loc.Get("rarity.uncommon", null);
				break;
			case CardRarity.Rare:
				text = Loc.Get("rarity.rare", null);
				break;
			case CardRarity.Ancient:
				text = Loc.Get("rarity.ancient", null);
				break;
			case CardRarity.Event:
				text = Loc.Get("rarity.event", null);
				break;
			case CardRarity.Token:
				text = Loc.Get("rarity.token", null);
				break;
			case CardRarity.Status:
				text = Loc.Get("rarity.status", null);
				break;
			case CardRarity.Curse:
				text = Loc.Get("rarity.curse", null);
				break;
			case CardRarity.Quest:
				text = Loc.Get("rarity.quest", null);
				break;
			default:
				text = rarity.ToString();
				break;
			}
			return text;
		}

		// Token: 0x060000F6 RID: 246 RVA: 0x0000ACAC File Offset: 0x00008EAC
		[return: TupleElementNames(new string[] { "Name", "Order" })]
		[return: Nullable(new byte[] { 0, 1 })]
		private static ValueTuple<string, int> GetCostGroup(CardModel c)
		{
			if (c.EnergyCost.CostsX)
			{
				return new ValueTuple<string, int>("X", 98);
			}
			int canonical = c.EnergyCost.Canonical;
			if (canonical < 0)
			{
				return new ValueTuple<string, int>(Loc.Get("sort.no_cost", null), 99);
			}
			return new ValueTuple<string, int>(Loc.Fmt("sort.cost_group", new object[] { canonical }), canonical);
		}

		// Token: 0x060000F7 RID: 247 RVA: 0x0000AD18 File Offset: 0x00008F18
		private static string GetAlphaGroup(string title)
		{
			if (string.IsNullOrEmpty(title))
			{
				return "#";
			}
			char c = title[0];
			if (c >= 'a')
			{
				if (c > 'z')
				{
					goto IL_0030;
				}
			}
			else if (c < 'A' || c > 'Z')
			{
				goto IL_0030;
			}
			bool flag = true;
			goto IL_0032;
			IL_0030:
			flag = false;
			IL_0032:
			if (flag)
			{
				return char.ToUpperInvariant(c).ToString();
			}
			if (c >= '一' && c <= '鿿')
			{
				if (CardsTab._zhCompare == null)
				{
					CardsTab._zhCompare = CardsTab.GetZhCompareInfo();
				}
				string text = c.ToString();
				for (int i = CardsTab._pinyinRefs.Length - 1; i >= 0; i--)
				{
					if (CardsTab._zhCompare.Compare(text, CardsTab._pinyinRefs[i].Item1) >= 0)
					{
						return CardsTab._pinyinRefs[i].Item2;
					}
				}
			}
			return "#";
		}

		// Token: 0x060000F8 RID: 248 RVA: 0x0000ADE0 File Offset: 0x00008FE0
		private static CompareInfo GetZhCompareInfo()
		{
			CompareInfo compareInfo;
			try
			{
				compareInfo = CultureInfo.GetCultureInfo("zh-CN").CompareInfo;
			}
			catch
			{
				compareInfo = CultureInfo.CurrentCulture.CompareInfo;
			}
			return compareInfo;
		}

		// Token: 0x060000F9 RID: 249 RVA: 0x0000AE20 File Offset: 0x00009020
		private static void AddCardToPlayer(CardModel card, Player player, PileType targetPile, bool upgraded, Control triggerNode)
		{
			RunManager instance = RunManager.Instance;
			RunState runState = ((instance != null) ? instance.DebugOnlyGetState() : null);
			if (runState == null)
			{
				return;
			}
			CardModel cardModel = runState.CreateCard(card, player);
			if (upgraded && cardModel.IsUpgradable)
			{
				CardCmd.Upgrade(cardModel, CardPreviewStyle.HorizontalLayout);
			}
			TaskHelper.RunSafely(CardPileCmd.Add(cardModel, PileType.Deck, CardPilePosition.Bottom, null, false));
			if (targetPile.IsCombatPile())
			{
				CombatManager instance2 = CombatManager.Instance;
				CombatState combatState = ((instance2 != null) ? instance2.DebugOnlyGetState() : null);
				if (combatState != null)
				{
					CardModel cardModel2 = combatState.CreateCard(card, player);
					if (upgraded && cardModel2.IsUpgradable)
					{
						CardCmd.Upgrade(cardModel2, CardPreviewStyle.HorizontalLayout);
					}
					TaskHelper.RunSafely(CardPileCmd.Add(cardModel2, targetPile, CardPilePosition.Bottom, null, false));
				}
			}
			CardsTab.DelayedRefresh(triggerNode);
		}

		// Token: 0x060000FA RID: 250 RVA: 0x0000AEBD File Offset: 0x000090BD
		private static void DelayedRefresh(Control node)
		{
			node.GetTree().CreateTimer(0.2, true, false, false).Timeout += delegate
			{
				LoadoutPanel.RequestRefresh();
			};
		}

		// Token: 0x060000FB RID: 251 RVA: 0x0000AEFC File Offset: 0x000090FC
		private static string GetLocalizedPoolName(string poolTitle)
		{
			if (CardsTab._poolNameMap == null)
			{
				CardsTab._poolNameMap = new Dictionary<string, string>();
				try
				{
					foreach (CharacterModel characterModel in ModelDb.AllCharacters)
					{
						try
						{
							string title = characterModel.CardPool.Title;
							string formattedText = characterModel.Title.GetFormattedText();
							if (!string.IsNullOrEmpty(formattedText))
							{
								CardsTab._poolNameMap[title] = formattedText;
							}
						}
						catch
						{
						}
					}
				}
				catch
				{
				}
			}
			string text;
			if (CardsTab._poolNameMap.TryGetValue(poolTitle, out text))
			{
				return text;
			}
			string text2 = Loc.Get("pool." + poolTitle, poolTitle);
			CardsTab._poolNameMap[poolTitle] = text2;
			return text2;
		}

		// Token: 0x04000059 RID: 89
		private const int CardsPerPage = 24;

		// Token: 0x0400005A RID: 90
		private const int Columns = 6;

		// Token: 0x0400005B RID: 91
		private static string _searchText = "";

		// Token: 0x0400005C RID: 92
		private static readonly HashSet<string> _collapsedGroups = new HashSet<string>();

		// Token: 0x0400005D RID: 93
		private static readonly Dictionary<string, int> _pageIndex = new Dictionary<string, int>();

		// Token: 0x0400005E RID: 94
		private static CardsTab.SortMode _sortMode = CardsTab.SortMode.None;

		// Token: 0x0400005F RID: 95
		[TupleElementNames(new string[] { "PoolName", "Cards" })]
		[Nullable(new byte[] { 2, 0, 1, 1, 1 })]
		private static List<ValueTuple<string, List<CardModel>>> _allCardsByPool;

		// Token: 0x04000060 RID: 96
		[Nullable(new byte[] { 2, 1, 1 })]
		private static Dictionary<string, string> _poolNameMap;

		// Token: 0x04000061 RID: 97
		private static readonly float NCardScale = 0.65f;

		// Token: 0x04000062 RID: 98
		private static readonly float NCardMinHeight = 280f;

		// Token: 0x04000063 RID: 99
		[Nullable(2)]
		private static CompareInfo _zhCompare;

		// Token: 0x04000064 RID: 100
		[TupleElementNames(new string[] { "RefChar", "Letter" })]
		[Nullable(new byte[] { 1, 0, 1, 1 })]
		private static readonly ValueTuple<string, string>[] _pinyinRefs = new ValueTuple<string, string>[]
		{
			new ValueTuple<string, string>("阿", "A"),
			new ValueTuple<string, string>("八", "B"),
			new ValueTuple<string, string>("擦", "C"),
			new ValueTuple<string, string>("搭", "D"),
			new ValueTuple<string, string>("蛾", "E"),
			new ValueTuple<string, string>("发", "F"),
			new ValueTuple<string, string>("噶", "G"),
			new ValueTuple<string, string>("哈", "H"),
			new ValueTuple<string, string>("击", "J"),
			new ValueTuple<string, string>("喀", "K"),
			new ValueTuple<string, string>("垃", "L"),
			new ValueTuple<string, string>("妈", "M"),
			new ValueTuple<string, string>("拿", "N"),
			new ValueTuple<string, string>("哦", "O"),
			new ValueTuple<string, string>("趴", "P"),
			new ValueTuple<string, string>("七", "Q"),
			new ValueTuple<string, string>("然", "R"),
			new ValueTuple<string, string>("撒", "S"),
			new ValueTuple<string, string>("他", "T"),
			new ValueTuple<string, string>("挖", "W"),
			new ValueTuple<string, string>("昔", "X"),
			new ValueTuple<string, string>("压", "Y"),
			new ValueTuple<string, string>("匝", "Z")
		};

		// Token: 0x02000061 RID: 97
		[NullableContext(0)]
		private enum SortMode
		{
			// Token: 0x040000D4 RID: 212
			None,
			// Token: 0x040000D5 RID: 213
			Type,
			// Token: 0x040000D6 RID: 214
			Cost,
			// Token: 0x040000D7 RID: 215
			Pinyin,
			// Token: 0x040000D8 RID: 216
			Rarity
		}

		// Token: 0x02000062 RID: 98
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x040000D9 RID: 217
			[Nullable(0)]
			public static Func<CardModel, bool> <0>__MatchesSearch;
		}
	}
}
