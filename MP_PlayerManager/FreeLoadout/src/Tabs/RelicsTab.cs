using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace MP_PlayerManager.Tabs
{
	// Token: 0x0200002B RID: 43
	[NullableContext(1)]
	[Nullable(0)]
	internal static class RelicsTab
	{
		// Token: 0x0600010E RID: 270 RVA: 0x0000C9A0 File Offset: 0x0000ABA0
		internal static void Build(VBoxContainer container, Player player)
		{
			List<RelicPoolModel> list = ModelDb.AllRelicPools.ToList<RelicPoolModel>();
			HashSet<ModelId> hashSet = player.Relics.Select((RelicModel r) => r.Id).ToHashSet<ModelId>();
			foreach (RelicPoolModel relicPoolModel in list)
			{
				List<RelicModel> list2 = relicPoolModel.AllRelics.ToList<RelicModel>();
				if (list2.Count != 0)
				{
					Color color = StsColors.gold;
					string entry = relicPoolModel.Id.Entry;
					bool flag = false;
					foreach (KeyValuePair<string, Color> keyValuePair in RelicsTab.PoolColors)
					{
						if (entry.Contains(keyValuePair.Key, StringComparison.OrdinalIgnoreCase) || relicPoolModel.EnergyColorName.Contains(keyValuePair.Key, StringComparison.OrdinalIgnoreCase))
						{
							color = keyValuePair.Value;
							flag = true;
							break;
						}
					}
					Label label = LoadoutPanel.CreateSectionHeader(entry ?? "");
					if (flag)
					{
						label.AddThemeColorOverride("font_color", color);
					}
					container.AddChild(label, false, Node.InternalMode.Disabled);
					foreach (IGrouping<RelicRarity, RelicModel> grouping in from r in list2
						group r by r.Rarity into g
						orderby g.Key
						select g)
					{
						string text;
						switch (grouping.Key)
						{
						case RelicRarity.Starter:
							text = Loc.Get("rarity.starter", null);
							break;
						case RelicRarity.Common:
							text = Loc.Get("rarity.common", null);
							break;
						case RelicRarity.Uncommon:
							text = Loc.Get("rarity.uncommon", null);
							break;
						case RelicRarity.Rare:
							text = Loc.Get("rarity.rare", null);
							break;
						case RelicRarity.Shop:
							text = Loc.Get("rarity.shop", null);
							break;
						case RelicRarity.Event:
							text = Loc.Get("rarity.event", null);
							break;
						case RelicRarity.Ancient:
							text = Loc.Get("rarity.ancient", null);
							break;
						default:
							text = grouping.Key.ToString();
							break;
						}
						string text2 = text;
						Label label2 = new Label();
						label2.Text = "  " + text2;
						label2.AddThemeFontSizeOverride("font_size", 15);
						label2.AddThemeColorOverride("font_color", StsColors.halfTransparentCream);
						container.AddChild(label2, false, Node.InternalMode.Disabled);
						FlowContainer flowContainer = new FlowContainer();
						flowContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
						flowContainer.AddThemeConstantOverride("h_separation", 6);
						flowContainer.AddThemeConstantOverride("v_separation", 6);
						container.AddChild(flowContainer, false, Node.InternalMode.Disabled);
						foreach (RelicModel relicModel in grouping.OrderBy((RelicModel r) => r.Id.Entry))
						{
							bool flag2 = hashSet.Contains(relicModel.Id);
							RelicsTab.AddRelicEntry(flowContainer, player, relicModel, flag2, flag ? new Color?(color) : null);
						}
					}
				}
			}
		}

		// Token: 0x0600010F RID: 271 RVA: 0x0000CD7C File Offset: 0x0000AF7C
		private static void AddRelicEntry(FlowContainer flow, Player player, RelicModel relic, bool owned, Color? highlightColor)
		{
			VBoxContainer vboxContainer = new VBoxContainer();
			vboxContainer.CustomMinimumSize = new Vector2(72f, 0f);
			vboxContainer.AddThemeConstantOverride("separation", 2);
			TextureRect textureRect = new TextureRect();
			textureRect.CustomMinimumSize = new Vector2(64f, 64f);
			textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
			textureRect.MouseFilter = Control.MouseFilterEnum.Ignore;
			try
			{
				Texture2D bigIcon = relic.BigIcon;
				if (bigIcon != null)
				{
					textureRect.Texture = bigIcon;
				}
			}
			catch
			{
			}
			PanelContainer iconPanel = new PanelContainer();
			iconPanel.CustomMinimumSize = new Vector2(68f, 68f);
			iconPanel.MouseFilter = Control.MouseFilterEnum.Stop;
			iconPanel.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			StyleBoxFlat iconStyle = new StyleBoxFlat();
			iconStyle.SetCornerRadiusAll(6);
			iconStyle.SetContentMarginAll(2f);
			if (owned)
			{
				iconStyle.BgColor = new Color(0.1f, 0.25f, 0.1f, 0.9f);
				iconStyle.BorderColor = new Color(0.3f, 0.7f, 0.3f, 0.7f);
				iconStyle.SetBorderWidthAll(2);
			}
			else
			{
				iconStyle.BgColor = new Color(0.12f, 0.1f, 0.15f, 0.8f);
				iconStyle.BorderColor = new Color(0.35f, 0.3f, 0.25f, 0.4f);
				iconStyle.SetBorderWidthAll(1);
			}
			iconPanel.AddThemeStyleboxOverride("panel", iconStyle);
			iconPanel.AddChild(textureRect, false, Node.InternalMode.Disabled);
			iconPanel.GuiInput += delegate(InputEvent ev)
			{
				InputEventMouseButton inputEventMouseButton = ev as InputEventMouseButton;
				if (inputEventMouseButton == null || !inputEventMouseButton.Pressed)
				{
					return;
				}
				if (inputEventMouseButton.ButtonIndex != MouseButton.Left && inputEventMouseButton.ButtonIndex != MouseButton.Right)
				{
					return;
				}
				Viewport viewport = iconPanel.GetViewport();
				if (viewport != null)
				{
					viewport.SetInputAsHandled();
				}
				if (inputEventMouseButton.ButtonIndex == MouseButton.Left)
				{
					int relicBatchCount = LoadoutPanel.RelicBatchCount;
					for (int i = 0; i < relicBatchCount; i++)
					{
						TaskHelper.RunSafely(RelicCmd.Obtain(relic.ToMutable(), player, -1));
					}
				}
				else
				{
					RelicModel relicById = player.GetRelicById(relic.Id);
					if (relicById != null)
					{
						TaskHelper.RunSafely(RelicCmd.Remove(relicById));
					}
				}
				iconPanel.GetTree().CreateTimer(0.3, true, false, false).Timeout += delegate
				{
					LoadoutPanel.RequestRefresh();
				};
			};
			StyleBoxFlat hoverStyle = new StyleBoxFlat();
			hoverStyle.BgColor = (owned ? new Color(0.15f, 0.35f, 0.15f, 0.95f) : new Color(0.18f, 0.15f, 0.22f, 0.92f));
			hoverStyle.BorderColor = StsColors.gold;
			hoverStyle.SetBorderWidthAll(2);
			hoverStyle.SetCornerRadiusAll(6);
			hoverStyle.SetContentMarginAll(2f);
			iconPanel.MouseEntered += delegate
			{
				iconPanel.AddThemeStyleboxOverride("panel", hoverStyle);
				try
				{
					IEnumerable<IHoverTip> hoverTips = relic.HoverTips;
					if (hoverTips != null)
					{
						LoadoutPanel.ShowHoverTips(iconPanel, hoverTips, HoverTipAlignment.Right);
					}
				}
				catch
				{
				}
			};
			iconPanel.MouseExited += delegate
			{
				iconPanel.AddThemeStyleboxOverride("panel", iconStyle);
				NHoverTipSet.Remove(iconPanel);
			};
			vboxContainer.AddChild(iconPanel, false, Node.InternalMode.Disabled);
			string text;
			try
			{
				text = relic.Title.GetFormattedText();
			}
			catch
			{
				text = relic.Id.Entry;
			}
			Label label = new Label();
			label.Text = text;
			label.AddThemeFontSizeOverride("font_size", 10);
			label.HorizontalAlignment = HorizontalAlignment.Center;
			label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
			label.CustomMinimumSize = new Vector2(72f, 0f);
			if (owned)
			{
				label.AddThemeColorOverride("font_color", StsColors.green);
			}
			else if (highlightColor != null)
			{
				label.AddThemeColorOverride("font_color", highlightColor.Value);
			}
			else
			{
				label.AddThemeColorOverride("font_color", StsColors.cream);
			}
			vboxContainer.AddChild(label, false, Node.InternalMode.Disabled);
			flow.AddChild(vboxContainer, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000110 RID: 272 RVA: 0x0000D128 File Offset: 0x0000B328
		// Note: this type is marked as 'beforefieldinit'.
		static RelicsTab()
		{
			Dictionary<string, Color> dictionary = new Dictionary<string, Color>();
			dictionary["ironclad"] = new Color("FF6347");
			dictionary["silent"] = new Color("32CD32");
			dictionary["regent"] = new Color("FFD700");
			dictionary["necrobinder"] = new Color("9E68FF");
			dictionary["defect"] = new Color("87CEEB");
			RelicsTab.PoolColors = dictionary;
		}

		// Token: 0x0400006C RID: 108
		private static readonly Dictionary<string, Color> PoolColors;
	}
}
