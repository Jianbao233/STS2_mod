# FreeLoadout · 源码完整解析

> 版本：v3 · 2026-03-26
> 来源：https://github.com/Quorafind/FreeLoadout
> 源码：11 个文件（branch master）

---

## 一、整体定位

FreeLoadout 是一个**游戏内置训练器型 Mod**。不是通过外部工具注入，而是以标准 Mod 形式加载，通过 `CanvasLayer` 在游戏 UI 上方叠放自定义面板。功能覆盖：无限资源、卡牌/遗物/药水编辑、能力预设、事件/遭遇重入。

---

## 二、源码架构（11 个文件）

| 文件 | 职责 |
|------|------|
| `TrainerRuntime.cs` | **入口与核心状态**：Harmony 补丁总表、TrainerBootstrap、热键分发、TrainerBuffUi |
| `LoadoutPanel.cs` | **主 UI 框架**：7 个 Tab 的切换、嵌入游戏屏幕（CardLibrary/RelicCollection/PotionLab）、动态样式 |
| `Config.cs` | **配置系统**：热键绑定（支持 Ctrl/Shift/Alt+键）、布尔开关、从 DLL 旁或 exe 旁 config.json 读取 |
| `EmbeddedScreenPatches.cs` | **游戏 UI 补丁**：10+ 个 Harmony Patch，改造卡牌/遗物/药水库的交互行为 |
| `InspectCardEdit.cs` | **卡牌编辑面板**：检查屏幕旁叠放的侧边栏，编辑附魔/能量/关键词/变量 |
| `InspectRelicEdit.cs` | **遗物编辑面板**：同上，编辑叠放数/状态/蜡封属性 |
| `InspectPotionEdit.cs` | **药水编辑面板**：独立弹窗（点击药水图标触发），编辑稀有度/使用限制/变量 |
| `Loc.cs` | **本地化**：从 `res://FreeLoadout/localization/{lang}/ui.json` 加载，随游戏语言动态切换 |
| `FreeLoadout.csproj` | 目标框架 `net9.0`，依赖 `HarmonyX 2.12.0`，引用 `sts2.dll` / `GodotSharp.dll` / `0Harmony.dll` |
| `mod_manifest.json` | `pck_name: FreeLoadout`，版本 `0.3.0`，`has_pck: true`，`has_dll: true` |
| `build.ps1` | 调用 `_tools/mod_build_common.ps1` 工具链：dotnet build → Godot 纹理导入 → PCK 打包 → ZIP 发布 |

---

## 三、TrainerRuntime.cs：入口与核心机制

### 3.1 初始化

```csharp
[ModInitializer(nameof(Init))]
public static class TrainerBootstrap
{
    public static void Init()
    {
        Config.Load();
        new Harmony("bon.freeloadout").PatchAll(Assembly.GetExecutingAssembly());
        GD.Print("[FreeLoadout] Initialized");
    }
}
```

标准 Harmony Mod 入口，通过 `[ModInitializer]` 属性由游戏在加载阶段调用。

### 3.2 无限资源系统

| 资源 | 字段 | 开关方法 | 实现方式 |
|------|------|---------|---------|
| HP | `TrainerState.InfiniteHpEnabled` | `ToggleInfiniteHp()` | 每帧 Patch `NRun._Process`，调 `Creature.SetMaxHpInternal` + `SetCurrentHpInternal` |
| Energy | `InfiniteEnergyEnabled` | `ToggleInfiniteEnergy()` | 设置 `player.MaxEnergy` + `player.PlayerCombatState.Energy` |
| Stars | `InfiniteStarsEnabled` | `ToggleInfiniteStars()` | 设置 `player.PlayerCombatState.Stars` |
| Gold | `InfiniteGoldEnabled` | `ToggleInfiniteGold()` | 设置 `player.Gold` |

### 3.3 TrainerBuffUi：Power 节点混入机制

**核心思路**：不使用独立的图标资源，而是将训练器的状态伪装成一个 `PowerModel`，复用游戏已有的 `NPower` 节点渲染。

```csharp
// 注册 3 个"假" Power
internal sealed class TrainerHpIndicatorPower : TrainerIndicatorPowerBase { }
internal sealed class TrainerEnergyIndicatorPower : TrainerIndicatorPowerBase { }
internal sealed class TrainerGoldIndicatorPower : TrainerIndicatorPowerBase { }

// 图标映射：复用已有 Power 的美术资源
private static readonly Dictionary<Type, Type> _iconPowerMap = new()
{
    [typeof(TrainerHpIndicatorPower)] = typeof(RegenPower),        // 心跳红心
    [typeof(TrainerEnergyIndicatorPower)] = typeof(EnergyNextTurnPower), // 能量图标
    [typeof(TrainerGoldIndicatorPower)] = typeof(TrashToTreasurePower), // 金币
};
```

运行时：
1. `Sync()` 找到 `NCreature` → `NCreatureStateDisplay` → `NPowerContainer` 的 `_powerNodes` 列表
2. 用 `ModelDb.DebugPower<T>()` 创建假 PowerModel，挂到 `Creature` 上
3. 调用 `NPower.Create(model)` 生成节点，加入游戏原生 `NPowerContainer`
4. 触发 `UpdatePositions()` 刷新布局——训练器状态就和真实 buff 并排显示了

### 3.4 热键分发

```csharp
// NGameInputPatch：拦截 _Input，在游戏之前处理热键
[HarmonyPatch(typeof(NGame), nameof(NGame._Input))]
internal static class NGameInputPatch
{
    private static bool Prefix(NGame __instance, InputEvent inputEvent)
    {
        if (TrainerState.TryTogglePanel(__instance, inputEvent)) return false; // F1
        if (TrainerState.IsTrainerHotkey(inputEvent, Key.Escape)) { /* 关面板 */ return false; }
        // ...
    }
}

// 热键从 config.json 读取，支持 Ctrl/Shift/Alt 组合键
```

---

## 四、LoadoutPanel.cs：主 UI 框架

### 4.1 面板结构

```
CanvasLayer (Layer=100)
  └─ backstop (ColorRect半透明遮罩，点击关闭)
  └─ panel (StyleBoxFlat 深色圆角)
       └─ mainVBox
            ├─ tabBar: [Cards] [Relics] [Potions] [Powers] [Events] [Encounters] [Character]
            ├─ divider
            └─ scrollContainer → contentContainer

CanvasLayer (Layer=101, HoverTips)
```

### 4.2 Tab 切换策略

```
Tab 0 Cards / Tab 1 Relics / Tab 2 Potions
  → ShowEmbeddedScreen()：将游戏原生 NCardLibrary / NRelicCollection / NPotionLab
    作为子节点直接嵌入 panel，隐藏 panel 的装饰（背景/分割线），隐藏原生 BackButton
  → 同时用 StyleBoxFlat=透明覆盖 panel，视觉上"透明窗口里装游戏界面"

Tab 3-6 (Powers/Events/Encounters/Character)
  → HideEmbeddedScreens()：还原 panel 装饰，contentContainer 中由各自 Tabs/*.cs 构建
```

### 4.3 嵌入屏幕的装饰处理

```csharp
// 隐藏原生屏幕的阴影/渐变/Vignette 装饰
HideScreenShadows(screen);

// 隐藏底部贴图（游戏地图底部有纹理遮罩）
HideBottomTextures(screen);
```

### 4.4 HoverTip 层

FreeLoadout 将 `NHoverTipSet` 从游戏原层重新 parent 到自己的 `CanvasLayer(Layer=101)`：

```csharp
[HarmonyPatch(typeof(NHoverTipSet), nameof(NHoverTipSet.CreateAndShow))]
internal static class HoverTipReparentPatch { ... } // Postfix: ReparentToHoverTipLayer()
```

---

## 五、EmbeddedScreenPatches.cs：游戏 UI 改造（10+ Harmony Patch）

这是 FreeLoadout 最核心的交互逻辑，通过修改游戏原生屏幕的行为实现"在游戏界面里操作"。

### 5.1 遗物库

| Patch | 目标 | 行为 |
|-------|------|------|
| `RelicEntryBadgePatch` | `NRelicCollectionEntry._Ready` | 如果玩家已持有该遗物，在右下角加红色数字徽章；右键点击移除遗物 |
| `RelicEntryAddPatch` | `NRelicCollectionCategory.OnRelicEntryPressed` | **Ctrl+点击** → 批量获取遗物（按 `RelicBatchCount` 配置 1/5/10/20） |
| `RelicVisibilityPatch` | `NRelicCollectionEntry.Create` | 在 LoadoutPanel 激活时，将 `visibility=ModelVisibility.Visible` 强制设为所有遗物可见 |
| `InspectRelicOpenPatch` / `InspectRelicClosePatch` / `InspectRelicNavPatch` | `NInspectRelicScreen.Open/Close/SetRelic` | 打开检查遗物界面时 attach `InspectRelicEdit`，关闭时 detach |

### 5.2 卡牌库

| Patch | 目标 | 行为 |
|-------|------|------|
| `CardDetailAddPatch` | `NCardLibrary.ShowCardDetail` | **Ctrl+点击** → 获取卡牌到牌组；普通点击 → 直接打开检查界面（跳过 `DiscoveredCards` 检查） |
| `CardVisibilityPatch` | `NCardLibraryGrid.GetCardVisibility` | LoadoutPanel 激活时全部 `Visible` |
| `InspectCardOpenPatch` / `InspectCardClosePatch` / `InspectCardNavPatch` | `NInspectCardScreen` | 同遗物，attach `InspectCardEdit` |

### 5.3 药水实验室

| Patch | 目标 | 行为 |
|-------|------|------|
| `PotionLabClickPatch` | `NLabPotionHolder._Ready` | 修改 `MouseFilter=Pass` 以接收点击；**左键普通** → 打开药水编辑弹窗；**左键 Ctrl** → 获取药水 |
| `PotionVisibilityPatch` | `NLabPotionHolder.Create` | 强制 `ModelVisibility.Visible` |

### 5.4 HoverTip 重定向

| Patch | 目标 | 行为 |
|-------|------|------|
| `CreatureHoverTipsPatch` | `Creature.get_HoverTips` | 在生物的悬停提示中混入训练器状态的提示 |

---

## 六、InspectCardEdit.cs：卡牌检查编辑（最复杂的编辑器）

### 6.1 布局

在检查卡牌屏幕旁叠放两个 `CanvasLayer(Layer=101)` 侧边栏面板：

```
左栏（AnchorLeft=0.02-0.20）：
  ├─ Values：所有 DynamicVar 的数值编辑器（+1/-1 按钮，Shift=+10，Ctrl=+5）
  ├─ Divider
  └─ Cost：0~5 和 X 的能量按钮（区分"花费 X"和"固定费用"）

右栏（AnchorLeft=0.80-0.98）：
  ├─ Keywords：Exhaust / Ethereal / Innate / Retain / Sly / Eternal / Unplayable 开关
  ├─ Enchantment：当前附魔（可清空、可调数值、可添加新附魔）
  └─ Actions：Upgrade / Downgrade / Remove（未拥有时还有 Acquire 按钮）
```

### 6.2 能量费用编辑细节

游戏中的 `CardEnergyCost` 有两种模式：
- **固定费用**：`canonicalCost = N`
- **可变费用**：`CostsX = true, canonicalCost = 基准值`

通过反射 `CardModel._energyCost` 字段，将 `CardEnergyCost(card, newCost, costsX)` 替换进去。

### 6.3 动态刷新

每次编辑操作后调用 `DoRefreshAll()`，依次：
1. `UpdateCardDisplay()` — 刷新检查屏幕
2. `RefreshGameCardVisuals()` — 递归遍历整棵树，找到所有 `NCard` 节点，调用 `UpdateVisuals()`
3. `pile.ContentsChanged()` — 通知所在卡堆刷新
4. 重新构建左右编辑面板

### 6.4 附魔系统

```csharp
// 从 ModelDb.DebugEnchantments 获取所有可用附魔，过滤 DeprecatedEnchantment
// canEnchant = enchantment.CanEnchantCardType(card.Type) 决定按钮颜色
// Enchant(model, card, amount) / ClearEnchantment(card)
```

---

## 七、InspectRelicEdit.cs：遗物检查编辑

比卡牌简单，核心功能：

| 功能 | 说明 |
|------|------|
| Acquire | 批量获取（×1/5/10），可配置 `RelicBatchCount` |
| Remove | 移除已持有的该遗物 |
| DynamicVars | 与卡牌相同的数值编辑器 |
| Status | Normal / Active / Disabled 三态切换（`RelicStatus` enum） |
| StackCount | 叠放层数（通过反射 `RelicModel. k__BackingField` 设置） |
| IsWax / IsMelted | 遗物特殊状态 toggle |

---

## 八、InspectPotionEdit.cs：药水编辑（独立弹窗）

与其他检查编辑不同，药水是**点击时新建 `CanvasLayer` 弹窗**（`Layer=102`，位于最顶层）：

```
背景遮罩（点击关闭）
  └─ panel (20%~80% 宽度)
       ├─ 标题栏 + 关闭按钮
       ├─ 药水图标 + 名称预览（128x128 TextureRect）
       ├─ Info：稀有度（Common/Uncommon/Rare/Event）/ 使用限制（CombatOnly/AnyTime/Automatic）
       ├─ Values：DynamicVar 数值编辑
       └─ Acquire 按钮
```

---

## 九、Config.cs：配置系统设计

### 9.1 热键格式

```
Key 或 Ctrl+Key 或 Shift+Alt+Key
```

解析器（`HotkeyBinding.Parse`）：
- 按 `+` 分割各项
- 前 N-1 项为修饰符（Ctrl/Shift/Alt）
- 最后一项为实际按键

### 9.2 配置路径查找顺序

```csharp
// 1. 从 DLL 所在目录
string? dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

// 2. Fallback：从 exe 同级目录的 mods/FreeLoadout/
string? exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
return Path.Combine(exeDir, "mods", "FreeLoadout", FileName);

// 3. 当前目录兜底
return FileName;
```

### 9.3 写入默认值

首次找不到 `config.json` 时自动创建：
```json
{
  "_readme": "Hotkey format: Key or Ctrl+Key or Shift+Alt+Key...",
  "hotkeys": { "toggle_panel": "F1" },
  "flags": { "show_topbar_icon": true }
}
```

---

## 十、TrainerRuntime.cs：事件/遭遇重入系统

### 10.1 事件重入

```csharp
// 保存当前房间信息（类型、MapPointType、ModelId）
TrainerEventState.SaveCurrentRoom();

// Patch NEventRoom.Proceed：检测 IsNestedEvent 后拦截
[HarmonyPatch(typeof(NEventRoom), nameof(NEventRoom.Proceed))]
// → 改为 ReturnToSavedRoom()：根据保存的房间类型重新进入
//   - Monster/Elite/Boss → EnterRoomDebug() 进入战斗
//   - RestSite → EnterRoomDebug(RoomType.RestSite)
//   - Shop → EnterRoomDebug(RoomType.Shop)
//   - Event → EnterRoomDebug(RoomType.Event)
//   - null → 退回地图
```

### 10.2 地图历史压制

```csharp
[HarmonyPatch(typeof(RunState), nameof(RunState.AppendToMapPointHistory))]
private static bool Prefix() => !TrainerEventState.SuppressMapHistory;
```

在重入过程中压制地图历史记录，避免退出事件后地图状态混乱。

### 10.3 Ctrl+地图跳跃

```csharp
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen._Process))]
// 每帧检测 Ctrl 键：ctrlHeld → SetDebugTravelEnabled(true)
```

按住 Ctrl 可以在地图上任意跳转（Debug 模式）。

---

## 十一、PowerPresets：能力预设系统

### 11.1 机制

在 `NRun._Process` 的 Patch 中（`NRunProcessPatch`），每帧检查：

```csharp
// 仅在 Player 侧（RoundStart）执行一次
if (state.CurrentSide == CombatSide.Player && state.RoundNumber != _lastAppliedRound)
{
    _lastAppliedRound = state.RoundNumber;
    foreach (Creature creature in state.Creatures)
    {
        if (creature.IsDead) continue;
        var presets = creature.IsPlayer ? PlayerPowers : EnemyPowers;
        foreach (var (powerType, amount) in presets)
            PowerCmd.Apply(power, creature, amount);
    }
}
```

### 11.2 UI 控件

在 `LoadoutPanel` 的上下文栏中：
- 预设总开关（PresetTarget=0 → Player / 1 → Enemy）
- 数量选择（×1/5/10/20）

---

## 十二、Loc.cs：本地化

```csharp
// 路径格式：res://FreeLoadout/localization/{language}/ui.json
// language 跟随 LocManager.Instance.Language

private static string GetLanguage()
{
    string lang = LocManager.Instance?.Language ?? "eng";
    if (_strings != null && _loadedLanguage == lang) return _strings;
    _strings = LoadTable(lang);
    if (_strings.Count == 0 && lang != "eng")
        _strings = LoadTable("eng"); // fallback 到英语
    _loadedLanguage = lang;
    return _strings;
}
```

---

## 十三、build.ps1：构建脚本分析

### 13.1 依赖工具链

```
$repoRoot/_tools/mod_build_common.ps1   ← 共享构建函数库
```

### 13.2 构建步骤

```
1. dotnet build -c Release
2. Copy DLL + PDB → mods/FreeLoadout/
3. Copy mod_manifest.json → mods/FreeLoadout/FreeLoadout.json
4. Copy mod_image.png → mods/FreeLoadout/FreeLoadout/
5. Copy assets/images 和 assets/FreeLoadout → _pck_src/
6. Import-GodotTextures()  → Godot 纹理导入（tpage 格式）
7. Build-ModPck()         → 打包 PCK
8. Package-ModZips()       → 生成 zip 发布包
```

### 13.3 与本地 MP_PlayerManager build.ps1 的差异

| 方面 | FreeLoadout build.ps1 | 本地 MP_PlayerManager build.ps1 |
|------|----------------------|-------------------------------|
| 目标框架 | net9.0 | net8.0 |
| 配置 | `-c Release` | `-c Debug` |
| PCK 源 | `_pck_src/`（含 Godot 纹理导入） | 直接从项目根 |
| Godot 纹理 | `Import-GodotTextures` 工具函数 | 无 |
| 发布包 | ZIP via `Package-ModZips` | 无 |
| PCK 路径 | `mods/{ModName}/{ModName}.pck` | `build/` 目录 |
| mod_manifest 重命名 | 复制为 `{ModName}.json` | 保持原名 |

---

## 十四、对 MP_PlayerManager 的参考价值

### 14.1 可直接借鉴的设计

1. **嵌入原生屏幕**（LoadoutPanel `ShowEmbeddedScreen`）
   → MP_PlayerManager 的 CardsTab/RelicsTab/PotionsTab 若需嵌入游戏库，可复用该模式。

2. **训练器指示器 Power 混入**（TrainerBuffUi）
   → 如果 MP_PlayerManager 需要在 UI 上显示"正在编辑的模板"状态，可创建一个假 Power 复用 `NPower` 节点。

3. **动态变量编辑器**（InspectCardEdit `AddValueEditor`）
   → 统一的 `LineEdit + [-] / [+] ` 组件，可直接移植。

4. **Ctrl+点击获取机制**（EmbeddedScreenPatches）
   → 在 MP_PlayerManager 的卡牌/遗物/药水选择器中实现 Ctrl+点击追加到模板。

5. **Config 热键解析**（Config.cs `HotkeyBinding`）
   → 非常完善的热键组合解析，可用于 F1 热键自定义。

6. **Patch 隔离层**（EmbeddedScreenPatches.cs）
   → 每个 Patch 保持单一职责，分类清晰（Card/Relic/Potion 三大类）。

### 14.2 需注意的实现细节

- **ModelDb 反射**：`ModelDb.DebugEnchantments`、`ModelDb.DebugPower<T>()`、`ModelDb.AllCards` 来自 `sts2.dll`，需要正确引用。
- **MutableClone**：`card.ToMutable()` / `relic.ToMutable()` 是创建可编辑副本的标准方式。
- **Deferred 调用**：UI 修改尽量通过 `Callable.From(...).CallDeferred()` 延迟到主线程。
- **Layer 层级**：训练器 UI=100，HoverTip=101，药水弹窗=102。游戏 UI 通常在 0-50。
- **StyleBoxFlat vs StyleBoxEmpty**：按钮用 Flat，正常状态/悬浮状态/按下状态各自不同的配色。

---

*文档随源码更新。源码版本：master 分支最新提交。*
