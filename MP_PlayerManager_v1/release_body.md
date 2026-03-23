# MP_PlayerManager v1.0.0 正式发布

## 多人存档玩家管理工具

> 首次发布。管理 Slay the Spire 2 多人联机存档，无需放入游戏目录，独立 exe 运行。

---

## 功能

| 操作 | 说明 |
|------|------|
| **夺舍玩家** | 输入离线玩家的序号 + 接替者 Steam64位ID，继承离线玩家的所有状态继续游戏 |
| **添加新玩家 — 复制模式** | 选择源玩家，复制其牌组/遗物/金币/随机数状态，满血加入；可换角色 |
| **添加新玩家 — 初始牌组模式** | 选择角色，以该角色的初始状态（基础牌组 + 初始遗物 + 100金币 + 满血）加入 |
| **移除玩家** | 清理离线玩家的所有数据（deck/relics/potions/grab_bag/map_history/map_drawings） |

---

## 其他特性

- 自动备份存档（修改前生成 `current_run_mp.save.backup.{timestamp}`）
- Steam 昵称映射（`steam_names.json`）
- Mod 角色自动发现：支持通过 `player_template.json` 注册自定义角色（内置观者 Watcher 支持）
- 纯 Python，无外部依赖
