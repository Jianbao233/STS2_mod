using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace MP_PlayerManager.Tabs
{
	// Token: 0x0200002C RID: 44
	[NullableContext(1)]
	[Nullable(0)]
	internal static class PotionsTab
	{
		// Token: 0x06000111 RID: 273 RVA: 0x0000D1A8 File Offset: 0x0000B3A8
		internal static void Build(VBoxContainer container, Player player)
		{
			PotionsTab.BuildCurrentPotions(container, player);
			container.AddChild(new ColorRect
			{
				CustomMinimumSize = new Vector2(0f, 2f),
				Color = new Color(0.91f, 0.86f, 0.75f, 0.15f),
				MouseFilter = Control.MouseFilterEnum.Ignore
			}, false, Node.InternalMode.Disabled);
			container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Get("potions.add_header", null)), false, Node.InternalMode.Disabled);
			PotionsTab.BuildAllPotions(container, player);
		}

		// Token: 0x06000112 RID: 274 RVA: 0x0000D228 File Offset: 0x0000B428
		private static void BuildCurrentPotions(VBoxContainer container, Player player)
		{
			IReadOnlyList<PotionModel> potionSlots = player.PotionSlots;
			int num = potionSlots.Count((PotionModel p) => p != null);
			container.AddChild(LoadoutPanel.CreateSectionHeader(Loc.Fmt("potions.current", new object[] { num, player.MaxPotionCount })), false, Node.InternalMode.Disabled);
			if (num == 0)
			{
				Label label = new Label();
				label.Text = "  " + Loc.Get("potions.no_potions", null);
				label.AddThemeFontSizeOverride("font_size", 14);
				label.AddThemeColorOverride("font_color", StsColors.gray);
				container.AddChild(label, false, Node.InternalMode.Disabled);
			}
			else
			{
				FlowContainer flowContainer = new FlowContainer();
				flowContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				flowContainer.AddThemeConstantOverride("h_separation", 6);
				flowContainer.AddThemeConstantOverride("v_separation", 6);
				container.AddChild(flowContainer, false, Node.InternalMode.Disabled);
				for (int i = 0; i < potionSlots.Count; i++)
				{
					PotionModel potionModel = potionSlots[i];
					if (potionModel != null)
					{
						PotionsTab.AddPotionEntry(flowContainer, player, potionModel, true, null);
					}
				}
			}
			HBoxContainer hboxContainer = new HBoxContainer();
			hboxContainer.AddThemeConstantOverride("separation", 6);
			container.AddChild(hboxContainer, false, Node.InternalMode.Disabled);
			Label label2 = new Label();
			label2.Text = Loc.Fmt("potions.slots", new object[] { player.MaxPotionCount });
			label2.AddThemeFontSizeOverride("font_size", 14);
			label2.AddThemeColorOverride("font_color", StsColors.cream);
			label2.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
			hboxContainer.AddChild(label2, false, Node.InternalMode.Disabled);
			Button addSlotBtn = LoadoutPanel.CreateActionButton("+1", new Color?(StsColors.green));
			addSlotBtn.CustomMinimumSize = new Vector2(36f, 28f);
			addSlotBtn.Pressed += delegate
			{
				player.AddToMaxPotionCount(1);
				PotionsTab.DelayedRefresh(addSlotBtn);
			};
			hboxContainer.AddChild(addSlotBtn, false, Node.InternalMode.Disabled);
			Button removeSlotBtn = LoadoutPanel.CreateActionButton("-1", new Color?(StsColors.red));
			removeSlotBtn.CustomMinimumSize = new Vector2(36f, 28f);
			removeSlotBtn.Pressed += delegate
			{
				if (player.MaxPotionCount > 0)
				{
					player.SubtractFromMaxPotionCount(1);
				}
				PotionsTab.DelayedRefresh(removeSlotBtn);
			};
			hboxContainer.AddChild(removeSlotBtn, false, Node.InternalMode.Disabled);
		}

		// Token: 0x06000113 RID: 275 RVA: 0x0000D4CC File Offset: 0x0000B6CC
		private static void BuildAllPotions(VBoxContainer container, Player player)
		{
			foreach (PotionPoolModel potionPoolModel in ModelDb.AllPotionPools.ToList<PotionPoolModel>())
			{
				List<PotionModel> list = potionPoolModel.AllPotions.ToList<PotionModel>();
				if (list.Count != 0)
				{
					Color color = StsColors.gold;
					string entry = potionPoolModel.Id.Entry;
					bool flag = false;
					foreach (KeyValuePair<string, Color> keyValuePair in PotionsTab.PoolColors)
					{
						if (entry.Contains(keyValuePair.Key, StringComparison.OrdinalIgnoreCase) || potionPoolModel.EnergyColorName.Contains(keyValuePair.Key, StringComparison.OrdinalIgnoreCase))
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
					foreach (IGrouping<PotionRarity, PotionModel> grouping in from p in list
						group p by p.Rarity into g
						orderby g.Key
						select g)
					{
						string text;
						switch (grouping.Key)
						{
						case PotionRarity.Common:
							text = Loc.Get("rarity.common", null);
							break;
						case PotionRarity.Uncommon:
							text = Loc.Get("rarity.uncommon", null);
							break;
						case PotionRarity.Rare:
							text = Loc.Get("rarity.rare", null);
							break;
						case PotionRarity.Event:
							text = Loc.Get("rarity.event", null);
							break;
						case PotionRarity.Token:
							text = Loc.Get("rarity.token", null);
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
						using (IEnumerator<PotionModel> enumerator4 = grouping.OrderBy((PotionModel p) => p.Id.Entry).GetEnumerator())
						{
							while (enumerator4.MoveNext())
							{
								PotionModel potion = enumerator4.Current;
								bool flag2 = player.Potions.Any((PotionModel op) => op.Id == potion.Id);
								PotionsTab.AddPotionEntry(flowContainer, player, potion, flag2, flag ? new Color?(color) : null);
							}
						}
					}
				}
			}
		}

		// Token: 0x06000114 RID: 276 RVA: 0x0000D86C File Offset: 0x0000BA6C
		private static void AddPotionEntry(FlowContainer flow, Player player, PotionModel potion, bool owned, Color? highlightColor)
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
				Texture2D image = potion.Image;
				if (image != null)
				{
					textureRect.Texture = image;
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
			Func<PotionModel, bool> <>9__3;
			Func<PotionModel, bool> <>9__4;
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
					if (!owned)
					{
						try
						{
							TaskHelper.RunSafely(PotionCmd.TryToProcure(potion.ToMutable(), player, -1));
						}
						catch
						{
						}
						PotionsTab.DelayedRefresh(iconPanel);
						return;
					}
					IEnumerable<PotionModel> potions = player.Potions;
					Func<PotionModel, bool> func;
					if ((func = <>9__3) == null)
					{
						func = (<>9__3 = (PotionModel p) => p.Id == potion.Id);
					}
					PotionModel potionModel = potions.FirstOrDefault(func);
					if (potionModel != null)
					{
						PotionEditPanel.Open(potionModel, player);
						return;
					}
				}
				else
				{
					IEnumerable<PotionModel> potions2 = player.Potions;
					Func<PotionModel, bool> func2;
					if ((func2 = <>9__4) == null)
					{
						func2 = (<>9__4 = (PotionModel p) => p.Id == potion.Id);
					}
					PotionModel potionModel2 = potions2.FirstOrDefault(func2);
					if (potionModel2 != null)
					{
						try
						{
							TaskHelper.RunSafely(PotionCmd.Discard(potionModel2));
						}
						catch
						{
						}
						PotionsTab.DelayedRefresh(iconPanel);
					}
				}
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
					IEnumerable<IHoverTip> hoverTips = potion.HoverTips;
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
				text = potion.Title.GetFormattedText();
			}
			catch
			{
				text = potion.Id.Entry;
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

		// Token: 0x06000115 RID: 277 RVA: 0x0000DC2C File Offset: 0x0000BE2C
		private static void DelayedRefresh(Control node)
		{
			node.GetTree().CreateTimer(0.3, true, false, false).Timeout += delegate
			{
				LoadoutPanel.RequestRefresh();
			};
		}

		// Token: 0x06000116 RID: 278 RVA: 0x0000DC6C File Offset: 0x0000BE6C
		// Note: this type is marked as 'beforefieldinit'.
		static PotionsTab()
		{
			Dictionary<string, Color> dictionary = new Dictionary<string, Color>();
			dictionary["ironclad"] = new Color("FF6347");
			dictionary["silent"] = new Color("32CD32");
			dictionary["regent"] = new Color("FFD700");
			dictionary["necrobinder"] = new Color("9E68FF");
			dictionary["defect"] = new Color("87CEEB");
			PotionsTab.PoolColors = dictionary;
		}

		// Token: 0x0400006D RID: 109
		private static readonly Dictionary<string, Color> PoolColors;
	}
}
