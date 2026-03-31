# MP_PlayerManager | 多人存档玩家管理

**当前版本**：v0.2.0（开发中）  
**多人联机存档玩家管理**：游戏内角色模板 Mod（FreeLoadout 扩展）+ 游戏外存档工具（Python）。

---

## 功能一览

### 游戏内（Mod — FreeLoadout 扩展，v0.2.0 ✅）

| 功能 | 说明 | 状态 |
|------|------|------|
| **F1 呼出面板** | 继承 FreeLoadout 全部原有功能 | ✅ |
| **Templates Tab** | 创建 / 复制 / 删除 / 重命名角色模板 | ✅ |
| **角色选择** | Ironclad / Silent / Defect / Necrobinder / Regent + Mod 角色（动态从 ModelDb 读取） | ✅ |
| **基础属性编辑** | MaxHp / CurHp / Energy / Gold（滑块 + 输入框） | ✅ |
| **卡牌列表** | 右键移除，Shift+点击批量添加 | ✅ |
| **模板导入 / 导出** | FileDialog 选择路径，JSON 文件格式 | ✅ |
| **模板应用到游戏** | Apply 按钮 → HP/Gold/Energy/卡组/遗物 实时生效 | ✅ |
| **Shift+点击卡牌库** | 从游戏卡牌库追加单张卡牌到当前模板 | ✅ |
| **Shift+右键卡牌库** | 从当前模板移除单张卡牌 | ✅ |
| **Shift+点击遗物库** | 从游戏遗物库追加遗物到当前模板 | ✅ |
| **Shift+右键遗物库** | 从当前模板移除遗物 | ✅ |
| **Character Tab** | HP 滑块 / Gold / Energy / Stars / 快捷操作（满血/+100金币/+3能量） | ✅ |
| **Relics Tab** | 遗物获取/移除 UI，搜索 + 弹窗选择 | ✅ |
| **Potions Tab** | 药水浏览 UI（PotionCmd API 待验证） | ✅ |
| **Powers Tab** | Power 获取/移除，预设快捷按钮（Strength/Dex/Thorns/Regen） | ✅ |
| **Events Tab** | 事件重入/跳过 UI，地图快捷打开 | ✅ |
| **Encounters Tab** | 遭遇重置/假战斗 UI，当前房间信息显示 | ✅ |
| **PlayerOpsService** | 夺舍/添加/移除玩家核心逻辑骨架（游戏 API 验证中） | ⚠️ |
| **SaveManagerHelper** | 存档读写（CRLF JSON）/ 扫描 / 备份 / 恢复 / 删除 | ✅ |
| **SteamIntegration** | Steam ID / 昵称读取（localconfig.vdf / loginusers.vdf） | ✅ |
| **Save Tab** | 存档列表 / 保存 / 恢复 / 删除 | ✅ |
| **Backup Tab** | 全局备份列表 / 创建 / 恢复 / 清理旧备份 | ✅ |
| **本地化** | 中文（zho）/ 英文（eng）完整覆盖，随游戏语言切换 | ✅ |

### 游戏外（外部工具 — Python）

| 操作 | 说明 |
|------|------|
| **夺舍玩家** | 输入离线玩家序号 + 接替者 Steam64 位 ID，继承所有状态继续游戏 |
| **添加新玩家 — 复制模式** | 选择源玩家，复制其牌组 / 遗物 / 金币 / 随机数状态，满血加入 |
| **添加新玩家 — 初始牌组模式** | 选择角色，以该角色初始状态加入（基础牌组 + 初始遗物 + 100 金 + 满血） |
| **移除玩家** | 清理离线玩家的所有数据（deck / relics / potions / map_history 等） |
| **备份管理** | 自动备份历史 + 手动恢复 |

---

## 安装 | Installation

### Mod（游戏内）

1. **停游戏**（游戏运行时会锁定 DLL）
2. 从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载 `MP_PlayerManager-vX.X.X.zip`，或自行构建（见下）
3. 将 `MP_PlayerManager.dll` 和 `mod_manifest.json` 放入游戏 mods 目录：
   ```
   Steam\steamapps\common\Slay the Spire 2\mods\MP_PlayerManager\
   ```
4. 重启游戏，按 **F1** 打开面板

### 外部工具（游戏外）

1. **退出游戏**（重要！修改存档期间游戏必须关闭）
2. 从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载 `MP_PlayerManager-vX.X.X.exe`
3. 双击运行，无需放入游戏目录

---

## 从源码构建

### Mod（FreeLoadout 扩展）

```powershell
cd "K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout"
.\build.ps1
```

**依赖**：.NET 8 SDK、Godot 4.5.1 Mono

**输出**：
- Mod 安装目录：`Steam\steamapps\common\Slay the Spire 2\mods\MP_PlayerManager\`
- 发布快照：`FreeLoadout\toRelease\`

### 外部工具（Python）

```powershell
cd "K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\tools"
pyinstaller --onefile --noconsole manage_players.py
```

**依赖**：Python 3.8+、PyInstaller

---

## 项目结构

```
MP_PlayerManager/
├── FreeLoadout/                   ← 游戏内 Mod（FreeLoadout 扩展，Godot C# Mod）
│   ├── src/                       ← C# 源码（命名空间：MP_PlayerManager）
│   ├── MP_PlayerManager.csproj
│   ├── project.godot
│   ├── mod_manifest.json
│   ├── build.ps1                  ← 构建脚本
│   ├── export_presets.cfg
│   ├── toRelease/                 ← 各版本发布快照
│   └── README.md                  ← Mod 端详细文档
├── tools/                         ← 游戏外工具（Python + CustomTkinter）
│   ├── manage_players.py          ← 主程序
│   ├── characters.py
│   ├── save_editor.py
│   └── friends_selector.py
├── data/                          ← 运行时数据（模板文件等）
├── doc/
│   ├── v1_方案文档.md              ← v1 方案（归档）
│   └── v2_方案文档.md              ← v2 方案
├── MEMORY.md                      ← 开发状态与架构笔记（维护者 / AI 上下文）
├── WORKFLOW_RULES.md              ← 工作流约定
└── README.md                      ← 本文件
```

---

## 版本历史

| 版本 | 日期 | 说明 |
|------|------|------|
| **v0.2.0** | 2026-03-28 | 核心闭环完成：TemplateApplier / Shift+点击追加-移除卡牌-遗物 / 全部 Tab（Character/Relics/Powers/Potions/Events/Encounters） / PlayerOpsService 骨架 / SaveManagerHelper / SteamIntegration / SaveTab / BackupTab / 中英双语完整覆盖 |
| **v0.1.0** | 2026-03-26 | FreeLoadout 框架继承、TemplatesTab 基础 UI、模板导入导出 |
| **v1** | 已归档 | 独立 exe 工具，Python + PyInstaller → 整合入 v2 |

> v1 归档文档见 [MP_PlayerManager_v1/README.md](../MP_PlayerManager_v1/README.md)

---

## 致谢

- [FreeLoadout](https://github.com/boninall/FreeLoadout)（[@BravoBon](https://space.bilibili.com/370335371)）：本 Mod 基于其框架开发
- [Lib.Harmony](https://github.com/pardeike/Harmony)：运行时 IL Hook
- STS2 Mod 开发社区

---

## License

MIT
