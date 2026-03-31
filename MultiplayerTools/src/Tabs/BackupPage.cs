using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Backup page — mirrors v2 _page_backup (global groups + current save section).
    ///
    /// Layout:
    ///   [title + subtitle]
    ///   → Global backup groups (collapsible by Steam user)
    ///     → profile sub-headers
    ///       → backup cards (timestamp, player count, restore/delete/view buttons)
    ///   → Current save backup section (if a save is loaded)
    ///     → save path + player count + act info
    ///     → "Create Backup Now" button
    ///     → current save's backup list
    ///   → Rescan button
    /// </summary>
    internal static class BackupPage
    {
        // Collapse state: steamId → bool
        private static Dictionary<string, bool> _collapsedGroups = new();
        private static List<GlobalBackupEntry> _allBackups = new();
        private static bool _loaded = false;

        internal static void Build(VBoxContainer container)
        {
            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("backup.all.title", "Backup Management")), false, Node.InternalMode.Disabled);
            var subtitle = new Label { Text = Loc.Get("backup.all.subtitle", "Create and restore backups for all profiles") };
            subtitle.AddThemeFontSizeOverride("font_size", 17);
            subtitle.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            container.AddChild(subtitle, false, Node.InternalMode.Disabled);

            // ── Scan all backups ──────────────────────────────────────────────
            if (!_loaded)
            {
                ScanAllBackups();
                _loaded = true;
            }

            // Global backup groups
            if (_allBackups.Count == 0)
            {
                var notFound = new Label { Text = Loc.Get("backup.no_backups_global", "No backups found") };
                notFound.AddThemeFontSizeOverride("font_size", 18);
                notFound.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
                container.AddChild(notFound, false, Node.InternalMode.Disabled);
            }
            else
            {
                RenderBackupGroups(container);
            }

            // ── Current save section ────────────────────────────────────────
            if (!string.IsNullOrEmpty(MpSessionState.CurrentSavePath))
            {
                AddSeparator(container);

                var curTitle = new Label { Text = Loc.Get("backup.current.title", "Current Save Backup") };
                curTitle.AddThemeFontSizeOverride("font_size", 19);
                curTitle.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
                container.AddChild(curTitle, false, Node.InternalMode.Disabled);

                var curSubtitle = new Label { Text = Loc.Get("backup.current.subtitle", "Backup the currently loaded save") };
                curSubtitle.AddThemeFontSizeOverride("font_size", 16);
                curSubtitle.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
                container.AddChild(curSubtitle, false, Node.InternalMode.Disabled);

                // Save info card
                string savePath = MpSessionState.CurrentSavePath ?? "";
                int playerN = MpSessionState.PlayerCount;
                int act = MpSessionState.ActIndex + 1;
                int asc = MpSessionState.Ascension;
                var infoCard = new PanelContainer
                {
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(0, 44)
                };
                var infoStyle = new StyleBoxFlat
                {
                    BgColor = Panel.Styles.MpNavSelected,
                    BorderColor = Panel.Styles.PanelBorder,
                    ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
                };
                infoStyle.SetBorderWidthAll(0);
                infoStyle.SetCornerRadiusAll(8);
                infoCard.AddThemeStyleboxOverride("panel", infoStyle);
                container.AddChild(infoCard, false, Node.InternalMode.Disabled);
                var infoMargin = new MarginContainer();
                infoMargin.AddThemeConstantOverride("margin_left", 12);
                infoMargin.AddThemeConstantOverride("margin_right", 12);
                infoMargin.AddThemeConstantOverride("margin_top", 6);
                infoMargin.AddThemeConstantOverride("margin_bottom", 6);
                infoCard.AddChild(infoMargin, false, Node.InternalMode.Disabled);
                var infoLbl = new Label { Text = $"{savePath}  ·  {playerN} players  ·  Act {act}  ·  Asc {asc}" };
                infoLbl.AddThemeFontSizeOverride("font_size", 16);
                infoLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
                infoMargin.AddChild(infoLbl, false, Node.InternalMode.Disabled);

                // Create backup button
                var backupBtn = MpPanel.CreateActionButton(
                    Loc.Get("backup.create_now", "Backup Now"), Panel.Styles.Green);
                backupBtn.CustomMinimumSize = new Vector2(140, 36);
                backupBtn.AddThemeFontSizeOverride("font_size", 17);
                backupBtn.Pressed += () =>
                {
                    if (string.IsNullOrEmpty(MpSessionState.CurrentSavePath)) return;
                    var bp = SaveManagerHelper.BackupCurrent(
                        MpSessionState.CurrentSteamId, MpSessionState.CurrentProfileKey);
                    if (bp != null)
                    {
                        MpSessionState.ReloadSave();
                        ScanAllBackups();
                        MpPanel.RefreshStatusBar();
                        MpPanel.ShowStatusMessage(
                            Loc.Get("backup.created", "Backup created: ") + Path.GetFileName(bp),
                            Panel.Styles.Green);
                        MpPanel.SwitchPage(MpPanel.PAGE_BACKUP);
                    }
                    else
                    {
                        MpPanel.ShowStatusMessage(
                            Loc.Get("backup.create_failed", "Failed to create backup"),
                            Panel.Styles.Red);
                    }
                };
                container.AddChild(backupBtn, false, Node.InternalMode.Disabled);
            }

            // Rescan
            var rescanBtn = MpPanel.CreateActionButton(
                Loc.Get("backup.rescan", "Rescan"), Panel.Styles.MpPrimaryBtn);
            rescanBtn.CustomMinimumSize = new Vector2(120, 34);
            rescanBtn.Pressed += () =>
            {
                ScanAllBackups();
                MpPanel.SwitchPage(MpPanel.PAGE_BACKUP);
            };
            container.AddChild(rescanBtn, false, Node.InternalMode.Disabled);
        }

        private static void ScanAllBackups()
        {
            _allBackups.Clear();
            try
            {
                var backupRoot = SaveManagerHelper.GetBackupRoot();
                if (!Directory.Exists(backupRoot)) return;

                // Walk all subfolders: each is named "{profileName}_{timestamp}"
                foreach (var dir in Directory.GetDirectories(backupRoot))
                {
                    var di = new DirectoryInfo(dir);
                    var name = di.Name;
                    // Try to parse: profileName_timestamp
                    string profileKey = name;
                    string timestamp = "";
                    int usIdx = name.LastIndexOf('_');
                    if (usIdx > 0)
                    {
                        profileKey = name[..usIdx];
                        timestamp = name[(usIdx + 1)..];
                    }

                    // Determine steamId from parent path
                    string steamId = "";
                    try
                    {
                        var parts = dir.Replace('\\', '/').Split('/');
                        int idx = Array.IndexOf(parts, "backups");
                        if (idx > 0 && idx + 2 < parts.Length)
                            steamId = parts[idx - 1]; // backups is inside SlayTheSpire2/{steamId}/backups
                    }
                    catch { }

                    // Scan for save files in this backup
                    var saveFiles = Directory.GetFiles(dir, "*.save").Concat(
                        Directory.GetFiles(dir, "*.run")).ToList();

                    int playerCount = 0;
                    string saveTime = "";
                    if (saveFiles.Count > 0)
                    {
                        // Lightweight: just get first few bytes to check player count
                        // For now, just store file count
                        var fi = new FileInfo(saveFiles[0]);
                        saveTime = fi.LastWriteTime.ToString("MM-dd HH:mm");
                    }

                    long size = 0;
                    foreach (var f in saveFiles)
                        size += new FileInfo(f).Length;

                    _allBackups.Add(new GlobalBackupEntry
                    {
                        Path = dir,
                        SteamId = steamId,
                        ProfileKey = profileKey,
                        Timestamp = timestamp,
                        SaveTimeStr = saveTime,
                        PlayerCount = playerCount,
                        SizeBytes = size,
                        FileCount = saveFiles.Count
                    });
                }

                // Sort newest first
                _allBackups.Sort((a, b) => string.Compare(b.Timestamp, a.Timestamp, StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] ScanAllBackups failed: " + ex.Message);
            }
        }

        private static void RenderBackupGroups(VBoxContainer container)
        {
            // Group by steamId
            var groups = new Dictionary<string, List<GlobalBackupEntry>>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in _allBackups)
            {
                var sid = string.IsNullOrEmpty(e.SteamId) ? "?" : e.SteamId;
                if (!groups.ContainsKey(sid)) groups[sid] = new List<GlobalBackupEntry>();
                groups[sid].Add(e);
            }

            // Scroll area
            var scroll = new ScrollContainer
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 300)
            };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(scroll, false, Node.InternalMode.Disabled);

            var scrollContent = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin
            };
            scrollContent.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(scrollContent, false, Node.InternalMode.Disabled);

            foreach (var sid in groups.Keys.OrderBy(s => s))
            {
                var entries = groups[sid];
                RenderGroupHeader(scrollContent, sid, entries);
            }
        }

        private static void RenderGroupHeader(VBoxContainer parent, string steamId, List<GlobalBackupEntry> entries)
        {
            bool collapsed = _collapsedGroups.TryGetValue(steamId, out var c) && c;
            string shortId = MpSessionState.ShortenSteamId(steamId);

            var header = new PanelContainer
            {
                CustomMinimumSize = new Vector2(0, 48),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            var hStyle = new StyleBoxFlat
            {
                BgColor = Panel.Styles.MpCard,
                BorderColor = Panel.Styles.PanelBorder,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            hStyle.SetBorderWidthAll(0);
            hStyle.SetCornerRadiusAll(8);
            header.AddThemeStyleboxOverride("panel", hStyle);
            parent.AddChild(header, false, Node.InternalMode.Disabled);

            var hMargin = new MarginContainer();
            hMargin.AddThemeConstantOverride("margin_left", 8);
            hMargin.AddThemeConstantOverride("margin_right", 8);
            hMargin.AddThemeConstantOverride("margin_top", 0);
            hMargin.AddThemeConstantOverride("margin_bottom", 0);
            header.AddChild(hMargin, false, Node.InternalMode.Disabled);

            var hH = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            hH.AddThemeConstantOverride("separation", 8);
            hMargin.AddChild(hH, false, Node.InternalMode.Disabled);

            // Arrow
            var arrow = new Label { Text = collapsed ? "▶" : "▼" };
            arrow.CustomMinimumSize = new Vector2(24, 0);
            arrow.AddThemeFontSizeOverride("font_size", 19);
            arrow.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            hH.AddChild(arrow, false, Node.InternalMode.Disabled);

            // Steam ID
            var idLbl = new Label { Text = shortId };
            idLbl.AddThemeFontSizeOverride("font_size", 18);
            idLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            hH.AddChild(idLbl, false, Node.InternalMode.Disabled);

            hH.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }, false, Node.InternalMode.Disabled);

            // Count
            var countLbl = new Label { Text = Loc.Fmt("backup.backup_count", entries.Count) };
            countLbl.AddThemeFontSizeOverride("font_size", 16);
            countLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
            hH.AddChild(countLbl, false, Node.InternalMode.Disabled);

            // Cards container
            var cards = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            cards.AddThemeConstantOverride("separation", 4);
            parent.AddChild(cards, false, Node.InternalMode.Disabled);

            // Toggle
            string capturedSid = steamId;
            Action toggle = () =>
            {
                bool nowCollapsed = !(_collapsedGroups.TryGetValue(capturedSid, out var cv) && cv);
                _collapsedGroups[capturedSid] = nowCollapsed;
                arrow.Text = nowCollapsed ? "▶" : "▼";
                cards.Visible = !nowCollapsed;
            };
            header.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    toggle();
            };
            arrow.GuiInput += ev =>
            {
                if (ev is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    toggle();
            };

            if (collapsed) cards.Visible = false;

            // Render each entry card
            foreach (var entry in entries)
                RenderBackupCard(cards, entry);
        }

        private static void RenderBackupCard(VBoxContainer parent, GlobalBackupEntry entry)
        {
            // Parse timestamp
            string timeStr = "?";
            if (!string.IsNullOrEmpty(entry.Timestamp) && entry.Timestamp.Length >= 15)
            {
                try { timeStr = $"{entry.Timestamp[..4]}-{entry.Timestamp[4..6]}-{entry.Timestamp[6..8]} {entry.Timestamp[9..11]}:{entry.Timestamp[11..13]}"; }
                catch { timeStr = entry.Timestamp; }
            }

            var card = new PanelContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(0, 60)
            };
            var cStyle = new StyleBoxFlat
            {
                BgColor = Panel.Styles.MpNavSelected,
                BorderColor = Panel.Styles.PanelBorder,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            cStyle.SetBorderWidthAll(0);
            cStyle.SetCornerRadiusAll(6);
            card.AddThemeStyleboxOverride("panel", cStyle);
            parent.AddChild(card, false, Node.InternalMode.Disabled);

            var cMargin = new MarginContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            cMargin.AddThemeConstantOverride("margin_left", 12);
            cMargin.AddThemeConstantOverride("margin_right", 8);
            cMargin.AddThemeConstantOverride("margin_top", 4);
            cMargin.AddThemeConstantOverride("margin_bottom", 4);
            card.AddChild(cMargin, false, Node.InternalMode.Disabled);

            var cH = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            cH.AddThemeConstantOverride("separation", 8);
            cMargin.AddChild(cH, false, Node.InternalMode.Disabled);

            // Time
            var timeLbl = new Label { Text = timeStr };
            timeLbl.AddThemeFontSizeOverride("font_size", 18);
            timeLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            timeLbl.CustomMinimumSize = new Vector2(140, 0);
            cH.AddChild(timeLbl, false, Node.InternalMode.Disabled);

            // Profile
            var profLbl = new Label { Text = entry.ProfileKey };
            profLbl.AddThemeFontSizeOverride("font_size", 16);
            profLbl.AddThemeColorOverride("font_color", Panel.Styles.MpTextMuted);
            cH.AddChild(profLbl, false, Node.InternalMode.Disabled);

            cH.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }, false, Node.InternalMode.Disabled);

            // Size
            var sizeLbl = new Label { Text = SaveManagerHelper.FormatSize(entry.SizeBytes) };
            sizeLbl.AddThemeFontSizeOverride("font_size", 16);
            sizeLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
            cH.AddChild(sizeLbl, false, Node.InternalMode.Disabled);

            // Buttons
            var btnRow = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            btnRow.AddThemeConstantOverride("separation", 4);
            cH.AddChild(btnRow, false, Node.InternalMode.Disabled);

            // Restore button
            var restoreBtn = new Button { Text = Loc.Get("backup.restore", "Restore") };
            restoreBtn.CustomMinimumSize = new Vector2(60, 26);
            restoreBtn.AddThemeFontSizeOverride("font_size", 16);
            var rStyle = new StyleBoxFlat
            {
                BgColor = Panel.Styles.Green,
                BorderColor = Panel.Styles.PanelBorder,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            rStyle.SetBorderWidthAll(0);
            rStyle.SetCornerRadiusAll(4);
            restoreBtn.AddThemeStyleboxOverride("normal", rStyle);
            GlobalBackupEntry capEntry = entry;
            restoreBtn.Pressed += () => RestoreBackup(capEntry);
            btnRow.AddChild(restoreBtn, false, Node.InternalMode.Disabled);

            // Delete button
            var delBtn = new Button { Text = Loc.Get("backup.delete", "Del") };
            delBtn.CustomMinimumSize = new Vector2(44, 26);
            delBtn.AddThemeFontSizeOverride("font_size", 16);
            var dStyle = new StyleBoxFlat
            {
                BgColor = new Godot.Color("8B2A2A"),
                BorderColor = Panel.Styles.PanelBorder,
                ShadowSize = 0, ShadowColor = Godot.Colors.Transparent
            };
            dStyle.SetBorderWidthAll(0);
            dStyle.SetCornerRadiusAll(4);
            delBtn.AddThemeStyleboxOverride("normal", dStyle);
            delBtn.Pressed += () => DeleteBackup(capEntry);
            btnRow.AddChild(delBtn, false, Node.InternalMode.Disabled);
        }

        private static void RestoreBackup(GlobalBackupEntry entry)
        {
            // Find the corresponding current save path
            string? currentSave = SaveManagerHelper.FindCurrentSave(entry.SteamId, entry.ProfileKey.Split('/').Last());
            if (string.IsNullOrEmpty(currentSave))
                currentSave = MpSessionState.CurrentSavePath;

            if (string.IsNullOrEmpty(currentSave))
            {
                MpPanel.ShowStatusMessage(Loc.Get("backup.restore_no_target", "Cannot determine target save path"), Panel.Styles.Red);
                return;
            }

            try
            {
                // Copy all save files from backup to target
                var backupFiles = Directory.GetFiles(entry.Path, "*.save");
                foreach (var f in backupFiles)
                {
                    string destName = Path.GetFileName(f);
                    string dest = Path.Combine(Path.GetDirectoryName(currentSave) ?? "", destName);
                    File.Copy(f, dest, true);
                }
                ScanAllBackups();
                MpSessionState.ReloadSave();
                MpPanel.RefreshStatusBar();
                MpPanel.ShowStatusMessage(Loc.Get("backup.restored", "Backup restored successfully"), Panel.Styles.Green);
                MpPanel.SwitchPage(MpPanel.PAGE_BACKUP);
            }
            catch (Exception ex)
            {
                MpPanel.ShowStatusMessage(Loc.Get("backup.restore_failed", "Restore failed: ") + ex.Message, Panel.Styles.Red);
            }
        }

        private static void DeleteBackup(GlobalBackupEntry entry)
        {
            try
            {
                if (Directory.Exists(entry.Path))
                    Directory.Delete(entry.Path, true);
                ScanAllBackups();
                MpPanel.ShowStatusMessage(Loc.Get("backup.deleted", "Backup deleted"), Panel.Styles.Green);
                MpPanel.SwitchPage(MpPanel.PAGE_BACKUP);
            }
            catch (Exception ex)
            {
                MpPanel.ShowStatusMessage(Loc.Get("backup.delete_failed", "Delete failed: ") + ex.Message, Panel.Styles.Red);
            }
        }

        private static void AddSeparator(VBoxContainer container)
        {
            var sep = new ColorRect
            {
                CustomMinimumSize = new Vector2(0, 1),
                Color = Panel.Styles.Divider,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            sep.AddThemeConstantOverride("custom_minimum_size_y", 1);
            container.AddChild(sep, false, Node.InternalMode.Disabled);
        }

        private class GlobalBackupEntry
        {
            internal string Path = "";
            internal string SteamId = "";
            internal string ProfileKey = "";
            internal string Timestamp = "";
            internal string SaveTimeStr = "";
            internal int PlayerCount;
            internal long SizeBytes;
            internal int FileCount;
        }
    }
}
