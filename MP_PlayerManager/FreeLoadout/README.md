# MP_PlayerManager

> 多人联机存档角色模板管理工具 v2 · FreeLoadout 扩展版

**版本状态**：开发中（v0.1.0）

本 Mod 在 [FreeLoadout](https://github.com/boninall/FreeLoadout)（哔哩哔哩 [@BravoBon](https://space.bilibili.com/370335371)）的框架基础上扩展，新增**角色模板配置**功能，服务于 Slay the Spire 2 多人联机开黑场景。

---

## 致谢 / Credits

### 核心依赖

| 项目 | 作者 | 说明 |
|------|------|------|
| **FreeLoadout** | [Boninall / BravoBon](https://space.bilibili.com/370335371) | 本 Mod 基于其框架开发，提供了完整的游戏内 UI 面板（F1 呼出）、卡牌/遗物/药水浏览与操作、游戏数据接口（ModelDb）等全部基础设施 |
| **Slay the Spire 2** | MegaCrit | 游戏本体 |
| **Lib.Harmony** | .NET Harmony Community | 运行时 IL Hook |
| **Godot Engine 4.5.1** | Godot Community | 跨平台游戏引擎 |

### 技术参考

| 项目 | 作者 | 说明 |
|------|------|------|
| NoClientCheats | 煎包 | STS2 Mod 开发参考项目（Godot C# SDK 模式） |
| STS2 Modding Wiki | STS2 社区 | Mod 开发文档与游戏内 API 参考 |

---

## 功能概述

### 游戏内（Mod）

- 继承 FreeLoadout 全部原有功能（F1 呼出面板、卡牌浏览、遗物获取、HP/能量修改等）
- **新增 Templates Tab**：配置角色模板（角色 + 卡组 + 遗物 + 药水）
- 模板持久化为 JSON 文件（`user_templates.json`）
- Steam 好友列表缓存写入

### 游戏外（外部工具）

- Python + **PyQt-SiliconUI**（灵动优雅的桌面 UI）
- 读取模板 JSON + `current_run_mp.save`
- 注入/夺舍/移除玩家，自动备份

---

## 目录结构

```
MP_PlayerManager/
├── FreeLoadout/              ← FreeLoadout 扩展项目（Godot C# Mod）
│   ├── src/                  ← C# 源码（命名空间已改为 MP_PlayerManager）
│   ├── MP_PlayerManager.csproj
│   ├── project.godot
│   ├── mod_manifest.json
│   ├── build.ps1             ← 构建脚本
│   └── README.md             ← 本文件
├── doc/
│   ├── v1_方案文档.md       ← v1 方案（归档）
│   └── v2_方案文档.md      ← v2 方案（当前）
├── tools/
│   ├── PYQT_SILICON_UI_ANALYSIS.md
│   └── extract_icons.py
├── data/                     ← 运行时数据（模板文件等）
└── WORKFLOW_RULES.md        ← 工作流规则
```

---

## 快速开始

### 前提条件

- Godot 4.5.1 Mono（用于导出 PCK）
- .NET 8 SDK（用于 `dotnet build`）
- Slay the Spire 2 已安装

### 构建

```powershell
# 1. 打开 PowerShell，进入 Mod 目录
cd "K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout"

# 2. 运行构建（如 Godot 不在默认位置，指定路径）
.\build.ps1
# 或手动指定 Godot 路径：
.\build.ps1 -GodotExe "C:\path\to\Godot_v4.5.1-stable_mono_win64.exe"

# 3. 输出位置
#    Mod 安装目录：K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\MP_PlayerManager\
#    发布快照：K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout\torelease\
```

### 安装

1. **停游戏**（重要：游戏运行时会锁定 DLL）
2. 将 `mods\MP_PlayerManager\` 下的全部文件复制到游戏 mods 目录：
   ```
   K:\SteamLibrary\steamapps\common\Slay the Spire 2\mods\MP_PlayerManager\
   ```
3. 重启游戏，按 **F1** 打开 FreeLoadout 面板

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| **0.1.0** | 2026-03-23 | 初始构建，基于 FreeLoadout 源码，命名空间重命名，Godot.NET.Sdk 项目结构 |

---

## 许可证

本项目代码（`src/` 目录下）遵循原 FreeLoadout 的开源协议。

---

*MP_PlayerManager · 煎包 · 2026-03-23*
