using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.InspectScreens;

namespace MP_PlayerManager
{
	// Token: 0x02000015 RID: 21
	[NullableContext(1)]
	[Nullable(0)]
	internal static class InspectRelicEdit
	{
		// Token: 0x0600004B RID: 75 RVA: 0x000053F4 File Offset: 0x000035F4
		internal static void Attach(NInspectRelicScreen screen)
		{
			InspectRelicEdit.CleanupPanel();
			InspectRelicEdit._screen = screen;
			InspectRelicEdit._panelWasHidden = true;
			LoadoutPanel.HideForInspect();
			if (InspectRelicEdit.RelicsField != null)
			{
				IReadOnlyList<RelicModel> readOnlyList = InspectRelicEdit.RelicsField.GetValue(screen) as IReadOnlyList<RelicModel>;
				if (readOnlyList != null)
				{
					List<RelicModel> list = new List<RelicModel>(readOnlyList.Count);
					for (int i = 0; i < readOnlyList.Count; i++)
					{
						list.Add(readOnlyList[i].IsMutable ? readOnlyList[i] : readOnlyList[i].ToMutable());
					}
					InspectRelicEdit.RelicsField.SetValue(screen, list);
				}
			}
			Action action;
			if ((action = InspectRelicEdit.<>O.<0>__BuildPanel) == null)
			{
				action = (InspectRelicEdit.<>O.<0>__BuildPanel = new Action(InspectRelicEdit.BuildPanel));
			}
			Callable.From(action).CallDeferred(Array.Empty<Variant>());
		}

		// Token: 0x0600004C RID: 76 RVA: 0x000054B7 File Offset: 0x000036B7
		internal static void Detach()
		{
			InspectRelicEdit.CleanupPanel();
			InspectRelicEdit._screen = null;
			if (InspectRelicEdit._panelWasHidden)
			{
				InspectRelicEdit._panelWasHidden = false;
				LoadoutPanel.ShowAfterInspect();
			}
		}

		// Token: 0x0600004D RID: 77 RVA: 0x000054D8 File Offset: 0x000036D8
		internal static void Refresh()
		{
			if (InspectRelicEdit._screen == null || !GodotObject.IsInstanceValid(InspectRelicEdit._screen))
			{
				return;
			}
			InspectRelicEdit.CleanupPanel();
			Action action;
			if ((action = InspectRelicEdit.<>O.<0>__BuildPanel) == null)
			{
				action = (InspectRelicEdit.<>O.<0>__BuildPanel = new Action(InspectRelicEdit.BuildPanel));
			}
			Callable.From(action).CallDeferred(Array.Empty<Variant>());
		}

		// Token: 0x0600004E RID: 78 RVA: 0x0000552B File Offset: 0x0000372B
		private static void CleanupPanel()
		{
			if (InspectRelicEdit._panel != null && GodotObject.IsInstanceValid(InspectRelicEdit._panel))
			{
				InspectRelicEdit._panel.QueueFree();
			}
			InspectRelicEdit._panel = null;
		}

		// Token: 0x0600004F RID: 79 RVA: 0x00005550 File Offset: 0x00003750
		[NullableContext(2)]
		private static RelicModel GetCurrentRelic()
		{
			if (InspectRelicEdit._screen == null || InspectRelicEdit.RelicsField == null || InspectRelicEdit.IndexField == null)
			{
				return null;
			}
			IReadOnlyList<RelicModel> readOnlyList = InspectRelicEdit.RelicsField.GetValue(InspectRelicEdit._screen) as IReadOnlyList<RelicModel>;
			int num = (int)(InspectRelicEdit.IndexField.GetValue(InspectRelicEdit._screen) ?? 0);
			if (readOnlyList == null || num < 0 || num >= readOnlyList.Count)
			{
				return null;
			}
			return readOnlyList[num];
		}

		// Token: 0x06000050 RID: 80 RVA: 0x000055D0 File Offset: 0x000037D0
		private static void BuildPanel()
		{
			if (InspectRelicEdit._screen == null || !GodotObject.IsInstanceValid(InspectRelicEdit._screen))
			{
				return;
			}
			if (InspectRelicEdit._panel != null && GodotObject.IsInstanceValid(InspectRelicEdit._panel))
			{
				return;
			}
			RelicModel currentRelic = InspectRelicEdit.GetCurrentRelic();
			if (currentRelic == null)
			{
				return;
			}
			Player player = LoadoutPanel.GetPlayer();
			if (player == null)
			{
				return;
			}
			InspectRelicEdit._panel = new PanelContainer();
			InspectRelicEdit._panel.AnchorLeft = 0.78f;
			InspectRelicEdit._panel.AnchorRight = 0.98f;
			InspectRelicEdit._panel.AnchorTop = 0.05f;
			InspectRelicEdit._panel.AnchorBottom = 0.95f;
			InspectRelicEdit._panel.MouseFilter = Control.MouseFilterEnum.Stop;
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.06f, 0.05f, 0.08f, 0.92f);
			styleBoxFlat.SetBorderWidthAll(0);
			styleBoxFlat.SetCornerRadiusAll(8);
			styleBoxFlat.SetContentMarginAll(10f);
			InspectRelicEdit._panel.AddThemeStyleboxOverride("panel", styleBoxFlat);
			ScrollContainer scrollContainer = new ScrollContainer();
			scrollContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			scrollContainer.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
			scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
			InspectRelicEdit._panel.AddChild(scrollContainer, false, Node.InternalMode.Disabled);
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			vboxContainer.AddThemeConstantOverride("separation", 8);
			scrollContainer.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
			InspectRelicEdit.BuildActionsSection(vboxContainer, currentRelic, player);
			InspectRelicEdit.AddDivider(vboxContainer);
			InspectRelicEdit.BuildValuesSection(vboxContainer, currentRelic);
			InspectRelicEdit.AddDivider(vboxContainer);
			InspectRelicEdit.BuildPropertiesSection(vboxContainer, currentRelic);
			InspectRelicEdit._screen.AddChild(InspectRelicEdit._panel, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000051 RID: 81 RVA: 0x00005750 File Offset: 0x00003950
		private static void BuildActionsSection(VBoxContainer vbox, RelicModel relic, Player player)
		{
			vbox.AddChild(InspectRelicEdit.CreateSectionLabel(Loc.Get("edit.actions", null)), false, Node.InternalMode.Disabled);
			int num = player.Relics.Count((RelicModel r) => r.Id == relic.Id);
			Label label = new Label();
			label.Text = Loc.Fmt("inspect.owned", new object[] { num });
			label.AddThemeFontSizeOverride("font_size", 13);
			label.AddThemeColorOverride("font_color", (num > 0) ? StsColors.green : StsColors.cream);
			vbox.AddChild(label, false, Node.InternalMode.Disabled);
			Button button = LoadoutPanel.CreateActionButton(Loc.Get("inspect.acquire", null), new Color?(StsColors.green));
			button.CustomMinimumSize = new Vector2(0f, 30f);
			button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			button.Pressed += delegate
			{
				Player player2 = LoadoutPanel.GetPlayer();
				if (player2 == null)
				{
					return;
				}
				int relicBatchCount = LoadoutPanel.RelicBatchCount;
				for (int j = 0; j < relicBatchCount; j++)
				{
					TaskHelper.RunSafely(RelicCmd.Obtain((RelicModel)relic.MutableClone(), player2, -1));
				}
				InspectRelicEdit.DelayedRefresh();
			};
			vbox.AddChild(button, false, Node.InternalMode.Disabled);
			if (num > 0)
			{
				Button button2 = LoadoutPanel.CreateActionButton(Loc.Get("inspect.remove_relic", null), new Color?(StsColors.red));
				button2.CustomMinimumSize = new Vector2(0f, 30f);
				button2.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				button2.Pressed += delegate
				{
					Player player3 = LoadoutPanel.GetPlayer();
					if (player3 == null)
					{
						return;
					}
					RelicModel relicById = player3.GetRelicById(relic.Id);
					if (relicById != null)
					{
						TaskHelper.RunSafely(RelicCmd.Remove(relicById));
					}
					InspectRelicEdit.DelayedRefresh();
				};
				vbox.AddChild(button2, false, Node.InternalMode.Disabled);
			}
			Label label2 = new Label();
			label2.Text = Loc.Get("quantity", null);
			label2.AddThemeFontSizeOverride("font_size", 12);
			label2.AddThemeColorOverride("font_color", StsColors.cream);
			vbox.AddChild(label2, false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 3);
			hflowContainer.AddThemeConstantOverride("v_separation", 3);
			vbox.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			int[] array = new int[] { 1, 5, 10 };
			for (int i = 0; i < array.Length; i++)
			{
				int num2 = array[i];
				int c = num2;
				bool flag = LoadoutPanel.RelicBatchCount == c;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(1, 1);
				defaultInterpolatedStringHandler.AppendLiteral("×");
				defaultInterpolatedStringHandler.AppendFormatted<int>(c);
				Button button3 = LoadoutPanel.CreateToggleButton(defaultInterpolatedStringHandler.ToStringAndClear(), flag);
				button3.CustomMinimumSize = new Vector2(40f, 26f);
				button3.Pressed += delegate
				{
					LoadoutPanel.RelicBatchCount = c;
					InspectRelicEdit.Refresh();
				};
				hflowContainer.AddChild(button3, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x06000052 RID: 82 RVA: 0x000059EC File Offset: 0x00003BEC
		private static void BuildValuesSection(VBoxContainer container, RelicModel relic)
		{
			container.AddChild(InspectRelicEdit.CreateSectionLabel(Loc.Get("edit.values", null)), false, Node.InternalMode.Disabled);
			Label label = new Label();
			label.Text = Loc.Get("modifier_hint", null);
			label.AddThemeFontSizeOverride("font_size", 10);
			label.AddThemeColorOverride("font_color", StsColors.gray);
			container.AddChild(label, false, Node.InternalMode.Disabled);
			bool flag = false;
			foreach (KeyValuePair<string, DynamicVar> keyValuePair in relic.DynamicVars)
			{
				string text;
				DynamicVar dynamicVar;
				keyValuePair.Deconstruct(out text, out dynamicVar);
				string text2 = text;
				DynamicVar dynamicVar2 = dynamicVar;
				flag = true;
				HBoxContainer hboxContainer = new HBoxContainer();
				hboxContainer.AddThemeConstantOverride("separation", 3);
				string text3 = Loc.Get("var." + text2, text2);
				Label label2 = new Label();
				label2.Text = text3 + ":";
				label2.CustomMinimumSize = new Vector2(60f, 0f);
				label2.AddThemeFontSizeOverride("font_size", 12);
				label2.AddThemeColorOverride("font_color", StsColors.cream);
				hboxContainer.AddChild(label2, false, Node.InternalMode.Disabled);
				DynamicVar capturedVar = dynamicVar2;
				Button button = LoadoutPanel.CreateActionButton("-", new Color?(StsColors.red));
				button.CustomMinimumSize = new Vector2(26f, 22f);
				button.Pressed += delegate
				{
					capturedVar.BaseValue -= InspectRelicEdit.GetModifierAmount();
					InspectRelicEdit.Refresh();
				};
				hboxContainer.AddChild(button, false, Node.InternalMode.Disabled);
				Label label3 = new Label();
				Label label4 = label3;
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
				defaultInterpolatedStringHandler.AppendFormatted<decimal>(dynamicVar2.BaseValue, "0");
				label4.Text = defaultInterpolatedStringHandler.ToStringAndClear();
				label3.CustomMinimumSize = new Vector2(32f, 0f);
				label3.AddThemeFontSizeOverride("font_size", 12);
				label3.AddThemeColorOverride("font_color", StsColors.gold);
				label3.HorizontalAlignment = HorizontalAlignment.Center;
				hboxContainer.AddChild(label3, false, Node.InternalMode.Disabled);
				Button button2 = LoadoutPanel.CreateActionButton("+", new Color?(StsColors.green));
				button2.CustomMinimumSize = new Vector2(26f, 22f);
				button2.Pressed += delegate
				{
					capturedVar.BaseValue += InspectRelicEdit.GetModifierAmount();
					InspectRelicEdit.Refresh();
				};
				hboxContainer.AddChild(button2, false, Node.InternalMode.Disabled);
				container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			}
			if (!flag)
			{
				Label label5 = new Label();
				label5.Text = Loc.Get("edit.no_values", null);
				label5.AddThemeFontSizeOverride("font_size", 11);
				label5.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label5, false, Node.InternalMode.Disabled);
			}
		}

		// Token: 0x06000053 RID: 83 RVA: 0x00005CD8 File Offset: 0x00003ED8
		private static void BuildPropertiesSection(VBoxContainer container, RelicModel relic)
		{
			container.AddChild(InspectRelicEdit.CreateSectionLabel(Loc.Get("inspect.properties", null)), false, Node.InternalMode.Disabled);
			Label label = new Label();
			label.Text = Loc.Get("inspect.status", null);
			label.AddThemeFontSizeOverride("font_size", 12);
			label.AddThemeColorOverride("font_color", StsColors.cream);
			container.AddChild(label, false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer = new HFlowContainer();
			hflowContainer.AddThemeConstantOverride("h_separation", 3);
			hflowContainer.AddThemeConstantOverride("v_separation", 3);
			container.AddChild(hflowContainer, false, Node.InternalMode.Disabled);
			ValueTuple<RelicStatus, string>[] array = new ValueTuple<RelicStatus, string>[]
			{
				new ValueTuple<RelicStatus, string>(RelicStatus.Normal, Loc.Get("inspect.status_normal", null)),
				new ValueTuple<RelicStatus, string>(RelicStatus.Active, Loc.Get("inspect.status_active", null)),
				new ValueTuple<RelicStatus, string>(RelicStatus.Disabled, Loc.Get("inspect.status_disabled", null))
			};
			for (int i = 0; i < array.Length; i++)
			{
				ValueTuple<RelicStatus, string> valueTuple = array[i];
				RelicStatus item = valueTuple.Item1;
				string item2 = valueTuple.Item2;
				bool flag = relic.Status == item;
				Button button = LoadoutPanel.CreateToggleButton(item2, flag);
				button.CustomMinimumSize = new Vector2(52f, 24f);
				RelicStatus capturedStatus = item;
				button.Pressed += delegate
				{
					try
					{
						relic.Status = capturedStatus;
					}
					catch (Exception ex)
					{
						GD.PrintErr("[InspectRelicEdit] Status: " + ex.Message);
					}
					InspectRelicEdit.Refresh();
				};
				hflowContainer.AddChild(button, false, Node.InternalMode.Disabled);
			}
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 3);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			Label label2 = new Label();
			label2.Text = Loc.Get("inspect.stack_count", null);
			label2.CustomMinimumSize = new Vector2(55f, 0f);
			label2.AddThemeFontSizeOverride("font_size", 12);
			label2.AddThemeColorOverride("font_color", StsColors.cream);
			hboxContainer.AddChild(label2, false, Node.InternalMode.Disabled);
			Button button2 = LoadoutPanel.CreateActionButton("-", new Color?(StsColors.red));
			button2.CustomMinimumSize = new Vector2(26f, 22f);
			button2.Pressed += delegate
			{
				if (relic.StackCount > 0)
				{
					InspectRelicEdit.SetStackCount(relic, relic.StackCount - 1);
				}
				InspectRelicEdit.Refresh();
			};
			hboxContainer.AddChild(button2, false, Node.InternalMode.Disabled);
			Label label3 = new Label();
			Label label4 = label3;
			DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(0, 1);
			defaultInterpolatedStringHandler.AppendFormatted<int>(relic.StackCount);
			label4.Text = defaultInterpolatedStringHandler.ToStringAndClear();
			label3.CustomMinimumSize = new Vector2(32f, 0f);
			label3.AddThemeFontSizeOverride("font_size", 12);
			label3.AddThemeColorOverride("font_color", StsColors.gold);
			label3.HorizontalAlignment = HorizontalAlignment.Center;
			hboxContainer.AddChild(label3, false, Node.InternalMode.Disabled);
			Button button3 = LoadoutPanel.CreateActionButton("+", new Color?(StsColors.green));
			button3.CustomMinimumSize = new Vector2(26f, 22f);
			button3.Pressed += delegate
			{
				InspectRelicEdit.SetStackCount(relic, relic.StackCount + 1);
				InspectRelicEdit.Refresh();
			};
			hboxContainer.AddChild(button3, false, Node.InternalMode.Disabled);
			HFlowContainer hflowContainer2 = new HFlowContainer();
			hflowContainer2.AddThemeConstantOverride("h_separation", 3);
			hflowContainer2.AddThemeConstantOverride("v_separation", 3);
			container.AddChild(hflowContainer2, false, Node.InternalMode.Disabled);
			Button button4 = LoadoutPanel.CreateToggleButton(Loc.Get("inspect.is_wax", null), relic.IsWax);
			button4.CustomMinimumSize = new Vector2(50f, 24f);
			button4.Pressed += delegate
			{
				try
				{
					relic.IsWax = !relic.IsWax;
				}
				catch (Exception ex2)
				{
					GD.PrintErr("[InspectRelicEdit] IsWax: " + ex2.Message);
				}
				InspectRelicEdit.Refresh();
			};
			hflowContainer2.AddChild(button4, false, Node.InternalMode.Disabled);
			Button button5 = LoadoutPanel.CreateToggleButton(Loc.Get("inspect.is_melted", null), relic.IsMelted);
			button5.CustomMinimumSize = new Vector2(50f, 24f);
			button5.Pressed += delegate
			{
				try
				{
					relic.IsMelted = !relic.IsMelted;
				}
				catch (Exception ex3)
				{
					GD.PrintErr("[InspectRelicEdit] IsMelted: " + ex3.Message);
				}
				InspectRelicEdit.Refresh();
			};
			hflowContainer2.AddChild(button5, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000054 RID: 84 RVA: 0x000060E6 File Offset: 0x000042E6
		private static void SetStackCount(RelicModel relic, int value)
		{
			if (InspectRelicEdit.StackCountBackingField != null)
			{
				InspectRelicEdit.StackCountBackingField.SetValue(relic, Math.Max(0, value));
			}
		}

		// Token: 0x06000055 RID: 85 RVA: 0x0000610C File Offset: 0x0000430C
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

		// Token: 0x06000056 RID: 86 RVA: 0x0000612E File Offset: 0x0000432E
		private static Label CreateSectionLabel(string text)
		{
			Label label = new Label();
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 14);
			label.AddThemeColorOverride("font_color", StsColors.gold);
			return label;
		}

		// Token: 0x06000057 RID: 87 RVA: 0x00006164 File Offset: 0x00004364
		private static void AddDivider(VBoxContainer container)
		{
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 1f),
				Color = new Color(0.91f, 0.86f, 0.75f, 0.1f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000058 RID: 88 RVA: 0x000061C0 File Offset: 0x000043C0
		private static void DelayedRefresh()
		{
			if (InspectRelicEdit._panel == null || !GodotObject.IsInstanceValid(InspectRelicEdit._panel))
			{
				return;
			}
			SceneTree tree = InspectRelicEdit._panel.GetTree();
			SceneTreeTimer sceneTreeTimer = ((tree != null) ? tree.CreateTimer(0.2, true, false, false) : null);
			if (sceneTreeTimer != null)
			{
				sceneTreeTimer.Timeout += delegate
				{
					InspectRelicEdit.Refresh();
				};
			}
		}

		// Token: 0x0400001F RID: 31
		[Nullable(2)]
		private static NInspectRelicScreen _screen;

		// Token: 0x04000020 RID: 32
		[Nullable(2)]
		private static PanelContainer _panel;

		// Token: 0x04000021 RID: 33
		private static bool _panelWasHidden;

		// Token: 0x04000022 RID: 34
		[Nullable(2)]
		private static readonly FieldInfo RelicsField = AccessTools.Field(typeof(NInspectRelicScreen), "_relics");

		// Token: 0x04000023 RID: 35
		[Nullable(2)]
		private static readonly FieldInfo IndexField = AccessTools.Field(typeof(NInspectRelicScreen), "_index");

		// Token: 0x04000024 RID: 36
		[Nullable(2)]
		private static readonly FieldInfo StackCountBackingField = AccessTools.Field(typeof(RelicModel), "<StackCount>k__BackingField");

		// Token: 0x0200004C RID: 76
		[CompilerGenerated]
		private static class <>O
		{
			// Token: 0x040000AF RID: 175
			[Nullable(0)]
			public static Action <0>__BuildPanel;
		}
	}
}
