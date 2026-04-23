## 禁止客机作弊 / No Client Cheats v1.3.1

### 本次更新 / What's New

- 修复最新版本手机端（PE）作弊拦截失效的 BUG：`ClientCheatBlockPrefix.TargetMethod()` 返回 `null` 时，关键 Harmony 补丁未挂上
- 适配 `ActionQueueSynchronizer` 方法变更：`TargetMethod` 改为 `Public/NonPublic + 参数形状(?, ulong)` 双通道匹配
- 增强反射兜底，兼容 `message.action/Action` 与 `cmd/Cmd` 命名差异，提高跨版本稳定性
- Fixed anti-cheat interception failure on the latest mobile (PE) build caused by `TargetMethod()` resolving to `null`
- Added robust method resolution for `ActionQueueSynchronizer` visibility/signature drift and stronger reflection fallbacks for `action/cmd` field naming differences

### 核心功能 / Core Features

| 功能 / Feature | 说明 / Description |
|------|------|
| Block Client Cheats | 禁止客机作弊指令（房主安装，客机无需安装） / Block client cheat console commands (host-only install) |
| Deck Rollback | 检测到异常卡组变化时，主机强制回滚客机卡组 / Force rollback on suspicious deck changes |
| History Panel (F6) | 可拖拽/可缩放作弊记录面板 / Draggable and resizable cheat history panel |
| ModConfig | 游戏内配置所有选项 / In-game configuration support |

### 依赖 / Dependencies

- Slay the Spire 2（Steam 正式版） / Slay the Spire 2
- ModConfig（推荐） / Recommended
- Harmony（游戏内置） / Built-in
- STS2 LAN Connect（可选） / Optional: [STS2-Game-Lobby](https://github.com/emptylower/STS2-Game-Lobby)
