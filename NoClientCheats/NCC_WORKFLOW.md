# NCC 工作全流程图

本文档详细说明 **NoClientCheats（NCC）** 从游戏启动到拦截作弊的完整工作流程。

---

## 图 1：游戏启动 → Mod 初始化

```mermaid
sequenceDiagram
    autonumber
    participant G as Godot 引擎
    participant DLL as NCC.dll 加载
    participant Harmony as HarmonyLib
    participant ModMgr as ModManager.Initialize()
    participant NCC as NCC 核心模块

    Note over G, NCC: 阶段一：游戏启动 & Mod 加载

    G->>DLL: Godot 加载所有 .dll Mod
    DLL->>Harmony: HarmonyLib 扫描所有 [HarmonyPatch] 类
    Harmony->>Harmony: 注册所有 Patch（尚未执行）

    Note over DLL, Harmony: HarmonyPatcher.cs 被扫描<br/>HarmonyPatch 注解触发 PatchAll

    Harmony->>DLL: ModManagerInitPostfix 静态构造函数 执行
    DLL->>NCC: TryScheduleInit() ← 第一次尝试（可能 Engine.GetMainLoop() 为 null，失败静默）
    NCC-->>DLL: _initScheduled = false，静默跳过

    G->>ModMgr: ModManager.Initialize() 同步执行
    ModMgr-->>G: Mod 列表读取完毕，初始化完成
    ModMgr->>Harmony: ModManagerInitPostfix.Postfix() 触发
    Harmony->>NCC: TryScheduleInit() ← 第二次尝试（Engine 已就绪）
    NCC-->>Harmony: _initScheduled = true，注册 OnInitFrame1 到 ProcessFrame

    Note over NCC: 两帧延迟初始化（三重保险）

    G->>NCC: OnInitFrame1() — ProcessFrame 第 1 帧
    NCC->>NCC: 解注册自身 → 注册 OnInitFrame2

    G->>NCC: OnInitFrame2() — ProcessFrame 第 2 帧
    NCC->>NCC: EnsureInitialized() + ApplyHarmonyPatches()
    NCC->>G: 创建 CheatNotification (CanvasLayer, Layer=900)
    NCC->>G: 创建 CheatHistoryPanel (CanvasLayer, Layer=800)
    NCC->>G: AddChild 到 Tree.Root（永久节点）
    NCC->>NCC: ModConfigIntegration.Register() → ModConfig API 注册 15 项配置
    NCC->>Harmony: Harmony.PatchAll() 应用所有 Patch

    Note over NCC: 初始化完成 → 打印日志
    NCC-->>G: [NoClientCheats] Loaded. Block=True Hide=True Notify=True ...
    NCC-->>G: [NoClientCheats] Harmony patches applied.
    NCC-->>G: [禁止客机作弊] ModConfig 注册完成
```

---

## 图 2：ModConfig 依赖（可选）

```mermaid
flowchart TB
    subgraph ModConfig["ModConfig（可选，非核心依赖）"]
        direction TB
        A[主菜单 → 模组配置] --> B[ModConfig.InjectModsTab]
        B --> C{NSettingsPanel<br/>子项结构正常？}
        C -->|是| D[正常显示配置界面]
        C -->|否 v0.1.4| E[Sequence contains no elements<br/>ERROR 弹窗崩溃]
        D --> F[玩家修改 NCC 配置项]
        F --> G[回调写入 NoClientCheatsMod.XXX]
    end

    subgraph NCC_UI["NCC 自建 UI（完全不依赖 ModConfig）"]
        H[CheatNotification<br/>CanvasLayer Layer=900] 
        I[CheatHistoryPanel<br/>CanvasLayer Layer=800]
    end

    G --> H
    G --> I
    G --> J["HideFromModList 配置"]

    style E fill:#ffcccc,stroke:#cc0000
    style ModConfig fill:#fff3e0,stroke:#ff9800
    style NCC_UI fill:#e8f5e9,stroke:#4caf50
```

> **核心结论**：作弊拦截、通知弹窗、历史面板均为 NCC 自建 UI，不经过 ModConfig。ModConfig 仅影响「游戏内配置面板」的 UI 稳定性。

---

## 图 3：联机作弊拦截完整流程

```mermaid
sequenceDiagram
    autonumber
    participant Host as 房主客户端
    participant Client as 客机客户端
    participant Sync as ActionQueueSynchronizer
    participant Patch as ClientCheatBlockPrefix (Harmony)
    participant Mod as NoClientCheatsMod
    participant UI as UI 层<br/>CheatNotification /<br/>CheatHistoryPanel
    participant Net as Steam 联机网络
    participant ModList as ModManager<br/>GetGameplayRelevantModNameList
    participant OtherClient as 其他客机

    Note over Host, OtherClient: 阶段一：联机建立（Mod 列表同步）

    Host->>ModList: 游戏启动时注册 Harmony Patch
    OtherClient->>ModList: 请求获取 Mod 列表
    ModList->>Patch: GetGameplayRelevantModNameList() Postfix
    Patch->>Patch: HideFromModList == true ?
    Patch->>ModList: RemoveAll("NoClientCheats*")
    ModList-->>OtherClient: Mod 列表（不包含 NCC）
    Note over OtherClient: 客机看不到 NCC，无法检测

    Note over Host, OtherClient: 阶段二：客机发送作弊指令

    Client->>Net: 发送 NetConsoleCmdGameAction<br/>cmd="gold 1000"
    Net->>Host: Steam P2P 传输
    Host->>Sync: HandleRequestEnqueueActionMessage()
    Sync->>Patch: Prefix 拦截

    Patch->>Patch: 1️⃣ 提取 message.action → NetConsoleCmdGameAction
    Patch->>Patch: 2️⃣ 提取 action.cmd → "gold 1000"
    Patch->>Patch: 3️⃣ cmdName = "gold"<br/>匹配 CheatCommands[] → isCheat = true
    Patch->>Patch: 4️⃣ 获取玩家名 + 角色名<br/>_GetPlayerNameFromSync / _GetPlayerCharacterFromSync

    alt BlockEnabled == true（拦截模式）
        Patch->>Mod: RecordCheat(senderId, name, char, cmd, wasBlocked=true)
        Mod->>Mod: 写入 _historyRecords
        Mod->>Mod: ShowNotification == true → CheatNotification.Show()
        Mod->>Mod: CallDeferred("RefreshList") → 刷新历史面板
        Mod->>UI: 动画弹出红色通知弹窗（5s 自动消失）
        Patch-->>Sync: return false → 丢弃，不入队，不广播
        Sync-->>OtherClient: 不广播此 Action
        Note over OtherClient: 客机无法获得作弊效果
        Mod-->>Host: [NoClientCheats] Blocked client cheat: 'gold 1000' from xxx
    else BlockEnabled == false（放行模式）
        Patch->>Mod: RecordCheat(..., wasBlocked=false)
        Mod->>Mod: 仍然记录历史（方便排查作弊习惯）
        Patch-->>Sync: return true → 正常入队
        Sync-->>OtherClient: 广播 Action
    end

    Note over Host, OtherClient: 阶段三：作弊检测（终局兜底）

    alt 终局特殊场景
        Patch->>Patch: TryScheduleInit() ← 第三重保险
        Note over Patch: 若终局 Engine.GetMainLoop() 才可用<br/>再次尝试创建 UI 节点
    end
```

---

## 图 4：作弊命令判断逻辑

```mermaid
flowchart TD
    A[提取 action.cmd] --> B{命令为空？}
    B -->|是| Z[return true 放行]
    B -->|否| C[cmdName = 第一个空格前单词]
    C --> D[遍历 CheatCommands[]]

    D -->|"gold"| E{✅ 匹配}
    D -->|"relic"| E
    D -->|"card"| E
    D -->|"potion"| E
    D -->|"damage"| E
    D -->|"heal"| E
    D -->|"power"| E
    D -->|"kill"| E
    D -->|"win"| E
    D -->|"godmode"| E
    D -->|"stars"| E
    D -->|"room"| E
    D -->|"event"| E
    D -->|"fight"| E
    D -->|"act"| E
    D -->|"travel"| E
    D -->|"ancient"| E
    D -->|"afflict"| E
    D -->|"enchant"| E
    D -->|"upgrade"| E
    D -->|"draw"| E
    D -->|"energy"| E
    D -->|"remove_card"| E
    D -->|"其他"| F[return true 放行]

    E --> G{BlockEnabled?}
    G -->|是| H[记录 → 拦截 → 弹通知]
    G -->|否| I[记录 → 放行]

    style E fill:#c8e6c9,stroke:#4caf50
    style F fill:#ffecb3,stroke:#ffc107
    style H fill:#ffcdd2,stroke:#f44336
    style I fill:#fff3e0,stroke:#ff9800
```

---

## 图 5：初始化三重保险机制

```mermaid
flowchart LR
    subgraph P1["保险 1️⃣：静态构造函数"]
        A1[Harmony PatchAll 时] --> A2[ModManagerInitPostfix static ctor]
        A2 --> A3{TryScheduleInit()<br/>Engine.GetMainLoop() == null ?}
        A3 -->|通常为 null| A4[静默跳过]
        A4 -.-> A5[等待保险 2]
    end

    subgraph P2["保险 2️⃣：ModManager.Initialize Postfix"]
        B1[ModManager.Initialize() 完成后] --> B2[Harmony Postfix 触发]
        B2 --> B3{TryScheduleInit()<br/>Engine.GetMainLoop() 已就绪}
        B3 -->|✅ 成功| B4[注册 OnInitFrame1 → 两帧后执行]
        B4 -.-> C3
    end

    subgraph P3["保险 3️⃣：作弊拦截触发时兜底"]
        C1[客机首次发送作弊指令] --> C2[ClientCheatBlockPrefix.TryScheduleInit()]
        C2 --> C3{确保 UI 节点已创建}
        C3 -.->|两帧内| D[EnsureInitialized()]
    end

    subgraph Init["实际初始化"]
        D --> E[ModConfigIntegration.Register()]
        D --> F[ApplyHarmonyPatches()]
        D --> G[创建 CheatNotification 节点]
        D --> H[创建 CheatHistoryPanel 节点]
    end

    P1 --> P3
    P2 --> Init
    P3 --> Init

    style P1 fill:#fff9c4,stroke:#f9a825
    style P2 fill:#fff9c4,stroke:#f9a825
    style P3 fill:#fff9c4,stroke:#f9a825
    style Init fill:#e8f5e9,stroke:#4caf50
```

---

## 图 6：全局模块依赖关系

```mermaid
flowchart BT
    subgraph Entry["入口"]
        Entry["ModManagerInitPostfix.cs<br/>HarmonyPatcher.cs"]
    end

    subgraph Core["NoClientCheatsMod.cs（核心）"]
        CM1["BlockEnabled / HideFromModList /<br/>ShowNotification / HistoryMaxRecords ..."]
        CM2["RecordCheat() — 统一入口"]
        CM3["EnsureInitialized()"]
        CM4["ApplyHarmonyPatches()"]
    end

    subgraph Patches["Harmony Patch"]
        P1["ClientCheatBlockPatch.cs<br/>拦截作弊 Action"]
        P2["ModListFilterPatch.cs<br/>隐藏 Mod 检测"]
    end

    subgraph UI["UI 组件（自建，不依赖 ModConfig）"]
        U1["CheatNotification.cs<br/>红色弹窗，顶部居中"]
        U2["CheatHistoryPanel.cs<br/>左下角面板，F6 呼出"]
    end

    subgraph Util["工具"]
        L["CheatLocHelper.cs<br/>本地化汉化"]
        M["ModConfigIntegration.cs<br///ModConfig API 注册（可选）"]
    end

    Entry --> CM3
    CM3 --> CM4
    CM4 --> P1
    CM4 --> P2
    CM2 --> U1
    CM2 --> U2
    CM2 --> L
    CM3 --> M

    P1 --> CM2
    P2 --> CM1

    style Entry fill:#e3f2fd,stroke:#1565c0
    style Core fill:#e8f5e9,stroke:#4caf50
    style Patches fill:#fce4ec,stroke:#c62828
    style UI fill:#fff3e0,stroke:#ef6c00
    style Util fill:#f3e5f5,stroke:#7b1fa2
```
