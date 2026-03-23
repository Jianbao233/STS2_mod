using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Events;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager.Tabs
{
	// Token: 0x0200002F RID: 47
	[NullableContext(1)]
	[Nullable(0)]
	internal static class EventsTab
	{
		// Token: 0x06000130 RID: 304 RVA: 0x0000FF44 File Offset: 0x0000E144
		internal static void Build(VBoxContainer container, Player player)
		{
			if (!RunManager.Instance.IsInProgress)
			{
				Label label = new Label();
				label.Text = Loc.Get("not_in_game", null);
				label.AddThemeFontSizeOverride("font_size", 16);
				label.AddThemeColorOverride("font_color", StsColors.cream);
				container.AddChild(label, false, Node.InternalMode.Disabled);
				return;
			}
			HSplitContainer hsplitContainer = new HSplitContainer();
			hsplitContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			hsplitContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			hsplitContainer.SplitOffset = -400;
			Viewport viewport = container.GetViewport();
			float num = ((viewport != null) ? viewport.GetVisibleRect().Size.Y : 720f);
			hsplitContainer.CustomMinimumSize = new Vector2(0f, num * 0.78f);
			container.AddChild(hsplitContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vboxContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			vboxContainer.CustomMinimumSize = new Vector2(300f, 0f);
			vboxContainer.AddThemeConstantOverride("separation", 4);
			hsplitContainer.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
			EventsTab.BuildSearchBar(vboxContainer);
			ScrollContainer scrollContainer = new ScrollContainer();
			scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			vboxContainer.AddChild(scrollContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer2 = new VBoxContainer();
			vboxContainer2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vboxContainer2.AddThemeConstantOverride("separation", 2);
			scrollContainer.AddChild(vboxContainer2, false, Node.InternalMode.Disabled);
			IEnumerable<EventModel> enumerable = ModelDb.AllEvents.Where((EventModel e) => !(e is AncientEventModel));
			Func<EventModel, bool> func;
			if ((func = EventsTab.<>O.<0>__MatchesSearch) == null)
			{
				func = (EventsTab.<>O.<0>__MatchesSearch = new Func<EventModel, bool>(EventsTab.MatchesSearch));
			}
			List<EventModel> list = (from e in enumerable.Where(func)
				orderby e.Id.Entry
				select e).ToList<EventModel>();
			IEnumerable<AncientEventModel> allAncients = ModelDb.AllAncients;
			Func<AncientEventModel, bool> func2;
			if ((func2 = EventsTab.<>O.<1>__MatchesSearch) == null)
			{
				func2 = (EventsTab.<>O.<1>__MatchesSearch = new Func<AncientEventModel, bool>(EventsTab.MatchesSearch));
			}
			List<AncientEventModel> list2 = (from e in allAncients.Where(func2)
				orderby e.Id.Entry
				select e).ToList<AncientEventModel>();
			EventsTab.BuildEventList(vboxContainer2, player, Loc.Get("events.regular", null), list, StsColors.gold);
			EventsTab.BuildEventList(vboxContainer2, player, Loc.Get("events.ancient", null), list2.Cast<EventModel>().ToList<EventModel>(), StsColors.purple);
			PanelContainer panelContainer = new PanelContainer();
			panelContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			panelContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			panelContainer.CustomMinimumSize = new Vector2(400f, 0f);
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.06f, 0.05f, 0.08f, 0.9f);
			styleBoxFlat.SetBorderWidthAll(0);
			styleBoxFlat.SetCornerRadiusAll(6);
			styleBoxFlat.SetContentMarginAll(12f);
			panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
			hsplitContainer.AddChild(panelContainer, false, Node.InternalMode.Disabled);
			ScrollContainer scrollContainer2 = new ScrollContainer();
			scrollContainer2.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			scrollContainer2.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			panelContainer.AddChild(scrollContainer2, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer3 = new VBoxContainer();
			vboxContainer3.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vboxContainer3.AddThemeConstantOverride("separation", 8);
			scrollContainer2.AddChild(vboxContainer3, false, Node.InternalMode.Disabled);
			if (EventsTab._selectedEvent != null)
			{
				EventsTab.BuildPreview(vboxContainer3, player, EventsTab._selectedEvent);
				return;
			}
			EventsTab.BuildEmptyPreview(vboxContainer3);
		}

		// Token: 0x06000131 RID: 305 RVA: 0x000102AC File Offset: 0x0000E4AC
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
			searchInput.PlaceholderText = Loc.Get("search_events_placeholder", null);
			searchInput.Text = EventsTab._searchText;
			searchInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			searchInput.CustomMinimumSize = new Vector2(150f, 28f);
			searchInput.AddThemeFontSizeOverride("font_size", 13);
			searchInput.TextChanged += delegate(string newText)
			{
				EventsTab._searchText = newText;
				LoadoutPanel.RequestRefresh();
			};
			hboxContainer.AddChild(searchInput, false, Node.InternalMode.Disabled);
			if (!string.IsNullOrEmpty(EventsTab._searchText))
			{
				Button button = LoadoutPanel.CreateActionButton(Loc.Get("clear", null), new Color?(StsColors.red));
				button.Pressed += delegate
				{
					EventsTab._searchText = "";
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

		// Token: 0x06000132 RID: 306 RVA: 0x00010448 File Offset: 0x0000E648
		private static bool MatchesSearch(EventModel evt)
		{
			if (string.IsNullOrEmpty(EventsTab._searchText))
			{
				return true;
			}
			string[] array = EventsTab._searchText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (array.Length == 0)
			{
				return true;
			}
			List<string> list = new List<string> { evt.Id.Entry.ToLowerInvariant() };
			try
			{
				list.Add(evt.Title.GetRawText().ToLowerInvariant());
			}
			catch
			{
			}
			try
			{
				LocString initialDescription = evt.InitialDescription;
				string text = ((initialDescription != null) ? initialDescription.GetRawText() : null);
				if (!string.IsNullOrEmpty(text))
				{
					list.Add(text.ToLowerInvariant());
				}
			}
			catch
			{
			}
			try
			{
				IEnumerable<LocString> gameInfoOptions = evt.GameInfoOptions;
				if (gameInfoOptions != null)
				{
					foreach (LocString locString in gameInfoOptions)
					{
						try
						{
							list.Add(locString.GetRawText().ToLowerInvariant());
						}
						catch
						{
						}
					}
				}
			}
			catch
			{
			}
			foreach (string text2 in array)
			{
				bool flag = false;
				using (List<string>.Enumerator enumerator2 = list.GetEnumerator())
				{
					while (enumerator2.MoveNext())
					{
						if (enumerator2.Current.Contains(text2))
						{
							flag = true;
							break;
						}
					}
				}
				if (!flag)
				{
					return false;
				}
			}
			return true;
		}

		// Token: 0x06000133 RID: 307 RVA: 0x000105DC File Offset: 0x0000E7DC
		private static void BuildEventList(VBoxContainer container, Player player, string groupName, List<EventModel> events, Color headerColor)
		{
			if (events.Count == 0 && !string.IsNullOrEmpty(EventsTab._searchText))
			{
				return;
			}
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(3, 2);
			defaultInterpolatedStringHandler.AppendFormatted(groupName);
			defaultInterpolatedStringHandler.AppendLiteral(" (");
			defaultInterpolatedStringHandler.AppendFormatted<int>(events.Count);
			defaultInterpolatedStringHandler.AppendLiteral(")");
			Label label = LoadoutPanel.CreateSectionHeader(defaultInterpolatedStringHandler.ToStringAndClear());
			label.AddThemeColorOverride("font_color", headerColor);
			container.AddChild(label, false, Node.InternalMode.Disabled);
			foreach (EventModel eventModel in events)
			{
				string text;
				try
				{
					text = eventModel.Title.GetFormattedText();
				}
				catch
				{
					text = eventModel.Id.Entry;
				}
				bool flag = EventsTab._selectedEvent != null && EventsTab._selectedEvent.Id.Entry == eventModel.Id.Entry;
				Button button = new Button();
				button.Text = text;
				button.CustomMinimumSize = new Vector2(0f, 28f);
				button.AddThemeFontSizeOverride("font_size", 13);
				button.Alignment = HorizontalAlignment.Left;
				button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				if (flag)
				{
					button.AddThemeColorOverride("font_color", StsColors.gold);
					StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
					styleBoxFlat.BgColor = new Color(0.2f, 0.18f, 0.1f, 0.9f);
					styleBoxFlat.BorderColor = StsColors.gold;
					styleBoxFlat.SetBorderWidthAll(1);
					styleBoxFlat.SetCornerRadiusAll(4);
					styleBoxFlat.SetContentMarginAll(4f);
					button.AddThemeStyleboxOverride("normal", styleBoxFlat);
				}
				else
				{
					button.AddThemeColorOverride("font_color", StsColors.cream);
					button.AddThemeColorOverride("font_hover_color", StsColors.gold);
					StyleBoxFlat styleBoxFlat2 = new StyleBoxFlat();
					styleBoxFlat2.BgColor = new Color(0.1f, 0.08f, 0.12f, 0.6f);
					styleBoxFlat2.SetCornerRadiusAll(4);
					styleBoxFlat2.SetContentMarginAll(4f);
					button.AddThemeStyleboxOverride("normal", styleBoxFlat2);
					StyleBoxFlat styleBoxFlat3 = new StyleBoxFlat();
					styleBoxFlat3.BgColor = new Color(0.15f, 0.12f, 0.18f, 0.8f);
					styleBoxFlat3.BorderColor = new Color(0.5f, 0.4f, 0.3f, 0.5f);
					styleBoxFlat3.SetBorderWidthAll(1);
					styleBoxFlat3.SetCornerRadiusAll(4);
					styleBoxFlat3.SetContentMarginAll(4f);
					button.AddThemeStyleboxOverride("hover", styleBoxFlat3);
				}
				EventModel capturedEvt = eventModel;
				button.Pressed += delegate
				{
					EventsTab._selectedEvent = capturedEvt;
					LoadoutPanel.RequestRefresh();
				};
				container.AddChild(button, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x06000134 RID: 308 RVA: 0x000108F4 File Offset: 0x0000EAF4
		private static void BuildEmptyPreview(VBoxContainer container)
		{
			Label label = new Label();
			label.Text = Loc.Get("events.select_preview", null);
			label.AddThemeFontSizeOverride("font_size", 18);
			label.AddThemeColorOverride("font_color", StsColors.gray);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			container.AddChild(label, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000135 RID: 309 RVA: 0x0001095C File Offset: 0x0000EB5C
		private static void BuildPreview(VBoxContainer container, Player player, EventModel evt)
		{
			string text;
			try
			{
				text = evt.Title.GetFormattedText();
			}
			catch
			{
				text = evt.Id.Entry;
			}
			Label label = new Label();
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 22);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			Font font = GD.Load<Font>("res://themes/kreon_bold_glyph_space_two.tres");
			if (font != null)
			{
				label.AddThemeFontOverride("font", font);
			}
			container.AddChild(label, false, Node.InternalMode.Disabled);
			Label label2 = new Label();
			label2.Text = evt.Id.Entry;
			label2.AddThemeFontSizeOverride("font_size", 12);
			label2.AddThemeColorOverride("font_color", StsColors.gray);
			label2.HorizontalAlignment = HorizontalAlignment.Center;
			container.AddChild(label2, false, Node.InternalMode.Disabled);
			try
			{
				Texture2D texture2D = evt.CreateInitialPortrait();
				if (texture2D != null)
				{
					container.AddChild(new TextureRect
					{
						Texture = texture2D,
						CustomMinimumSize = new Vector2(0f, 240f),
						ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
						StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
						SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
					}, false, Node.InternalMode.Disabled);
				}
			}
			catch
			{
			}
			try
			{
				LocString initialDescription = evt.InitialDescription;
				string text2 = ((initialDescription != null) ? initialDescription.GetFormattedText() : null);
				if (!string.IsNullOrEmpty(text2))
				{
					RichTextLabel richTextLabel = new RichTextLabel();
					richTextLabel.BbcodeEnabled = true;
					richTextLabel.Text = EventsTab.ConvertStsBbcode(text2);
					richTextLabel.FitContent = true;
					richTextLabel.ScrollActive = false;
					richTextLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
					richTextLabel.AddThemeFontSizeOverride("normal_font_size", 14);
					richTextLabel.AddThemeColorOverride("default_color", StsColors.cream);
					container.AddChild(richTextLabel, false, Node.InternalMode.Disabled);
				}
			}
			catch
			{
			}
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 1f),
				Color = new Color(0.91f, 0.86f, 0.75f, 0.2f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
			try
			{
				IEnumerable<LocString> gameInfoOptions = evt.GameInfoOptions;
				List<LocString> list = ((gameInfoOptions != null) ? gameInfoOptions.ToList<LocString>() : null);
				if (list != null && list.Count > 0)
				{
					Dictionary<string, ValueTuple<LocString, LocString>> dictionary = new Dictionary<string, ValueTuple<LocString, LocString>>();
					foreach (LocString locString in list)
					{
						string locEntryKey = locString.LocEntryKey;
						string text4;
						bool flag;
						if (locEntryKey.EndsWith(".title"))
						{
							string text3 = locEntryKey;
							int num = ".title".Length;
							text4 = text3.Substring(0, text3.Length - num);
							flag = true;
						}
						else
						{
							if (!locEntryKey.EndsWith(".description"))
							{
								continue;
							}
							string text3 = locEntryKey;
							int num = ".description".Length;
							text4 = text3.Substring(0, text3.Length - num);
							flag = false;
						}
						ValueTuple<LocString, LocString> valueTuple;
						if (!dictionary.TryGetValue(text4, out valueTuple))
						{
							valueTuple = new ValueTuple<LocString, LocString>(null, null);
						}
						dictionary[text4] = (flag ? new ValueTuple<LocString, LocString>(locString, valueTuple.Item2) : new ValueTuple<LocString, LocString>(valueTuple.Item1, locString));
					}
					Label label3 = new Label();
					label3.Text = Loc.Fmt("events.options", new object[] { dictionary.Count });
					label3.AddThemeFontSizeOverride("font_size", 16);
					label3.AddThemeColorOverride("font_color", StsColors.gold);
					container.AddChild(label3, false, Node.InternalMode.Disabled);
					foreach (KeyValuePair<string, ValueTuple<LocString, LocString>> keyValuePair in dictionary)
					{
						string text3;
						ValueTuple<LocString, LocString> valueTuple2;
						keyValuePair.Deconstruct(out text3, out valueTuple2);
						ValueTuple<LocString, LocString> valueTuple3 = valueTuple2;
						EventsTab.AddLocOptionPreview(container, valueTuple3.Item1, valueTuple3.Item2);
					}
				}
			}
			catch
			{
			}
			AncientEventModel ancientEventModel = evt as AncientEventModel;
			if (ancientEventModel != null)
			{
				try
				{
					IEnumerable<EventOption> allPossibleOptions = ancientEventModel.AllPossibleOptions;
					List<EventOption> list2 = ((allPossibleOptions != null) ? allPossibleOptions.ToList<EventOption>() : null);
					if (list2 != null && list2.Count > 0)
					{
						Label label4 = new Label();
						label4.Text = Loc.Fmt("events.possible_options", new object[] { list2.Count });
						label4.AddThemeFontSizeOverride("font_size", 16);
						label4.AddThemeColorOverride("font_color", StsColors.purple);
						container.AddChild(label4, false, Node.InternalMode.Disabled);
						foreach (EventOption eventOption in list2)
						{
							EventsTab.AddEventOptionPreview(container, eventOption);
						}
					}
				}
				catch
				{
				}
			}
			Label label5 = new Label();
			label5.Text = Loc.Get("events.auto_return_hint", null);
			label5.AddThemeFontSizeOverride("font_size", 12);
			label5.AddThemeColorOverride("font_color", StsColors.gray);
			label5.HorizontalAlignment = HorizontalAlignment.Center;
			container.AddChild(label5, false, Node.InternalMode.Disabled);
			Button button = LoadoutPanel.CreateActionButton(Loc.Get("events.enter", null), new Color?(StsColors.green));
			button.CustomMinimumSize = new Vector2(200f, 36f);
			button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
			button.AddThemeFontSizeOverride("font_size", 16);
			button.Pressed += delegate
			{
				LoadoutPanel.Hide();
				TrainerEventState.IsNestedEvent = true;
				TrainerEventState.SuppressMapHistory = true;
				TrainerEventState.SaveCurrentRoom();
				MapPointType mapPointType = ((evt is AncientEventModel) ? MapPointType.Ancient : MapPointType.Unknown);
				TaskHelper.RunSafely(RunManager.Instance.EnterRoomDebug(RoomType.Event, mapPointType, evt, true));
			};
			container.AddChild(button, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000136 RID: 310 RVA: 0x00010FD4 File Offset: 0x0000F1D4
		[NullableContext(2)]
		private static void AddLocOptionPreview([Nullable(1)] VBoxContainer container, LocString titleLoc, LocString descLoc)
		{
			if (titleLoc == null && descLoc == null)
			{
				return;
			}
			PanelContainer panelContainer = new PanelContainer();
			panelContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.1f, 0.08f, 0.12f, 0.8f);
			styleBoxFlat.SetBorderWidthAll(0);
			styleBoxFlat.SetCornerRadiusAll(4);
			styleBoxFlat.SetContentMarginAll(8f);
			panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.AddThemeConstantOverride("separation", 4);
			if (titleLoc != null)
			{
				string text;
				try
				{
					text = titleLoc.GetFormattedText();
				}
				catch
				{
					try
					{
						text = titleLoc.GetRawText();
					}
					catch
					{
						text = "???";
					}
				}
				RichTextLabel richTextLabel = new RichTextLabel();
				richTextLabel.BbcodeEnabled = true;
				richTextLabel.Text = EventsTab.ConvertStsBbcode(text);
				richTextLabel.FitContent = true;
				richTextLabel.ScrollActive = false;
				richTextLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				richTextLabel.AddThemeFontSizeOverride("normal_font_size", 14);
				richTextLabel.AddThemeColorOverride("default_color", StsColors.cream);
				vboxContainer.AddChild(richTextLabel, false, Node.InternalMode.Disabled);
			}
			if (descLoc != null)
			{
				string text2;
				try
				{
					text2 = descLoc.GetFormattedText();
				}
				catch
				{
					try
					{
						text2 = descLoc.GetRawText();
					}
					catch
					{
						text2 = null;
					}
				}
				if (!string.IsNullOrEmpty(text2))
				{
					RichTextLabel richTextLabel2 = new RichTextLabel();
					richTextLabel2.BbcodeEnabled = true;
					richTextLabel2.Text = EventsTab.ConvertStsBbcode(text2);
					richTextLabel2.FitContent = true;
					richTextLabel2.ScrollActive = false;
					richTextLabel2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
					richTextLabel2.AddThemeFontSizeOverride("normal_font_size", 12);
					richTextLabel2.AddThemeColorOverride("default_color", StsColors.gray);
					vboxContainer.AddChild(richTextLabel2, false, Node.InternalMode.Disabled);
				}
			}
			panelContainer.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
			container.AddChild(panelContainer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000137 RID: 311 RVA: 0x000111C8 File Offset: 0x0000F3C8
		private static void AddEventOptionPreview(VBoxContainer container, EventOption option)
		{
			PanelContainer panelContainer = new PanelContainer();
			panelContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.1f, 0.08f, 0.12f, 0.8f);
			styleBoxFlat.SetBorderWidthAll(0);
			styleBoxFlat.SetCornerRadiusAll(4);
			styleBoxFlat.SetContentMarginAll(8f);
			panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.AddThemeConstantOverride("separation", 4);
			string text2;
			try
			{
				LocString title = option.Title;
				string text;
				if ((text = ((title != null) ? title.GetFormattedText() : null)) == null)
				{
					text = option.TextKey ?? "???";
				}
				text2 = text;
			}
			catch
			{
				text2 = option.TextKey ?? "???";
			}
			Label label = new Label();
			label.Text = text2;
			label.AddThemeFontSizeOverride("font_size", 15);
			label.AddThemeColorOverride("font_color", option.IsLocked ? StsColors.gray : StsColors.cream);
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			vboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			try
			{
				LocString description = option.Description;
				string text3 = ((description != null) ? description.GetFormattedText() : null);
				if (!string.IsNullOrEmpty(text3))
				{
					Label label2 = new Label();
					label2.Text = text3;
					label2.AddThemeFontSizeOverride("font_size", 12);
					label2.AddThemeColorOverride("font_color", StsColors.gray);
					label2.AutowrapMode = TextServer.AutowrapMode.WordSmart;
					vboxContainer.AddChild(label2, false, Node.InternalMode.Disabled);
				}
			}
			catch
			{
			}
			if (option.IsLocked)
			{
				Label label3 = new Label();
				label3.Text = Loc.Get("events.locked", null);
				label3.AddThemeFontSizeOverride("font_size", 12);
				label3.AddThemeColorOverride("font_color", StsColors.red);
				vboxContainer.AddChild(label3, false, Node.InternalMode.Disabled);
			}
			panelContainer.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
			container.AddChild(panelContainer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000138 RID: 312 RVA: 0x000113D4 File Offset: 0x0000F5D4
		private static string ConvertStsBbcode(string text)
		{
			text = EventsTab.EffectTagRegex.Replace(text, "");
			foreach (KeyValuePair<string, string> keyValuePair in EventsTab.StsColorMap)
			{
				text = text.Replace("[" + keyValuePair.Key + "]", "[color=" + keyValuePair.Value + "]", StringComparison.OrdinalIgnoreCase);
				text = text.Replace("[/" + keyValuePair.Key + "]", "[/color]", StringComparison.OrdinalIgnoreCase);
			}
			return text;
		}

		// Token: 0x06000139 RID: 313 RVA: 0x0001148C File Offset: 0x0000F68C
		// Note: this type is marked as 'beforefieldinit'.
		static EventsTab()
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			dictionary["red"] = "#FF6347";
			dictionary["green"] = "#50C878";
			dictionary["gold"] = "#E8C874";
			dictionary["blue"] = "#6495ED";
			dictionary["purple"] = "#9E68FF";
			dictionary["gray"] = "#999999";
			dictionary["white"] = "#FFFFFF";
			dictionary["cream"] = "#E8DAC0";
			EventsTab.StsColorMap = dictionary;
			EventsTab.EffectTagRegex = new Regex("\\[/?(jitter|sine|wave|shake|pulse|fade|rainbow)\\]", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		}

		// Token: 0x04000075 RID: 117
		[Nullable(2)]
		private static EventModel _selectedEvent;

		// Token: 0x04000076 RID: 118
		private static string _searchText = "";

		// Token: 0x04000077 RID: 119
		private static readonly Dictionary<string, string> StsColorMap;

		// Token: 0x04000078 RID: 120
		private static readonly Regex EffectTagRegex;

		// Token: 0x02000094 RID: 148
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x04000179 RID: 377
			[Nullable(0)]
			public static Func<EventModel, bool> <0>__MatchesSearch;

			// Token: 0x0400017A RID: 378
			[Nullable(0)]
			public static Func<AncientEventModel, bool> <1>__MatchesSearch;
		}
	}
}
