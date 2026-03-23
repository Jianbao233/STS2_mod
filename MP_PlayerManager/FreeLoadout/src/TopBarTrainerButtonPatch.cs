using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace MP_PlayerManager
{
	// Token: 0x02000026 RID: 38
	[HarmonyPatch(typeof(NTopBar), "_Ready")]
	internal static class TopBarTrainerButtonPatch
	{
		// Token: 0x060000AE RID: 174 RVA: 0x00007210 File Offset: 0x00005410
		private static void Postfix(NTopBar __instance)
		{
			if (!Config.GetFlag("show_topbar_icon"))
			{
				return;
			}
			try
			{
				Control nodeOrNull = __instance.GetNodeOrNull<Control>("%PauseButton");
				if (nodeOrNull != null)
				{
					Node parent = nodeOrNull.GetParent();
					if (parent != null)
					{
						TextureRect textureRect = new TextureRect();
						textureRect.Texture = GD.Load<Texture2D>("res://images/ui/run_history/treasure.png");
						textureRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
						textureRect.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
						textureRect.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.Minsize, 0);
						Button btn = new Button();
						btn.Flat = true;
						btn.FocusMode = Control.FocusModeEnum.None;
						btn.MouseFilter = Control.MouseFilterEnum.Stop;
						btn.CustomMinimumSize = new Vector2(80f, 80f);
						btn.AddChild(textureRect, false, Node.InternalMode.Disabled);
						btn.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
						btn.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
						btn.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
						btn.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
						btn.Modulate = new Color(0.8f, 0.8f, 0.8f, 1f);
						btn.MouseEntered += delegate
						{
							btn.Modulate = Colors.White;
						};
						btn.MouseExited += delegate
						{
							btn.Modulate = new Color(0.8f, 0.8f, 0.8f, 1f);
						};
						parent.AddChild(btn, false, Node.InternalMode.Disabled);
						parent.MoveChild(btn, nodeOrNull.GetIndex(false));
						btn.Pressed += delegate
						{
							LoadoutPanel.Toggle();
						};
					}
				}
			}
			catch (Exception ex)
			{
				DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(40, 1);
				defaultInterpolatedStringHandler.AppendLiteral("[Trainer] Failed to add top bar button: ");
				defaultInterpolatedStringHandler.AppendFormatted<Exception>(ex);
				GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
			}
		}
	}
}
