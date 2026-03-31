# MP_PlayerManager · 子记忆文本

> 本文件为 MP_PlayerManager v2 项目的专属记忆，每次新对话开始时请先阅读本文。
> 当前版本：v0.2.0（开发中）
> 更新日期：2026-03-28

---

## 1. 项目概述

| 项目 | 说明 |
|------|------|
| **路径** | `K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\` |
| **目标** | 在 FreeLoadout Mod 框架内扩展，实现角色模板配置 + 多人存档管理 |
| **发布** | https://github.com/Jianbao233/STS2_mod/releases |
| **父项目** | `MP_PlayerManager_v1/`（v1 独立 exe 工具，已归档） |

---

## 2. 活跃版本

| 版本 | 状态 | 说明 |
|------|------|------|
| **v0.2.0** | 开发中 | 核心闭环完成：TemplateApplier / Shift 交互 / 全部 Tab / Steam+Save+Backup / 中英双语 |
| **v0.1.0** | 2026-03-26 | FreeLoadout 框架继承、TemplatesTab 基础 UI、模板导入导出 |
| **v1** | 已归档 | 独立 exe 工具，Python + PyInstaller |

---

## 3. 当前开发状态（v0.2.0）

### 3.1 总览

**Mod 端（FreeLoadout 扩展）：核心功能 100% 完成 ✅**

- [x] 角色模板创建 / 复制 / 删除 / 重命名
- [x] 角色选择（Ironclad / Silent / Defect / Necrobinder / Regent + Mod 角色，动态 ModelDb）
- [x] 基础属性编辑（MaxHp / CurHp / Energy / Gold，滑块 + 输入框）
- [x] 卡牌列表（右键移除，单个添加）
- [x] 模板导出 / 导入 FileDialog（JSON 文件）
- [x] **TemplateApplier.cs**：将模板应用到局内玩家（HP/Gold/Energy/卡组/遗物）
- [x] **Shift+左键卡牌库**：追加单张卡牌到当前模板
- [x] **Shift+右键卡牌库**：从当前模板移除单张卡牌
- [x] **Shift+左键遗物库**：追加遗物到当前模板
- [x] **Shift+右键遗物库**：从当前模板移除遗物
- [x] **Apply 按钮**（TemplatesTab + CharacterTab 内）
- [x] 本地化（中文 `zho` / 英文 `eng`，跟随游戏语言）
- [x] dotnet build 通过，PCK ~122KB，DLL ~73KB

**Tab 完成度：**

| Tab | 状态 | 说明 |
|-----|------|------|
| TemplatesTab | ✅ | 完整：列表+编辑器+Apply+Shift交互 |
| CharacterTab | ✅ | HP滑块/Gold/Energy/Stars/快捷操作 |
| RelicsTab | ✅ | 遗物获取/移除 UI，搜索弹窗 |
| PowersTab | ✅ | Power获取/移除，预设快捷（Strength/Dex/Thorns/Regen） |
| PotionsTab | ⚠️ | UI 完成，PotionCmd API 待验证 |
| EventsTab | ✅ | 事件重入/跳过 UI，地图快捷打开 |
| EncountersTab | ✅ | 遭遇重置/假战斗 UI，当前房间信息 |

**Core 服务：**

| 服务 | 状态 | 说明 |
|------|------|------|
| TemplateApplier | ✅ | 核心：Apply模板到玩家（HP/Gold/Energy/卡组/遗物） |
| PlayerOpsService | ⚠️ | 骨架：夺舍/添加/移除玩家（游戏API验证中） |
| SaveManagerHelper | ✅ | 存档读写（CRLF JSON）/ 扫描 / 备份 / 恢复 / 删除 |
| SteamIntegration | ✅ | Steam ID/昵称读取（localconfig.vdf / loginusers.vdf） |

**Tab（Steam/Save/Backup）：**

| Tab | 状态 | 说明 |
|-----|------|------|
| SaveTab | ✅ | 存档列表/保存/恢复/删除 |
| BackupTab | ✅ | 全局备份列表/创建/恢复/清理旧备份 |

**外部工具端（Python）：进度 95%**

- [x] 夺舍 / 添加 / 移除玩家
- [x] Steam 好友选择（本地离线）
- [x] 存档读写（CRLF 明文 JSON）
- [x] 备份管理
- [ ] **读取 Mod 导出的模板 JSON**（与 Mod 端对接）

---

## 4. 架构

```
游戏内（Mod / C# — FreeLoadout 扩展）
  ├── F1 呼出主面板（LoadoutPanel）
  ├── TemplatesTab — 模板列表 + 编辑器
  │     ├── 创建 / 复制 / 删除 / 重命名
  │     ├── 角色选择 + 属性（MaxHp/CurHp/Energy/Gold）
  │     ├── 卡牌列表（右键移除，Shift+点击追加）
  │     ├── 导出 / 导入 FileDialog
  │     └── Apply 按钮（→ TemplateApplier）
  ├── EmbeddedScreenPatches — Shift+点击卡牌库/遗物库
  │     ├── Shift+左键追加 / Shift+右键移除
  │     └── NCardLibrary / NRelicCollectionCategory Harmony Postfix
  ├── CardBrowserPanel — 模态选卡弹窗（支持普通 / 批量两种模式）
  ├── TemplateApplier — 模板应用到玩家（HP/Gold/Energy/卡组/遗物）
  ├── Core/
  │     ├── TemplateApplier.cs  ✅
  │     ├── PlayerOpsService.cs ⚠️（骨架）
  │     └── SaveManagerHelper.cs ✅
  ├── Steam/
  │     └── SteamIntegration.cs ✅
  ├── Tabs/
  │     ├── TemplatesTab.cs  ✅
  │     ├── CharacterTab.cs  ✅
  │     ├── RelicsTab.cs     ✅
  │     ├── PowersTab.cs     ✅
  │     ├── PotionsTab.cs    ⚠️（API待验证）
  │     ├── EventsTab.cs     ✅
  │     ├── EncountersTab.cs ✅
  │     ├── SaveTab.cs       ✅
  │     └── BackupTab.cs     ✅
  └── localization/
        ├── eng/ui.json      ✅（完整）
        └── zho/ui.json      ✅（完整）

游戏外（外部工具 / Python）
  ├── CustomTkinter GUI（暗色，1200×780）
  ├── 夺舍 / 添加 / 移除玩家
  ├── 读取存档 current_run_mp.save（CRLF 明文 JSON）
  ├── Steam 好友选择（本地离线）
  └── 读取 Mod 模板 JSON（待实现 → 对接 TemplatesTab 导出文件）
```

---

## 5. Mod 端文件清单

路径：`K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout\`

| 文件 | 状态 | 说明 |
|------|------|------|
| `src/TemplateData.cs` | ✅ | 数据模型（Id/Name/CharacterId/CurHp/MaxHp/Energy/Gold/Cards/Relics/Potions） |
| `src/TemplateStorage.cs` | ✅ | JSON 持久化（`OS.GetUserDataDir()`） |
| `src/LoadoutPanel.cs` | ✅ | 主面板 + 9 个 Tab 注册 + 嵌入游戏原生屏幕 |
| `src/TrainerBootstrap.cs` | ✅ | 入口，Harmony Patch 注册，两帧延迟初始化 |
| `src/TemplateApplier.cs` | ✅ | **核心**：模板应用到玩家（HP/Gold/Energy/卡组/遗物） |
| `src/EmbeddedScreenPatches.cs` | ✅ | **Shift 交互**：NCardLibrary/NRelicCollectionCategory Harmony Postfix |
| `src/Core/PlayerOpsService.cs` | ⚠️ | 夺舍/添加/移除玩家骨架（游戏API验证中） |
| `src/Core/SaveManagerHelper.cs` | ✅ | 存档读写/CRLF/扫描/备份/恢复/删除 |
| `src/Steam/SteamIntegration.cs` | ✅ | Steam ID/昵称读取（localconfig.vdf / loginusers.vdf） |
| `src/Tabs/TemplatesTab.cs` | ✅ | 模板列表+编辑器+Apply+Shift交互 |
| `src/Tabs/CharacterTab.cs` | ✅ | HP滑块/Gold/Energy/Stars/快捷操作 |
| `src/Tabs/RelicsTab.cs` | ✅ | 遗物获取/移除 UI，搜索弹窗 |
| `src/Tabs/PotionsTab.cs` | ⚠️ | 药水浏览 UI（PotionCmd API 待验证） |
| `src/Tabs/PowersTab.cs` | ✅ | Power获取/移除，预设快捷按钮 |
| `src/Tabs/EventsTab.cs` | ✅ | 事件重入/跳过 UI，地图快捷打开 |
| `src/Tabs/EncountersTab.cs` | ✅ | 遭遇重置/假战斗 UI，当前房间信息 |
| `src/Tabs/SaveTab.cs` | ✅ | 存档列表/保存/恢复/删除 |
| `src/Tabs/BackupTab.cs` | ✅ | 全局备份列表/创建/恢复/清理旧备份 |
| `src/CardBrowserPanel.cs` | ✅ | 选卡弹窗（普通模式 + 批量模式） |
| `src/Config.cs` | ✅ | 配置（热键 / 语言 / export_dir） |
| `src/Loc.cs` | ✅ | 国际化（`res://localization/{lang}/ui.json`） |
| `src/HotkeyBinding.cs` | ✅ | 热键解析（支持 Ctrl/Shift/Alt 组合键） |
| `src/UiHelper.cs` | ✅ | UI 辅助工具 |
| `localization/zho/ui.json` | ✅ | 简体中文（完整覆盖） |
| `localization/eng/ui.json` | ✅ | English（完整覆盖） |
| `mod_manifest.json` | ✅ | version: 0.2.0 |
| `build.ps1` | ✅ | dotnet build → Godot PCK 导出 → 同步 mods + torelease |
| `MP_PlayerManager.csproj` | ✅ | Godot.NET.Sdk 4.5.1，net9.0 |
| `MODIFICATION_SCHEME.md` | ✅ | 源码完整解析 + 修改方案 |

---

## 6. 构建与部署

### 构建命令
```powershell
cd "K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout"
.\build.ps1
```

### 输出路径
| 路径 | 说明 |
|------|------|
| `K:\SteamLibrary\...\Slay the Spire 2\mods\MP_PlayerManager\` | 游戏 mods（调试用） |
| `K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout\torelease\` | 发布快照 |
| `%APPDATA%\SlayTheSpire2\mod_settings\MP_PlayerManager\config.json` | 配置文件 |
| `%APPDATA%\SlayTheSpire2\logs\godot.log` | 调试日志 |

### 当前构建产物
- DLL：~73 KB
- PCK：~122 KB
- mod_manifest.json：0.2.0

### 注意事项
- **必须停游戏构建**（DLL 被游戏锁定）
- **mods 子目录内禁止放非 manifest 的 `.json`**（`ui.json`/`config.json` 只打进 PCK）
- **导出/导入**：通过 `FileDialog` 让用户自行选择文件路径，导出为 JSON
- **PotionCmd API 待验证**（游戏内部 API 未确认）

---

## 7. TemplatesTab 交互规范

### 模板列表（左栏）
- 搜索筛选
- 新建 / 复制 / 删除
- 导出 / 导入（FileDialog）
- 选中高亮（金色文字）

### 模板编辑器（右栏）
- 模板名称
- 角色选择（HFlow，自动填默认值）
- 基础属性（MaxHp / CurHp / Energy / Gold）
- 卡牌列表（右键移除）
- 添加卡牌（普通点击 → 单选弹窗）
- Apply 按钮（绿色 → TemplateApplier）
- Shift 操作提示文本

### Shift+点击机制
- **触发位置**：游戏原生 NCardLibrary / NRelicCollectionCategory（Harmony Postfix）
- **Shift+左键卡牌库**：追加单张卡牌到当前模板
- **Shift+右键卡牌库**：从当前模板移除该卡牌
- **Shift+左键遗物库**：追加遗物到当前模板
- **Shift+右键遗物库**：从当前模板移除该遗物

### 导出/导入
- **导出**：`FileDialog`（Mode=SaveFile）→ 用户选路径 → 写入 `templates_YYYYMMDD_HHMMSS.json`
- **导入**：`FileDialog`（Mode=OpenFile）→ 用户选 JSON 文件 → **完整替换**现有列表（避免残留）

---

## 8. 下一步开发任务

| 优先级 | 任务 | 涉及文件 | 状态 |
|--------|------|---------|------|
| **P0** | PotionCmd API 验证 | `TemplateApplier.cs` / `PotionsTab.cs` | ❌ 未验证 |
| **P1** | PlayerOpsService 真实实现 | `PlayerOpsService.cs` | ⚠️ 骨架完成 |
| **P1** | 工具端读取 Mod 模板 JSON | `characters.py` | ❌ 未实现 |
| **P2** | GitHub Release v0.2.0 | — | ❌ 未执行 |

---

## 9. 关键技术备忘

### 样式颜色
```csharp
Color Gold    = new("E3A83D")  // 标题/选中
Color Cream   = new("E3D5C1")  // 正文
Color Gray    = new("7F8C8D")  // 禁用
Color Red     = new("C0392B")  // 删除
Color Green   = new("27AE60")  // 添加
Color Blue    = new("2980B9")  // 特殊
Color Purple  = new("CC77FF")  // Power
```

### 数据获取
```csharp
ModelDb.AllCards       // 所有卡牌
ModelDb.AllRelics      // 所有遗物
ModelDb.AllPotions     // 所有药水
ModelDb.AllPowers      // 所有能力
ModelDb.AllCharacters  // 所有角色
RunManager.Instance.DebugOnlyGetState()  // 当前跑图
LocalContext.GetMe(state)              // 当前玩家
CardPile.Get(pileType, player)         // 牌堆
player.Creature?.SetMaxHpInternal(n)   // 设置最大HP
player.Creature?.SetCurrentHpInternal(n) // 设置当前HP
player.Gold = n                         // 设置金币
player.MaxEnergy = n                    // 设置最大能量
player.PlayerCombatState.Energy = n     // 设置当前能量
RelicCmd.Obtain(model, player)         // 获取遗物
RelicCmd.Remove(relic)                  // 移除遗物
CardPileCmd.Add(card, PileType.Deck, Bottom) // 添加卡牌
CardPileCmd.RemoveFromDeck(card, false) // 移除卡牌
PowerCmd.Apply(power, creature, amount) // 应用能力
PowerCmd.Remove(power, creature)        // 移除能力
```

### 本地化
```csharp
Loc.Get("key", "fallback")  // 获取文本
Loc.Fmt("key_fmt", arg0, arg1)  // 格式化文本
// 路径 res://localization/{lang}/ui.json
// lang: zho / eng，跟随游戏语言（可被 config.json 覆盖）
```

### 初始化模式（与 NCC/RHA 一致）
```csharp
tree.ProcessFrame += OnFrame1; // 帧1
tree.ProcessFrame -= OnFrame1;
tree.ProcessFrame += OnFrame2; // 帧2
// OnFrame2: DoRegister() + ApplyHarmonyPatches() + SteamIntegration.Initialize()
```

### FileDialog API（Godot 4.5.1）
```csharp
var dialog = new FileDialog {
    Access = FileDialog.AccessEnum.Filesystem,
    Title = "Title",
    CurrentPath = "default.json"
};
dialog.Mode = (FileDialog.ModeEnum)2; // 2=SaveFile, 0=OpenFile
dialog.AddFilter("*.json ; JSON Files");
dialog.FileSelected += path => { /* ... */ };
NGame.Instance?.AddChild(dialog);
dialog.PopupCentered(new Vector2I(800, 600));
```

### TemplateApplier 核心逻辑
```csharp
// 1. HP: creature.SetMaxHpInternal(maxHp) → creature.SetCurrentHpInternal(curHp)
// 2. Gold: player.Gold = gold
// 3. Energy: player.MaxEnergy = energy; player.PlayerCombatState.Energy = energy
// 4. 清空卡组: CardPileCmd.RemoveFromDeck(existingCards)
// 5. 重建卡组: CardPileCmd.Add(card, PileType.Deck, CardPilePosition.Bottom)
// 6. 清空遗物: RelicCmd.Remove(held)
// 7. 重建遗物: RelicCmd.Obtain(relicModel.ToMutable(), player)
// 8. 药水: PotionCmd（API 待验证）
```

---

## 10. 已知 Bug

| Bug | 描述 | 状态 |
|-----|------|------|
| CardBrowserPanel 卡顿 | 一次创建 500+ NCard 实例 | 已改善（改为 Shift+点击触发，非默认展示） |
| 新建出多条 | 连点导致多次新建 | 已修复（450ms 去抖） |
| 模板列表窄条 | ScrollContainer MinHeight=0 导致塌缩 | 已修复（固定 MinWidth + 外层高度 ExpandFill） |
| 导入残留 | 导入后原列表未清空导致重复 | 已修复（完整替换列表） |
| PotionCmd API | 药水获取/移除无验证可用 API | ⚠️ 待验证 |

---

## 11. 会话记录

| 日期 | 内容 | 摘要 |
|------|------|------|
| 2026-03-23 | FreeLoadout 扩展项目搭建 | 项目结构、命名空间重命名、README 致谢 |
| 2026-03-24 | v2 Python 工具多项修复 | 修复非房主昵称重复 Bug、移除 friends.json |
| 2026-03-25 | v2 复制玩家 + Templates Tab | 深拷贝 map_point_history、TemplateData/TemplatesTab 基础框架 |
| 2026-03-25 | Mod 构建成功 | export_presets.cfg、obj 冲突修复、PCK 61KB+DLL 43KB 同步到 mods |
| 2026-03-25 | 模组加载红字修复 | localization 路径改为 res://、config 改 AppData、清理 loose 文件 |
| 2026-03-25 | 初始化架构对齐 NCC | 改为 Harmony Postfix + 两帧延迟、F1InputNode 替代 NGameInputPatch |
| 2026-03-25 | TemplatesTab UI 重构 | 修复窄条/新建多条/删除无效、中文本地化 |
| 2026-03-26 | 修改方案审阅与文档 | 生成 MODIFICATION_SCHEME.md、记录 10 条修改要求 |
| 2026-03-26 | 导出导入改 FileDialog + Shift+点击批量添加 | 导出：用户选路径；导入：用户选文件（完整替换）；Shift+拦截层 + CardBrowserPanel 批量模式 |
| **2026-03-28** | **v0.2.0 全部步骤收尾** | TemplateApplier / EmbeddedScreenPatches / 全部 Tab / PlayerOpsService骨架 / SaveManagerHelper / SteamIntegration / SaveTab / BackupTab / 中英双语 / README同步 / mod_manifest 0.2.0 |

---

*v2 记忆 · 2026-03-28*

---

## 1. 项目概述

| 项目 | 说明 |
|------|------|
| **路径** | `K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\` |
| **目标** | 在 FreeLoadout Mod 框架内扩展，实现角色模板配置 + 多人存档管理 |
| **发布** | https://github.com/Jianbao233/STS2_mod/releases |
| **父项目** | `MP_PlayerManager_v1/`（v1 独立 exe 工具，已归档） |

---

## 2. 活跃版本

| 版本 | 状态 | 说明 |
|------|------|------|
| **v1** | 已归档 | 独立 exe 工具，Python + PyInstaller，约 8.4MB |
| **v2** | 开发中 | Godot Mod（FreeLoadout 扩展）+ 外部工具（Python + CustomTkinter） |

---

## 3. 当前开发状态（v2）

### 3.1 总览

**Mod 端（FreeLoadout 扩展）：模板系统 95% 完成**

- [x] 角色模板创建 / 复制 / 删除 / 重命名
- [x] 角色选择（Ironclad / Silent / Defect / Necrobinder / Regent + Mod 角色）
- [x] 基础属性编辑（MaxHp / CurHp / Energy / Gold）
- [x] 卡牌列表（右键移除，单个添加 / 批量 Shift+添加）
- [x] 模板导出 FileDialog（用户选择路径，JSON 文件）
- [x] 模板导入 FileDialog（用户选择文件，完整替换）
- [x] 本地化（中文 `zho` / 英文 `eng`，跟随游戏语言）
- [x] dotnet build 通过，PCK ~122KB，DLL ~73KB
- [ ] **Apply 按钮**：将模板数据应用到当前游戏局内玩家（高优先级）
- [ ] **Shift+右键移除**：在游戏原生卡牌库中右键移除模板中的卡牌

**外部工具端（Python）：进度 95%**

- [x] 夺舍 / 添加 / 移除玩家
- [x] Steam 好友选择（本地离线）
- [x] 存档读写（CRLF 明文 JSON）
- [x] 备份管理
- [ ] **读取 Mod 导出的模板 JSON**（与 Mod 端对接）

---

## 4. 架构

```
游戏内（Mod / C# — FreeLoadout 扩展）
  ├── F1 呼出主面板（LoadoutPanel）
  ├── TemplatesTab — 模板列表 + 编辑器
  │     ├── 创建 / 复制 / 删除 / 重命名
  │     ├── 角色选择 + 属性（MaxHp/CurHp/Energy/Gold）
  │     ├── 卡牌列表（右键移除，Shift+点击批量添加）
  │     └── 导出 / 导入 FileDialog
  ├── CardBrowserPanel — 模态选卡弹窗（支持普通 / 批量两种模式）
  ├── CardsTab / RelicsTab / PowersTab / EventsTab / EncountersTab / CharacterTab
  └── NRunProcessPatch — 每帧刷新 + PowerPresets

游戏外（外部工具 / Python）
  ├── CustomTkinter GUI（暗色，1200×780）
  ├── 夺舍 / 添加 / 移除玩家
  ├── 读取存档 current_run_mp.save（CRLF 明文 JSON）
  ├── Steam 好友选择（本地离线）
  └── 读取 Mod 模板 JSON（待实现 → 对接 TemplatesTab 导出文件）
```

---

## 5. Mod 端文件清单

路径：`K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout\`

| 文件 | 状态 | 说明 |
|------|------|------|
| `src/TemplateData.cs` | ✅ | 数据模型（Id/Name/CharacterId/CurHp/MaxHp/Energy/Gold/Cards/Relics/Potions） |
| `src/TemplateStorage.cs` | ✅ | JSON 持久化（`OS.GetUserDataDir()`） |
| `src/LoadoutPanel.cs` | ✅ | 主面板 + 8 个 Tab 注册 + 嵌入游戏原生屏幕 |
| `src/TrainerBootstrap.cs` | ✅ | `[ModInitializer]` 入口，Harmony Patch 注册 |
| `src/TrainerState.cs` | ✅ | 状态管理，F1 开关 |
| `src/F1InputNode.cs` | ✅ | `_Process` 轮询 F1，不阻断游戏输入 |
| `src/NRunProcessPatch.cs` | ✅ | 每帧应用 TrainerState + PowerPresets |
| `src/TrainerBuffUi.cs` | ✅ | Power 节点混入显示训练器状态 |
| `src/PowerPresets.cs` | ✅ | 战斗预设能力系统 |
| `src/Tabs/TemplatesTab.cs` | ✅ | **核心**：模板列表 + 编辑器 + 导出/导入 |
| `src/Tabs/CardsTab.cs` | ✅ | NCard 包装器 + 选中高亮 |
| `src/Tabs/RelicsTab.cs` | ✅ | 遗物管理 |
| `src/Tabs/PowersTab.cs` | ✅ | 能力管理 |
| `src/Tabs/EventsTab.cs` | ✅ | 事件重入 |
| `src/Tabs/EncountersTab.cs` | ✅ | 遭遇管理 |
| `src/Tabs/CharacterTab.cs` | ✅ | 角色信息（Build 为空，内容待实现） |
| `src/CardBrowserPanel.cs` | ✅ | 选卡弹窗（普通模式 + 批量模式） |
| `src/Config.cs` | ✅ | 配置（热键 / 语言 / export_dir） |
| `src/Loc.cs` | ✅ | 国际化（`res://localization/{lang}/ui.json`） |
| `src/HotkeyBinding.cs` | ✅ | 热键解析（支持 Ctrl/Shift/Alt 组合键） |
| `src/UiHelper.cs` | ✅ | UI 辅助工具 |
| `localization/zho/ui.json` | ✅ | 简体中文 |
| `localization/eng/ui.json` | ✅ | English |
| `mod_manifest.json` | ✅ | version: 0.1.0 |
| `build.ps1` | ✅ | dotnet build → Godot PCK 导出 → 同步 mods + torelease |
| `MP_PlayerManager.csproj` | ✅ | Godot.NET.Sdk 4.5.1，net9.0 |
| `MODIFICATION_SCHEME.md` | ✅ | 源码完整解析 + 修改方案 |

---

## 6. 构建与部署

### 构建命令
```powershell
cd "K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout"
.\build.ps1
```

### 输出路径
| 路径 | 说明 |
|------|------|
| `K:\SteamLibrary\...\Slay the Spire 2\mods\MP_PlayerManager\` | 游戏 mods（调试用） |
| `K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager\FreeLoadout\torelease\` | 发布快照 |
| `%APPDATA%\SlayTheSpire2\mod_settings\MP_PlayerManager\config.json` | 配置文件 |
| `%APPDATA%\SlayTheSpire2\logs\godot.log` | 调试日志 |

### 当前构建产物
- DLL：73 KB
- PCK：122 KB
- mod_manifest.json：0.6 KB

### 注意事项
- **必须停游戏构建**（DLL 被游戏锁定）
- **mods 子目录内禁止放非 manifest 的 `.json`**（`ui.json`/`config.json` 只打进 PCK）
- **导出/导入**：通过 `FileDialog` 让用户自行选择文件路径，导出为 JSON

---

## 7. TemplatesTab 交互规范

### 模板列表（左栏）
- 搜索筛选
- 新建 / 复制 / 删除
- 导出 / 导入（FileDialog）
- 选中高亮（金色文字）

### 模板编辑器（右栏）
- 模板名称
- 角色选择（HFlow，自动填默认值）
- 基础属性（MaxHp / CurHp / Energy / Gold）
- 卡牌列表（右键移除）
- 添加卡牌（普通点击 → 单选弹窗）

### Shift+点击机制
- **触发位置**：Cards Tab（嵌入游戏原生卡牌库）
- **实现方式**：`LoadoutPanel.ShowEmbeddedCardScreen()` 时在 `_mainVBox` 顶层叠加 `MouseFilter=Pass` 透明拦截节点
- **行为**：Shift+左键 → 弹出 `CardBrowserPanel` 批量选卡模式 → 完成后追加到当前模板
- **不阻断**：MouseFilter=Pass 正常点击继续向下传递给游戏原生 UI

### 导出/导入
- **导出**：`FileDialog`（Mode=SaveFile）→ 用户选路径 → 写入 `templates_YYYYMMDD_HHMMSS.json`
- **导入**：`FileDialog`（Mode=OpenFile）→ 用户选 JSON 文件 → **完整替换**现有列表（避免残留）

---

## 8. 下一步开发任务

| 优先级 | 任务 | 涉及文件 | 状态 |
|--------|------|---------|------|
| **P0** | Apply 按钮（将模板应用到局内玩家） | `CharacterTab.cs` / `TemplatesTab.cs` | ❌ 未实现 |
| **P0** | Shift+右键从模板移除卡牌 | `LoadoutPanel.cs` / `TemplatesTab.cs` | ❌ 未实现 |
| **P1** | 工具端读取 Mod 模板 JSON | `characters.py` | ❌ 未实现 |
| **P1** | CharacterTab 内容（当前玩家属性显示） | `CharacterTab.cs` | ❌ 未实现 |
| **P2** | GitHub Release v0.1.0 | — | ❌ 未执行 |

---

## 9. 关键技术备忘

### 样式颜色
```csharp
Color Gold    = new("E3A83D")  // 标题/选中
Color Cream   = new("E3D5C1")  // 正文
Color Gray    = new("7F8C8D")  // 禁用
Color Red     = new("C0392B")  // 删除
Color Green   = new("27AE60")  // 添加
Color Blue    = new("2980B9")  // 特殊
```

### 数据获取
```csharp
ModelDb.AllCards       // 所有卡牌
ModelDb.AllRelicPools  // 所有遗物
ModelDb.AllCharacters  // 所有角色
RunManager.Instance.DebugOnlyGetState()  // 当前跑图
LocalContext.GetMe(state)              // 当前玩家
CardPile.Get(pileType, player)         // 牌堆
```

### 本地化
```csharp
Loc.Get("key", "fallback")  // 获取文本
Loc.Fmt("key_fmt", arg0, arg1)  // 格式化文本
// 路径 res://localization/{lang}/ui.json
// lang: zho / eng，跟随游戏语言（可被 config.json 覆盖）
```

### 初始化模式（与 NCC/RHA 一致）
```csharp
tree.ProcessFrame += OnFrame1; // 帧1
tree.ProcessFrame -= OnFrame1;
tree.ProcessFrame += OnFrame2; // 帧2
// OnFrame2: DoRegister() + ApplyHarmonyPatches()
```

### FileDialog API（Godot 4.5.1）
```csharp
var dialog = new FileDialog {
    Access = FileDialog.AccessEnum.Filesystem,
    Title = "Title",
    CurrentPath = "default.json"
};
dialog.Mode = (FileDialog.ModeEnum)2; // 2=SaveFile, 0=OpenFile
dialog.AddFilter("*.json ; JSON Files");
dialog.FileSelected += path => { /* ... */ };
NGame.Instance?.AddChild(dialog);
dialog.PopupCentered(new Vector2I(800, 600));
```

---

## 10. 已知 Bug

| Bug | 描述 | 状态 |
|-----|------|------|
| CardBrowserPanel 卡顿 | 一次创建 500+ NCard 实例 | 已改善（改为 Shift+点击触发，非默认展示） |
| 新建出多条 | 连点导致多次新建 | 已修复（450ms 去抖） |
| 模板列表窄条 | ScrollContainer MinHeight=0 导致塌缩 | 已修复（固定 MinWidth + 外层高度 ExpandFill） |
| 导入残留 | 导入后原列表未清空导致重复 | 已修复（完整替换列表） |

---

## 11. 会话记录

| 日期 | 内容 | 摘要 |
|------|------|------|
| 2026-03-23 | FreeLoadout 扩展项目搭建 | 项目结构、命名空间重命名、README 致谢 |
| 2026-03-24 | v2 Python 工具多项修复 | 修复非房主昵称重复 Bug、移除 friends.json |
| 2026-03-25 | v2 复制玩家 + Templates Tab | 深拷贝 map_point_history、TemplateData/TemplatesTab 基础框架 |
| 2026-03-25 | Mod 构建成功 | export_presets.cfg、obj 冲突修复、PCK 61KB+DLL 43KB 同步到 mods |
| 2026-03-25 | 模组加载红字修复 | localization 路径改为 res://、config 改 AppData、清理 loose 文件 |
| 2026-03-25 | 初始化架构对齐 NCC | 改为 Harmony Postfix + 两帧延迟、F1InputNode 替代 NGameInputPatch |
| 2026-03-25 | TemplatesTab UI 重构 | 修复窄条/新建多条/删除无效、中文本地化 |
| 2026-03-26 | 修改方案审阅与文档 | 生成 MODIFICATION_SCHEME.md、记录 10 条修改要求 |
| 2026-03-26 | **导出导入改 FileDialog + Shift+点击批量添加** | 导出：用户选路径；导入：用户选文件（完整替换）；Shift+拦截层 + CardBrowserPanel 批量模式 |

---

*v2 记忆 · 2026-03-26*
