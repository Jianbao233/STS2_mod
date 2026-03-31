using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager.Tabs
{
    /// <summary>
    /// 全局备份管理 Tab：
    /// 显示所有备份文件，支持备份/恢复/删除操作。
    /// </summary>
    internal static class BackupTab
    {
        private static class SC
        {
            internal static readonly Color Gold   = new Color("E3A83D");
            internal static readonly Color Cream  = new Color("E3D5C1");
            internal static readonly Color Gray   = new Color("7F8C8D");
            internal static readonly Color Green   = new Color("27AE60");
            internal static readonly Color Red    = new Color("C0392B");
            internal static readonly Color Blue    = new Color("2980B9");
        }

        internal static void Build(VBoxContainer container)
        {
            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("backup.title", "Backup Management")), false, Node.InternalMode.Disabled);

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(actionRow, false, Node.InternalMode.Disabled);

            var createBackupBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("backup.create", "Create Backup"), SC.Green);
            createBackupBtn.CustomMinimumSize = new Vector2(130, 36);
            createBackupBtn.Pressed += () =>
            {
                var path = SaveManagerHelper.BackupCurrentRun();
                if (!string.IsNullOrEmpty(path))
                    GD.Print("[MP_PlayerManager] Backup created: " + path);
                else
                    GD.PrintErr("[MP_PlayerManager] Backup failed.");
                LoadoutPanel.RequestRefresh();
            };
            actionRow.AddChild(createBackupBtn, false, Node.InternalMode.Disabled);

            var refreshBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("backup.refresh", "Refresh"), SC.Gray);
            refreshBtn.CustomMinimumSize = new Vector2(80, 36);
            refreshBtn.Pressed += () => LoadoutPanel.RequestRefresh();
            actionRow.AddChild(refreshBtn, false, Node.InternalMode.Disabled);

            var cleanOldBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("backup.clean_old", "Clean Old (< 3 days)"), SC.Red);
            cleanOldBtn.CustomMinimumSize = new Vector2(160, 36);
            cleanOldBtn.Pressed += () =>
            {
                int removed = CleanOldBackups(3);
                GD.Print($"[MP_PlayerManager] Cleaned {removed} old backup(s).");
                LoadoutPanel.RequestRefresh();
            };
            actionRow.AddChild(cleanOldBtn, false, Node.InternalMode.Disabled);

            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("backup.list", "Backups")), false, Node.InternalMode.Disabled);

            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(scroll, false, Node.InternalMode.Disabled);

            var listVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            listVBox.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(listVBox, false, Node.InternalMode.Disabled);

            BuildBackupList(listVBox);
        }

        private static void BuildBackupList(VBoxContainer container)
        {
            LoadoutPanel.ClearChildren(container);

            string saveRoot = SaveManagerHelper.GetSaveRoot();
            if (string.IsNullOrEmpty(saveRoot))
            {
                var empty = new Label { Text = Loc.Get("backup.no_path", "Cannot determine save directory") };
                empty.AddThemeFontSizeOverride("font_size", 14);
                empty.AddThemeColorOverride("font_color", SC.Gray);
                container.AddChild(empty, false, Node.InternalMode.Disabled);
                return;
            }

            var allBackups = new List<FileInfo>();
            try
            {
                var dir = new DirectoryInfo(saveRoot);
                var backups = dir.GetFiles("*backup*.save", SearchOption.TopDirectoryOnly)
                    .Concat(dir.GetFiles("*.deleted_*", SearchOption.AllDirectories))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();
                allBackups.AddRange(backups);
            }
            catch { }

            if (allBackups.Count == 0)
            {
                var empty = new Label { Text = Loc.Get("backup.no_backups", "No backups found") };
                empty.AddThemeFontSizeOverride("font_size", 14);
                empty.AddThemeColorOverride("font_color", SC.Gray);
                container.AddChild(empty, false, Node.InternalMode.Disabled);
                return;
            }

            foreach (var fi in allBackups.Take(50))
            {
                var row = new HBoxContainer();
                row.AddThemeConstantOverride("separation", 8);
                container.AddChild(row, false, Node.InternalMode.Disabled);

                string size = (fi.Length / 1024.0).ToString("F1") + " KB";
                string date = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm");
                string label = $"{fi.Name}  |  {size}  |  {date}";

                var lbl = new Label { Text = $"  {label}", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                lbl.AddThemeFontSizeOverride("font_size", 12);
                lbl.AddThemeColorOverride("font_color", SC.Cream);
                row.AddChild(lbl, false, Node.InternalMode.Disabled);

                var restoreBtn = LoadoutPanel.CreateActionButton(
                    Loc.Get("backup.restore", "Restore"), SC.Green);
                restoreBtn.CustomMinimumSize = new Vector2(70, 26);
                string path = fi.FullName;
                restoreBtn.Pressed += () =>
                {
                    if (SaveManagerHelper.RestoreFromBackup(path))
                        GD.Print("[MP_PlayerManager] Restored from: " + path);
                };
                row.AddChild(restoreBtn, false, Node.InternalMode.Disabled);

                var deleteBtn = LoadoutPanel.CreateActionButton("x", SC.Red);
                deleteBtn.CustomMinimumSize = new Vector2(26, 26);
                deleteBtn.Pressed += () =>
                {
                    if (SaveManagerHelper.DeleteSaveFile(path))
                        LoadoutPanel.RequestRefresh();
                };
                row.AddChild(deleteBtn, false, Node.InternalMode.Disabled);
            }
        }

        private static int CleanOldBackups(int daysOld)
        {
            int removed = 0;
            try
            {
                string saveRoot = SaveManagerHelper.GetSaveRoot();
                if (string.IsNullOrEmpty(saveRoot)) return 0;

                var cutoff = DateTime.Now.AddDays(-daysOld);
                var dir = new DirectoryInfo(saveRoot);
                var oldBackups = dir.GetFiles("*backup*.save", SearchOption.AllDirectories)
                    .Where(f => f.LastWriteTime < cutoff)
                    .ToList();

                foreach (var fi in oldBackups)
                {
                    if (SaveManagerHelper.DeleteSaveFile(fi.FullName)) removed++;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] CleanOldBackups failed: {ex.Message}");
            }
            return removed;
        }
    }
}