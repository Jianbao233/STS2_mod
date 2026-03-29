using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.HoverTips;

namespace MP_PlayerManager.Tabs
{
    internal static class CardsTab
    {
        /// <summary>
        /// 批量模式选卡时，给已选中卡牌的 clip 容器加亮边框。
        /// </summary>
        internal static void ApplyCardSelectedHighlight(Control clip)
        {
            var sb = new StyleBoxFlat
            {
                BgColor = Colors.Transparent,
                BorderColor = new Color(0.2f, 0.7f, 1f, 0.9f)
            };
            sb.SetBorderWidthAll(3);
            sb.SetCornerRadiusAll(4);
            clip.AddThemeStyleboxOverride("normal", sb);
        }

        internal static Control CreateNCardWrapperPublic(CardModel card, PileType pileType, List<CardModel> inspectList, Action onClick = null)
        {
            return CreateNCardWrapper(card, pileType, inspectList, onClick);
        }

        internal static void Build(VBoxContainer container, object player) { }

        private static Control CreateNCardWrapper(CardModel card, PileType pileType, List<CardModel> inspectList, Action onClick = null)
        {
            var clip = new Control
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, NCardMinHeight),
                ClipContents = true,
                MouseFilter = Control.MouseFilterEnum.Stop,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };

            NCard? ncard = null;
            try
            {
                ncard = NCard.Create(card.IsMutable ? card : card.ToMutable());
                if (ncard != null)
                {
                    ncard.Scale = new Vector2(NCardScale, NCardScale);
                    ncard.MouseFilter = Control.MouseFilterEnum.Ignore;
                    clip.AddChild(ncard, false, Node.InternalMode.Disabled);

                    NCard cardRef = ncard;
                    PileType captured = pileType;
                    cardRef.Ready += () =>
                    {
                        if (GodotObject.IsInstanceValid(cardRef))
                            cardRef.UpdateVisuals(captured, CardPreviewMode.Normal);
                    };
                }
            }
            catch { }

            NCard? capturedCard = ncard;
            clip.Resized += () =>
            {
                if (capturedCard != null && GodotObject.IsInstanceValid(capturedCard))
                    capturedCard.Position = new Vector2(clip.Size.X / 2f, clip.Size.Y / 2f);
            };

            clip.MouseEntered += () =>
            {
                try { LoadoutPanel.ShowHoverTips(clip, card.HoverTips, HoverTipAlignment.Left); } catch { }
            };
            clip.MouseExited += () => NHoverTipSet.Remove(clip);

            clip.GuiInput += ev =>
            {
                if (ev is not InputEventMouseButton mb || !mb.Pressed || mb.ButtonIndex != MouseButton.Left) return;
                clip.GetViewport()?.SetInputAsHandled();
                NHoverTipSet.Remove(clip);
                if (onClick != null) { onClick(); return; }
                try
                {
                    var list = inspectList ?? new List<CardModel> { card };
                    NGame.Instance?.GetInspectCardScreen()?.Open(list, list.IndexOf(card), false);
                }
                catch { }
            };

            return clip;
        }

        private const float NCardScale = 0.65f;
        private const float NCardMinHeight = 280f;
    }
}
