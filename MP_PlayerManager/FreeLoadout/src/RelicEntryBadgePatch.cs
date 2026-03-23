using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;

namespace MP_PlayerManager
{
	// Token: 0x02000005 RID: 5
	[HarmonyPatch(typeof(NRelicCollectionEntry), "_Ready")]
	internal static class RelicEntryBadgePatch
	{
		// Token: 0x06000012 RID: 18 RVA: 0x000028A8 File Offset: 0x00000AA8
		private static void Postfix(NRelicCollectionEntry __instance)
		{
			if (!LoadoutPanel.IsEmbeddedScreenActive)
			{
				return;
			}
			if (__instance.ModelVisibility != ModelVisibility.Visible)
			{
				return;
			}
			Player player = LoadoutPanel.GetPlayer();
			if (player == null)
			{
				return;
			}
			int count = player.Relics.Count((RelicModel r) => r.Id == __instance.relic.Id);
			if (count > 0)
			{
				Callable.From(delegate
				{
					RelicEntryBadgePatch.AddBadge(__instance, count);
				}).CallDeferred(Array.Empty<Variant>());
			}
			NRelicCollectionEntry captured = __instance;
			captured.GuiInput += delegate(InputEvent ev)
			{
				if (!LoadoutPanel.IsEmbeddedScreenActive)
				{
					return;
				}
				InputEventMouseButton inputEventMouseButton = ev as InputEventMouseButton;
				if (inputEventMouseButton == null || !inputEventMouseButton.Pressed || inputEventMouseButton.ButtonIndex != MouseButton.Right)
				{
					return;
				}
				Viewport viewport = captured.GetViewport();
				if (viewport != null)
				{
					viewport.SetInputAsHandled();
				}
				Player player2 = LoadoutPanel.GetPlayer();
				if (player2 == null)
				{
					return;
				}
				RelicModel relicById = player2.GetRelicById(captured.relic.Id);
				if (relicById != null)
				{
					TaskHelper.RunSafely(RelicCmd.Remove(relicById));
				}
			};
		}

		// Token: 0x06000013 RID: 19 RVA: 0x0000294C File Offset: 0x00000B4C
		private static void AddBadge(NRelicCollectionEntry entry, int count)
		{
			if (!GodotObject.IsInstanceValid(entry))
			{
				return;
			}
			Label label = new Label();
			label.Name = "TrainerCountBadge";
			label.Text = count.ToString();
			label.AddThemeFontSizeOverride("font_size", 11);
			label.AddThemeColorOverride("font_color", Colors.White);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.VerticalAlignment = VerticalAlignment.Center;
			label.CustomMinimumSize = new Vector2(20f, 20f);
			label.ZIndex = 10;
			label.MouseFilter = Control.MouseFilterEnum.Ignore;
			StyleBoxFlat styleBoxFlat = new StyleBoxFlat();
			styleBoxFlat.BgColor = new Color(0.85f, 0.15f, 0.1f, 0.95f);
			styleBoxFlat.SetCornerRadiusAll(10);
			styleBoxFlat.SetContentMarginAll(2f);
			label.AddThemeStyleboxOverride("normal", styleBoxFlat);
			label.AnchorLeft = 1f;
			label.AnchorTop = 0f;
			label.OffsetLeft = -24f;
			label.OffsetTop = 2f;
			label.OffsetRight = -2f;
			label.OffsetBottom = 22f;
			entry.AddChild(label, false, Node.InternalMode.Disabled);
		}
	}
}
