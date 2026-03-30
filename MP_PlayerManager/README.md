# MP_PlayerManager

多人存档玩家管理：**游戏内 Mod**（基于 FreeLoadout 扩展，角色模板等）+ **游戏外工具**（夺舍 / 添加 / 移除玩家，读写 `current_run_mp.save`）。

---

## 文档入口

| 内容 | 说明 |
|------|------|
| [FreeLoadout/README.md](FreeLoadout/README.md) | **Mod 端**：安装、功能、构建、致谢（主文档） |
| [MP_PlayerManager_v1/README.md](../MP_PlayerManager_v1/README.md) | **v1**：独立 exe 工具（已归档，仍可参考） |
| [MEMORY.md](MEMORY.md) | 开发状态与架构笔记（维护者 / AI 上下文） |
| [WORKFLOW_RULES.md](WORKFLOW_RULES.md) | 工作流约定 |

---

## 发行

从 **[STS2_mod Releases](https://github.com/Jianbao233/STS2_mod/releases)** 下载对应 zip / exe。

---

## 快速构建

| 组件 | 命令 | 依赖 |
|------|------|------|
| Mod | `cd FreeLoadout` → `.\build.ps1` | .NET 8、Godot 4.5.1 Mono |
| 外部工具 | `.\build_exe.bat` 或自行 `pyinstaller` 打包 `manage_players.py` | Python 3.8+、PyInstaller |

> 退出游戏后再运行外部工具修改存档。

---

## English (short)

**MP_PlayerManager** bundles an in-game **FreeLoadout-based mod** (templates, loadout UI) and an **out-of-game Python tool** for multiplayer save editing (possess / add / remove players).

- **Mod docs:** [FreeLoadout/README.md](FreeLoadout/README.md)  
- **Releases:** [STS2_mod Releases](https://github.com/Jianbao233/STS2_mod/releases)
