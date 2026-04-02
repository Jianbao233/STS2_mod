using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Backup management tab. Create, restore, and delete backups.
    /// </summary>
    internal static class BackupTab
    {
        private static List<BackupInfo> _backups = new();
        private static BackupInfo? _selected;
        private static VBoxContainer? _listVBox;
        private static VBoxContainer? _detailVBox;

        internal static void Build(VBoxContainer container)
        {
            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("backup.title", "Backup Management")), false, Node.InternalMode.Disabled);

            var hbox = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            hbox.AddThemeConstantOverride("separation", 12);
            container.AddChild(hbox, false, Node.InternalMode.Disabled);

            // Left: backup list
            var leftCol = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(340, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 0.45f
            };
            leftCol.AddThemeConstantOverride("separation", 6);
            hbox.AddChild(leftCol, false, Node.InternalMode.Disabled);

            // Toolbar
            var toolbar = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            toolbar.AddThemeConstantOverride("separation", 6);
            leftCol.AddChild(toolbar, false, Node.InternalMode.Disabled);

            var createBtn = MpPanel.CreateActionButton(Loc.Get("backup.create", "Create Backup"), Panel.Styles.Green);
            createBtn.CustomMinimumSize = new Vector2(120, 32);
            createBtn.Pressed += OnCreateBackup;
            toolbar.AddChild(createBtn, false, Node.InternalMode.Disabled);

            var refreshBtn = MpPanel.CreateActionButton(Loc.Get("backup.refresh", "Refresh"), Panel.Styles.Blue);
            refreshBtn.CustomMinimumSize = new Vector2(80, 32);
            refreshBtn.Pressed += () => { LoadBackups(); RebuildList(leftCol); };
            toolbar.AddChild(refreshBtn, false, Node.InternalMode.Disabled);

            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            leftCol.AddChild(scroll, false, Node.InternalMode.Disabled);
            _listVBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            _listVBox.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(_listVBox, false, Node.InternalMode.Disabled);

            // Right: detail
            _detailVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 0.55f
            };
            _detailVBox.AddThemeConstantOverride("separation", 8);
            hbox.AddChild(_detailVBox, false, Node.InternalMode.Disabled);

            var hintLbl = new Label
            {
                Text = Loc.Get("backup.no_backups", "No backups found"),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            hintLbl.AddThemeFontSizeOverride("font_size", 21);
            hintLbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
            _detailVBox.AddChild(hintLbl, false, Node.InternalMode.Disabled);

            LoadBackups();
            RebuildList(leftCol);
        }

        private static void LoadBackups()
        {
            _backups.Clear();
            try
            {
                // Scan %APPDATA%\SlayTheSpire2\backups\ (matches SaveManagerHelper.GetBackupRoot / Python v2)
                var backupRoot = SaveManagerHelper.GetBackupRoot();
                if (!Directory.Exists(backupRoot)) return;

                foreach (var dir in Directory.GetDirectories(backupRoot).OrderByDescending(d => Directory.GetCreationTime(d)))
                {
                    var fi = new DirectoryInfo(dir);
                    var files = Directory.GetFiles(dir);
                    long size = files.Sum(f => new FileInfo(f).Length);
                    _backups.Add(new BackupInfo
                    {
                        Path = dir,
                        Name = fi.Name,
                        CreatedTime = fi.CreationTime,
                        SizeBytes = size,
                        FileCount = files.Length
                    });
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] LoadBackups failed: " + ex.Message);
            }
        }

        private static void RebuildList(VBoxContainer listContainer)
        {
            if (_listVBox == null || !GodotObject.IsInstanceValid(_listVBox)) return;
            MpPanel.ClearChildren(_listVBox);

            if (_backups.Count == 0)
            {
                var lbl = new Label { Text = Loc.Get("backup.no_backups", "No backups") };
                lbl.AddThemeFontSizeOverride("font_size", 19);
                lbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                _listVBox.AddChild(lbl, false, Node.InternalMode.Disabled);
                return;
            }

            foreach (var b in _backups)
            {
                var btn = new Button
                {
                    Text = $"  {b.Name}  ({SaveManagerHelper.FormatSize(b.SizeBytes)})",
                    CustomMinimumSize = new Vector2(0, 34),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                btn.AddThemeFontSizeOverride("font_size", 18);
                btn.AddThemeColorOverride("font_color", _selected == b ? Panel.Styles.Gold : Panel.Styles.Cream);
                btn.AddThemeColorOverride("font_hover_color", Panel.Styles.Gold);
                Panel.Styles.ApplyListRowButton(btn);
                BackupInfo captured = b;
                btn.Pressed += () => { _selected = captured; RebuildList(_listVBox!); ShowDetail(b); };
                _listVBox.AddChild(btn, false, Node.InternalMode.Disabled);
            }
        }

        private static void ShowDetail(BackupInfo b)
        {
            if (_detailVBox == null || !GodotObject.IsInstanceValid(_detailVBox)) return;
            MpPanel.ClearChildren(_detailVBox);

            _detailVBox.AddChild(MpPanel.CreateSectionHeader(b.Name), false, Node.InternalMode.Disabled);

            var infoGrid = new VBoxContainer();
            infoGrid.AddThemeConstantOverride("separation", 6);
            _detailVBox.AddChild(infoGrid, false, Node.InternalMode.Disabled);

            AddDetailRow(infoGrid, Loc.Get("backup.timestamp", "Created"), b.CreatedTime.ToString("yyyy-MM-dd HH:mm:ss"));
            AddDetailRow(infoGrid, Loc.Get("backup.size", "Size"), SaveManagerHelper.FormatSize(b.SizeBytes));
            AddDetailRow(infoGrid, "Files", b.FileCount.ToString());

            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 8);
            _detailVBox.AddChild(btnRow, false, Node.InternalMode.Disabled);

            var restoreBtn = MpPanel.CreateActionButton(Loc.Get("backup.restore", "Restore"), Panel.Styles.Green);
            restoreBtn.CustomMinimumSize = new Vector2(80, 32);
            restoreBtn.Pressed += () => OnRestoreBackup(b);
            btnRow.AddChild(restoreBtn, false, Node.InternalMode.Disabled);

            var delBtn = MpPanel.CreateActionButton(Loc.Get("backup.delete", "Delete"), Panel.Styles.Red);
            delBtn.CustomMinimumSize = new Vector2(70, 32);
            delBtn.Pressed += () => OnDeleteBackup(b);
            btnRow.AddChild(delBtn, false, Node.InternalMode.Disabled);
        }

        private static void AddDetailRow(VBoxContainer parent, string label, string value)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var lbl = new Label { Text = label + ":", CustomMinimumSize = new Vector2(100, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 18);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.Cream);
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
            var val = new Label { Text = value };
            val.AddThemeFontSizeOverride("font_size", 18);
            val.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            row.AddChild(val, false, Node.InternalMode.Disabled);
            parent.AddChild(row, false, Node.InternalMode.Disabled);
        }

        private static void OnCreateBackup()
        {
            try
            {
                // Use the currently loaded save's identity (correct Steam ID + modded/profileN key).
                var currentSteamId = !string.IsNullOrEmpty(MpSessionState.CurrentSteamId) ? MpSessionState.CurrentSteamId : Steam.SteamIntegration.GetCurrentSteamId() ?? "unknown";
                var currentProfileKey = !string.IsNullOrEmpty(MpSessionState.CurrentProfileKey) ? MpSessionState.CurrentProfileKey : "profile1";
                var path = SaveManagerHelper.BackupCurrent(currentSteamId, currentProfileKey);
                if (path != null)
                {
                    LoadBackups();
                    RebuildList(_listVBox!);
                    ShowStatusMsg(Loc.Get("backup.created", "Backup created"), Panel.Styles.Green);
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] OnCreateBackup failed: " + ex.Message);
            }
        }

        private static void OnRestoreBackup(BackupInfo b)
        {
            try
            {
                // Find the current run save that corresponds to this backup
                var savesDir = Path.GetDirectoryName(b.Path);
                if (string.IsNullOrEmpty(savesDir)) return;
                var currentSave = Path.Combine(savesDir, "current_run_mp.save");
                var backupFiles = Directory.GetFiles(b.Path, "*.save");
                if (backupFiles.Length == 0)
                {
                    ShowStatusMsg(Loc.Get("backup.restore_no_files", "No save files in backup"), Panel.Styles.Red);
                    return;
                }
                // Restore the first matching save file found in the backup
                var srcFile = backupFiles[0];
                File.Copy(srcFile, currentSave, true);
                ShowStatusMsg(Loc.Get("backup.restored", "Backup restored"), Panel.Styles.Green);
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] OnRestoreBackup failed: " + ex.Message);
                ShowStatusMsg(Loc.Get("error", "Error") + ": " + ex.Message, Panel.Styles.Red);
            }
        }

        private static void OnDeleteBackup(BackupInfo b)
        {
            try
            {
                if (Directory.Exists(b.Path))                 Directory.Delete(b.Path, true);
                _backups.Remove(b);
                _selected = null;
                RebuildList(_listVBox!);
                MpPanel.ClearChildren(_detailVBox!);
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] OnDeleteBackup failed: " + ex.Message);
            }
        }

        private static void ShowStatusMsg(string text, Color color)
        {
            if (_detailVBox == null || !GodotObject.IsInstanceValid(_detailVBox)) return;
            var msg = new Label { Text = text };
            msg.AddThemeFontSizeOverride("font_size", 14);
            msg.AddThemeColorOverride("font_color", color);
            _detailVBox.AddChild(msg, false, Node.InternalMode.Disabled);
        }
    }

    internal class BackupInfo
    {
        internal string Path { get; set; } = "";
        internal string Name { get; set; } = "";
        internal DateTime CreatedTime { get; set; }
        internal long SizeBytes { get; set; }
        internal int FileCount { get; set; }
    }
}
