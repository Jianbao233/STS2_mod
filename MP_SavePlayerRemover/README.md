# MP Save Player Remover / 多人存档移除玩家工具

Standalone tool to remove disconnected or leaving players from STS2 multiplayer save files. **Host-only** — run before loading the game when someone drops.

多人联机时若有玩家掉线或不玩了，可在读档前用本工具修改存档，移除该玩家并调整人数。**仅房主使用**，无需其他人安装。

---

## Features / 功能

| 功能 | 说明 |
|------|------|
| **删除玩家** | 从存档中移除指定玩家，清理 players、map_point_history、map_drawings 等引用 |
| **Steam 名称显示** | 支持 `steam_names.json` 映射文件，告别纯 ID 认人时代 |
| **自动路径检测** | 自动搜索原版/模组模式的存档路径，支持多 Steam 账号 |
| **存档摘要** | 选择时标明：难度、层数、玩家角色与 Steam 64位 ID |
| **自动备份** | 修改前自动备份为 `current_run_mp.save.backup.{时间戳}` |
| **Mod 角色支持** | 支持 Mod 角色，未知角色显示其 ID |

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
3. 输入要**删除**的玩家序号，逗号分隔，如 `1,3`（输入 `all` = 删除全部，留空 = 取消）
4. 确认后自动备份并修改
5. 启动游戏，继续读档

### Steam 名称映射（可选）

存档内无 Steam 昵称，工具默认仅显示 64 位 ID。

如需显示昵称，在存档目录创建 `steam_names.json`：

```json
{
  "76561198679823594": "煎包",
  "76561199032167696": "小明"
}
```

这样玩家列表会显示为 `煎包 (76561198679823594)`，一目了然。

### 存档路径

- **原版**：`%APPDATA%\SlayTheSpire2\steam\{SteamId}\profile1\saves\`
- **模组**：`%APPDATA%\SlayTheSpire2\steam\{SteamId}\modded\profile1\saves\`

### 备份位置

备份与原存档同目录，格式：`current_run_mp.save.backup.{年月日_时分秒}`

---

## Build from Source / 从源码构建

```powershell
cd MP_SavePlayerRemover
.\build_exe.bat
```

需要：Python 3.8+、PyInstaller。产出在 `dist/MP_SavePlayerRemover-vX.X.X.exe`。

或直接运行 Python：

```bash
python remove_players.py
```

---

## Release / 发行版

```powershell
.\prepare-release.ps1 -Version "1.1.0"
```

产出 `release/MP_SavePlayerRemover-v1.1.0.zip`，可上传至 GitHub Releases。

---

## Changelog / 版本记录

| 版本 | 变更 |
|------|------|
| v1.1.0 | 新增 Steam 名称映射显示；删除模式替代保留模式（更直观） |
| v1.0.0 | 初始版本 |

---

## License

MIT
