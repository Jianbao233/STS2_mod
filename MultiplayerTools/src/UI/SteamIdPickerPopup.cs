using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

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
                MpPanel.ShowStatusMessage(Loc.Get("friend.no_contacts", "No Steam contacts found."), Panel.Styles.MpTextMuted);
                return;
            }

            var popup = new Popup { Exclusive = true, Title = Loc.Get("friend.title", "Select Steam ID") };

            var root = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            root.AddThemeConstantOverride("separation", 8);
            popup.AddChild(root, false, Node.InternalMode.Disabled);

            // Search row
            var searchRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            searchRow.AddThemeConstantOverride("separation", 8);
            root.AddChild(searchRow, false, Node.InternalMode.Disabled);

            var searchEdit = new LineEdit
            {
                PlaceholderText = Loc.Get("friend.search_placeholder", "Search name / SteamID..."),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(280, 32)
            };
            searchEdit.AddThemeFontSizeOverride("font_size", 18);
            searchRow.AddChild(searchEdit, false, Node.InternalMode.Disabled);

            var closeBtn = MpPanel.CreateActionButton(Loc.Get("close", "Close"), Panel.Styles.MpPrimaryBtn);
            closeBtn.CustomMinimumSize = new Vector2(90, 32);
            closeBtn.Pressed += () => { popup.Hide(); popup.QueueFree(); };
            searchRow.AddChild(closeBtn, false, Node.InternalMode.Disabled);

            // Scroll list
            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(520, 420),
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            root.AddChild(scroll, false, Node.InternalMode.Disabled);

            var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            list.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(list, false, Node.InternalMode.Disabled);

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
                    RenderSection(list, Loc.Get("friend.room_members", "Players in Room"), roomFiltered, targetEdit, popup);

                if (friendsFiltered.Count > 0)
                    RenderSection(list, Loc.Get("friend.friends", "Steam Friends"), friendsFiltered, targetEdit, popup);

                if (roomFiltered.Count == 0 && friendsFiltered.Count == 0)
                {
                    var empty = new Label { Text = Loc.Get("friend.no_match", "No matches."), HorizontalAlignment = HorizontalAlignment.Center };
                    empty.AddThemeFontSizeOverride("font_size", 16);
                    empty.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
                    list.AddChild(empty, false, Node.InternalMode.Disabled);
                }
            }

            searchEdit.TextChanged += text => Render(text);
            Render("");

            popup.Connect(Window.SignalName.CloseRequested, new Callable(closeBtn, Button.SignalName.Pressed));
            (Engine.GetMainLoop() as SceneTree)?.Root.AddChild(popup, false, Node.InternalMode.Disabled);
            popup.PopupCentered();
            searchEdit.GrabFocus();
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
            header.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
            parent.AddChild(header, false, Node.InternalMode.Disabled);

            foreach (var opt in items.Take(80))
            {
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddThemeConstantOverride("separation", 10);
                parent.AddChild(row, false, Node.InternalMode.Disabled);

                var textCol = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                textCol.AddThemeConstantOverride("separation", 2);
                row.AddChild(textCol, false, Node.InternalMode.Disabled);

                var nameLbl = new Label { Text = opt.DisplayName, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                nameLbl.AddThemeFontSizeOverride("font_size", 18);
                nameLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextNav);
                textCol.AddChild(nameLbl, false, Node.InternalMode.Disabled);

                var sub = opt.SubText;
                if (!string.IsNullOrEmpty(sub))
                {
                    var subLbl = new Label { Text = sub, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    subLbl.AddThemeFontSizeOverride("font_size", 15);
                    subLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
                    textCol.AddChild(subLbl, false, Node.InternalMode.Disabled);
                }

                var idLbl = new Label { Text = opt.SteamId };
                idLbl.AddThemeFontSizeOverride("font_size", 16);
                idLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
                row.AddChild(idLbl, false, Node.InternalMode.Disabled);

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

