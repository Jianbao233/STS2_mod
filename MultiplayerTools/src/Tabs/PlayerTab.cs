using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;
using MultiplayerTools.Core;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Player list tab. Shows all players in the current run (read-only).
    /// Apply Template per player via the TemplatesTab data.
    /// </summary>
    internal static class PlayerTab
    {
        internal static void Build(VBoxContainer container)
        {
            container.AddChild(MpPanel.CreateSectionHeader(Loc.Get("player.title", "Players in Run")), false, Node.InternalMode.Disabled);

            var player = MpPanel.GetLocalPlayer();
            if (player == null)
            {
                var hintLbl = new Label
                {
                    Text = Loc.Get("not_in_game", "Not in a run"),
                    SizeFlagsVertical = Control.SizeFlags.ExpandFill
                };
                hintLbl.AddThemeFontSizeOverride("font_size", 20);
                hintLbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                container.AddChild(hintLbl, false, Node.InternalMode.Disabled);
                return;
            }

            var runState = RunManager.Instance?.DebugOnlyGetState();
            if (runState == null)
            {
                var lbl = new Label { Text = Loc.Get("not_in_game", "Not in a run"), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
                lbl.AddThemeFontSizeOverride("font_size", 18);
                lbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                container.AddChild(lbl, false, Node.InternalMode.Disabled);
                return;
            }

            var players = runState.Players;
            if (players == null || players.Count == 0)
            {
                var lbl = new Label { Text = Loc.Get("player.no_players", "No players found"), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
                lbl.AddThemeFontSizeOverride("font_size", 16);
                lbl.AddThemeColorOverride("font_color", Panel.Styles.Gray);
                container.AddChild(lbl, false, Node.InternalMode.Disabled);
                return;
            }

            // Table header
            var tableHeader = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            tableHeader.AddThemeConstantOverride("separation", 4);
            container.AddChild(tableHeader, false, Node.InternalMode.Disabled);
            AddHeaderCell(tableHeader, Loc.Get("player.index", "#"), 40);
            AddHeaderCell(tableHeader, Loc.Get("player.name", "Name"), 130);
            AddHeaderCell(tableHeader, Loc.Get("player.steam_id", "Steam ID"), 160);
            AddHeaderCell(tableHeader, Loc.Get("player.hp", "HP"), 90);
            AddHeaderCell(tableHeader, Loc.Get("player.gold", "Gold"), 80);

            var listScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
            listScroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            container.AddChild(listScroll, false, Node.InternalMode.Disabled);
            var listVBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            listVBox.AddThemeConstantOverride("separation", 3);
            listScroll.AddChild(listVBox, false, Node.InternalMode.Disabled);

            var templates = TemplateStorage.LoadAll();

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p == null) continue;
                bool isLocal = p == player;

                // Player data row
                var row = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                row.AddThemeConstantOverride("separation", 4);
                row.CustomMinimumSize = new Vector2(0, 34);
                var rowColor = isLocal ? Panel.Styles.Gold : Panel.Styles.Cream;

                AddCell(row, (i + 1).ToString(), 40, rowColor);
                AddCell(row, GetPlayerDisplayName(p), 130, rowColor);
                AddCell(row, (i + 1).ToString(), 160, Panel.Styles.Gray);

                int curHp = p.Creature?.CurrentHp ?? 0;
                int maxHp = p.Creature?.MaxHp ?? 0;
                AddCell(row, $"{curHp}/{maxHp}", 90, curHp < maxHp / 3 && maxHp > 0 ? Panel.Styles.Red : rowColor);
                AddCell(row, GetPlayerGold(p).ToString(), 80, rowColor);

                listVBox.AddChild(row, false, Node.InternalMode.Disabled);

                // Apply template buttons
                if (templates.Count > 0)
                {
                    var tmplRow = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
                    tmplRow.AddThemeConstantOverride("separation", 6);
                    tmplRow.CustomMinimumSize = new Vector2(0, 30);

                    var applyBtn = MpPanel.CreateActionButton(Loc.Get("player.apply_tmpl", "Apply Template"), Panel.Styles.Green);
                    applyBtn.CustomMinimumSize = new Vector2(110, 26);
                    Player capP = p;
                    applyBtn.Pressed += async () => await TemplateApplier.ApplyToPlayerAsync(capP, templates[0]);
                    tmplRow.AddChild(applyBtn, false, Node.InternalMode.Disabled);

                    foreach (var tmpl in templates.Take(4))
                    {
                        var tmplBtn = MpPanel.CreateActionButton(tmpl.Name, null);
                        tmplBtn.CustomMinimumSize = new Vector2(90, 26);
                        TemplateData capTmpl = tmpl;
                        Player capPlayer = p;
                        tmplBtn.Pressed += async () => await TemplateApplier.ApplyToPlayerAsync(capPlayer, capTmpl);
                        tmplRow.AddChild(tmplBtn, false, Node.InternalMode.Disabled);
                    }
                    listVBox.AddChild(tmplRow, false, Node.InternalMode.Disabled);
                }
            }
        }

        private static string GetPlayerDisplayName(Player p)
        {
            try
            {
                // Try Name property first
                var nameProp = p.GetType().GetProperty("Name");
                if (nameProp != null)
                {
                    var val = nameProp.GetValue(p);
                    if (val is string s && !string.IsNullOrEmpty(s)) return s;
                }
                // Try CharacterName via reflection
                var charNameProp = p.GetType().GetProperty("CharacterName");
                if (charNameProp != null)
                {
                    var val = charNameProp.GetValue(p);
                    if (val is string s && !string.IsNullOrEmpty(s)) return s;
                }
            }
            catch { }
            return "Player";
        }

        private static int GetPlayerGold(Player p)
        {
            try
            {
                var goldProp = p.GetType().GetProperty("Gold");
                if (goldProp != null)
                {
                    var val = goldProp.GetValue(p);
                    if (val is int i) return i;
                }
            }
            catch { }
            return 0;
        }

        private static void AddHeaderCell(HBoxContainer row, string text, float width)
        {
            var lbl = new Label { Text = text, CustomMinimumSize = new Vector2(width, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", Panel.Styles.Gold);
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
        }

        private static void AddCell(HBoxContainer row, string text, float width, Color color)
        {
            var lbl = new Label { Text = text, CustomMinimumSize = new Vector2(width, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
            lbl.AddThemeFontSizeOverride("font_size", 13);
            lbl.AddThemeColorOverride("font_color", color);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(lbl, false, Node.InternalMode.Disabled);
        }
    }
}
