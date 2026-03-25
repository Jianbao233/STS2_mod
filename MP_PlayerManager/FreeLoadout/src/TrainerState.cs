using System;
using System.Linq;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace MP_PlayerManager
{
    /// <summary>
    /// Trainer 状态管理：无限血量/能量/金币等运行时数据。
    /// </summary>
    internal static class TrainerState
    {
        public static int CurrentHpLockValue { get; set; } = 99;
        public static int MaxHpLockValue { get; set; } = 99;
        public static int CurrentEnergyLockValue { get; set; } = 10;
        public static int MaxEnergyLockValue { get; set; } = 10;
        public static int StarsLockValue { get; set; } = 99;
        public static int GoldLockValue { get; set; } = 999;
        public static bool InfiniteHpEnabled { get; private set; }
        public static bool InfiniteEnergyEnabled { get; private set; }
        public static bool InfiniteStarsEnabled { get; private set; }
        public static bool InfiniteGoldEnabled { get; private set; }

        public static bool IsTrainerHotkey(InputEvent inputEvent, Key key)
        {
            if (inputEvent is InputEventKey k && k.Pressed && !k.Echo)
                return k.Keycode == key;
            return false;
        }

        public static bool IsTrainerHotkey(InputEvent inputEvent, HotkeyBinding binding)
        {
            return inputEvent is InputEventKey k && k.Pressed && !k.Echo && binding.Matches(k);
        }

        public static bool IsAnyTrainerHotkey(InputEvent inputEvent)
        {
            return inputEvent is InputEventKey k && k.Pressed && !k.Echo && Config.MatchesAny(k);
        }

        public static void ToggleInfiniteHp() { InfiniteHpEnabled = !InfiniteHpEnabled; }
        public static void ToggleInfiniteEnergy() { InfiniteEnergyEnabled = !InfiniteEnergyEnabled; }
        public static void ToggleInfiniteStars() { InfiniteStarsEnabled = !InfiniteStarsEnabled; }
        public static void ToggleInfiniteGold() { InfiniteGoldEnabled = !InfiniteGoldEnabled; }

        public static void SetHpOnce(Player player, int currentHp, int maxHp)
        {
            maxHp = Math.Max(1, maxHp);
            currentHp = (int)Math.Clamp(currentHp, 1, maxHp);
            player.Creature.SetMaxHpInternal(maxHp);
            player.Creature.SetCurrentHpInternal(currentHp);
        }

        public static void SetEnergyOnce(Player player, int currentEnergy, int maxEnergy)
        {
            player.MaxEnergy = Math.Max(0, maxEnergy);
            player.PlayerCombatState.Energy = Math.Max(0, currentEnergy);
        }

        public static void SetStarsOnce(Player player, int stars)
        {
            player.PlayerCombatState.Stars = Math.Max(0, stars);
        }

        public static void SetGoldOnce(Player player, int gold)
        {
            player.Gold = Math.Max(0, gold);
        }

        public static bool TryTogglePanel(NGame game, InputEvent inputEvent)
        {
            if (!IsTrainerHotkey(inputEvent, Config.GetHotkey("toggle_panel"))) return false;
            game.GetViewport()?.SetInputAsHandled();
            LoadoutPanel.Toggle();
            return true;
        }

        public static void Apply(NRun run)
        {
            if (!InfiniteHpEnabled && !InfiniteEnergyEnabled && !InfiniteStarsEnabled && !InfiniteGoldEnabled) return;

            FieldInfo? fi = _runStateField ?? AccessTools.Field(typeof(NRun), "_state");
            if (fi == null) return;
            _runStateField ??= fi;

            var runState = fi.GetValue(run) as RunState;
            if (runState == null) return;

            foreach (var player in runState.Players)
            {
                if (InfiniteHpEnabled)
                {
                    int maxHp = Math.Max(1, MaxHpLockValue);
                    if (player.Creature.MaxHp != maxHp) player.Creature.SetMaxHpInternal(maxHp);
                    int curHp = (int)Math.Clamp(CurrentHpLockValue, 1, maxHp);
                    if (player.Creature.CurrentHp != curHp) player.Creature.SetCurrentHpInternal(curHp);
                }
                if (InfiniteEnergyEnabled)
                {
                    if (player.MaxEnergy != MaxEnergyLockValue) player.MaxEnergy = MaxEnergyLockValue;
                    if (player.PlayerCombatState != null && player.PlayerCombatState.Energy != CurrentEnergyLockValue)
                        player.PlayerCombatState.Energy = CurrentEnergyLockValue;
                }
                if (InfiniteStarsEnabled && player.PlayerCombatState != null && player.PlayerCombatState.Stars != StarsLockValue)
                    player.PlayerCombatState.Stars = StarsLockValue;
                if (InfiniteGoldEnabled && player.Gold != GoldLockValue)
                    player.Gold = GoldLockValue;
            }

            var me = LocalContext.GetMe(runState) ?? runState.Players.FirstOrDefault();
            if (me != null) TrainerBuffUi.Sync(me);
        }

        private static FieldInfo? _runStateField;
    }
}
