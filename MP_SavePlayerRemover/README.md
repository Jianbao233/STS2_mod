# MP Save Player Remover / 多人存档移除玩家工具

Standalone tool to remove disconnected or leaving players from STS2 multiplayer save files. **Host-only** — run before loading the game when someone drops.

多人联机时若有玩家掉线或不玩了，可在读档前用本工具修改存档，移除该玩家并调整人数。**仅房主使用**，无需其他人安装。

---

## Features / 功能

| 功能 | 说明 |
|------|------|
| **Remove Players** | 从存档中移除指定玩家，清理 players、map_point_history、map_drawings 等引用 |
| **Auto Path Detect** | 自动搜索原版/模组模式的存档路径，支持多 Steam 账号 |
| **Save Summary** | 选择时标明：难度、层数、玩家角色与 Steam 64位 ID |
| **Auto Backup** | 修改前自动备份为 `current_run_mp.save.backup.{时间戳}` |
| **Mod Characters** | 支持 Mod 角色，未知角色显示其 ID |

---

## Requirements / 依赖

- **Slay the Spire 2** 多人存档 `current_run_mp.save`
- **无** Python 安装要求（使用 exe 时）；或 Python 3.8+（使用 py 时）

---

## Installation / 安装

1. 从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载 `MP_SavePlayerRemover-vX.X.X.zip`
2. 解压到任意位置（无需放入游戏目录）
3. 双击 `MP_SavePlayerRemover.exe` 运行

---

## Usage / 使用

1. **先退出游戏**
2. 运行工具，按提示选择存档（多份时会显示模式、Steam 账号、难度、层数、玩家）
3. 输入要**保留**的玩家序号，逗号分隔，如 `1,3`
4. 确认后自动备份并修改
5. 启动游戏，继续读档

### 存档路径

- **原版**：`%APPDATA%\SlayTheSpire2\steam\{SteamId}\profile1\saves\`
- **模组**：`%APPDATA%\SlayTheSpire2\steam\{SteamId}\modded\profile1\saves\`

存档内无 Steam 昵称，仅显示 64 位 ID。可访问 `steamcommunity.com/profiles/{64位ID}` 查看。

### 备份位置

备份与原存档同目录，格式：`current_run_mp.save.backup.{年月日_时分秒}`

---

## Build from Source / 从源码构建

```powershell
cd MP_SavePlayerRemover
.\build_exe.bat
```

需要：Python 3.8+、PyInstaller。产出在 `dist/MP_SavePlayerRemover.exe`。

或直接运行 Python：

```bash
python remove_players.py
```

---

## Release / 发行版

```powershell
.\prepare-release.ps1 -Version "1.0.0"
```

产出 `release/MP_SavePlayerRemover-v1.0.0.zip`，可上传至 GitHub Releases。

---

## License

MIT
