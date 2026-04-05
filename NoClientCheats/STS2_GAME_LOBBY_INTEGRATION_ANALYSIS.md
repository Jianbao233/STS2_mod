# STS2-Game-Lobby 对接分析文档

> 分析对象：`emptylower/STS2-Game-Lobby`（GitHub 公开仓库）
> 分析日期：2026-03-25
> 分析目的：为 NoClientCheats 项目（NCC）与 STS2 LAN Connect 大厅系统对接提供技术参考

---

## 一、项目概述

### 1.1 STS2 LAN Connect 是什么

STS2 LAN Connect 是《Slay the Spire 2》的一个联机大厅方案，由 `emptylower` 团队开发。它包含两个核心组件：

| 组件 | 技术栈 | 职责 |
|------|--------|------|
| **客户端 MOD** (`sts2-lan-connect/`) | Godot 4.5 + C# / .NET 9 | 大厅 UI、建房/加房流程、续局绑定、房间聊天、调试报告 |
| **大厅服务端** (`lobby-service/`) | Node.js 20 + TypeScript | 房间目录、密码校验、房主心跳、房间聊天、控制通道、relay fallback |

当前公开版本：**0.2.2**

项目源码：https://github.com/emptylower/STS2-Game-Lobby
协议：GPL-3.0-only

### 1.2 关键链接

- 官方母面板地址：`http://47.111.146.69:18787`
- 官方默认大厅：`http://47.111.146.69:8787`
- 服务端默认端口：HTTP `8787/TCP`，Relay `39000-39149/UDP`

---

## 二、系统架构分析

### 2.1 整体架构图

```
┌─────────────────────────────────────────────────────────────┐
│                     玩家游戏客户端                           │
│  ┌──────────────────────────────────────────────────────┐   │
│  │              STS2 LAN Connect MOD                     │   │
│  │  ┌──────────────┐  ┌──────────────┐  ┌───────────┐  │   │
│  │  │ LanConnect   │  │ LanConnect   │  │ LanConnect│  │   │
│  │  │ LobbyOverlay │  │ LobbyRuntime │  │  Config   │  │   │
│  │  └──────┬───────┘  └──────┬───────┘  └───────────┘  │   │
│  │         │                 │                          │   │
│  │         └────────┬────────┘                          │   │
│  │                  │ LobbyApiClient                      │   │
│  └──────────────────┼──────────────────────────────────┘   │
└─────────────────────┼────────────────────────────────────────┘
                      │ HTTP / WebSocket
                      ▼
         ┌────────────────────────────┐
         │     lobby-service           │
         │   (Node.js + TypeScript)    │
         │                             │
         │  ┌─────────┐  ┌───────────┐  │
         │  │ Lobby   │  │   Room    │  │
         │  │ Store   │  │  Relay    │  │
         │  └─────────┘  │  Manager  │  │
         │               └───────────┘  │
         │  ┌─────────────────────────┐ │
         │  │    /server-admin        │ │
         │  │    (管理面板)           │ │
         │  └─────────────────────────┘ │
         └──────────────┬────────────────┘
                        │
         ┌──────────────┴──────────────┐
         │        母面板                │
         │  (http://47.111.146.69:18787)│
         └─────────────────────────────┘
```

### 2.2 客户端 MOD 核心模块（`sts2-lan-connect/`）

源码位于 `Scripts/Lobby/` 目录，命名空间 `Sts2LanConnect.Scripts`：

| 文件 | 职责 |
|------|------|
| `Entry.cs` | MOD 入口，安装 config/runtime/overlay/monitor |
| `LanConnectConfig.cs` | 持久化配置（大厅 URL、玩家名、聊天面板位置、续局绑定等） |
| `LanConnectLobbyRuntime.cs` | 核心运行时：房间生命周期、心跳、控制通道连接、房间聊天 |
| `LanConnectLobbyOverlay.cs` | 大厅 UI：大厅列表、房间卡片、创建/加入对话框、公告栏 |
| `LanConnectMultiplayerSaveRoomBinding.cs` | 续局存档与大厅房间的双向绑定逻辑 |
| `LanConnectLobbyPlayerNameDirectory.cs` | 房间内玩家名目录管理 |
| `LanConnectRuntimeMonitor.cs` | 运行时监控和调试报告生成 |

### 2.3 服务端核心模块（`lobby-service/src/`）

| 文件 | 职责 |
|------|------|
| `server.ts` | HTTP API 路由、WebSocket 控制通道、relay 管理 |
| `store.ts` | 房间状态、票据、会话、版本/Mod 校验 |
| `relay.ts` | UDP Relay 分配与会话管理 |
| `server-admin-ui.ts` | 管理面板 HTML 渲染 |
| `server-admin-sync.ts` | 向母面板同步心跳和公开列表申请 |

---

## 三、API 接口详解

### 3.1 HTTP REST API

服务端暴露以下端点（端口 8787）：

#### 房间管理

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/health` | 健康检查，返回房间数、带宽、relay 状态 |
| `GET` | `/probe` | 简单探活 |
| `GET` | `/rooms` | 获取房间列表 |
| `GET` | `/announcements` | 获取大厅公告 |
| `POST` | `/rooms` | **创建房间** |
| `POST` | `/rooms/:id/join` | **申请加入房间** |
| `POST` | `/rooms/:id/heartbeat` | **房主心跳保活** |
| `DELETE` | `/rooms/:id` | 删除房间 |
| `POST` | `/rooms/:id/connection-events` | 上报连接阶段事件 |

#### 管理面板

| 方法 | 路径 | 说明 |
|------|------|------|
| `GET` | `/server-admin` | 管理面板 HTML |
| `POST` | `/server-admin/login` | 登录 |
| `GET` | `/server-admin/settings` | 获取设置 |
| `PATCH` | `/server-admin/settings` | 修改设置 |

### 3.2 POST /rooms（创建房间）请求体

```typescript
interface CreateRoomInput {
  roomName: string;          // 房间名（最多32字符）
  password?: string;         // 可选密码（最多10字符）
  hostPlayerName: string;    // 房主显示名
  gameMode: string;         // "standard" | "custom" | "daily"
  version: string;           // 游戏版本字符串
  modVersion: string;        // MOD 版本字符串
  modList?: string[];        // MOD 列表（哈希排序）
  maxPlayers: number;        // 最大玩家数
  hostConnectionInfo: {
    enetPort: number;         // ENet 端口（通常 33771）
    localAddresses?: string[]; // 局域网地址列表
  };
  savedRun?: {               // 可选：续局绑定
    saveKey: string;
    slots: SavedRunSlotInput[];
    connectedPlayerNetIds?: string[];
  };
}
```

### 3.3 POST /rooms/:id/join（加入房间）请求体

```typescript
interface JoinRoomInput {
  playerName: string;
  password?: string;
  version: string;
  modVersion: string;
  modList?: string[];
  desiredSavePlayerNetId?: string;  // 续局时指定要接管的角色槽位
}
```

**错误码体系**：

| code | 说明 |
|------|------|
| `version_mismatch` | 游戏版本不一致 |
| `mod_version_mismatch` | MOD 版本不一致 |
| `mod_mismatch` | MOD 列表不一致（会返回 missingModsOnLocal/missingModsOnHost） |
| `room_started` | 房间已开始游戏 |
| `room_full` | 房间已满 |
| `invalid_password` | 密码错误 |
| `save_slot_required` | 续局房间需要先选择角色槽位 |
| `save_slot_unavailable` | 续局角色槽位已被占用 |

### 3.4 POST /rooms/:id/heartbeat（心跳）请求体

```typescript
interface HeartbeatInput {
  hostToken: string;              // 创建房间时获取的令牌
  currentPlayers: number;          // 当前玩家数
  status: "open" | "starting" | "full" | "closed";
  connectedPlayerNetIds?: string[]; // 续局时上报已连接的 netId 列表
}
```

### 3.5 WebSocket /control（控制通道）

查询参数：
- `roomId` — 房间 ID
- `controlChannelId` — 控制通道 ID
- `role=host|client` — 角色
- `token`（host）或 `ticketId`（client）— 认证凭据

**信封（Envelope）类型**：

| type | 说明 | 方向 |
|------|------|------|
| `connected` | 连接成功 | 服务端→客户端 |
| `ping` / `pong` | 保活 | 双向 |
| `player_name_sync` | 玩家名同步 | 客户端→服务端→广播 |
| `player_name_snapshot` | 玩家名目录快照 | 房主→客户端 |
| `room_chat` | 房间聊天消息 | 双向广播 |

```typescript
interface LobbyControlEnvelope {
  type: string;
  roomId: string;
  controlChannelId: string;
  role: "host" | "client";
  PlayerName?: string;
  PlayerNetId?: string;
  PlayerNames?: Record<string, string>;  // player_name_snapshot
  MessageId?: string;
  MessageText?: string;
  SentAtUnixMs?: number;
  TicketId?: string;
}
```

---

## 四、关键数据结构

### 4.1 续局存档绑定（SavedRun）

续局是大厅系统最复杂的特性之一。关键流程：

1. **房主建房时**：将当前 `SerializableRun` 的信息（saveKey、slots、connectedPlayerNetIds）提交到 `/rooms`
2. **服务端存储**：`LobbyStore` 维护每个房间的 `SavedRunInfo`
3. **客户端加入时**：通过 `desiredSavePlayerNetId` 指定要接管的角色槽位
4. **心跳上报**：`heartbeat` 时携带 `connectedPlayerNetIds`，服务端更新槽位占用状态
5. **SaveKey 计算**：对 `mode + startTime + dailyTime + ascension + playerSignature` 做 SHA256

### 4.2 连接策略（Connection Strategy）

客户端和服务端协商连接方式：

| 策略 | 说明 |
|------|------|
| `direct-first` | 优先尝试直连，失败后 relay |
| `relay-first` | 优先尝试 relay，失败后直连 |
| `relay-only` | 只使用 relay（当前公开服务器默认） |

**直连候选地址**：`directCandidates` 包含公网 IP 和 LAN 地址。
**Relay fallback**：当直连超时后，客户端使用服务返回的 `relayEndpoint`（UDP）连接。

### 4.3 版本和 MOD 校验

服务端有两个严格开关：
- `STRICT_GAME_VERSION_CHECK`（默认 true）
- `STRICT_MOD_VERSION_CHECK`（默认 true）

当客户端和服务端都上报 `modList` 时，服务端会额外比较双方缺失项，返回详细的不一致信息。

---

## 五、与 NCC 的潜在对接点

### 5.1 对接可行性分析

NCC（NoClientCheats）的核心功能是**在多人联机时禁止客机使用控制台作弊指令**。与 STS2-Game-Lobby 大厅系统的潜在对接点如下：

#### 对接点 1：作弊拦截记录上报到大厅

**现状**：NCC 在本地记录作弊拦截历史，面板仅本地可见。

**对接思路**：利用大厅的 `POST /rooms/:id/connection-events` 或扩展控制通道信封，将作弊拦截事件上报到大厅服务。

```typescript
// 扩展信封类型
interface NCC_CheatEvent {
  type: "ncc_cheat_blocked";
  roomId: string;
  SenderId: string;         // Steam ID
  SenderName: string;       // 玩家显示名
  Command: string;          // 作弊指令
  Timestamp: number;        // Unix ms
}
```

**优点**：房主可以在大厅后台看到客机的作弊尝试记录。

**挑战**：
- 需要扩展 `lobby-service` 的控制通道处理逻辑
- 需要 NCC 和大厅服务建立信任关系（防止伪造上报）
- 隐私考量：是否要记录、记录哪些信息

#### 对接点 2：MOD 版本一致性校验

**现状**：NCC 是房主专属 MOD，客机无需安装。

**对接思路**：在大厅的 `/rooms/:id/join` 校验逻辑中，增加对 NCC 状态的检查：
- 房主有 NCC → 客机无需安装（但需要在 join 流程中标注 "此房间使用 NCC"）
- NCC 版本不同 → 给出明确的版本不匹配提示

**当前大厅的 modList 机制已支持此功能**，NCC 可以通过标准的 MOD 版本字符串（`modVersion`）上报版本。

#### 对接点 3：作弊通知扩展到房间聊天

**现状**：NCC 弹出本地通知弹窗（`CheatNotification`），仅当前玩家可见。

**对接思路**：通过控制通道的 `room_chat` 信封，广播作弊拦截通知给同房间所有玩家。

```typescript
interface CheatChatMessage {
  type: "room_chat";  // 复用现有房间聊天信封
  PlayerName: "NCC_系统";
  MessageText: `[作弊拦截] ${playerName} 尝试使用 ${command}`;
  // ...
}
```

**优点**：无需修改大厅服务端代码，只需在客户端 MOD 中增加消息构造逻辑。
**挑战**：需要房间内所有玩家都安装了 NCC（或至少大厅 MOD）。

#### 对接点 4：作弊记录的持久化和查询

**现状**：NCC 的作弊记录仅存在于内存，退出游戏后丢失。

**对接思路**：利用大厅服务的 `/rooms/:id/connection-events` 或新增一个事件日志接口，将作弊事件持久化到大厅数据库。

### 5.2 不需要对接的功能

| NCC 功能 | 与大厅的关系 | 说明 |
|----------|-------------|------|
| 控制台作弊拦截 | **独立运行** | 在 ENet/游戏引擎层面拦截，不需要大厅参与 |
| ModList 隐藏 | **已支持** | NCC 已使用 `GetGameplayRelevantModNameList` patch |
| 历史面板 UI | **本地功能** | 纯本地 GDScript UI，与网络无关 |
| ModConfig 配置 | **本地功能** | 配置存储在本地 JSON |

---

## 六、技术实现细节

### 6.1 客户端 SDK 设计

如果要和 STS2-Game-Lobby 对接，NCC 需要引入以下组件：

#### LobbyApiClient（大厅 API 客户端）

参考 `LanConnectLobbyRuntime.cs` 中的设计，API 客户端需要封装以下 HTTP 请求：

```csharp
// 概念性代码
public class NccApiClient {
    // 上报作弊事件
    public async Task ReportCheatEventAsync(string roomId, CheatRecord record);
    
    // 获取房间作弊记录（可选）
    public async Task<List<CheatRecord>> GetRoomCheatHistoryAsync(string roomId);
}
```

#### 控制通道信封扩展

参考 `LobbyControlEnvelope.cs` 的设计，在现有信封类型基础上增加 `ncc_cheat_blocked` 类型。

### 6.2 服务端扩展需求

如果需要大厅服务端支持 NCC 的作弊记录功能，`lobby-service` 需要增加：

1. **新增 API 端点**（或在现有 endpoint 上扩展）：
   - `POST /rooms/:id/ncc-events` — 接收 NCC 作弊事件上报

2. **新增存储表/字段**：
   - `RoomNccEvents` — 房间级别的作弊事件记录

3. **管理面板扩展**（可选）：
   - `/server-admin` 增加 NCC 事件查看页面

4. **认证考量**：
   - 需要验证上报来自真实的 NCC 客户端（非伪造）
   - 可能的方案：在 NCC MOD 中硬编码一个签名密钥，或使用房间的 `hostToken` 做 HMAC

### 6.3 房间状态和元数据

当前大厅服务的 `RoomSummary` 和 `Room` 数据结构中，与作弊相关的字段可以通过以下方式扩展：

```typescript
// 扩展 Room 元数据
interface RoomNccMetadata {
  nccEnabled: boolean;           // 是否启用 NCC
  nccVersion: string;            // NCC 版本
  cheatBlockCount: number;        // 作弊拦截次数（可选）
}
```

通过 `POST /rooms` 的 `savedRun` 字段类似的扩展机制，可以携带 NCC 元数据。

---

## 七、对接方式建议

### 7.1 推荐的对接路径

考虑到实现的复杂度和收益，建议分阶段进行：

#### 阶段 1：最小集成（无需修改大厅服务端）

1. **在 NCC 中引入大厅 SDK**：添加 `LobbyApiClient` 封装
2. **利用房间聊天广播作弊通知**：通过 `room_chat` 信封，同房间广播作弊拦截消息
3. **利用现有的 modVersion 机制**：NCC 版本号纳入 `modVersion` 字符串（如 `"ncc:1.1.6"`，或合并到 MOD 版本中）

**代码示例**：

```csharp
// 在 CheatHistoryPanel.cs 中，拦截到作弊时
if (LanConnectLobbyRuntime.HasInstance) {
    // 复用大厅的房间聊天信封
    LanConnectLobbyRuntime.Instance.SendRoomChatMessage(
        $"[作弊拦截] {record.SenderName} 尝试 {record.Command}");
}
```

**前置条件**：房间内所有玩家都安装了 STS2 LAN Connect MOD。

#### 阶段 2：服务端协同（需要修改 lobby-service）

1. 新增 `POST /rooms/:id/ncc-events` 端点
2. 存储作弊事件到房间元数据
3. 在 `/server-admin` 管理面板中增加作弊记录查看

#### 阶段 3：完整集成

1. NCC 元数据写入房间信息
2. 加入房间时前端展示"NCC 已启用"
3. 作弊记录持久化到数据库
4. 管理面板作弊趋势分析

### 7.2 技术风险和注意事项

| 风险 | 描述 | 缓解方案 |
|------|------|---------|
| **大厅 MOD 未安装** | 如果房间内只有部分玩家安装了 STS2-Game-Lobby，无法通过房间聊天广播 | 阶段1需要检测 `LanConnectLobbyRuntime` 是否存在 |
| **作弊事件伪造** | 恶意客户端可能伪造作弊上报 | 使用 HMAC 签名或利用 hostToken 做认证 |
| **隐私合规** | 作弊记录包含 Steam ID 和玩家名 | 仅房主可见，不对外广播 |
| **大厅服务端版本依赖** | 修改 lobby-service 需要 NCC 和大厅服务同步升级 | 通过 API 版本协商处理 |
| **Relay 带宽** | 作弊事件通过控制通道广播可能增加 relay 负载 | 使用独立的轻量事件通道 |

---

## 八、代码级参考

### 8.1 关键源码文件索引

| 文件 | 内容摘要 |
|------|---------|
| `LanConnectLobbyRuntime.cs` (743行) | 房间生命周期管理、心跳、控制通道、房间聊天 |
| `LanConnectLobbyOverlay.cs` (4584行) | 大厅 UI 核心，包含房间列表、对话框 |
| `LanConnectConfig.cs` | 配置持久化，含大厅 URL、续局绑定 |
| `LanConnectMultiplayerSaveRoomBinding.cs` | 续局存档绑定，saveKey SHA256 计算 |
| `server.ts` (981行) | HTTP/WebSocket 服务端，房间 CRUD、relay 管理 |
| `store.ts` (775行) | 房间状态、票据、校验逻辑 |
| `research/LAN_MOD_RESEARCH_RECONSTRUCTED.md` | ENet 直连机制、游戏内部 MP 链路研究 |

### 8.2 NCC 可以复用的模式

NCC 在架构上可以参考 STS2-Game-Lobby 的以下设计：

1. **LobbyApiClient 封装**：统一的 HTTP 请求处理和错误解析
2. **控制通道信封模式**：通过 WebSocket 广播房间级事件
3. **心跳保活机制**：`LanConnectLobbyRuntime._Process` 中的定时心跳
4. **续局绑定模式**：`LanConnectMultiplayerSaveRoomBinding` 的 saveKey 计算
5. **配置持久化**：`LanConnectConfig` 的 `lock` + `SaveUnsafe()` 模式

---

## 九、附录

### 9.1 环境变量速查

**客户端打包**（sts2-lan-connect）：
```
STS2_LOBBY_DEFAULT_BASE_URL
STS2_LOBBY_DEFAULT_WS_URL
STS2_LOBBY_DEFAULT_REGISTRY_BASE_URL
STS2_LOBBY_COMPATIBILITY_PROFILE
STS2_LOBBY_CONNECTION_STRATEGY
```

**服务端部署**（lobby-service）：
```
HOST / PORT
HEARTBEAT_TIMEOUT_SECONDS
TICKET_TTL_SECONDS
STRICT_GAME_VERSION_CHECK / STRICT_MOD_VERSION_CHECK
CONNECTION_STRATEGY
RELAY_BIND_HOST / RELAY_PUBLIC_HOST
SERVER_ADMIN_PASSWORD_HASH / SERVER_ADMIN_SESSION_SECRET
SERVER_REGISTRY_BASE_URL
SERVER_REGISTRY_PUBLIC_BASE_URL / SERVER_REGISTRY_PUBLIC_WS_URL
```

### 9.2 官方资源

- 官方母面板：`http://47.111.146.69:18787`
- 官方默认大厅：`http://47.111.146.69:8787`
- GitHub 仓库：https://github.com/emptylower/STS2-Game-Lobby

---

## 十、游戏源码关键分析与 Bug 修复记录

> 本章节记录 NCC 开发过程中对游戏源码的深度分析，以及发现的 Bug 和修复方案。
> 游戏源码来源：`K:\杀戮尖塔mod制作\Tools\sts.dll历史存档\sts2_decompiled20260322\sts2\`

### 10.1 RestSiteSynchronizer.ChooseOption — 关键源码分析

游戏源码路径：`MegaCrit.Sts2.Core.Multiplayer.Game.RestSiteSynchronizer`

**方法签名**：
```csharp
private async Task<bool> ChooseOption(Player player, int optionIndex)
```

**源码关键流程**（第 158-233 行）：

```csharp
// 第 176-180 行：BeforePlayerOptionChosen 事件
Action<RestSiteOption, ulong> beforePlayerOptionChosen = this.BeforePlayerOptionChosen;
if (beforePlayerOptionChosen != null)
{
    beforePlayerOptionChosen(option, player.NetId);
}

// 第 181 行：执行选项操作（核心！卡组变换发生在这里）
bool flag = await option.OnSelect();

// 第 195-198 行：AfterPlayerOptionChosen 事件
Action<RestSiteOption, bool, ulong> afterPlayerOptionChosen = this.AfterPlayerOptionChosen;
if (afterPlayerOptionChosen != null)
{
    afterPlayerOptionChosen(option, flag2, player.NetId);
}
```

**关键洞察**：
1. `BeforePlayerOptionChosen` 在 `option.OnSelect()` 之前触发，此时玩家卡组是**变换前的状态**
2. `option.OnSelect()` 是异步操作，**卡组变换在这个方法内部发生**
3. `AfterPlayerOptionChosen` 在操作完成后触发，此时卡组已经是**变换后的最终状态**

**NCC Patch 策略**：
- Prefix Patch：在 `BeforePlayerOptionChosen`（即 NCC 的 Prefix）时记录卡组快照
- Postfix Patch：在 `AfterPlayerOptionChosen`（即 NCC 的 Postfix）时记录最终卡组快照
- 对比两次快照，可以检测客机是否通过作弊指令修改了卡组

---

### 10.2 NetPlayerChoiceResult — 关键数据结构

游戏源码路径：`MegaCrit.Sts2.Core.Entities.Multiplayer.NetPlayerChoiceResult`

**结构定义**：

```csharp
public struct NetPlayerChoiceResult : IPacketSerializable
{
    public PlayerChoiceType type;
    public List<CardModel> canonicalCards;        // 作弊前的卡组（CanonicalCard 类型）
    public List<NetCombatCard> combatCards;       // 战斗卡（CombatCard 类型）
    public List<NetDeckCard> deckCards;           // 卡组卡（DeckCard 类型）
    public List<SerializableCard> mutableCards;   // 可变卡（MutableCard 类型）
    public ulong? mutableCardOwner;
    public List<int> indexes;
    public ulong? playerId;
}
```

**PlayerChoiceType 枚举**：

| 枚举值 | 对应字段 | 说明 |
|--------|----------|------|
| `CanonicalCard` | `canonicalCards` | 变换前的卡组快照 |
| `CombatCard` | `combatCards` | 战斗中的卡 |
| `DeckCard` | `deckCards` | 卡组中的卡 |
| `MutableCard` | `mutableCards` | 可变卡（升级/附魔后的卡） |
| `Player` | `playerId` | 玩家ID |
| `Index` | `indexes` | 索引列表 |

**作弊检测核心字段**：
- `canonicalCards`：游戏内置作弊引擎记录的"变换前卡组"，作弊指令（如 `card`、`remove_card`）不会修改这个字段
- `mutableCards`：作弊后的卡牌列表

---

### 10.3 PlayerChoiceSynchronizer — 选卡同步机制

游戏源码路径：`MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceSynchronizer`

**事件触发流程**（第 208-214 行）：

```csharp
private void OnReceivePlayerChoice(Player player, uint choiceId, NetPlayerChoiceResult result)
{
    Action<Player, uint, NetPlayerChoiceResult> playerChoiceReceived = this.PlayerChoiceReceived;
    if (playerChoiceReceived != null)
    {
        playerChoiceReceived(player, choiceId, result);
    }
    // ...
}
```

**关键发现**：
1. `PlayerChoiceReceived` 事件在 `OnReceivePlayerChoice` 方法内同步触发
2. `result` 参数包含 `canonicalCards`（作弊前卡组）和 `mutableCards`（作弊后卡组）
3. NCC 通过 Hook 这个事件的订阅者，捕获 `canonicalCards` 用于作弊检测

**PlayerChoiceMessage 序列化**（第 44-57 行）：

```csharp
public void Serialize(PacketWriter writer)
{
    writer.WriteUInt(this.choiceId, 32);
    writer.Write<NetPlayerChoiceResult>(this.result);
}

public void Deserialize(PacketReader reader)
{
    this.choiceId = reader.ReadUInt(32);
    this.result = reader.Read<NetPlayerChoiceResult>();
}
```

---

### 10.4 Bug 修复记录

#### Bug 1：CardPile 回滚时 ID 不匹配

**问题描述**：
旧版回滚策略通过比较 `SerializableCard.Id` 来判断"多余卡"和"缺失卡"，但 `FromSerializable()` 每次返回新的 CardModel 实例，导致 ID 引用不一致。

**源码问题**（DeckSyncPatches.cs 第 1460-1462 行注释）：

```
旧 delta 策略的问题：snapshot 的卡 ID 和 CardPile 中卡的实际 ID 不匹配
（FromSerializable 每次返回新实例），导致按 ID 比较时全部标记为"多余"，
反而删掉了正确的变换后卡，留下了错误的卡。
```

**修复方案**：全量替换策略
1. 清空 `CardPile._cards`
2. 从 `canonicalCards` 按原序重建整个卡组
3. 调用 `InvokeContentsChanged` 刷新

**修复后代码**（DeckSyncPatches.cs 第 1467-1545 行）：

```csharp
if (cardsField != null && preDeck != null && preDeck.Count > 0)
{
    LogDiag("Rollback", $"FULL REPLACE: {currentCards?.Count ?? 0} -> {preDeck.Count} cards");

    // 1) 清空
    if (currentCards != null)
    {
        currentCards.Clear();
        cardsField.SetValue(cardPileObj, currentCards);
    }

    // 2) 从 snapshot 重建（按 SerializableCards 顺序）
    foreach (object sCard in preDeck)
    {
        if (fromSerializable != null)
        {
            var cm = fromSerializable.Invoke(null, new[] { sCard });
            if (cm != null && currentCards != null)
                currentCards.Add(cm);
        }
    }

    // 3) 触发刷新
    invokeContentsChanged?.Invoke(cardPileObj, null);
    return "CardPile.full_replace";
}
```

---

#### Bug 2：Prefix 和 Postfix 时序问题

**问题描述**：
`OnReceivePlayerChoice` 的 Postfix 在 action queue 执行后触发，此时卡组已经被变换。如果在 Postfix 中记录"变换前"状态，会拿到错误的（变换后）数据。

**源码时序分析**：

```
1. Prefix（action queue 执行前）→ 记录卡数
2. action queue 执行 → 卡组被变换
3. Postfix（action queue 执行后）→ 收到 canonicalCards（但此时记录已晚）
```

**修复方案**（DeckSyncPatches.cs 第 2056-2099 行）：

```csharp
// 核心修复：在 Prefix（action queue 执行前）记录当前卡组大小。
// 这才是真正的"变换前"状态。Postfix 在执行后触发，此时卡组已被变换。
if (isRemote)
{
    try
    {
        // 尝试从 RunManager 获取该玩家的当前卡组大小
        var rmType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Runs.RunManager");
        var inst = rmType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
        if (inst != null)
        {
            object state = inst.GetType().GetProperty("State", BindingFlags.Public | BindingFlags.Instance)?.GetValue(inst);
            // ... 获取 Players 并记录卡数
            lock (_pendingPreDeckSize)
                _pendingPreDeckSize[playerId] = deckCount;
        }
    }
    catch (Exception ex) { DIAG($"[PREFIX] deck capture error: {ex.Message}"); }
}
```

---

#### Bug 3：Transform 检测漏计问题

**问题描述**：
当 `preDeckSize` 为空（未被缓存）时，`choiceCallCount` 也为 0，导致无法检测多选作弊。

**修复方案**（DeckSyncPatches.cs 第 513-521 行）：

```csharp
// 修复后强制累加：在无 preDeck 时也记录 choiceCallCount（防止漏计）
// 若 _pendingPreDeckSize 已为空，说明之前被第一次 SyncReceived 消费了，
// 此时 choiceCallCount 仍有效，直接用于检测
if (preDeckSize == 0 && choiceCallCount == 0)
{
    // 尝试从 _lastSyncDeckSize 取上一次的卡数作为 preDeck
    lock (_lastSyncDeckSize)
        _lastSyncDeckSize.TryGetValue(senderId, out preDeckSize);
    DIAG($"[FULLTRACE] TransformCheck FIXED: no preDeck cached, using _lastSyncDeckSize={preDeckSize} for delta calc");
}
```

---

### 10.5 源码索引速查表

| 游戏源码文件 | 关键方法/类 | NCC 对应 Patch |
|--------------|-------------|----------------|
| `RestSiteSynchronizer.cs` | `ChooseOption()` | `ChooseOptionPrefix` / `ChooseOptionPostfix` |
| `PlayerChoiceSynchronizer.cs` | `OnReceivePlayerChoice()` | `OnReceivePlayerChoicePatch` |
| `NetPlayerChoiceResult.cs` | 结构体定义 | 作弊检测数据源 |
| `CombatStateSynchronizer.cs` | `OnSyncPlayerMessageReceived()` | `SyncReceivedPostfix` |
| `PlayerChoiceMessage.cs` | `PlayerChoiceMessage` | 网络消息序列化 |

---

*本文档由 AI 基于 STS2-Game-Lobby 公开仓库源码和游戏反编译源码自动生成。*
