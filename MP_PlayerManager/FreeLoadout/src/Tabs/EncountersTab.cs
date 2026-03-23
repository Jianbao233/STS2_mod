using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;

namespace MP_PlayerManager.Tabs
{
	// Token: 0x02000030 RID: 48
	[NullableContext(1)]
	[Nullable(0)]
	internal static class EncountersTab
	{
		// Token: 0x0600013A RID: 314 RVA: 0x00011540 File Offset: 0x0000F740
		internal static void Build(VBoxContainer container, Player player)
		{
			CombatManager instance = CombatManager.Instance;
			if (instance == null || !instance.IsInProgress)
			{
				Label label = new Label();
				label.Text = Loc.Get("encounters.not_in_combat", null);
				label.AddThemeFontSizeOverride("font_size", 16);
				label.AddThemeColorOverride("font_color", StsColors.cream);
				container.AddChild(label, false, Node.InternalMode.Disabled);
				return;
			}
			CombatState combatState = instance.DebugOnlyGetState();
			if (combatState == null)
			{
				return;
			}
			EncountersTab.BuildCurrentEnemies(container, combatState);
			EncountersTab.AddSeparator(container);
			EncountersTab.BuildSearchBar(container);
			EncountersTab.EnsureCategories();
			IEnumerable<MonsterModel> monsters = ModelDb.Monsters;
			Func<MonsterModel, bool> func;
			if ((func = EncountersTab.<>O.<0>__MatchesSearch) == null)
			{
				func = (EncountersTab.<>O.<0>__MatchesSearch = new Func<MonsterModel, bool>(EncountersTab.MatchesSearch));
			}
			List<MonsterModel> list = monsters.Where(func).ToList<MonsterModel>();
			ValueTuple<string, string, Color, List<MonsterModel>>[] array = new ValueTuple<string, string, Color, List<MonsterModel>>[]
			{
				new ValueTuple<string, string, Color, List<MonsterModel>>("normal", Loc.Get("encounters.group_normal", null), new Color(0.6f, 0.6f, 0.6f, 1f), new List<MonsterModel>()),
				new ValueTuple<string, string, Color, List<MonsterModel>>("elite", Loc.Get("encounters.group_elite", null), new Color(1f, 0.85f, 0.4f, 1f), new List<MonsterModel>()),
				new ValueTuple<string, string, Color, List<MonsterModel>>("boss", Loc.Get("encounters.group_boss", null), new Color(0.9f, 0.3f, 0.3f, 1f), new List<MonsterModel>())
			};
			foreach (MonsterModel monsterModel in list)
			{
				string category = EncountersTab.GetCategory(monsterModel);
				if (category == "boss")
				{
					array[2].Item4.Add(monsterModel);
				}
				else if (category == "elite")
				{
					array[1].Item4.Add(monsterModel);
				}
				else
				{
					array[0].Item4.Add(monsterModel);
				}
			}
			foreach (ValueTuple<string, string, Color, List<MonsterModel>> valueTuple in array)
			{
				string item = valueTuple.Item1;
				string item2 = valueTuple.Item2;
				Color item3 = valueTuple.Item3;
				List<MonsterModel> item4 = valueTuple.Item4;
				if (item4.Count != 0)
				{
					item4.Sort((MonsterModel a, MonsterModel b) => string.Compare(EncountersTab.GetMonsterName(a), EncountersTab.GetMonsterName(b), StringComparison.Ordinal));
					EncountersTab.BuildMonsterGroup(container, item2, item3, item, item4);
				}
			}
		}

		// Token: 0x0600013B RID: 315 RVA: 0x000117CC File Offset: 0x0000F9CC
		private static void BuildCurrentEnemies(VBoxContainer container, CombatState state)
		{
			List<Creature> list = state.Enemies.Where((Creature c) => !c.IsDead).ToList<Creature>();
			Label label = LoadoutPanel.CreateSectionHeader(Loc.Fmt("encounters.current_enemies", new object[] { list.Count }));
			container.AddChild(label, false, Node.InternalMode.Disabled);
			if (list.Count == 0)
			{
				Label label2 = new Label();
				label2.Text = Loc.Get("encounters.no_enemies", null);
				label2.AddThemeFontSizeOverride("font_size", 14);
				label2.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label2, false, Node.InternalMode.Disabled);
				return;
			}
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 6);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			foreach (Creature creature in list)
			{
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(6, 3);
				defaultInterpolatedStringHandler.AppendFormatted(creature.Name ?? "?");
				defaultInterpolatedStringHandler.AppendLiteral("\nHP: ");
				defaultInterpolatedStringHandler.AppendFormatted<int>(creature.CurrentHp);
				defaultInterpolatedStringHandler.AppendLiteral("/");
				defaultInterpolatedStringHandler.AppendFormatted<int>(creature.MaxHp);
				string text = defaultInterpolatedStringHandler.ToStringAndClear();
				Label label3 = new Label();
				label3.Text = text;
				label3.AddThemeFontSizeOverride("font_size", 12);
				label3.AddThemeColorOverride("font_color", StsColors.cream);
				PanelContainer panelContainer = new PanelContainer();
				StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
				styleBoxFlat.BgColor = new Color(0.12f, 0.08f, 0.08f, 0.8f);
				styleBoxFlat.BorderColor = StsColors.red;
				styleBoxFlat.SetBorderWidthAll(1);
				styleBoxFlat.SetCornerRadiusAll(4);
				styleBoxFlat.SetContentMarginAll(6f);
				panelContainer.AddThemeStyleboxOverride("panel", styleBoxFlat);
				panelContainer.AddChild(label3, false, Node.InternalMode.Disabled);
				hboxContainer.AddChild(panelContainer, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x0600013C RID: 316 RVA: 0x00011A10 File Offset: 0x0000FC10
		private static void BuildMonsterGroup(VBoxContainer container, string label, Color color, string key, List<MonsterModel> monsters)
		{
			Label label2 = new Label();
			Label label3 = label2;
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(3, 2);
			defaultInterpolatedStringHandler.AppendFormatted(label);
			defaultInterpolatedStringHandler.AppendLiteral(" (");
			defaultInterpolatedStringHandler.AppendFormatted<int>(monsters.Count);
			defaultInterpolatedStringHandler.AppendLiteral(")");
			label3.Text = defaultInterpolatedStringHandler.ToStringAndClear();
			label2.AddThemeFontSizeOverride("font_size", 16);
			label2.AddThemeColorOverride("font_color", color);
			Font font = GD.Load<Font>("res://themes/kreon_bold_glyph_space_two.tres");
			if (font != null)
			{
				label2.AddThemeFontOverride("font", font);
			}
			container.AddChild(label2, false, Node.InternalMode.Disabled);
			Color color2;
			if (!(key == "boss"))
			{
				if (!(key == "elite"))
				{
					color2 = new Color(0.5f, 0.5f, 0.5f, 0.5f);
				}
				else
				{
					color2 = new Color(1f, 0.85f, 0.4f, 0.8f);
				}
			}
			else
			{
				color2 = new Color(0.9f, 0.3f, 0.3f, 0.8f);
			}
			Color color3 = color2;
			GridContainer gridContainer = new GridContainer
			{
				Columns = 5
			};
			gridContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			gridContainer.AddThemeConstantOverride("h_separation", 4);
			gridContainer.AddThemeConstantOverride("v_separation", 4);
			container.AddChild(gridContainer, false, Node.InternalMode.Disabled);
			foreach (MonsterModel monsterModel in monsters)
			{
				EncountersTab.BuildMonsterCell(gridContainer, monsterModel, color3);
			}
		}

		// Token: 0x0600013D RID: 317 RVA: 0x00011BB0 File Offset: 0x0000FDB0
		private static void BuildMonsterCell(GridContainer grid, MonsterModel monster, Color borderColor)
		{
			string monsterName = EncountersTab.GetMonsterName(monster);
			Button button = new Button
			{
				Text = "",
				CustomMinimumSize = new Vector2(110f, 120f),
				ClipContents = true
			};
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat
			{
				BgColor = new Color(0.1f, 0.09f, 0.14f, 0.9f),
				BorderColor = borderColor
			};
			styleBoxFlat.SetBorderWidthAll(1);
			styleBoxFlat.SetCornerRadiusAll(4);
			styleBoxFlat.SetContentMarginAll(4f);
			button.AddThemeStyleboxOverride("normal", styleBoxFlat);
			StyleBoxFlat styleBoxFlat2 = new StyleBoxFlat
			{
				BgColor = new Color(0.15f, 0.14f, 0.2f, 0.95f),
				BorderColor = new Color(borderColor.R, borderColor.G, borderColor.B, 1f)
			};
			styleBoxFlat2.SetBorderWidthAll(2);
			styleBoxFlat2.SetCornerRadiusAll(4);
			styleBoxFlat2.SetContentMarginAll(4f);
			button.AddThemeStyleboxOverride("hover", styleBoxFlat2);
			StyleBoxFlat styleBoxFlat3 = new StyleBoxFlat
			{
				BgColor = new Color(0.18f, 0.17f, 0.24f, 0.98f),
				BorderColor = StsColors.gold
			};
			styleBoxFlat3.SetBorderWidthAll(2);
			styleBoxFlat3.SetCornerRadiusAll(4);
			styleBoxFlat3.SetContentMarginAll(4f);
			button.AddThemeStyleboxOverride("pressed", styleBoxFlat3);
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect, false);
			vboxContainer.OffsetLeft = 4f;
			vboxContainer.OffsetTop = 4f;
			vboxContainer.OffsetRight = -4f;
			vboxContainer.OffsetBottom = -4f;
			vboxContainer.AddThemeConstantOverride("separation", 2);
			button.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
			Control control = EncountersTab.CreateSmallPreview(monster);
			if (control != null)
			{
				vboxContainer.AddChild(control, false, Node.InternalMode.Disabled);
			}
			Label label = new Label
			{
				Text = monsterName,
				HorizontalAlignment = HorizontalAlignment.Center,
				AutowrapMode = TextServer.AutowrapMode.WordSmart
			};
			label.AddThemeFontSizeOverride("font_size", 11);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			vboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			EncountersTab.SetMouseFilterRecursive(vboxContainer);
			MonsterModel captured = monster;
			button.Pressed += delegate
			{
				EncountersTab.SpawnMonster(captured);
			};
			grid.AddChild(button, false, Node.InternalMode.Disabled);
		}

		// Token: 0x0600013E RID: 318 RVA: 0x00011E18 File Offset: 0x00010018
		[return: Nullable(2)]
		private static Control CreateSmallPreview(MonsterModel monster)
		{
			Control control;
			try
			{
				NCreatureVisuals ncreatureVisuals = monster.CreateVisuals();
				if (ncreatureVisuals == null)
				{
					control = null;
				}
				else
				{
					SubViewportContainer subViewportContainer = new SubViewportContainer();
					subViewportContainer.CustomMinimumSize = new Vector2(0f, 80f);
					subViewportContainer.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
					subViewportContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
					subViewportContainer.Stretch = false;
					subViewportContainer.MouseFilter = Control.MouseFilterEnum.Ignore;
					SubViewport subViewport = new SubViewport
					{
						Size = new Vector2I(120, 80),
						TransparentBg = true,
						RenderTargetUpdateMode = SubViewport.UpdateMode.Always
					};
					subViewportContainer.AddChild(subViewport, false, Node.InternalMode.Disabled);
					subViewport.AddChild(ncreatureVisuals, false, Node.InternalMode.Disabled);
					NCreatureVisuals capturedVisuals = ncreatureVisuals;
					SubViewport capturedViewport = subViewport;
					Callable.From(delegate
					{
						EncountersTab.AlignSmallPreview(capturedVisuals, capturedViewport);
					}).CallDeferred(Array.Empty<Variant>());
					control = subViewportContainer;
				}
			}
			catch
			{
				control = null;
			}
			return control;
		}

		// Token: 0x0600013F RID: 319 RVA: 0x00011EF4 File Offset: 0x000100F4
		private static void SetMouseFilterRecursive(Control control)
		{
			control.MouseFilter = Control.MouseFilterEnum.Ignore;
			foreach (Node node in control.GetChildren(false))
			{
				Control control2 = node as Control;
				if (control2 != null)
				{
					EncountersTab.SetMouseFilterRecursive(control2);
				}
			}
		}

		// Token: 0x06000140 RID: 320 RVA: 0x00011F54 File Offset: 0x00010154
		private static void AlignSmallPreview(NCreatureVisuals visuals, SubViewport viewport)
		{
			if (!GodotObject.IsInstanceValid(visuals) || !GodotObject.IsInstanceValid(viewport))
			{
				return;
			}
			Control bounds = visuals.Bounds;
			Vector2 vector = ((bounds != null) ? bounds.Size : Vector2.Zero);
			if (vector.X > 1f && vector.Y > 1f)
			{
				float num = (float)viewport.Size.X - 10f;
				float num2 = (float)viewport.Size.Y - 10f;
				float num3 = num / vector.X;
				float num4 = num2 / vector.Y;
				float num5 = Mathf.Clamp(Mathf.Min(num3, num4), 0.2f, 0.7f);
				visuals.Scale = Vector2.One * num5;
			}
			else
			{
				visuals.Scale = new Vector2(0.5f, 0.5f);
			}
			visuals.Position = new Vector2((float)viewport.Size.X / 2f, (float)viewport.Size.Y * 0.82f);
		}

		// Token: 0x06000141 RID: 321 RVA: 0x0001204C File Offset: 0x0001024C
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
			searchInput.PlaceholderText = Loc.Get("search_encounters_placeholder", null);
			searchInput.Text = EncountersTab._searchText;
			searchInput.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			searchInput.CustomMinimumSize = new Vector2(150f, 28f);
			searchInput.AddThemeFontSizeOverride("font_size", 13);
			searchInput.TextChanged += delegate(string newText)
			{
				EncountersTab._searchText = newText;
				LoadoutPanel.RequestRefresh();
			};
			hboxContainer.AddChild(searchInput, false, Node.InternalMode.Disabled);
			if (!string.IsNullOrEmpty(EncountersTab._searchText))
			{
				Button button = LoadoutPanel.CreateActionButton(Loc.Get("clear", null), new Color?(StsColors.red));
				button.Pressed += delegate
				{
					EncountersTab._searchText = "";
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

		// Token: 0x06000142 RID: 322 RVA: 0x000121E8 File Offset: 0x000103E8
		private static void EnsureCategories()
		{
			if (EncountersTab._eliteTypes != null)
			{
				return;
			}
			EncountersTab._eliteTypes = new HashSet<Type>();
			EncountersTab._bossTypes = new HashSet<Type>();
			try
			{
				foreach (EncounterModel encounterModel in ModelDb.AllEncounters)
				{
					IEnumerable<Type> enumerable = encounterModel.AllPossibleMonsters.Select((MonsterModel m) => m.GetType());
					if (encounterModel.RoomType == RoomType.Boss)
					{
						using (IEnumerator<Type> enumerator2 = enumerable.GetEnumerator())
						{
							while (enumerator2.MoveNext())
							{
								Type type = enumerator2.Current;
								EncountersTab._bossTypes.Add(type);
							}
							continue;
						}
					}
					if (encounterModel.RoomType == RoomType.Elite)
					{
						foreach (Type type2 in enumerable)
						{
							EncountersTab._eliteTypes.Add(type2);
						}
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x06000143 RID: 323 RVA: 0x0001231C File Offset: 0x0001051C
		private static string GetCategory(MonsterModel monster)
		{
			Type type = monster.GetType();
			if (EncountersTab._bossTypes.Contains(type))
			{
				return "boss";
			}
			if (EncountersTab._eliteTypes.Contains(type))
			{
				return "elite";
			}
			return "normal";
		}

		// Token: 0x06000144 RID: 324 RVA: 0x0001235C File Offset: 0x0001055C
		private static bool MatchesSearch(MonsterModel monster)
		{
			if (string.IsNullOrEmpty(EncountersTab._searchText))
			{
				return true;
			}
			string[] array = EncountersTab._searchText.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (array.Length == 0)
			{
				return true;
			}
			List<string> list = new List<string>
			{
				monster.Id.Entry.ToLowerInvariant(),
				EncountersTab.GetMonsterName(monster).ToLowerInvariant()
			};
			foreach (string text in array)
			{
				bool flag = false;
				using (List<string>.Enumerator enumerator = list.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						if (enumerator.Current.Contains(text))
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

		// Token: 0x06000145 RID: 325 RVA: 0x00012424 File Offset: 0x00010624
		private static string GetMonsterName(MonsterModel monster)
		{
			string text;
			try
			{
				text = monster.Title.GetFormattedText();
			}
			catch
			{
				text = monster.Id.Entry;
			}
			return text;
		}

		// Token: 0x06000146 RID: 326 RVA: 0x00012460 File Offset: 0x00010660
		private static void AddSeparator(VBoxContainer container)
		{
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 1f),
				Color = new Color(0.91f, 0.86f, 0.75f, 0.2f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000147 RID: 327 RVA: 0x000124BC File Offset: 0x000106BC
		private static void SpawnMonster(MonsterModel template)
		{
			CombatManager instance = CombatManager.Instance;
			if (instance == null || !instance.IsInProgress)
			{
				return;
			}
			CombatState combatState = instance.DebugOnlyGetState();
			if (combatState == null)
			{
				return;
			}
			try
			{
				string text = null;
				EncounterModel encounter = combatState.Encounter;
				string text2 = ((encounter != null) ? encounter.GetNextSlot(combatState) : null) ?? "";
				if (!string.IsNullOrEmpty(text2))
				{
					text = text2;
				}
				TaskHelper.RunSafely(EncountersTab.SpawnMonsterAsync(template.ToMutable(), combatState, text));
			}
			catch (Exception ex)
			{
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(34, 1);
				defaultInterpolatedStringHandler.AppendLiteral("[FreeLoadout] SpawnMonster error: ");
				defaultInterpolatedStringHandler.AppendFormatted<Exception>(ex);
				GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
			}
		}

		// Token: 0x06000148 RID: 328 RVA: 0x0001256C File Offset: 0x0001076C
		private static async Task SpawnMonsterAsync(MonsterModel monster, CombatState state, [Nullable(2)] string slotName)
		{
			Creature creature = await CreatureCmd.Add(monster, state, CombatSide.Enemy, slotName);
			if (slotName == null)
			{
				NCombatRoom instance = NCombatRoom.Instance;
				if (instance != null)
				{
					NCreature creatureNode = instance.GetCreatureNode(creature);
					if (creatureNode != null)
					{
						float num = 0f;
						float num2 = 0f;
						int num3 = 0;
						foreach (NCreature ncreature in instance.CreatureNodes)
						{
							if (ncreature != creatureNode && !ncreature.Entity.IsPlayer && !ncreature.Entity.IsDead)
							{
								num += ncreature.Position.X;
								num2 += ncreature.Position.Y;
								num3++;
							}
						}
						Random random = new Random();
						float num4 = ((num3 > 0) ? (num / (float)num3) : 500f);
						float num5 = ((num3 > 0) ? (num2 / (float)num3) : 200f);
						float num6 = num4 + (float)(random.NextDouble() * 80.0 - 40.0);
						float num7 = num5 - (float)(random.NextDouble() * 20.0);
						creatureNode.Position = new Vector2(num6, num7);
					}
				}
			}
		}

		// Token: 0x04000079 RID: 121
		private static string _searchText = "";

		// Token: 0x0400007A RID: 122
		[Nullable(new byte[] { 2, 1 })]
		private static HashSet<Type> _eliteTypes;

		// Token: 0x0400007B RID: 123
		[Nullable(new byte[] { 2, 1 })]
		private static HashSet<Type> _bossTypes;

		// Token: 0x02000099 RID: 153
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x04000184 RID: 388
			[Nullable(0)]
			public static Func<MonsterModel, bool> <0>__MatchesSearch;
		}
	}
}
