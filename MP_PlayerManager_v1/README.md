# MP_PlayerManager · 多人存档玩家管理工具

> 管理 Slay the Spire 2 多人存档：夺舍（接管离线玩家）、添加新玩家、移除离线玩家。

---

## 架构说明

```
┌─────────────────────────────────────────────────────┐
│  游戏内 Mod 部分 (MP_PlayerManager_mod)              │
│  职责：导出角色模板 (player_template.json)            │
│  运行环境：游戏进程中                               │
└─────────────────────────────────────────────────────┘
                      ↓
          player_template.json 文件
                      ↓
┌─────────────────────────────────────────────────────┐
│  游戏外工具部分 (MP_PlayerManager_v2)               │
│  职责：存档改写（夺舍/添加/移除玩家）                  │
│  运行环境：游戏退出后，Windows 独立运行             │
│  技术栈：Python 3 + tkinter（无外部依赖）             │
└─────────────────────────────────────────────────────┘
```

---

## 功能一览

| 操作 | 说明 |
|------|------|
| **夺舍玩家** | 输入离线玩家的序号 + 接替者 Steam64位ID，继承离线玩家的所有状态继续游戏 |
| **添加新玩家 — 复制模式** | 选择源玩家，复制其牌组/遗物/金币/随机数状态，满血加入；可换角色 |
| **添加新玩家 — 初始牌组模式** | 选择角色，以该角色的初始状态（基础牌组 + 初始遗物 + 100金币 + 满血）加入 |
| **移除玩家** | 清理离线玩家的所有数据（deck/relics/potions/grab_bag/map_history/map_drawings） |
| **备份管理** | 自动备份历史 + 手动恢复 |

---

## 安装与运行

1. **退出游戏**（重要！修改存档期间游戏必须关闭）
2. 运行 `MP_PlayerManager-v2.x.x.exe`
3. 选择存档路径（自动扫描，或手动输入）
4. 选择操作：夺舍 / 添加玩家 / 移除玩家 / 备份管理
5. 确认后自动备份原存档并执行修改
6. 重新启动游戏

> 无需放入游戏目录，放在任意位置均可独立运行。

---

## Steam 好友选择

在"夺舍玩家"和"添加玩家"页面中，Steam64 位 ID 输入框旁提供**好友选择按钮**，点击可从本地好友列表中直接选择，无需手动输入 ID。

> 需要在工具同目录下放置 `friends.json` 文件（格式见下方）。未来版本计划支持 Steam API 自动拉取。

### friends.json 格式

```json
{
  "friends": [
    {"id": "76561199032167696", "name": "小明"},
    {"id": "76561198812345678", "name": "煎包"}
  ]
}
```

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

### 已注册的 Mod 角色

| 模组 | character_id | 初始遗物 | 初始牌组 |
|------|------|------|------|
| Watcher（观者）| `CHARACTER.WATCHER` | `RELIC.PURE_WATER` | 4×STRIKE_P + 4×DEFEND_P + ERUPTION_P + VIGILANCE + GREED（11张）|

---

## 从源码构建

```powershell
cd MP_PlayerManager_v2
pip install -r requirements.txt   # 可选（tkinter 为内置）
python main.py                    # 直接运行
pyinstaller --onefile --name "MP_PlayerManager-v2.0.0" main.spec
```

需要：Python 3.8+

---

## 版本记录

### v2.0.0（开发中）

- GUI 重构（tkinter 单窗口 + 左侧导航）
- 兼容 schema_version=14 存档格式
- 新增备份管理功能
- 新增 Steam 好友选择器（friends.json 本地模式）
- 新增完整字段处理（enchantment/props/discovered_* 等）
- 修复 map_point_history 注入逻辑

### v1.0.0（2026-03-20）

- 夺舍玩家：接管离线玩家状态继续游戏
- 添加新玩家：复制模式 + 初始牌组模式
- 移除玩家：清理离线玩家所有关联数据
- 自动备份存档
- Steam 昵称映射支持（`steam_names.json`）
- Mod 角色模板接口

---

## License

MIT
