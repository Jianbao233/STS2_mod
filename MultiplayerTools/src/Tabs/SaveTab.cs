using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Save management tab. Browse all Steam profiles and their save files.
    /// </summary>
    internal static class SaveTab
    {
        private static List<SaveProfile> _profiles = new();
        private static SaveProfile? _selectedProfile;
        private static SaveInfo? _selectedSave;
        private static VBoxContainer? _listVBox;
        private static VBoxContainer? _detailVBox;

        internal static void Build(VBoxContainer container)
        {
            if (_listVBox != null && !GodotObject.IsInstanceValid(_listVBox)) _listVBox = null;
            if (_detailVBox != null && !GodotObject.IsInstanceValid(_detailVBox)) _detailVBox = null;

            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("save.title", "Save Management")), false, Node.InternalMode.Disabled);

            var hbox = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            hbox.AddThemeConstantOverride("separation", 12);
            container.AddChild(hbox, false, Node.InternalMode.Disabled);

            // Left: profile/save list
            var leftCol = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(320, 0),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsStretchRatio = 0.45f
            };
            leftCol.AddThemeConstantOverride("separation", 4);
            hbox.AddChild(leftCol, false, Node.InternalMode.Disabled);

            // Toolbar
            var toolbar = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            toolbar.AddThemeConstantOverride("separation", 6);
            leftCol.AddChild(toolbar, false, Node.InternalMode.Disabled);

            var refreshBtn = MpPanel.CreateActionButton(Loc.Get("save.refresh", "Refresh"), Panel.Styles.Blue);
            refreshBtn.CustomMinimumSize = new Vector2(80, 30);
            refreshBtn.Pressed += () => { _profiles = SaveManagerHelper.GetAllProfiles(); RebuildList(leftCol); };
            toolbar.AddChild(refreshBtn, false, Node.InternalMode.Disabled);

            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            leftCol.AddChild(scroll, false, Node.InternalMode.Disabled);
            _listVBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ShrinkBegin };
            _listVBox.AddThemeConstantOverride("separation", 3);
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
                Text = Loc.Get("save.select_hint", "Click a save to view details"),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill
            };
            hintLbl.AddThemeFontSizeOverride("font_size", 16);
            hintLbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
            _detailVBox.AddChild(hintLbl, false, Node.InternalMode.Disabled);

            _profiles = SaveManagerHelper.GetAllProfiles();
            RebuildList(leftCol);
        }

        private static void RebuildList(VBoxContainer listContainer)
        {
            if (_listVBox == null || !GodotObject.IsInstanceValid(_listVBox)) return;
            MpPanel.ClearChildren(_listVBox);

            if (_profiles.Count == 0)
            {
                var lbl = new Label { Text = Loc.Get("save.no_saves", "No saves found") };
                lbl.AddThemeFontSizeOverride("font_size", 14);
                lbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                _listVBox.AddChild(lbl, false, Node.InternalMode.Disabled);
                return;
            }

            foreach (var profile in _profiles)
            {
                // Profile header
                var profileHeader = new Label
                {
                    Text = $"  [{profile.SteamId}] {profile.ProfileName}",
                    CustomMinimumSize = new Vector2(0, 26)
                };
                profileHeader.AddThemeFontSizeOverride("font_size", 13);
                profileHeader.AddThemeColorOverride("font_color", Panel.Styles.Gold);
                _listVBox.AddChild(profileHeader, false, Node.InternalMode.Disabled);

                foreach (var save in profile.Saves.OrderByDescending(s => s.LastWriteTime).Take(5))
                {
                    var saveBtn = new Button
                    {
                        Text = $"    {save.FileName}",
                        CustomMinimumSize = new Vector2(0, 28),
                        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                    };
                    saveBtn.AddThemeFontSizeOverride("font_size", 12);
                    saveBtn.AddThemeColorOverride("font_color", _selectedSave == save ? Panel.Styles.Gold : Panel.Styles.Cream);
                    saveBtn.AddThemeColorOverride("font_hover_color", Panel.Styles.Gold);
                    Panel.Styles.ApplyFlatButton(saveBtn);
                    SaveInfo captured = save;
                    SaveProfile capProfile = profile;
                    saveBtn.Pressed += () => SelectSave(capProfile, captured);
                    _listVBox.AddChild(saveBtn, false, Node.InternalMode.Disabled);
                }
            }
        }

        private static void SelectSave(SaveProfile profile, SaveInfo save)
        {
            _selectedProfile = profile;
            _selectedSave = save;
            RebuildList(_listVBox!);
            ShowSaveDetail(profile, save);
        }

        private static void ShowSaveDetail(SaveProfile profile, SaveInfo save)
        {
            if (_detailVBox == null || !GodotObject.IsInstanceValid(_detailVBox)) return;
            MpPanel.ClearChildren(_detailVBox);

            _detailVBox.AddChild(MpPanel.CreateSectionHeader(save.FileName), false, Node.InternalMode.Disabled);

            var infoGrid = new VBoxContainer();
            infoGrid.AddThemeConstantOverride("separation", 6);
            _detailVBox.AddChild(infoGrid, false, Node.InternalMode.Disabled);

            AddDetailRow(infoGrid, Loc.Get("save.timestamp", "Last Modified"), save.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"));
            AddDetailRow(infoGrid, Loc.Get("save.size", "Size"), SaveManagerHelper.FormatSize(save.SizeBytes));
            AddDetailRow(infoGrid, "Steam ID", profile.SteamId);
            AddDetailRow(infoGrid, "Profile", profile.ProfileName);

            var btnRow = new HBoxContainer();
            btnRow.AddThemeConstantOverride("separation", 8);
            _detailVBox.AddChild(btnRow, false, Node.InternalMode.Disabled);

            var loadBtn = MpPanel.CreateActionButton(Loc.Get("save.load", "Load"), Panel.Styles.Green);
            loadBtn.CustomMinimumSize = new Vector2(80, 32);
            loadBtn.Pressed += () => ShowStatusMsg(Loc.Get("save.loaded", "Save loaded"), Panel.Styles.Green);
            btnRow.AddChild(loadBtn, false, Node.InternalMode.Disabled);

            var delBtn = MpPanel.CreateActionButton(Loc.Get("save.delete", "Delete"), Panel.Styles.Red);
            delBtn.CustomMinimumSize = new Vector2(70, 32);
            delBtn.Pressed += () => OnDeleteSave(profile, save);
            btnRow.AddChild(delBtn, false, Node.InternalMode.Disabled);
        }

        private static void AddDetailRow(VBoxContainer parent, string label, string value)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var lbl = new Label { Text = label + ":", CustomMinimumSize = new Vector2(120, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.Cream);
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
            var val = new Label { Text = value };
            val.AddThemeFontSizeOverride("font_size", 13);
            val.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            row.AddChild(val, false, Node.InternalMode.Disabled);
            parent.AddChild(row, false, Node.InternalMode.Disabled);
        }

        private static void ShowStatusMsg(string text, Color color)
        {
            if (_detailVBox == null || !GodotObject.IsInstanceValid(_detailVBox)) return;
            var msg = new Label { Text = text };
            msg.AddThemeFontSizeOverride("font_size", 16);
            msg.AddThemeColorOverride("font_color", color);
            _detailVBox.AddChild(msg, false, Node.InternalMode.Disabled);
        }

        private static void OnDeleteSave(SaveProfile profile, SaveInfo save)
        {
            try
            {
                if (File.Exists(save.Path)) File.Delete(save.Path);
                _profiles = SaveManagerHelper.GetAllProfiles();
                _selectedSave = null;
                RebuildList(_listVBox!);
                ShowStatusMsg(Loc.Get("save.deleted", "Save deleted"), Panel.Styles.Red);
                GD.Print("[MultiplayerTools] Deleted save: " + save.Path);
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] OnDeleteSave failed: " + ex.Message);
            }
        }
    }
}
