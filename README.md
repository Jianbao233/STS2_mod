# 杀戮尖塔2 Mod 制作 | Slay the Spire 2 Mods

Mods for Slay the Spire 2. Built with **vibe coding** — pure chaos, no guarantees. The author has zero programming background; all projects are created with AI assistance (Cursor / claude-4.6-sonnet).

杀戮尖塔 2 Mod 合集。采用 **vibe coding** 方式开发——代码混乱，质量不保证。作者是零编程基础的小白，项目均由 AI 协助完成。

---

## Projects | 子项目

| 项目 | 说明 | 链接 |
|------|------|------|
| ~~MP_SavePlayerRemover~~ | ~~多人存档玩家移除工具~~（已废弃，功能已被 MP_PlayerManager 整合） | ~~[README](MP_SavePlayerRemover/README.md)~~ |
| **NoClientCheats** | 多人联机禁止客机作弊；仅房主需安装 | [README](NoClientCheats/README.md) · [Releases](https://github.com/Jianbao233/STS2_mod/releases) |
| **MP_PlayerManager** | 多人存档玩家管理：夺舍/添加/移除玩家 | [README](MP_PlayerManager/README.md) · [Releases](https://github.com/Jianbao233/STS2_mod/releases) |
| **RunHistoryAnalyzer** | 历史记录异常检测：守恒定律+来源追溯，检测作弊 | [README](RunHistoryAnalyzer/README.md) · [Releases](https://github.com/Jianbao233/STS2_mod/releases) |
| **ControlPanel** | F7 控制面板：卡牌/药水/遗物/战斗快捷 | [README](ControlPanel/README.md) |
| **RichPing** | 自定义联机 Ping 文本（存活催促/死亡调侃） | [README](RichPing/VC_RICH_PING_README.md) · [Releases](https://github.com/Jianbao233/STS2_mod/releases) |
| **ModListHider** | 联机时隐藏 Mod 列表 / 原版模式；源码已迁移到独立仓库 | [Repo](https://github.com/Jianbao233/ModListHider) · [Releases](https://github.com/Jianbao233/ModListHider/releases) |
| **LoadOrderManager** | 手动调整 Mod 加载顺序；源码已迁移到独立仓库 | [Repo](https://github.com/Jianbao233/STS2-LoadOrderManager) · [Releases](https://github.com/Jianbao233/STS2-LoadOrderManager/releases) |
| **PVP_ParallelTurn** | 双人 PVP 并行回合同步实验；源码已迁移到独立仓库 | [Repo](https://github.com/Jianbao233/sts2-parallel-turn-pvp) |
| **RefreshShop** | 商店免费无限刷新；源码为独立仓库 | [Repo](https://github.com/Jianbao233/RefreshShop) |
| **DimensionalTraveler** | 次元旅人炼金协作角色；源码为独立仓库 | [Repo](https://github.com/Jianbao233/STS2-DimensionalTraveler) |
| **AutoModSubscriber** | 联机时自动订阅房主有但本机缺失的工坊 mod；本地保留于本仓 | [README](AutoModSubscriber/README.md) · [Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=3750485606) |

主仓库继续保存未迁移小 mod 源码；`ModListHider`、`LoadOrderManager`、`PVP_ParallelTurn`、`RefreshShop`、`DimensionalTraveler` 位于本地工作区内，但由各自独立仓库管理，`STS2_mod` 主仓只在根 README/MEMORY 中保留跳转说明。

---

## Quick Install | 快速安装

**Mod 类**（NoClientCheats、RichPing、RunHistoryAnalyzer、LoadOrderManager）：

1. 找到游戏目录：`Steam\steamapps\common\Slay the Spire 2`
2. 在 `mods` 文件夹内创建对应 Mod 子目录（如 `mods\RunHistoryAnalyzer\`）
3. 从对应项目的 GitHub Releases 下载 zip，解压到 `mods` 内（`LoadOrderManager` 使用其分仓库发布页）

**工具类**（~~MP_SavePlayerRemover~~ → 已废弃，使用 MP_PlayerManager）：解压到任意位置，双击 exe 运行，无需放入游戏目录。

---

## Build from Source | 从源码构建

| 项目 | 命令 | 依赖 |
|------|------|------|
| 主仓内 Mod（NoClientCheats、ControlPanel、RichPing、RunHistoryAnalyzer） | `cd 项目` → `.\build.ps1` | .NET 8、Godot 4.5.1 Mono |
| 独立仓 Mod（ModListHider、LoadOrderManager、PVP_ParallelTurn） | 到对应独立仓库执行构建脚本 | 以独立仓库 README 为准 |
| MP_PlayerManager | `cd MP_PlayerManager` → `.\build_exe.bat` 或 `pyinstaller ... manage_players.py` | Python 3.8+、PyInstaller |
| ~~MP_SavePlayerRemover~~ | ~~已废弃，使用 MP_PlayerManager~~ | ~~Python 3.8+、PyInstaller~~ |

---

## License

MIT
