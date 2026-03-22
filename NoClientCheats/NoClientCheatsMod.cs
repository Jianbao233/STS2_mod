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
    internal static Key HistoryToggleKey = Key.F9;

    // ── 内部状态 ─────────────────────────────────────────────────────────
    private static bool _initialized;
    private static bool _harmonyPatched;

    private static CheatNotification _notificationNode;
    private static CheatHistoryPanel _historyPanel;
    private static InputHandlerNode _inputHandler;

    private static readonly List<CheatRecord> _historyRecords = new();
    private static readonly object _historyLock = new();

    // ── 公开入口 ─────────────────────────────────────────────────────────
    /// <summary>切换历史面板显示状态（由 InputHandlerNode 每帧轮询调用）。</summary>
    public static void ToggleHistoryPanel()
    {
        if (!ShowHistoryPanel) return;
        EnsureHistoryPanelCreated();
        if (_historyPanel != null && GodotObject.IsInstanceValid(_historyPanel))
            _historyPanel.TogglePanel();
    }

    /// <summary>显示历史面板。</summary>
    public static void ShowHistoryPanelUI()
    {
        if (!ShowHistoryPanel) return;
        EnsureHistoryPanelCreated();
        if (_historyPanel != null && GodotObject.IsInstanceValid(_historyPanel))
            _historyPanel.ShowPanel();
    }

    // ── 初始化（仅执行一次）──────────────────────────────────────────────
    internal static void EnsureInitialized()
    {
        if (_initialized) return;
        _initialized = true;

        ModConfigIntegration.Register();

        // 通知弹窗立即创建（节点很轻量，随时可能触发）
        _notificationNode = new CheatNotification();
        var tree = Engine.GetMainLoop() as SceneTree;
        tree?.Root?.CallDeferred(Node.MethodName.AddChild, _notificationNode);

        // InputHandler 立即创建并常驻（热键始终监听）
        EnsureInputHandler();

        GD.Print($"[NoClientCheats] Loaded. Block={BlockEnabled} Hide={HideFromModList} "
            + $"Notify={ShowNotification} Dur={NotificationDuration}s "
            + $"History={HistoryMaxRecords} key={GetHistoryKeyDisplayName()}");
    }

    /// <summary>确保 InputHandlerNode 存在且已加入树中。</summary>
    internal static void EnsureInputHandler()
    {
        if (!GodotObject.IsInstanceValid(_inputHandler))
        {
            _inputHandler = new InputHandlerNode();
        }
        if (_inputHandler.GetParent() == null)
        {
            var tree = Engine.GetMainLoop() as SceneTree;
            tree?.Root?.CallDeferred(Node.MethodName.AddChild, _inputHandler);
        }
    }

    /// <summary>确保历史面板节点已创建（延迟到首次使用时）。</summary>
    internal static void EnsureHistoryPanelCreated()
    {
        if (GodotObject.IsInstanceValid(_historyPanel)) return;
        _historyPanel = new CheatHistoryPanel();
        var tree = Engine.GetMainLoop() as SceneTree;
        tree?.Root?.CallDeferred(Node.MethodName.AddChild, _historyPanel);
    }

    /// <summary>销毁历史面板（可在不需要时释放资源）。</summary>
    internal static void DestroyHistoryPanel()
    {
        if (GodotObject.IsInstanceValid(_historyPanel))
            _historyPanel.QueueFree();
        _historyPanel = null;
    }

    /// <summary>将历史面板窗口居中（ModConfig 按钮调用）。</summary>
    internal static void CenterHistoryWindow()
    {
        if (!ShowHistoryPanel) return;
        EnsureHistoryPanelCreated();
        if (GodotObject.IsInstanceValid(_historyPanel))
            _historyPanel.CallDeferred("CenterWindow");
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

    // ── 拦截记录 ────────────────────────────────────────────────────────
    /// <summary>记录一次作弊拦截，触发通知弹窗（若开启）并写入历史。</summary>
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

        if (GodotObject.IsInstanceValid(_historyPanel))
            _historyPanel.CallDeferred("RefreshList");
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
        if (GodotObject.IsInstanceValid(_historyPanel))
            _historyPanel.CallDeferred("RefreshList");
    }

    /// <summary>从字符串设置历史面板快捷键（如 "F9"）。</summary>
    public static void SetHistoryKey(string keyName)
    {
        if (string.IsNullOrWhiteSpace(keyName)) return;
        var kl = (Key)Enum.Parse(typeof(Key), keyName, ignoreCase: true);
        HistoryToggleKey = kl;
    }

    /// <summary>从 long（Godot KeyCode）设置历史面板快捷键。</summary>
    public static void SetHistoryKeyFromLong(long keyCode)
    {
        HistoryToggleKey = (Key)keyCode;
    }

    /// <summary>返回当前历史面板快捷键的显示名称。</summary>
    public static string GetHistoryKeyDisplayName()
    {
        return HistoryToggleKey switch
        {
            Key.F1 => "F1", Key.F2 => "F2", Key.F3 => "F3", Key.F4 => "F4",
            Key.F5 => "F5", Key.F6 => "F6", Key.F7 => "F7", Key.F8 => "F8",
            Key.F9 => "F9", Key.F10 => "F10", Key.F11 => "F11", Key.F12 => "F12",
            _ => HistoryToggleKey.ToString()
        };
    }
}

/// <summary>单条作弊拦截记录。</summary>
public record CheatRecord(string Time, string SenderName, string CharacterName, string Command, ulong SenderId, bool WasBlocked);
