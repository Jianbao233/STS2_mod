# MP_PlayerManager · 多人存档玩家管理工具

> 管理 Slay the Spire 2 多人存档：夺舍（接管离线玩家）、添加新玩家、移除离线玩家。

**首次发布版本：v1.0.0（2026-03-20）**  
**exe**：`dist/MP_PlayerManager-v1.0.0.exe`（约 8.4 MB）  
**下载**：从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载 `MP_PlayerManager-v1.0.0.zip`

---

## 功能一览

| 操作 | 说明 |
|------|------|
| **夺舍玩家** | 输入离线玩家的序号 + 接替者 Steam64位ID，继承离线玩家的所有状态继续游戏 |
| **添加新玩家 — 复制模式** | 选择源玩家，复制其牌组/遗物/金币/随机数状态，满血加入；可换角色 |
| **添加新玩家 — 初始牌组模式** | 选择角色，以该角色的初始状态（基础牌组 + 初始遗物 + 100金币 + 满血）加入 |
| **移除玩家** | 清理离线玩家的所有数据（deck/relics/potions/grab_bag/map_history/map_drawings） |

---

## 安装与运行

从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载 `MP_PlayerManager-v1.0.0.zip`，解压后双击 `MP_PlayerManager-v1.0.0.exe` 运行。

> **无需放入游戏目录**，放在任意位置均可独立运行。

---

## 使用步骤

1. **退出游戏**（重要！修改存档期间游戏必须关闭）
2. 运行 `MP_PlayerManager-v1.0.0.exe`
3. 选择存档路径（自动扫描，或手动输入）
4. 选择操作：夺舍 / 添加玩家 / 移除玩家
5. 确认后自动备份原存档并执行修改
6. 重新启动游戏

---

## 操作详解

### 夺舍玩家

当有玩家断线后，房主可让新玩家接管：

- 输入离线玩家的**序号**
- 输入接替者的 **Steam64位ID**（如 `76561198679823594`）
- 接替者将以离线玩家的角色、牌组、遗物、金币状态继续游戏
- 接替者重新进入游戏房间即可

### 添加新玩家

支持两种模式：

- **复制模式**：选择一个现有玩家，复制其牌组/遗物/金币/概率状态，新玩家以满血状态加入；可选择是否更换角色（更换后遗物清空，需重新获取）
- **初始牌组模式**：选择角色，以该角色的初始状态（满血 + 100金币）加入

内置 5 个角色（铁甲战士、静默猎手、故障机器人、亡灵契约师、储君）。

> **观者（Watcher）** 通过 Watcher mod 的 `player_template.json` 自动注册，可从「添加新玩家→初始牌组模式」中选择，无需手动维护。
>
> 剥夺者、隐者属于 DLC 角色，默认不加入模板。

### Steam 昵称显示（可选）

在存档的 `saves/` 同级目录创建 `steam_names.json`，格式如下：

```json
{
  "76561198679823594": "煎包",
  "76561199032167696": "小明"
}
```

工具会自动读取并显示玩家昵称。

---

## 存档路径说明

工具自动扫描以下路径：

```
%APPDATA%\SlayTheSpire2\steam\{SteamId}\profile*\saves\current_run_mp.save
%APPDATA%\SlayTheSpire2\steam\{SteamId}\modded\profile*\saves\current_run_mp.save
```

找到多份存档时会显示每个存档的进阶等级、幕数、玩家列表，供选择。

---

## Mod 角色接口（Mod 作者指南）

如果你是 Mod 作者，希望你的自定义角色出现在工具的"初始牌组模式"中，只需在 mod 文件夹根目录放置一个 `player_template.json` 文件。

### 文件放置位置

```
Slay the Spire 2/mods/MyCharacterMod/
├── MyCharacterMod.json          <- 你的 mod 主文件
├── player_template.json         <- 新增：角色模板接口
└── ...
```

### 示例内容

```json
{
    "character_id": "MOD.MY_CHAR",
    "name": "我的角色",
    "max_hp": 75,
    "starter_relic": "RELIC.MY_CHAR_RELIC",
    "starter_deck": [
        "CARD.MY_STRIKE",
        "CARD.MY_STRIKE",
        "CARD.MY_STRIKE",
        "CARD.MY_DEFEND",
        "CARD.MY_DEFEND",
        "CARD.MY_DEFEND",
        "CARD.MY_SPECIAL"
    ]
}
```

### 字段说明

| 字段 | 必填 | 类型 | 说明 |
|------|------|------|------|
| `character_id` | **是** | string | 游戏内使用的角色 ID，必须以 `MOD.` 开头（如 `MOD.MY_CHAR`） |
| `name` | 否 | string | 在工具中显示的角色名称，不填则用 `character_id` |
| `max_hp` | 否 | int | 初始最大生命值，默认 70 |
| `starter_relic` | 否 | string | 初始遗物 ID，不填则新玩家无初始遗物 |
| `starter_deck` | 否 | string[] | 初始牌组（卡牌 ID 数组），不填则新玩家无初始牌组 |

### 工作原理

工具启动时会自动扫描 `mods/` 目录下所有包含 `player_template.json` 的文件夹，将角色注册到角色选择菜单。玩家选择该角色后，会以你定义的初始状态（满血 + 100金币）加入游戏。

### 已注册的 Mod 角色

| 模组 | character_id | 初始遗物 | 初始牌组 |
|------|------|------|------|
| Watcher（观者）| `CHARACTER.WATCHER` | `RELIC.PURE_WATER` | 4×STRIKE_P + 4×DEFEND_P + ERUPTION_P + VIGILANCE + GREED（11张）|

> Watcher 模板数据从实际存档 `current_run_mp.save` 中已使用观者的玩家数据提取：max_hp=72，遗物 RELIC.PURE_WATER + RELIC.CURSED_PEARL（诅咒珍珠为额外获取，非初始遗物）。

---

## 技术实现

- **纯 Python**：无外部依赖（标准库 json/base64/gzip/shutil）
- **自动备份**：修改前自动生成 `current_run_mp.save.backup.{timestamp}`
- **存档路径自动检测**：扫描 `%APPDATA%\SlayTheSpire2\steam\{SteamId}\{modded/}profile*/saves/`
- **Mod 角色自动发现**：启动时扫描 `mods/` 目录下的 `player_template.json`

### 存档数据关键字段

| 字段 | 说明 |
|------|------|
| `players[].net_id` | 数值型 Steam ID（如 76561198679823594） |
| `players[].steam_id` | 字符串型 Steam ID（与上同） |
| `players[].character_id` | 角色 ID（如 `CHARACTER.WATCHER`） |
| `players[].deck[]` | 牌组（`{id, floor_added_to_deck}`） |
| `players[].relics[]` | 遗物（`{id, floor_added_to_deck}`） |
| `players[].relic_grab_bag.relic_id_lists` | 遗物获取袋 |
| `players[].rng` / `players[].odds` | 随机数状态 / 概率状态 |
| `map_point_history[]` | 各层玩家统计（移除玩家时需清理引用） |
| `map_drawings` | Base64 + Gzip 编码的地图涂鸦（移除玩家时同步清理） |

---

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

---

## 版本记录

### v1.0.0（2026-03-20）

- 夺舍玩家：接管离线玩家状态继续游戏
- 添加新玩家：复制模式 + 初始牌组模式
- 移除玩家：清理离线玩家所有关联数据
- 自动备份存档
- Steam 昵称映射支持（`steam_names.json`）
- Mod 角色模板接口：mod 作者可通过 `player_template.json` 提供自定义角色

---

## License

MIT
