# STS2 多人存档 - 移除玩家工具

在游戏外修改 `current_run_mp.save`，移除指定玩家并清理相关引用。适用于有玩家掉线或不再继续时，将存档人数调少后继续游戏。

## 使用前提

- **先退出游戏**，再运行本工具
- 修改完成后，重新启动游戏读档即可

## 使用方式

### 方式一：直接运行 exe（推荐）

1. 双击 `MP_SavePlayerRemover.exe`
2. 程序会自动搜索存档路径
3. 按提示选择要**保留**的玩家（输入序号，如 `1,3`）
4. 确认后自动备份并修改

### 方式二：用 Python 运行

```bash
python remove_players.py
```

需安装 Python 3.8+，无需其他第三方库。

## 打包 exe

在项目目录下运行：

```
build_exe.bat
```

或手动：

```bash
pip install pyinstaller
pyinstaller --onefile --name MP_SavePlayerRemover remove_players.py
```

产出在 `dist/MP_SavePlayerRemover.exe`。

## 存档路径

游戏分**模组模式**与**原版模式**，存档分开存放：
- **原版**：`steam\{SteamId}\profile1\saves\current_run_mp.save`
- **模组**：`steam\{SteamId}\modded\profile1\saves\current_run_mp.save`

同一电脑可有**多个 Steam 账号**（不同 `SteamId` 文件夹）。选择存档时会标明：模式、Steam 账号、难度、层数、玩家角色与 64 位 ID。

注：存档内无 Steam 昵称，仅能显示 64 位 ID。可访问 `steamcommunity.com/profiles/{64位ID}` 手动查看。

若未找到，可手动输入完整路径。

## 备份

每次修改前会自动备份为 `current_run_mp.save.backup.{时间戳}`。

## 说明

- 移除玩家后，游戏内的怪物血量、难度等会按新的玩家数重新计算
- 仅房主使用即可，其他玩家无需安装
