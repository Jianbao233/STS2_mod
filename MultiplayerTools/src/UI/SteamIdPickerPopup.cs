using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using MultiplayerTools;
using MultiplayerTools.Panel;

namespace MultiplayerTools.UI
{
    internal static class SteamIdPickerPopup
    {
        internal readonly struct SteamIdOption
        {
            internal SteamIdOption(string steamId, string displayName, string? subText = null)
            {
                SteamId = steamId ?? "";
                DisplayName = displayName ?? "";
                SubText = subText;
            }

            internal string SteamId { get; }
            internal string DisplayName { get; }
            internal string? SubText { get; }
        }

        internal static void Show(
            LineEdit targetEdit,
            IEnumerable<SteamIdOption> roomMembers,
            IEnumerable<SteamIdOption> friends)
        {
            var roomList = roomMembers?.Where(o => !string.IsNullOrWhiteSpace(o.SteamId)).ToList() ?? new List<SteamIdOption>();
            var friendList = friends?.Where(o => !string.IsNullOrWhiteSpace(o.SteamId)).ToList() ?? new List<SteamIdOption>();

            // De-dup: room members should not repeat in friends list
            var roomIds = new HashSet<string>(roomList.Select(r => r.SteamId), StringComparer.OrdinalIgnoreCase);
            friendList = friendList.Where(f => !roomIds.Contains(f.SteamId)).ToList();

            if (roomList.Count == 0 && friendList.Count == 0)
            {
                MpPanel.ShowStatusMessage(Loc.Get("friend.no_contacts", "No Steam contacts found."), Styles.MpTextMuted);
                return;
            }

            const int popupW = 640;
            const int popupH = 500;

            var popup = new PopupPanel
            {
                Exclusive = true,
                Title = Loc.Get("friend.title", "Select Steam ID"),
                MinSize = new Vector2I(popupW, popupH),
                Size = new Vector2I(popupW, popupH)
            };

            // Apply dark background so popup is visible against the game backdrop
            var bgStyle = Panel.Styles.CreateFlat(Panel.Styles.PanelBg, Panel.Styles.PanelBorder, 8, 2);
            popup.AddThemeStyleboxOverride("panel", bgStyle);

            // Use VBox as root with FullRect anchors so it fills the popup client area
            var root = new VBoxContainer();
            root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            root.AddThemeConstantOverride("separation", 8);
            popup.AddChild(root, false, Node.InternalMode.Disabled);

            // Toolbar row inside a MarginContainer for padding
            var toolbarMargin = new MarginContainer();
            toolbarMargin.AddThemeConstantOverride("margin_left", 10);
            toolbarMargin.AddThemeConstantOverride("margin_right", 10);
            toolbarMargin.AddThemeConstantOverride("margin_top", 10);
            toolbarMargin.AddThemeConstantOverride("margin_bottom", 6);
            root.AddChild(toolbarMargin, false, Node.InternalMode.Disabled);

            var toolbar = new HBoxContainer();
            toolbar.AddThemeConstantOverride("separation", 8);
            toolbarMargin.AddChild(toolbar, false, Node.InternalMode.Disabled);

            var searchEdit = new LineEdit
            {
                PlaceholderText = Loc.Get("friend.search_placeholder", "Search name / SteamID..."),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(300, 36)
            };
            searchEdit.AddThemeFontSizeOverride("font_size", 18);
            toolbar.AddChild(searchEdit, false, Node.InternalMode.Disabled);

            var closeBtn = MpPanel.CreateActionButton(Loc.Get("close", "Close"), Styles.MpPrimaryBtn);
            closeBtn.CustomMinimumSize = new Vector2(90, 36);
            closeBtn.Pressed += () => { popup.Hide(); popup.QueueFree(); };
            toolbar.AddChild(closeBtn, false, Node.InternalMode.Disabled);

            // Scroll list with padding
            var scrollMargin = new MarginContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 320)
            };
            scrollMargin.AddThemeConstantOverride("margin_left", 10);
            scrollMargin.AddThemeConstantOverride("margin_right", 10);
            scrollMargin.AddThemeConstantOverride("margin_top", 4);
            scrollMargin.AddThemeConstantOverride("margin_bottom", 10);
            root.AddChild(scrollMargin, false, Node.InternalMode.Disabled);

            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 300),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            scrollMargin.AddChild(scroll, false, Node.InternalMode.Disabled);

            var list = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            list.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(list, false, Node.InternalMode.Disabled);

            // ── Render helpers ────────────────────────────────────────────────────
            void Render(string query)
            {
                MpPanel.ClearChildren(list);
                string q = (query ?? "").Trim();

                bool HasMatch(SteamIdOption o)
                {
                    if (string.IsNullOrEmpty(q)) return true;
                    return (o.DisplayName?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                           || (o.SteamId?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)
                           || (o.SubText?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false);
                }

                var roomFiltered = roomList.Where(HasMatch).ToList();
                var friendsFiltered = friendList.Where(HasMatch).ToList();

                if (roomFiltered.Count > 0)
                    RenderSection(list,
                        Loc.Get("friend.room_members", "Room Players") + $" ({roomFiltered.Count}/{roomList.Count})",
                        roomFiltered, targetEdit, popup);
                else if (roomList.Count > 0)
                    RenderEmptyHint(list, Loc.Get("friend.empty_room", "No other players in room"));

                if (friendsFiltered.Count > 0)
                    RenderSection(list,
                        Loc.Get("friend.friends", "Steam Friends") + $" ({friendsFiltered.Count}/{friendList.Count})",
                        friendsFiltered, targetEdit, popup);
                else if (friendList.Count > 0)
                    RenderEmptyHint(list, Loc.Get("friend.empty_friends", "No Steam friends parsed from local config"));

                if (roomFiltered.Count == 0 && friendsFiltered.Count == 0)
                    RenderEmptyHint(list, Loc.Get("friend.no_match", "No matches"));
            }

            searchEdit.TextChanged += text => Render(text);
            Render("");

            popup.Connect(Popup.SignalName.CloseRequested, new Callable(closeBtn, Button.SignalName.Pressed));
            (Engine.GetMainLoop() as SceneTree)?.Root.AddChild(popup, false, Node.InternalMode.Disabled);
            popup.PopupCentered(new Vector2I(popupW, popupH));
            searchEdit.GrabFocus();
        }

        private static void RenderEmptyHint(VBoxContainer parent, string text)
        {
            var hint = new Label { Text = $"    {text}" };
            hint.AddThemeFontSizeOverride("font_size", 16);
            hint.AddThemeColorOverride("font_color", Styles.MpTextMuted);
            parent.AddChild(hint, false, Node.InternalMode.Disabled);
        }

        private static void RenderSection(
            VBoxContainer parent,
            string title,
            List<SteamIdOption> items,
            LineEdit targetEdit,
            Popup popup)
        {
            var header = new Label { Text = title };
            header.AddThemeFontSizeOverride("font_size", 16);
            header.AddThemeColorOverride("font_color", Styles.MpGray);
            parent.AddChild(header, false, Node.InternalMode.Disabled);

            foreach (var opt in items.Take(80))
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddThemeConstantOverride("separation", 10);
                parent.AddChild(row, false, Node.InternalMode.Disabled);

                var textCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                textCol.AddThemeConstantOverride("separation", 2);
                row.AddChild(textCol, false, Node.InternalMode.Disabled);

                // Display name — show persona if available, otherwise just show shortened ID
                string displayText = !string.IsNullOrEmpty(opt.DisplayName) && opt.DisplayName != opt.SteamId
                    ? opt.DisplayName
                    : MpSessionState.ShortenSteamId(opt.SteamId);
                var nameLbl = new Label { Text = displayText, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                nameLbl.AddThemeFontSizeOverride("font_size", 18);
                nameLbl.AddThemeColorOverride("font_color", Styles.MpTextNav);
                textCol.AddChild(nameLbl, false, Node.InternalMode.Disabled);

                // Sub text: always show the Steam ID (shortened) as context
                string subDisplay = !string.IsNullOrEmpty(opt.SubText)
                    ? opt.SubText
                    : MpSessionState.ShortenSteamId(opt.SteamId);
                var subLbl = new Label { Text = subDisplay, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                subLbl.AddThemeFontSizeOverride("font_size", 15);
                subLbl.AddThemeColorOverride("font_color", Styles.MpTextMuted);
                textCol.AddChild(subLbl, false, Node.InternalMode.Disabled);

                // Select button
                var selBtn = new Button { Text = Loc.Get("friend.select", "Select") };
                selBtn.CustomMinimumSize = new Vector2(70, 30);
                string capId = opt.SteamId;
                selBtn.Pressed += () =>
                {
                    targetEdit.Text = capId;
                    popup.Hide();
                    popup.QueueFree();
                };
                row.AddChild(selBtn, false, Node.InternalMode.Disabled);
            }
        }
    }
}
