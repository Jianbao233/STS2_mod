# No Client Cheats / 禁止客机作弊

Mod for Slay the Spire 2. Blocks client players from using dev console cheat commands (gold, relic, card, potion, etc.) in multiplayer. **Host-only install** — clients do not need this mod.

多人联机时禁止客机（非房主）使用控制台作弊指令（如 gold、relic、card、potion 等）。**仅房主需安装**，客机无需安装。

---

## Features / 功能

| 功能 | 说明 |
|------|------|
| **Block Client Cheats** | 房主启用时，客机发出的作弊指令被静默丢弃，不入队、不生效 |
| **大厅聊天广播** | 作弊拦截时通过 [STS2 LAN Connect](https://github.com/emptylower/STS2-Game-Lobby) 向房间内所有玩家广播通知 |
| **Hide from Mod List** | 从联机 Mod 列表中移除本 Mod，客机无法检测到（参考 sts2-heybox-support） |
| **游戏顶栏呼出按钮** | 在游戏顶栏 PauseButton 左侧注入「记录」按钮，点击直接呼出/隐藏历史面板 |
| **F6 快捷键** | 按 **F6** 呼出/隐藏作弊拦截历史面板 |
| **作弊拦截历史面板** | 完整记录每条作弊拦截（时间、玩家、角色、指令），可拖拽移动、边缘调整大小 |
| **本地化汉化** | 角色名、遗物名、指令均已汉化（如 `relic ICE_CREAM` → 「遗物：冰淇淋」） |
| **ModConfig** | 游戏内「模组配置」可调整所有选项 |

---

## Requirements / 依赖

- **Slay the Spire 2**（Steam 正式版）
- **ModConfig**（推荐，用于游戏内配置；未安装时使用默认开启）
- **Harmony**（游戏内置，无需额外安装）
- **STS2 LAN Connect**（可选，用于大厅聊天广播功能；未安装时大厅广播功能自动跳过，[项目地址](https://github.com/emptylower/STS2-Game-Lobby)）

---

## Installation / 安装

1. 找到游戏目录，例如：`Steam\steamapps\common\Slay the Spire 2`
2. 确保存在 `mods` 文件夹（与 `SlayTheSpire2.exe` 同级）
3. 从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载最新版 `NoClientCheats-vX.X.X.zip`
4. 解压到 `mods` 文件夹内，确保目录结构为：
   ```
   mods/
   └── NoClientCheats/
       ├── NoClientCheats.dll
       └── mod_manifest.json
   ```
5. 启动游戏，仅**房主**需安装；客机无需安装本 Mod

---

## Configuration / 配置

在游戏主菜单 → **模组配置** → **禁止客机作弊** 中可调整：

| 选项 | 默认 | 说明 |
|------|------|------|
| Block Client Cheats | 开 | 禁止客机作弊指令 |
| Broadcast to Lobby Chat | 关 | 作弊拦截时向 [STS2 LAN Connect](https://github.com/emptylower/STS2-Game-Lobby) 房间聊天广播通知 |
| Show Top Bar Button | 开 | 在游戏顶栏显示呼出按钮 |
| Show Popup | 开 | 作弊被拦截时显示红色弹窗 |
| Popup Duration (s) | 5.0 | 弹窗持续时长（秒） |
| Show Panel on Cheat | 关 | 客机作弊被拦截时自动弹出历史面板 |
| Max History Records | 25 | 历史面板最多保存条数 |
| History Toggle Key | F6 | 呼出历史面板的快捷键 |
| 呼出历史面板（操作按钮） | — | 点击呼出作弊拦截历史面板 |
| 窗口居中（操作按钮） | — | 将历史面板窗口移回屏幕中央 |
| Hide from Mod List | 开 | 从联机 Mod 列表隐藏本 Mod |

---

## Build from Source / 从源码构建

**重要：发布前必须先构建，确保 torelease 目录有最新快照。**

```powershell
cd NoClientCheats
.\build.ps1 -GodotExe "K:\杀戮尖塔mod制作\Godot_v4.5.1\Godot_v4.5.1\Godot_v4.5.1-stable_mono_win64.exe"
# --- 测试游戏功能 ---
.\prepare-release.ps1 -Version "1.2.0"   # 从 torelease 打包 zip
gh release create v1.2.0 --title "No Client Cheats v1.2.0" --notes "..."   # 上传到 GitHub
```

需要：.NET 8 SDK、Godot 4.5.1 Mono。

**发布流程规则**：
1. `build.ps1` 每次都会清空 `torelease\` 并写入最新构建产物
2. `prepare-release.ps1` **强制要求** `torelease\` 存在所有文件——若文件缺失会报错退出，不会打包旧文件
3. 绝不能跳过 `build.ps1` 直接运行 `prepare-release.ps1`

---

## Thanks / 致谢

- **sts2-heybox-support**（小黑盒官方支持）：屏蔽 Mod 检测实现参考
- **皮一下就很凡** @ B站（DamageMeter 作者）：Mod 开发参考

---

## Changelog / 更新日志

### v1.3.2（2026-04-23）

- 优化拦截链路稳定性与跨端兼容性
- 清理弃用模块与冗余补丁，降低联机误干扰风险

### v1.3.1（2026-04-23）

- 修复手机端（PE）在部分版本上 `TargetMethod` 解析失败导致拦截失效的问题
- 增强 `ActionQueueSynchronizer` 目标方法匹配与反射兜底兼容性

### v1.2.0（2026-03-26）

- **新增大厅聊天广播**：作弊拦截时可通过 [STS2 LAN Connect](https://github.com/emptylower/STS2-Game-Lobby) 大厅聊天向房间内所有玩家广播通知
- 新增配置项 `Broadcast to Lobby Chat`：作弊拦截广播开关（默认关闭）
- 新增 `LanConnectBridge` 桥接类：反射调用 `Sts2LanConnect.LanConnectLobbyRuntime`，零编译期依赖
- **修复大厅聊天广播静默失败**：大厅 MOD 中 `SendRoomChatMessageAsync`/`HasActiveRoomSession`/`Instance` 均为 `internal`，反射须用 `BindingFlags.NonPublic`（只用 `Public` 会取到 `null`，广播静默跳过）

### v1.1.6（2026-03-23）

- **修复：打包流程重建**：修复 GitHub release zip 包含旧文件的问题
- `build.ps1` 同时将产物同步到 `Steam mods/` 和项目 `torelease/` 目录
- `prepare-release.ps1` 只从 `torelease/` 打包，检测到文件缺失时立即报错退出
- 新增 `.gitignore` 规则，排除 `torelease/` 目录

### v1.1.5（2026-03-22）

- **游戏顶栏注入呼出按钮**：Hook `NTopBar._Ready`，在顶栏 PauseButton 左侧注入「记录」按钮，点击直接呼出/隐藏历史面板，完全绕 ModConfig
- **彻底修复 `_BuildUI()` 未调用 Bug**：添加 `CheatHistoryPanel._Ready()` 覆盖调用 `EnsureWindowBuilt()`，窗口才真正构建出来（之前从未出现过）
- **移除 `show_history_panel` 开关**：历史面板始终存在，由顶栏按钮/F6 控制显隐，不再有关闭后无法再打开的问题
- **新增 `show_topbar_button` 开关**：控制顶栏呼出按钮是否显示
- **恢复 ModConfig 操作按钮**：`btn_open_history` / `btn_center_window` 改回 Toggle 类型（`DefaultValue=false`，点击触发后重置），可从配置菜单呼出面板和居中窗口
- **快捷键 F9 → F6**
- **移除 `#if DEBUG` 自动弹出**：调试构建不再自动弹出面板
- `SyncFromConfig` 加 `IsAvailable` 保护

### v1.1.4（2026-03-22）

- 修复操作按钮 OnChanged 不触发问题（改用 Godot 原生 .Pressed 委托）
- 快捷键从 F9 改为 F6
- SyncFromConfig 加 IsAvailable 保护

### v1.1.3（2026-03-22）

- 重构热键机制：新建独立 InputHandlerNode，纯 _Process 轮询
- ModConfig 新增操作按钮：打开历史面板 / 窗口居中
- 标题栏新增居中按钮

### v1.1.2（2026-03-21）

- **修复初始化时机 Bug**：原版本在 `ModInitializer` 阶段加载配置时 `LocManager` 尚未初始化，导致配置加载失败。新版本采用**静态构造函数 + 两帧延迟**方案，三重保险确保在 `LocManager` 就绪后才初始化

### v1.1.1（2026-03-20）

- 修复历史面板 `RefreshList()` 误删空状态标签 `_emptyLabel` 的问题
- `notification_duration` 改为 **Slider**（1–15秒，步长0.5）
- `history_max` 改为 **Dropdown**（10/15/20/25/30/35/40/45/50）
- 作弊记录新增角色名：`CheatRecord` 新增 `CharacterName` 字段
- 弹窗显示 Steam名 + 角色 + 指令
- Patch 解析玩家角色：`ClientCheatBlockPatch._GetPlayerCharacter(senderId)` 反射匹配

### v1.1.0（2026-03-20）

- 角色/遗物本地化汉化（使用游戏 LocString）
- 历史面板可拖拽/可调整大小（F6 呼出/隐藏）

---

## License

MIT
