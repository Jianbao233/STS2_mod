# STS2 多人存档玩家管理工具
> 管理 Slay the Spire 2 多人存档：夺舍（接管离线玩家）、添加新玩家、移除离线玩家。

## 功能

| 功能 | 说明 |
|------|------|
| **夺舍玩家** | 输入接替者 Steam64位ID，继承离线玩家的所有状态继续游戏 |
| **添加新玩家** | 以复制模式（继承某玩家的牌组/遗物/金币）或初始牌组模式加入 |
| **移除玩家** | 清理离线玩家的所有数据（deck/relics/potions/grab_bag/map_history） |

## 安装

从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载 `MP_PlayerManager-vX.X.X.zip`，解压后双击 exe 运行。

## 使用步骤

1. **退出游戏**（重要！）
2. 运行工具
3. 选择存档路径（自动检测或手动输入）
4. 选择操作：夺舍 / 添加玩家 / 移除玩家
5. 确认后自动备份并修改
6. 重新启动游戏

## 夺舍功能

当有玩家断线后，房主可使用此功能让新玩家接管：
- 输入离线玩家的序号
- 输入接替者的 **Steam64位ID**（如 `76561198679823594`）
- 接替者将以离线玩家的角色、牌组、遗物、金币状态继续游戏

## 添加玩家功能

支持两种模式：
- **复制模式**：选择一个现有玩家，复制其牌组/遗物/金币/概率状态，新玩家以满血状态加入
- **初始牌组模式**：选择角色，以该角色的初始状态（基础牌组 + 初始遗物 + 100金币）加入

新玩家的 Steam ID 由用户手动输入。

## Steam 昵称显示（可选）

在存档的 `saves/` 同级目录创建 `steam_names.json`，格式如下：

```json
{
  "76561198679823594": "煎包",
  "76561199032167696": "小明"
}
```

工具会自动读取并显示玩家昵称。

## 存档路径说明

工具自动扫描以下路径：
- `%APPDATA%\SlayTheSpire2\steam\{SteamId}\profile*\saves\current_run_mp.save`
- `%APPDATA%\SlayTheSpire2\steam\{SteamId}\modded\profile*\saves\current_run_mp.save`

## 从源码构建

```powershell
cd MP_PlayerManager
# 安装依赖（如需）
pip install -r requirements.txt
# 打包为 exe
pyinstaller --onefile --name "MP_PlayerManager-v1.0.0" --console=1 manage_players.py
# 或直接运行
python manage_players.py
```

需要：Python 3.8+、PyInstaller（打包时）

## 版本记录

### v1.0.0（2026-03-20）

- 夺舍玩家：接管离线玩家状态继续游戏
- 添加新玩家：复制模式 + 初始牌组模式
- 移除玩家：清理离线玩家所有关联数据
- 自动备份存档
- Steam 昵称映射支持

## License

MIT
