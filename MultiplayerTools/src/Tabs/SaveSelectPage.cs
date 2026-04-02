using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Save selection page — mirrors v2 _page_save_select + _render_user_group + _render_save_card.
    ///
    /// Layout:
    ///   [title] → section header
    ///   → scrollable list of SteamUserGroup (collapsible)
    ///     → group header (Steam ID short + persona + active badge + count)
    ///       → save cards (profile key + mod/standard tag + player count + act + ascension + status + time)
    ///         → click card → MpSessionState.LoadSave → MpPanel.SwitchPage("takeover")
    ///   → rescan button
    ///
    /// Data source: MpSessionState.AllProfiles filtered to multiplayer runs only, grouped by Steam ID.
    /// 仅显示存在 current_run_mp.save 且存档内 ≥2 名玩家的档位（与 MP v2 列表一致；单人档与空档位不显示）。
    /// </summary>
    internal static class SaveSelectPage
    {
        // Tracks collapse state: steamId → isCollapsed
        private static Dictionary<string, bool> _collapsed = new();

        internal static void Build(VBoxContainer container)
        {
            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("saveselect.title", "Select Save")), false, Node.InternalMode.Disabled);

            var allProfiles = MpSessionState.AllProfiles;
            var profiles = allProfiles.Where(SaveManagerHelper.IsMultiplayerRunProfile).ToList();
            GD.Print($"[MultiplayerTools] SaveSelect v{ModInfo.Version}: scanned={allProfiles.Count}, multiplayer_only={profiles.Count}");

            if (profiles.Count == 0)
            {
                string emptyMsg = allProfiles.Count == 0
                    ? Loc.Get("saveselect.not_found", "No saves found. Try rescanning.")
                    : Loc.Get("saveselect.not_found_mp", "No multiplayer saves (2+ players) found. Try rescanning.");
                var notFound = new Label
                {
                    Text = emptyMsg,
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill
                };
                notFound.AddThemeFontSizeOverride("font_size", 18);
                notFound.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
                container.AddChild(notFound, false, Node.InternalMode.Disabled);
            }
            else
            {
                // Group profiles by Steam ID (mirrors Python v2 _build_user_groups)
                var groups = BuildSteamUserGroups(profiles);

                var scroll = new ScrollContainer
                {
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
                container.AddChild(scroll, false, Node.InternalMode.Disabled);

                var scrollContent = new VBoxContainer
                {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
                };
                scrollContent.AddThemeConstantOverride("separation", 6);
                scroll.AddChild(scrollContent, false, Node.InternalMode.Disabled);

                foreach (var group in groups)
                    RenderSteamUserGroup(scrollContent, group);
            }

            // Rescan button
            var rescanBtn = MpPanel.CreateActionButton(
                Loc.Get("saveselect.rescan", "Rescan"), Panel.Styles.MpPrimaryBtn);
            rescanBtn.CustomMinimumSize = new Vector2(120, 34);
            rescanBtn.Pressed += () =>
            {
                MpSessionState.RefreshProfiles();
            };
            container.AddChild(rescanBtn, false, Node.InternalMode.Disabled);
        }

        // ── Steam user grouping ──────────────────────────────────────────────────

        private static List<SteamUserGroupData> BuildSteamUserGroups(List<SaveProfile> profiles)
        {
            var groupsMap = new Dictionary<string, SteamUserGroupData>(StringComparer.OrdinalIgnoreCase);

            foreach (var prof in profiles)
            {
                var sid = prof.SteamId;
                if (!groupsMap.TryGetValue(sid, out var group))
                {
                    string persona = TryGetSteamPersona(sid);
                    group = new SteamUserGroupData
                    {
                        SteamId = sid,
                        SteamIdShort = MpSessionState.ShortenSteamId(sid),
                        Persona = persona,
                        Profiles = new List<SaveProfile>()
                    };
                    groupsMap[sid] = group;
                }
                group.Profiles.Add(prof);
            }

            // Sort: has persona name first, then by steamId (Python v2 key = (not bool(persona), steam_id))
            var groups = groupsMap.Values.ToList();
            groups.Sort((a, b) =>
            {
                bool aHasPersona = !string.IsNullOrEmpty(a.Persona);
                bool bHasPersona = !string.IsNullOrEmpty(b.Persona);
                int cmp = bHasPersona.CompareTo(aHasPersona);
                if (cmp != 0) return cmp;
                return string.Compare(a.SteamId, b.SteamId, StringComparison.Ordinal);
            });

            // Within each group, sort profiles by file mtime descending (Python v2: sort by path.stat().st_mtime, reverse=True)
            foreach (var g in groupsMap.Values)
            {
                g.Profiles.Sort((p1, p2) =>
                {
                    try
                    {
                        var fp1 = new FileInfo(p1.SavePath);
                        var fp2 = new FileInfo(p2.SavePath);
                        return DateTime.Compare(fp2.LastWriteTime, fp1.LastWriteTime);
                    }
                    catch { return 0; }
                });
            }

            return groups;
        }

        private static string TryGetSteamPersona(string steamId)
        {
            try
            {
                // Try to read from steam config (loginusers.vdf already handled by SteamIntegration)
                var name = Steam.SteamIntegration.GetPersonaName(steamId);
                if (!string.IsNullOrEmpty(name) && name != steamId)
                    return name;
            }
            catch { }
            return "";
        }

        private static (string steamId, string persona) GetCurrentUserInfo()
        {
            try
            {
                var me = Steam.SteamIntegration.GetCurrentSteamId();
                if (!string.IsNullOrEmpty(me))
                {
                    var name = Steam.SteamIntegration.GetPersonaName(me);
                    if (string.IsNullOrEmpty(name) || name == me) name = "";
                    return (me, name);
                }
            }
            catch { }
            return ("", "");
        }

        // ── Group rendering ──────────────────────────────────────────────────────

        private static void RenderSteamUserGroup(VBoxContainer parent, SteamUserGroupData group)
        {
            bool collapsed = _collapsed.TryGetValue(group.SteamId, out var c) && c;

            // ── Group header ──────────────────────────────────────────────────
            var header = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 52),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var headerStyle = new StyleBoxFlat
            {
                BgColor = Panel.Styles.MpCard,
                BorderColor = Panel.Styles.PanelBorder,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            headerStyle.SetBorderWidthAll(0);
            headerStyle.SetCornerRadiusAll(8);
            header.AddThemeStyleboxOverride("panel", headerStyle);
            parent.AddChild(header, false, Node.InternalMode.Disabled);

            var headerMargin = new MarginContainer();
            headerMargin.AddThemeConstantOverride("margin_left", 8);
            headerMargin.AddThemeConstantOverride("margin_right", 8);
            headerMargin.AddThemeConstantOverride("margin_top", 0);
            headerMargin.AddThemeConstantOverride("margin_bottom", 0);
            header.AddChild(headerMargin, false, Node.InternalMode.Disabled);

            var headerH = new HBoxContainer();
            headerH.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            headerH.AddThemeConstantOverride("separation", 8);
            headerMargin.AddChild(headerH, false, Node.InternalMode.Disabled);

            // Arrow
            var arrowLbl = new Label { Text = collapsed ? "▶" : "▼" };
            arrowLbl.CustomMinimumSize = new Vector2(24, 0);
            arrowLbl.AddThemeFontSizeOverride("font_size", 19);
            arrowLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            headerH.AddChild(arrowLbl, false, Node.InternalMode.Disabled);

            // Steam ID short + persona merged: "76561...3594 · 昵称" or just "76561...3594"
            // If this is the current user's group, add "（我）" suffix
            var (myId, myPersona) = GetCurrentUserInfo();
            bool isMe = !string.IsNullOrEmpty(myId) && myId == group.SteamId;
            string headerIdText;
            Color idColor;
            if (!string.IsNullOrEmpty(group.Persona))
            {
                string meSuffix = isMe ? $"  {Loc.Get("saveselect.me", "（我）")}" : "";
                headerIdText = $"{group.SteamIdShort}  ·  {group.Persona}{meSuffix}";
                idColor = Panel.Styles.MpTextNav;
            }
            else
            {
                string meSuffix = isMe ? $"  {Loc.Get("saveselect.me", "（我）")}" : "";
                headerIdText = group.SteamIdShort + meSuffix;
                idColor = Panel.Styles.MpTextMuted;
            }
            var idLbl = new Label { Text = headerIdText };
            idLbl.AddThemeFontSizeOverride("font_size", 18);
            idLbl.AddThemeColorOverride("font_color", idColor);
            headerH.AddChild(idLbl, false, Node.InternalMode.Disabled);

            headerH.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }, false, Node.InternalMode.Disabled);

            // Active badge (if any profile is active — mirrors Python v2: sum(1 for p in group.profiles if p.is_active))
            int activeN = 0;
            foreach (var p in group.Profiles)
            {
                if (p.IsActive) activeN++;
            }
            if (activeN > 0)
            {
                var badge = new Label
                {
                    Text = Loc.Fmt("saveselect.active", activeN),
                    CustomMinimumSize = new Vector2(80, 22)
                };
                badge.AddThemeFontSizeOverride("font_size", 15);
                badge.AddThemeColorOverride("font_color", new Godot.Color("#1A1A2E"));
                badge.AddThemeColorOverride("font_outline_color", Panel.Styles.OutlineColor);
                badge.AddThemeConstantOverride("outline_size", 1);
                var badgeStyle = new StyleBoxFlat
                {
                    BgColor = Panel.Styles.MpGold,
                    BorderColor = Panel.Styles.MpGold,
                    ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
                };
                badgeStyle.SetBorderWidthAll(0);
                badgeStyle.SetCornerRadiusAll(10);
                badge.AddThemeStyleboxOverride("normal", badgeStyle);
                headerH.AddChild(badge, false, Node.InternalMode.Disabled);
            }

            // Profile count
            var countLbl = new Label { Text = Loc.Fmt("saveselect.profile_count", group.Profiles.Count) };
            countLbl.AddThemeFontSizeOverride("font_size", 16);
            countLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
            headerH.AddChild(countLbl, false, Node.InternalMode.Disabled);

            // ── Cards container (shown when expanded) ──────────────────────────
            var cardsContainer = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            cardsContainer.AddThemeConstantOverride("separation", 4);
            parent.AddChild(cardsContainer, false, Node.InternalMode.Disabled);

            // Wire up toggle
            string steamId = group.SteamId;
            Action toggle = () =>
            {
                bool nowCollapsed = !(_collapsed.TryGetValue(steamId, out var cv) && cv);
                _collapsed[steamId] = nowCollapsed;
                arrowLbl.Text = nowCollapsed ? "▶" : "▼";
                cardsContainer.Visible = !nowCollapsed;
            };

            // Make header clickable
            header.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    toggle();
            };
            arrowLbl.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    toggle();
            };

            // Initial visibility
            if (collapsed)
                cardsContainer.Visible = false;

            // Profiles are already sorted by mtime descending (done in BuildSteamUserGroups)
            foreach (var prof in group.Profiles)
                RenderSaveCard(cardsContainer, prof);
        }

        // ── Save card rendering ──────────────────────────────────────────────────

        private static void RenderSaveCard(VBoxContainer parent, SaveProfile profile)
        {
            // Use enriched data directly from Python v2-style SaveProfile
            bool isActive = profile.IsActive;
            bool isModded = profile.IsModded;
            int playerCount = profile.PlayerCount;
            int actIdx = profile.ActIndex;
            int ascension = profile.Ascension;
            string playersSummary = profile.PlayersSummary;
            string timeStr = profile.SaveTime.HasValue
                ? profile.SaveTime.Value.ToString("MM-dd HH:mm")
                : "?";

            // Status tag
            string statusTag = isActive
                ? Loc.Get("card.status_in_progress", "In Progress")
                : Loc.Get("card.status_not_started", "Not Started");

            // Profile key display: "[Mod/Standard] steamId · 昵称 · profileKey"
            string modeLabel = isModded ? Loc.Get("card.mod", "Mod") : Loc.Get("card.standard", "Standard");
            string persona = TryGetSteamPersona(profile.SteamId);
            string profileKeyDisplay = string.IsNullOrEmpty(persona)
                ? $"[{modeLabel}] {profile.ProfileKey}"
                : $"[{modeLabel}] {MpSessionState.ShortenSteamId(profile.SteamId)}  ·  {persona}  ·  {profile.ProfileKey}";

            // Uniform card style (no active/in-progress highlight — status text still shows 进行中/未开始)
            var baseColor = new Godot.Color("1F3460");

            var card = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 72),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var cardStyle = new StyleBoxFlat
            {
                BgColor = baseColor,
                BorderColor = Panel.Styles.PanelBorder,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            cardStyle.SetBorderWidthAll(0);
            cardStyle.SetCornerRadiusAll(8);
            card.AddThemeStyleboxOverride("panel", cardStyle);
            parent.AddChild(card, false, Node.InternalMode.Disabled);

            var cardMargin = new MarginContainer();
            cardMargin.AddThemeConstantOverride("margin_left", 12);
            cardMargin.AddThemeConstantOverride("margin_right", 12);
            cardMargin.AddThemeConstantOverride("margin_top", 10);
            cardMargin.AddThemeConstantOverride("margin_bottom", 8);
            card.AddChild(cardMargin, false, Node.InternalMode.Disabled);

            var cardV = new VBoxContainer();
            cardV.AddThemeConstantOverride("separation", 4);
            cardMargin.AddChild(cardV, false, Node.InternalMode.Disabled);

            // Profile key line (gold, bold — matches Python v2)
            var profileKeyLbl = new Label
            {
                Text = profileKeyDisplay
            };
            profileKeyLbl.AddThemeFontSizeOverride("font_size", 18);
            profileKeyLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            cardV.AddChild(profileKeyLbl, false, Node.InternalMode.Disabled);

            // Info line: player_count · act+1 · ascension · status · time
            string infoText = $"{playerCount}p · Act {actIdx + 1} · A{ascension} · {statusTag} · {timeStr}";
            var infoLbl = new Label { Text = infoText };
            infoLbl.AddThemeFontSizeOverride("font_size", 16);
            infoLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            cardV.AddChild(infoLbl, false, Node.InternalMode.Disabled);

            // Players summary (if any)
            if (!string.IsNullOrEmpty(playersSummary) && playersSummary != Loc.Get("summary.no_players", "No players"))
            {
                var summaryLbl = new Label { Text = playersSummary };
                summaryLbl.AddThemeFontSizeOverride("font_size", 15);
                summaryLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
                cardV.AddChild(summaryLbl, false, Node.InternalMode.Disabled);
            }

            // Click to load (Python v2: always clickable if profile has a path)
            Action loadAction = () =>
            {
                if (string.IsNullOrEmpty(profile.SavePath)) return;
                bool ok = MpSessionState.LoadSave(profile.SavePath);
                if (ok)
                    MpPanel.SwitchPage(MpPanel.PAGE_TAKEOVER);
                else
                    MpPanel.ShowStatusMessage(Loc.Get("error.load_save_failed", "Failed to load save"), Panel.Styles.Red);
            };

            card.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    loadAction();
            };
        }

        // ── Data types ───────────────────────────────────────────────────────────

        private class SteamUserGroupData
        {
            internal string SteamId { get; set; } = "";
            internal string SteamIdShort { get; set; } = "";
            internal string Persona { get; set; } = "";
            internal List<SaveProfile> Profiles { get; set; } = new();
        }
    }
}
