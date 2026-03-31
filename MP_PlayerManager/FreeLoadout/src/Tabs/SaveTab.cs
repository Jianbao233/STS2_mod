using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager.Tabs
{
    /// <summary>
    /// 存档管理 Tab：
    /// 显示存档列表（当前存档 / 历史存档），
    /// 支持切换存档、保存当前存档。
    /// </summary>
    internal static class SaveTab
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
                Loc.Get("save.title", "Save Management")), false, Node.InternalMode.Disabled);

            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("save.current", "Current Save")), false, Node.InternalMode.Disabled);

            string currentPath = SaveManagerHelper.GetCurrentRunSavePath();
            var currentRow = new HBoxContainer();
            currentRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(currentRow, false, Node.InternalMode.Disabled);

            var currentLbl = new Label
            {
                Text = string.IsNullOrEmpty(currentPath)
                    ? Loc.Get("save.no_current", "No current save")
                    : currentPath,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            currentLbl.AddThemeFontSizeOverride("font_size", 12);
            currentLbl.AddThemeColorOverride("font_color", SC.Cream);
            currentRow.AddChild(currentLbl, false, Node.InternalMode.Disabled);

            var saveBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("save.save_now", "Save Now"), SC.Green);
            saveBtn.CustomMinimumSize = new Vector2(100, 32);
            saveBtn.Pressed += () =>
            {
                GD.Print("[MP_PlayerManager] Save requested — RunManager.SaveCurrentRun not available, use in-game save.");
            };
            currentRow.AddChild(saveBtn, false, Node.InternalMode.Disabled);

            var actionRow = new HBoxContainer();
            actionRow.AddThemeConstantOverride("separation", 8);
            container.AddChild(actionRow, false, Node.InternalMode.Disabled);

            var refreshBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("save.refresh", "Refresh"), SC.Gray);
            refreshBtn.CustomMinimumSize = new Vector2(80, 32);
            refreshBtn.Pressed += () => LoadoutPanel.RequestRefresh();
            actionRow.AddChild(refreshBtn, false, Node.InternalMode.Disabled);

            var backupBtn = LoadoutPanel.CreateActionButton(
                Loc.Get("save.backup_now", "Backup Now"), SC.Blue);
            backupBtn.CustomMinimumSize = new Vector2(110, 32);
            backupBtn.Pressed += () =>
            {
                var backup = SaveManagerHelper.BackupCurrentRun();
                if (!string.IsNullOrEmpty(backup))
                    GD.Print("[MP_PlayerManager] Backup created: " + backup);
                else
                    GD.PrintErr("[MP_PlayerManager] Backup failed.");
            };
            actionRow.AddChild(backupBtn, false, Node.InternalMode.Disabled);

            container.AddChild(LoadoutPanel.CreateSectionHeader(
                Loc.Get("save.file_list", "Save Files")), false, Node.InternalMode.Disabled);

            var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(scroll, false, Node.InternalMode.Disabled);

            var listVBox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            listVBox.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(listVBox, false, Node.InternalMode.Disabled);

            BuildSaveFileList(listVBox);
        }

        private static void BuildSaveFileList(VBoxContainer container)
        {
            LoadoutPanel.ClearChildren(container);

            var files = SaveManagerHelper.ScanSaveFiles();
            if (files.Count == 0)
            {
                var empty = new Label { Text = Loc.Get("save.no_files", "No save files found") };
                empty.AddThemeFontSizeOverride("font_size", 14);
                empty.AddThemeColorOverride("font_color", SC.Gray);
                container.AddChild(empty, false, Node.InternalMode.Disabled);
                return;
            }

            foreach (var fi in files)
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
                    Loc.Get("save.restore", "Restore"), SC.Green);
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
    }
}