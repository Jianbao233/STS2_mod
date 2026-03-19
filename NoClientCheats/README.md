# No Client Cheats / 禁止客机作弊

Mod for Slay the Spire 2. Blocks client players from using dev console cheat commands (gold, relic, card, potion, etc.) in multiplayer. **Host-only install** — clients do not need this mod.

多人联机时禁止客机（非房主）使用控制台作弊指令（如 gold、relic、card、potion 等）。**仅房主需安装**，客机无需安装。

---

## Features / 功能

| 功能 | 说明 |
|------|------|
| **Block Client Cheats** | 房主启用时，客机发出的作弊指令被静默丢弃，不入队、不生效 |
| **Hide from Mod List** | 从联机 Mod 列表中移除本 Mod，客机无法检测到（参考 sts2-heybox-support） |
| **拦截历史面板** | 按 **F6** 呼出/隐藏，完整记录每条作弊拦截（时间、玩家、角色、指令），可拖拽移动、边缘调整大小 |
| **本地化汉化** | 角色名、遗物名、指令均已汉化（如 `relic ICE_CREAM` → 「遗物：冰淇淋」） |
| **ModConfig** | 游戏内「模组配置」可开关以上各项（拦截开关、弹窗时长、最大历史条数） |

---

## Requirements / 依赖

- **Slay the Spire 2**（Steam 正式版）
- **ModConfig**（推荐，用于游戏内配置；未安装时使用默认开启）
- **Harmony**（游戏内置，无需额外安装）

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
       ├── NoClientCheats.pck
       └── mod_manifest.json
   ```
5. 启动游戏，仅**房主**需安装；客机无需安装本 Mod

---

## Configuration / 配置

在游戏主菜单 → **模组配置** → **禁止客机作弊** 中可调整：

| 选项 | 默认 | 说明 |
|------|------|------|
| Block Client Cheats | 开 | 禁止客机作弊指令 |
| Hide from Mod List | 开 | 从联机 Mod 列表隐藏本 Mod |
| Notification Duration (s) | 5.0 | 弹窗持续时长（秒） |
| Max History Records | 25 | 历史面板最多保存条数 |
| Show History Panel | 开 | 启用 F6 呼出历史记录面板 |

---

## Build from Source / 从源码构建

```powershell
cd NoClientCheats
.\build.ps1
```

需要：.NET 8 SDK、Godot 4.5.1 Mono。构建产物会复制到游戏 `mods\NoClientCheats\` 目录。

---

## Thanks / 致谢

- **sts2-heybox-support**（小黑盒官方支持）：屏蔽 Mod 检测实现参考
- **皮一下就很凡** @ B站（DamageMeter 作者）：Mod 开发参考

---

## Changelog / 更新日志

### v1.1.1（2026-03-20）

- 修复历史面板 `RefreshList()` 误删空状态标签 `_emptyLabel` 的问题，面板不再崩溃
- `notification_duration` 改为 **Slider**（1–15秒，步长0.5）
- `history_max` 改为 **Dropdown**（10/15/20/25/30/35/40/45/50）
- `show_history_panel` 回调修正为设置 `NoClientCheatsMod.ShowHistoryPanel`，F6 仅在该配置开启时响应
- **作弊记录新增角色名**：`CheatRecord` 新增 `CharacterName` 字段
- **弹窗显示 Steam名 + 角色 + 指令**：`[禁止作弊] Steam名 | 角色：xxx | 指令`
- **历史面板行显示**：`时间 Steam名 (角色名) → 指令`
- **Patch 解析玩家角色**：`ClientCheatBlockPatch._GetPlayerCharacter(senderId)` 反射 `RunState.CurrentRun.Players` 按 SteamId 匹配

### v1.1.0（2026-03-20）

- 角色/遗物本地化汉化（使用游戏 LocString）
- 弹窗与面板内角色名、遗物名均已汉化
- 历史面板 UI 修复（标题计数、窗口宽度、时间列固定）
- **历史面板可拖拽/可调整大小**（F6 呼出/隐藏）

---

## License

MIT
