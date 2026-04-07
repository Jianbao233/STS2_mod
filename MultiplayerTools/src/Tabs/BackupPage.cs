using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                // Scan Python v2 .backup.* format (single files in saves directories)
                ScanLegacyBackupFiles();

                // Scan C# folder-based backups
                ScanFolderBasedBackups();

                // Sort newest first
                _allBackups.Sort((a, b) => string.Compare(b.Timestamp, a.Timestamp, StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] ScanAllBackups failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Scan Python v2 legacy .backup.* format:
        ///   saves/current_run_mp.save.backup.YYYYMMDD_HHMMSS
        ///   saves/current_run.save.backup.YYYYMMDD_HHMMSS
        /// (mirrors Python v2 save_io.scan_all_backups)
        /// </summary>
        private static void ScanLegacyBackupFiles()
        {
            try
            {
                var processedSavesDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var accountRoot in SaveManagerHelper.EnumerateAccountRoots())
                {
                    string steamId = accountRoot.AccountId;
                    string[] subPaths = new[]
                    {
                        Path.Combine(accountRoot.AccountDir, "modded"),
                        accountRoot.AccountDir
                    };

                    foreach (var subPath in subPaths)
                    {
                        if (!Directory.Exists(subPath)) continue;

                        foreach (var profileDir in Directory.GetDirectories(subPath))
                        {
                            if (!Path.GetFileName(profileDir).StartsWith("profile", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var savesDir = Path.Combine(profileDir, "saves");
                            if (!Directory.Exists(savesDir)) continue;

                            string resolvedDir;
                            try { resolvedDir = Path.GetFullPath(savesDir); }
                            catch { resolvedDir = savesDir; }
                            if (!processedSavesDirs.Add(resolvedDir)) continue;

                            string profileKey = Path.GetFileName(profileDir);
                            if (subPath.EndsWith("modded", StringComparison.OrdinalIgnoreCase))
                                profileKey = "modded/" + profileKey;

                            // Scan .backup.* files in this saves dir
                            foreach (var file in Directory.GetFiles(savesDir))
                            {
                                string fname = Path.GetFileName(file);
                                string? ts = null;

                                if (fname.StartsWith("current_run_mp.save.backup."))
                                    ts = fname.Substring("current_run_mp.save.backup.".Length);
                                else if (fname.StartsWith("current_run.save.backup."))
                                    ts = fname.Substring("current_run.save.backup.".Length);

                                if (string.IsNullOrEmpty(ts) || ts.Length < 12) continue;

                                // Determine main save path for restore
                                var mainSave = Path.Combine(savesDir, "current_run_mp.save");
                                if (!File.Exists(mainSave))
                                    mainSave = Path.Combine(savesDir, "current_run.save");

                                var fi = new FileInfo(file);
                                long size = fi.Length;

                                // Parse save time from backup file
                                string saveTimeStr = "";
                                try
                                {
                                    var data = SaveManagerHelper.ParseSaveFile(file);
                                    if (data != null)
                                    {
                                        if (data.TryGetValue("save_time", out var stVal) && stVal is long stUnix && stUnix > 0)
                                        {
                                            var dt = DateTimeOffset.FromUnixTimeSeconds(stUnix).LocalDateTime;
                                            saveTimeStr = dt.ToString("MM-dd HH:mm");
                                        }
                                    }
                                }
                                catch { }

                                _allBackups.Add(new GlobalBackupEntry
                                {
                                    Path = file,       // single file
                                    SteamId = steamId,
                                    ProfileKey = $"{steamId}/{profileKey}",
                                    Timestamp = ts,
                                    SaveTimeStr = string.IsNullOrEmpty(saveTimeStr) ? fi.LastWriteTime.ToString("MM-dd HH:mm") : saveTimeStr,
                                    PlayerCount = 0,
                                    SizeBytes = size,
                                    FileCount = 1,
                                    IsLegacy = true,
                                    RestoreTarget = mainSave
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] ScanLegacyBackupFiles failed: " + ex.Message);
            }
        }

        /// <summary>
        /// Scan C# folder-based backups in %APPDATA%\SlayTheSpire2\backups\.
        /// New names: <c>{steamId}_{profileWith+ForSlashes}_{yyyyMMddHHmmss}</c>.
        /// Legacy: <c>modded_profile2_yyyyMMdd_HHmmss</c> etc. (no Steam ID in folder name → group "?").
        /// </summary>
        private static void ScanFolderBasedBackups()
        {
            try
            {
                var backupRoot = SaveManagerHelper.GetBackupRoot();
                if (!Directory.Exists(backupRoot)) return;

                // One scan for all folders (avoid re-scanning disk per backup when resolving Steam ID).
                var allProfiles = SaveManagerHelper.GetAllProfiles();

                foreach (var dir in Directory.GetDirectories(backupRoot))
                {
                    var di = new DirectoryInfo(dir);
                    var name = di.Name;
                    TryParseFolderBackupDirName(name, out string steamId, out string profileKey, out string timestamp);

                    if (TryReadBackupMetaJson(dir, out var metaSid, out var metaProfile))
                    {
                        if (!string.IsNullOrEmpty(metaSid)) steamId = metaSid;
                        if (!string.IsNullOrEmpty(metaProfile)) profileKey = metaProfile.Replace('\\', '/');
                    }

                    if (string.IsNullOrEmpty(steamId))
                        steamId = ResolveSteamIdForBackupProfile(profileKey, allProfiles);

                    // Scan for save files in this backup
                    var saveFiles = Directory.GetFiles(dir, "*.save").Concat(
                        Directory.GetFiles(dir, "*.run")).ToList();

                    string saveTime = "";
                    if (saveFiles.Count > 0)
                    {
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
                        PlayerCount = 0,
                        SizeBytes = size,
                        FileCount = saveFiles.Count,
                        IsLegacy = false
                    });
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] ScanFolderBasedBackups failed: " + ex.Message);
            }
        }

        private static bool TryReadBackupMetaJson(string backupDir, out string steamId, out string profileKey)
        {
            steamId = "";
            profileKey = "";
            try
            {
                var path = Path.Combine(backupDir, SaveManagerHelper.BackupMetaFileName);
                if (!File.Exists(path)) return false;
                using var doc = JsonDocument.Parse(File.ReadAllText(path));
                var root = doc.RootElement;
                if (root.TryGetProperty("steam_id", out var sidEl))
                    steamId = sidEl.GetString() ?? "";
                if (root.TryGetProperty("profile_key", out var pkEl))
                    profileKey = pkEl.GetString() ?? "";
                return !string.IsNullOrEmpty(steamId) || !string.IsNullOrEmpty(profileKey);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Legacy folder names lack Steam ID; infer from scanned save profiles (unique match only).</summary>
        private static string ResolveSteamIdForBackupProfile(string profileKeyRaw, List<SaveProfile> profiles)
        {
            string norm = NormalizeUnderscoreProfileKey(profileKeyRaw).Replace('\\', '/').Trim('/');
            if (string.IsNullOrEmpty(norm)) return "";

            var matches = profiles
                .Where(p => ProfileSpecMatchesBackup(p, norm))
                .Select(p => p.SteamId)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (matches.Count == 1) return matches[0];
            if (matches.Count > 1 && !string.IsNullOrEmpty(MpSessionState.CurrentSteamId) &&
                matches.Contains(MpSessionState.CurrentSteamId, StringComparer.OrdinalIgnoreCase))
                return MpSessionState.CurrentSteamId;
            return "";
        }

        private static bool ProfileSpecMatchesBackup(SaveProfile p, string norm)
        {
            string spec = p.IsModded ? "modded/" + p.ProfileName : p.ProfileName;
            spec = spec.Replace('\\', '/');
            return string.Equals(spec, norm, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Parse backup folder name; Steam ID is only present in the new filename scheme.</summary>
        private static void TryParseFolderBackupDirName(string name, out string steamId, out string profileKey, out string timestamp)
        {
            steamId = "";
            profileKey = name;
            timestamp = "";

            // New: 76561198679823594_modded+profile2_20260402200046
            int lastUs = name.LastIndexOf('_');
            if (lastUs > 0 && lastUs < name.Length - 1)
            {
                string ts = name[(lastUs + 1)..];
                if (ts.Length == 14 && ts.All(char.IsDigit))
                {
                    string head = name[..lastUs];
                    int firstUs = head.IndexOf('_');
                    if (firstUs > 0 && firstUs < head.Length - 1)
                    {
                        string sid = head[..firstUs];
                        if (sid.Length >= 15 && sid.All(char.IsDigit))
                        {
                            steamId = sid;
                            profileKey = head[(firstUs + 1)..].Replace('+', '/');
                            timestamp = ts;
                            return;
                        }
                    }
                }
            }

            // Legacy: *_yyyyMMdd_HHmmss at end
            var m = Regex.Match(name, @"_(20\d{6})_(\d{6})$");
            if (m.Success)
            {
                profileKey = NormalizeUnderscoreProfileKey(name[..m.Index]);
                timestamp = m.Groups[1].Value + m.Groups[2].Value;
                return;
            }

            // Legacy: trailing _yyyyMMddHHmmss (14 digits)
            var m2 = Regex.Match(name, @"_(\d{14})$");
            if (m2.Success)
            {
                profileKey = NormalizeUnderscoreProfileKey(name[..m2.Index]);
                timestamp = m2.Groups[1].Value;
                return;
            }

            int u = name.LastIndexOf('_');
            if (u > 0)
            {
                profileKey = NormalizeUnderscoreProfileKey(name[..u]);
                timestamp = name[(u + 1)..];
            }
        }

        /// <summary>Turn modded_profile2 into modded/profile2 for <see cref="SaveManagerHelper.FindCurrentSave"/>.</summary>
        private static string NormalizeUnderscoreProfileKey(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            if (raw.Contains('/') || raw.Contains('\\')) return raw.Replace('\\', '/');
            if (raw.StartsWith("modded_", StringComparison.OrdinalIgnoreCase) && raw.Length > 7)
                return "modded/" + raw[7..];
            return raw;
        }

        private static string FormatBackupFolderTimestamp(string? ts)
        {
            if (string.IsNullOrEmpty(ts)) return "?";
            if (ts.Length == 14 && ts.All(char.IsDigit))
                return $"{ts[..4]}-{ts[4..6]}-{ts[6..8]} {ts[8..10]}:{ts[10..12]}:{ts[12..14]}";
            if (ts.Length >= 15 && ts[8] == '_')
                return $"{ts[..4]}-{ts[4..6]}-{ts[6..8]} {ts[9..11]}:{ts[11..13]}";
            return ts;
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
            string timeStr = FormatBackupFolderTimestamp(entry.Timestamp);

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

            // Time + legacy badge
            var timeRow = new HBoxContainer();
            timeRow.AddThemeConstantOverride("separation", 6);
            var timeLbl = new Label { Text = timeStr };
            timeLbl.AddThemeFontSizeOverride("font_size", 18);
            timeLbl.AddThemeColorOverride("font_color", Panel.Styles.MpGold);
            timeLbl.CustomMinimumSize = new Vector2(140, 0);
            timeRow.AddChild(timeLbl, false, Node.InternalMode.Disabled);
            if (entry.IsLegacy)
            {
                var badge = new Label { Text = Loc.Get("backup.legacy", "v2") };
                badge.AddThemeFontSizeOverride("font_size", 14);
                badge.AddThemeColorOverride("font_color", Panel.Styles.MpGray);
                timeRow.AddChild(badge, false, Node.InternalMode.Disabled);
            }
            cH.AddChild(timeRow, false, Node.InternalMode.Disabled);

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
            string? currentSave;
            string destPath;

            if (entry.IsLegacy)
            {
                // Legacy: single file → restore directly to its paired main save path
                currentSave = entry.RestoreTarget;
                if (string.IsNullOrEmpty(currentSave))
                {
                    MpPanel.ShowStatusMessage(Loc.Get("backup.restore_no_target", "Cannot determine target save path"), Panel.Styles.Red);
                    return;
                }
                destPath = entry.Path; // the backup file itself
            }
            else
            {
                // C# folder: copy all *.save files from backup dir to target
                string profileSpec = NormalizeUnderscoreProfileKey(entry.ProfileKey.Replace('\\', '/'));
                currentSave = SaveManagerHelper.FindCurrentSave(entry.SteamId, profileSpec);
                if (string.IsNullOrEmpty(currentSave))
                    currentSave = SaveManagerHelper.FindCurrentSave(entry.SteamId, profileSpec.Split('/').Last());
                if (string.IsNullOrEmpty(currentSave))
                    currentSave = MpSessionState.CurrentSavePath;

                if (string.IsNullOrEmpty(currentSave))
                {
                    MpPanel.ShowStatusMessage(Loc.Get("backup.restore_no_target", "Cannot determine target save path"), Panel.Styles.Red);
                    return;
                }
                destPath = entry.Path; // the backup directory
            }

            if (string.IsNullOrEmpty(currentSave))
            {
                MpPanel.ShowStatusMessage(Loc.Get("backup.restore_no_target", "Cannot determine target save path"), Panel.Styles.Red);
                return;
            }

            try
            {
                if (entry.IsLegacy)
                {
                    // Single file copy
                    File.Copy(entry.Path, currentSave, true);
                }
                else
                {
                    // Copy all save files from backup folder to target
                    var backupFiles = Directory.GetFiles(entry.Path, "*.save");
                    foreach (var f in backupFiles)
                    {
                        string destName = Path.GetFileName(f);
                        string dest = Path.Combine(Path.GetDirectoryName(currentSave) ?? "", destName);
                        File.Copy(f, dest, true);
                    }
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
                if (entry.IsLegacy)
                {
                    // Single backup file
                    if (File.Exists(entry.Path))
                        File.Delete(entry.Path);
                }
                else
                {
                    // Folder-based backup
                    if (Directory.Exists(entry.Path))
                        Directory.Delete(entry.Path, true);
                }
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
            /// <summary>True = Python v2 .backup.* single file; False = C# folder-based backup.</summary>
            internal bool IsLegacy;
            /// <summary>For legacy backups: the main save file path to restore to.</summary>
            internal string RestoreTarget = "";
        }
    }
}
