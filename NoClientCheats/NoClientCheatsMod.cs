using Godot;
using HarmonyLib;
using System;
using System.Collections.Generic;

namespace NoClientCheats;

public static class NoClientCheatsMod
{
    public const string ModId = "NoClientCheats";

    // ── 配置项（运行时值，由 ModConfig 或默认值控制）──────────────────────
    internal static bool BlockEnabled = true;
    internal static bool HideFromModList = true;
    internal static bool ShowNotification = true;
    internal static bool ShowHistoryPanel = true;
    /// <summary>客机作弊被拦截时是否自动唤起历史记录面板。</summary>
    internal static bool ShowHistoryOnCheat = false;
    internal static float NotificationDuration = 5.0f;
    internal static int HistoryMaxRecords = 25;
    internal static Key HistoryToggleKey = Key.F6;

    // ── 内部状态 ─────────────────────────────────────────────────────────
    private static bool _initialized;
    private static bool _harmonyPatched;

    private static CheatNotification _notificationNode;
    private static CheatHistoryPanel _historyPanel;

    private static readonly List<CheatRecord> _historyRecords = new();
    private static readonly object _historyLock = new();

    internal static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;
        ModConfigIntegration.Register();

        _notificationNode = new CheatNotification();
        _historyPanel = new CheatHistoryPanel();

        var tree = Engine.GetMainLoop() as SceneTree;
        tree?.Root?.AddChild(_notificationNode);
        tree?.Root?.AddChild(_historyPanel);

        GD.Print($"[NoClientCheats] Loaded. Block={BlockEnabled} Hide={HideFromModList} "
            + $"Notify={ShowNotification} Dur={NotificationDuration}s "
            + $"History={HistoryMaxRecords} key=F6");
    }

    internal static void ApplyHarmonyPatches()
    {
        if (_harmonyPatched) return;
        _harmonyPatched = true;
        try
        {
            var harmony = new Harmony(ModId);
            harmony.PatchAll();
            GD.Print("[NoClientCheats] Harmony patches applied.");
        }
        catch (System.Exception e)
        {
            GD.PushError($"[NoClientCheats] Harmony patch failed: {e}");
        }
    }

    // ── 拦截记录（通知弹窗 + 历史）────────────────────────────────────────
    /// <summary>记录一次作弊拦截，触发通知弹窗（若开启）并写入历史。</summary>
    /// <param name="senderId">作弊发送者 Steam ID</param>
    /// <param name="senderName">玩家 Steam 名</param>
    /// <param name="characterName">所玩角色（可为空）</param>
    /// <param name="cheatCommand">被拦截的作弊指令</param>
    /// <param name="wasBlocked">是否实际被拦截</param>
    public static void RecordCheat(ulong senderId, string senderName, string characterName, string cheatCommand, bool wasBlocked)
    {
        var time = DateTime.Now.ToString("HH:mm:ss");
        var record = new CheatRecord(time, senderName, characterName ?? "", cheatCommand, senderId, wasBlocked);

        lock (_historyLock)
        {
            _historyRecords.Add(record);
            while (_historyRecords.Count > HistoryMaxRecords * 2)
                _historyRecords.RemoveAt(0);
        }

        if (wasBlocked && ShowNotification)
            CheatNotification.Show(senderName, characterName ?? "", cheatCommand);

        _historyPanel?.CallDeferred("RefreshList");
        if (wasBlocked && ShowHistoryOnCheat)
            _historyPanel?.CallDeferred("ShowPanel");
    }

    /// <summary>返回当前所有历史记录（最新在末尾）。</summary>
    public static List<CheatRecord> GetHistoryRecords()
    {
        lock (_historyLock)
            return new List<CheatRecord>(_historyRecords);
    }

    /// <summary>返回历史记录总数。</summary>
    public static int GetHistoryCount()
    {
        lock (_historyLock)
            return _historyRecords.Count;
    }

    /// <summary>清空历史记录。</summary>
    public static void ClearHistory()
    {
        lock (_historyLock)
            _historyRecords.Clear();
        _historyPanel?.CallDeferred("RefreshList");
    }

    /// <summary>从字符串设置历史面板快捷键（如 "F6"、"F9"）。</summary>
    public static void SetHistoryKey(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return;
        var kl = (Key)Enum.Parse(typeof(Key), keyName, ignoreCase: true);
        HistoryToggleKey = kl;
    }
}

/// <summary>单条作弊拦截记录。</summary>
public record CheatRecord(string Time, string SenderName, string CharacterName, string Command, ulong SenderId, bool WasBlocked);
