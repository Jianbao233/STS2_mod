## 禁止客机作弊 v1.2.0

### 新增

- **大厅聊天广播**：作弊拦截时通过 [STS2 LAN Connect](https://github.com/emptylower/STS2-Game-Lobby) 向房间内所有玩家发送聊天消息（如 `[作弊拦截] 玩家名 尝试使用 指令`），默认关闭
- 新增 `LanConnectBridge` 桥接类：反射调用大厅 MOD 的 `LanConnectLobbyRuntime`，零编译期依赖

### 修复

- **大厅聊天广播静默失败**：大厅 MOD 中 `SendRoomChatMessageAsync`/`HasActiveRoomSession`/`Instance` 均为 `internal`，反射须使用 `BindingFlags.NonPublic`（之前只用 `Public` 导致反射恒返回 `null`，广播静默跳过）
- 增加详细调试日志（`godot.log` 中出现 `[NoClientCheats] LanConnect: ...`）

### 功能

| 功能 | 说明 |
|------|------|
| Block Client Cheats | 禁止客机作弊指令（房主安装，客机无需安装） |
| 大厅聊天广播 | 通过 STS2 LAN Connect 向房间聊天广播作弊拦截（需安装大厅 MOD） |
| F6 呼出历史面板 | 可拖拽/可调整大小的作弊拦截历史记录 |
| ModConfig | 游戏内配置所有选项 |

### 依赖

- Slay the Spire 2（Steam 正式版）
- ModConfig（推荐）
- Harmony（游戏内置）
- STS2 LAN Connect（可选，[项目地址](https://github.com/emptylower/STS2-Game-Lobby)）

### 致谢

- [STS2 LAN Connect](https://github.com/emptylower/STS2-Game-Lobby)
- sts2-heybox-support（小黑盒官方支持）
- 皮一下就很凡 @ B站（ModConfig / DamageMeter 作者）
