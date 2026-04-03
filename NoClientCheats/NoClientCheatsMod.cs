using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using Godot;
using HarmonyLib;
using static NoClientCheats.Localization;

namespace NoClientCheats;

public static class NoClientCheatsMod
{
    public const string ModId = "NoClientCheats";

    // ── 配置项（运行时值，由 ModConfig 或默认值控制）──────────────────────
    internal static bool BlockEnabled = true;
    internal static bool HideFromModList = true;
    internal static bool ShowNotification = true;
    /// <summary>顶栏按钮是否显示（控制游戏顶栏呼出按钮）。</summary>
    internal static bool ShowTopBarButton = true;
    /// <summary>客机作弊被拦截时是否自动唤起历史记录面板。</summary>
    internal static bool ShowHistoryOnCheat = false;
    /// <summary>作弊拦截时是否广播到大厅房间聊天。</summary>
    internal static bool BroadcastToLobby = false;
    internal static float NotificationDuration = 5.0f;
    internal static int HistoryMaxRecords = 25;
    internal static Key HistoryToggleKey = Key.F6;

    // ── 内部状态 ─────────────────────────────────────────────────────────
    private static bool _initialized;
    private static bool _harmonyPatched;

    private static CheatNotification _notificationNode;
    private static CheatHistoryPanel _historyPanel;
    private static InputHandlerNode _inputHandler;

    private static readonly List<CheatRecord> _historyRecords = new();
    private static readonly object _historyLock = new();

    // ── 当前正在处理的远程玩家 NetId（通过 AsyncLocal 在 HandleRequestEnqueueActionMessage → ChooseOption 间传递）────
    // 此值在一次 GameAction 的执行上下文中有效，本地玩家动作为 0
    private static readonly AsyncLocal<ulong> _currentRemotePlayerNetId = new();

    // ── 卡组快照（用于检测休息点/事件作弊）──────────────────────────────────
    // Key: 玩家 NetId，Value: SerializablePlayer（预期卡组状态）+ 时间戳
    private static readonly Dictionary<ulong, (object snapshot, long timestampMs)> _expectedDeckSnapshots = new();
    private static readonly Dictionary<ulong, (object snapshot, long timestampMs)> _preDeckSnapshots = new();
    private static readonly object _snapshotLock = new();

    // ── 公开入口 ─────────────────────────────────────────────────────────
    /// <summary>切换历史面板显示状态（由 InputHandlerNode 每帧轮询调用）。</summary>
    public static void ToggleHistoryPanel()
    {
        EnsureHistoryPanelCreated();
        if (_historyPanel != null && GodotObject.IsInstanceValid(_historyPanel))
            _historyPanel.TogglePanel();
    }

    /// <summary>显示历史面板。</summary>
    public static void ShowHistoryPanelUI()
    {
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
            // PatchAll / TargetMethod 在模组加载线程执行，禁止此处调用 GD.Print（会触发「不可在加载线程运行」）
            ThreadSafeLog("[NoClientCheats] Harmony patches applied.");
        }
        catch (System.Exception e)
        {
            ThreadSafeLog($"[NoClientCheats] Harmony patch failed: {e}");
        }
    }

    /// <summary>
    /// 文件日志，绕过 Godot Engine API，在任何线程都可用。
    /// 写入 %APPDATA%\SlayTheSpire2\NCC_diag.log。
    /// </summary>
    private static readonly System.IO.StreamWriter _diagWriter;

    static NoClientCheatsMod()
    {
        try
        {
            var appData = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "SlayTheSpire2");
            var logPath = Path.Combine(dir, "NCC_diag.log");
            _diagWriter = new System.IO.StreamWriter(logPath, false, System.Text.Encoding.UTF8);
            _diagWriter.WriteLine($"=== NCC {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
            _diagWriter.AutoFlush = true;
        }
        catch
        {
            // 静默失败
        }
    }

    internal static void ThreadSafeLog(string line)
    {
        try { _diagWriter?.WriteLine(line); } catch { }
        try
        {
            if (Engine.Singleton != null) GD.Print(line);
        }
        catch { }
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

        // 作弊拦截时广播到大厅房间聊天
        LanConnectBridge.Broadcast(record);
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

    // ── 卡组快照管理（供 DeckSyncPatches 调用）──────────────────────────────

    /// <summary>
    /// 记录客机在休息点/事件后的预期卡组快照。
    /// 由 RestSiteSynchronizer.ChooseOption Postfix 调用。
    /// </summary>
    internal static void SetExpectedDeckSnapshot(ulong netId, object serializablePlayer)
    {
        lock (_snapshotLock)
        {
            _expectedDeckSnapshots[netId] = (serializablePlayer, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
    }

    /// <summary>
    /// 获取并清除指定玩家的预期卡组快照。
    /// 由 CombatStateSynchronizer.OnSyncPlayerMessageReceived Prefix 调用。
    /// 返回快照，不存在则返回 null。
    /// </summary>
    internal static object GetAndClearExpectedDeckSnapshot(ulong netId)
    {
        lock (_snapshotLock)
        {
            if (_expectedDeckSnapshots.TryGetValue(netId, out var entry))
            {
                _expectedDeckSnapshots.Remove(netId);
                return entry.snapshot;
            }
            return null;
        }
    }

    /// <summary>
    /// 清除所有卡组快照（在进入新地图节点前调用）。
    /// </summary>
    internal static void ClearAllDeckSnapshots()
    {
        lock (_snapshotLock)
        {
            _expectedDeckSnapshots.Clear();
            _preDeckSnapshots.Clear();
        }
    }

    // ── Pre-Deck 快照管理 ─────────────────────────────────────────────────

    /// <summary>
    /// 存储客机在进入战斗前的卡组快照（用于 pre/post 即时对比）。
    /// </summary>
    internal static void SetPreDeckSnapshot(ulong netId, object snapshot)
    {
        lock (_snapshotLock)
        {
            _preDeckSnapshots[netId] = (snapshot, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
    }

    /// <summary>
    /// 获取并原子性移除预存的前置卡组快照。
    /// </summary>
    internal static object ConsumePreDeckSnapshot(ulong netId)
    {
        lock (_snapshotLock)
        {
            if (_preDeckSnapshots.TryGetValue(netId, out var entry))
            {
                _preDeckSnapshots.Remove(netId);
                return entry.snapshot;
            }
            return null;
        }
    }

    // ── 远程玩家上下文（通过 AsyncLocal 在 HandleRequestEnqueueActionMessage → ChooseOption 间传递）────

    /// <summary>
    /// 在处理远程玩家 action 时设置当前远程 NetId，结束时清除。
    /// 供 ClientCheatBlockPatch 在每次处理远程消息时调用。
    /// </summary>
    internal static void SetCurrentRemotePlayer(ulong netId)
    {
        _currentRemotePlayerNetId.Value = netId;
    }

    /// <summary>
    /// 获取当前正在处理的远程玩家 NetId。本地动作为 0。
    /// </summary>
    internal static ulong GetCurrentRemotePlayer()
    {
        return _currentRemotePlayerNetId.Value;
    }

    /// <summary>
    /// 清除当前远程玩家上下文。
    /// </summary>
    internal static void ClearCurrentRemotePlayer()
    {
        _currentRemotePlayerNetId.Value = 0;
    }

    // ── 快照过期清理 ──────────────────────────────────────────────────────

    /// <summary>
    /// 清理所有超过指定秒数的过期快照。
    /// </summary>
    internal static void CleanupExpiredSnapshots(int timeoutSeconds = 600)
    {
        lock (_snapshotLock)
        {
            long cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (timeoutSeconds * 1000L);

            var expiredPre = _preDeckSnapshots
                .Where(kv => kv.Value.timestampMs < cutoff)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var k in expiredPre)
                _preDeckSnapshots.Remove(k);

            var expiredExpected = _expectedDeckSnapshots
                .Where(kv => kv.Value.timestampMs < cutoff)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var k in expiredExpected)
                _expectedDeckSnapshots.Remove(k);
        }
    }

    /// <summary>
    /// 检测当前是否为联机主机。
    /// 通过 INetGameService.Type == NetGameType.Host 判断。
    /// </summary>
    internal static bool IsMultiplayerHost()
    {
        // #region NCC_DIAG_HOSTCHECK
        var diag = new System.Text.StringBuilder();
        diag.Append("[IsMultiplayerHost] ");
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var netServiceType = asm.GetType("MegaCrit.Sts2.Core.Multiplayer.INetGameService")
                    ?? asm.GetType("INetGameService");
                if (netServiceType == null) continue;

                var netGameType = asm.GetType("MegaCrit.Sts2.Core.Multiplayer.NetGameType")
                    ?? asm.GetType("NetGameType");
                if (netGameType == null) continue;

                var hostField = netGameType.GetField("Host",
                    BindingFlags.Public | BindingFlags.Static);
                if (hostField == null) continue;
                var hostValue = hostField.GetValue(null);
                int hostInt = Convert.ToInt32(hostValue);
                diag.Append($"hostInt={hostInt} ");

                var runManagerType = asm.GetType("MegaCrit.Sts2.Core.Runs.RunManager")
                    ?? asm.GetType("RunManager");
                if (runManagerType == null) continue;

                var instProp = runManagerType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
                var rm = instProp?.GetValue(null);
                if (rm == null) { diag.Append("RunManager.Instance=null "); continue; }
                diag.Append("RunManager.Instance=found ");

                var netServiceProp = rm.GetType().GetProperty("NetService",
                    BindingFlags.Public | BindingFlags.Instance);
                var netService = netServiceProp?.GetValue(rm);
                if (netService == null) { diag.Append("NetService=null "); continue; }
                diag.Append($"NetService.type={netService.GetType().Name} ");

                var typeProp = netService.GetType().GetProperty("Type",
                    BindingFlags.Public | BindingFlags.Instance);
                var netType = typeProp?.GetValue(netService);
                if (netType == null) { diag.Append("netType=null "); continue; }
                int currentInt = Convert.ToInt32(netType);
                diag.Append($"currentInt={currentInt} ");

                bool result = currentInt == hostInt;
                diag.Append($"result={result} ");
                GD.Print($"[NCC|DIAG] {diag}");
                return result;
            }
            diag.Append("no matching assembly ");
        }
        catch (Exception ex) { diag.Append($"EXCEPTION:{ex.Message} "); }
        GD.Print($"[NCC|DIAG] {diag}");
        return false;
        // #endregion
    }
}

/// <summary>单条作弊拦截记录。</summary>
public record CheatRecord(string Time, string SenderName, string CharacterName, string Command, ulong SenderId, bool WasBlocked);

/// <summary>
/// 与 STS2 LAN Connect 大厅 MOD 的桥接。
/// 通过反射调用 LanConnectLobbyRuntime，实现作弊拦截通知广播到房间聊天。
/// 不编译期依赖大厅 MOD，完全运行时解耦。
/// </summary>
internal static class LanConnectBridge
{
    private static Node _runtime;
    private static MethodInfo _sendChatMethod;
    private static PropertyInfo _hasRoomProp;
    private static bool _typeResolved;
    private static bool _bridgeSuccessLogged;

    /// <summary>确保已解析大厅运行时引用。程序集类型只解析一次；运行时实例每次调用时刷新。</summary>
    public static void EnsureInitialized()
    {
        try
        {
            // 程序集类型解析只做一次
            if (!_typeResolved)
            {
                var runtimeType = Type.GetType(
                    "Sts2LanConnect.Scripts.LanConnectLobbyRuntime, sts2_lan_connect");
                if (runtimeType == null)
                {
                    GD.Print("[NoClientCheats] LanConnect: 运行时类型未找到（大厅 MOD 未安装？跳过桥接。");
                    return;
                }

                // 大厅源码中 SendRoomChatMessageAsync / HasActiveRoomSession / Instance 均为 internal，
                // 仅用 Public 会取不到（_sendChatMethod 恒为 null，广播静默失败）。
                const BindingFlags inst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                _sendChatMethod = runtimeType.GetMethod(
                    "SendRoomChatMessageAsync",
                    inst,
                    null, new[] { typeof(string) }, null);
                _hasRoomProp = runtimeType.GetProperty("HasActiveRoomSession", inst);
                if (_sendChatMethod == null)
                    GD.Print("[NoClientCheats] LanConnect: SendRoomChatMessageAsync 反射未找到（检查大厅版本）");
                if (_hasRoomProp == null)
                    GD.Print("[NoClientCheats] LanConnect: HasActiveRoomSession 反射未找到");
                _typeResolved = true;
            }

            if (_sendChatMethod == null || _hasRoomProp == null) return;

            // 运行时实例随房间变化，每次重新获取
            var rt2 = Type.GetType(
                "Sts2LanConnect.Scripts.LanConnectLobbyRuntime, sts2_lan_connect");
            const BindingFlags statFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var instanceProp = rt2?.GetProperty("Instance", statFlags);
            var newRuntime = instanceProp?.GetValue(null) as Node;

            if (newRuntime != null && !_bridgeSuccessLogged)
            {
                _runtime = newRuntime;
                GD.Print("[NoClientCheats] LanConnect: 桥接成功，已绑定大厅运行时。");
                _bridgeSuccessLogged = true;
            }
            else
            {
                _runtime = newRuntime;
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[NoClientCheats] LanConnect: 桥接失败: {ex.Message}");
        }
    }

    /// <summary>当前是否处于大厅联机房间中。</summary>
    public static bool IsInLobbyRoom
    {
        get
        {
            if (_sendChatMethod == null) return false;
            var rt = _runtime;
            if (rt == null) return false;
            return (bool)(_hasRoomProp?.GetValue(rt) ?? false);
        }
    }

    /// <summary>
    /// 将作弊拦截广播到大厅房间聊天。
    /// 每调用一次发送一条消息，格式：[作弊拦截] 玩家名 尝试使用 指令
    /// </summary>
    public static void Broadcast(CheatRecord record)
    {
        if (!NoClientCheatsMod.BroadcastToLobby) return;

        EnsureInitialized();
        if (_sendChatMethod == null || _runtime == null)
        {
            GD.Print("[NoClientCheats] LanConnect: 广播跳过（桥接未就绪）");
            return;
        }
        if (!IsInLobbyRoom)
        {
            GD.Print("[NoClientCheats] LanConnect: 广播跳过（未在大厅房间）");
            return;
        }

        try
        {
            string msg;
            if (record.WasBlocked)
                msg = $"{Tr("lobby_blocked")} {record.SenderName} {Tr("tried_use")} {record.Command}";
            else
                msg = $"{Tr("lobby_logged")} {record.SenderName} {Tr("executed")} {record.Command}";

            // 消息最长60字符截断（大厅聊天限制）
            if (msg.Length > 60) msg = msg[..60];

            var task = _sendChatMethod.Invoke(_runtime, new object[] { msg }) as Task;
            if (task != null)
            {
                _ = task.ContinueWith(t =>
                {
                    if (t.IsFaulted)
                        GD.Print($"[NoClientCheats] LanConnect: 广播失败: {t.Exception?.InnerException?.Message ?? t.Exception?.Message}");
                });
            }
        }
        catch (Exception ex)
        {
            GD.Print($"[NoClientCheats] LanConnect: 广播失败: {ex.Message}");
        }
    }
}
