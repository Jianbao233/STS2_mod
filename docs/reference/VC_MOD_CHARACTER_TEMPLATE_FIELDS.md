# Mod 角色初始模板 · 字段参考报告

> 整理日期：2026-03-20
> 目的：为 MP_PlayerManager / 其他项目提供一致的 mod 角色初始遗物/卡组字段命名参考

---

## 一、player_template.json 格式（Mod 标准）

这是当前项目中 mod 角色注册初始模板的约定格式，**已由 MP_PlayerManager 采纳**。

```json
{
    "character_id": "CHARACTER.WATCHER",
    "name": "观者",
    "max_hp": 72,
    "starter_relic": "RELIC.PURE_WATER",
    "starter_deck": [
        "CARD.STRIKE_P",
        "CARD.STRIKE_P",
        "CARD.STRIKE_P",
        "CARD.STRIKE_P",
        "CARD.DEFEND_P",
        "CARD.DEFEND_P",
        "CARD.DEFEND_P",
        "CARD.DEFEND_P",
        "CARD.ERUPTION_P",
        "CARD.VIGILANCE_P",
        "CARD.GREED_P"
    ]
}
```

### 字段说明

| JSON 字段 | 类型 | 说明 |
|-----------|------|------|
| `character_id` | string | 角色 ID（格式：`CHARACTER.XXX`，需与游戏/Mod 注册一致） |
| `name` | string | 显示名称（中文/英文均可） |
| `max_hp` | int | 初始最大生命值 |
| `starter_relic` | string | 初始遗物 ID（格式：`RELIC.XXX`） |
| `starter_deck` | string[] | 初始牌组，卡牌 ID 数组（格式：`CARD.XXX`） |

### 约束

- `starter_deck` 内同一卡牌 ID 可重复出现（表示多张同名卡）
- `starter_relic` 应为对应角色的专属遗物
- 卡牌 ID 末尾的 `_P` 表示该卡牌归属于该角色（Player character suffix）

---

## 二、原版角色初始模板

### 角色 ID 列表

| 角色 | ID |
|------|----|
| Ironclad（铁血） | `CHARACTER.IRONCLAD` |
| Silent（静谧） | `CHARACTER.SILENT` |
| Defect（缺陷） | `CHARACTER.DEFECT` |
| Watcher（观者） | `CHARACTER.WATCHER` |
| Regent（摄政官） | `CHARACTER.REGENT` |
| Necrobinder（死灵法师） | `CHARACTER.NECROBINDER` |

### 原版初始遗物

| 角色 | 初始遗物 ID | 中文 |
|------|------------|------|
| Ironclad | `RELIC.BURNING_BLOOD` | 燃烧之血 |
| Silent | `RELIC.VINEOULAR` | 毒藤 |
| Defect | `RELIC.CRACKED_CORE` | 裂隙核心 |
| Watcher | `RELIC.PURE_WATER` | 纯水 |
| Regent | `RELIC.BLOOM` | 绽放 |
| Necrobinder | `RELIC.ODD_MUSHROOM` | 奇怪的蘑菇 |

### 原版初始牌组（每角色 10 张）

#### Ironclad（铁血）— 4 攻击 + 4 防御 + 2 能力

```
CARD.STRIKE_I（打击 ×4）
CARD.DEFEND_I（防御 ×4）
CARD.BASH（重锤）
CARD.VICTORY（胜利？）
```

#### Silent（静谧）— 4 攻击 + 4 防御 + 2 能力

```
CARD.STRIKE_S（打击 ×4）
CARD.DEFEND_S（防御 ×4）
CARD.SURVIVOR（幸存者）
CARD.NEutralize（中和）
```

#### Defect（缺陷）— 4 攻击 + 4 防御 + 2 能力

```
CARD.STRIKE_D（打击 ×4）
CARD.DEFEND_D（防御 ×4）
CARD.ZAP（电击）
CARD.DUALCAST（双重施法）
```

#### Watcher（观者）— 4 攻击 + 4 防御 + 2 能力

```
CARD.STRIKE_P（打击 ×4）
CARD.DEFEND_P（防御 ×4）
CARD.ERUPTION_P（爆发）
CARD.VIGILANCE_P（警惕）
（+ GREED_P 贪婪？视版本而定）
```

> **注意**：原版 Watcher 初始牌组可能包含 11 张（含 `CARD.GREED_P`），具体以游戏实际数据为准。参考 `player_template.json`（已实测）中的 Watcher 初始牌组为 11 张。

### 原版角色起始生命值

| 角色 | 起始 HP |
|------|--------|
| Ironclad | 80 |
| Silent | 70 ~ 75 |
| Defect | 70 ~ 75 |
| Watcher | 72 |
| Regent | 72 |
| Necrobinder | 70 ~ 75 |

---

## 三、BaseLib CustomCharacterModel 中的相关字段

BaseLib 定义了 `CustomCharacterModel` 抽象类，自定义角色需要覆盖以下虚属性：

### 视觉/资源路径

| 属性 | 类型 | 说明 |
|------|------|------|
| `CustomVisualPath` | `string?` | 角色立绘场景路径 |
| `CustomTrailPath` | `string?` | 卡牌拖尾效果路径 |
| `CustomIconTexturePath` | `string?` | 小图标（运行信息弹窗） |
| `CustomIconPath` | `string?` | 大图标（右上角 + 百科筛选） |
| `CustomCharacterSelectBg` | `string?` | 角色选择背景 |
| `CustomCharacterSelectIconPath` | `string?` | 角色选择图标 |
| `CustomCharacterSelectLockedIconPath` | `string?` | 角色选择锁定图标 |
| `CustomCharacterSelectTransitionPath` | `string?` | 角色选择过渡动画 |
| `CustomMapMarkerPath` | `string?` | 地图标记图标 |
| `CustomRestSiteAnimPath` | `string?` | 休息站动画 |
| `CustomMerchantAnimPath` | `string?` | 商店动画 |

### 能量相关

| 属性 | 类型 | 说明 |
|------|------|------|
| `CustomEnergyCounter` | `CustomEnergyCounter?` | 自定义能量计数器（结构体） |
| `CustomEnergyCounterPath` | `string?` | 自定义能量计数器 Godot 场景路径 |

### 手势（猜拳）

| 属性 | 类型 | 说明 |
|------|------|------|
| `CustomArmPointingTexturePath` | `string?` | 指向手势 |
| `CustomArmRockTexturePath` | `string?` | 石头手势 |
| `CustomArmPaperTexturePath` | `string?` | 布手势 |
| `CustomArmScissorsTexturePath` | `string?` | 剪刀手势 |

### 音效

| 属性 | 类型 | 说明 |
|------|------|------|
| `CustomAttackSfx` | `string?` | 攻击音效（`event:/sfx/...`） |
| `CustomCastSfx` | `string?` | 技能音效 |
| `CustomDeathSfx` | `string?` | 死亡音效 |

### 默认值覆盖

```csharp
public override int StartingGold => 99;
public override float AttackAnimDelay => 0.15f;
public override float CastAnimDelay => 0.25f;
```

### ⚠️ BaseLib 未直接处理 starter_deck / starter_relic

BaseLib 的 `CustomCharacterModel` **不负责定义初始遗物和初始牌组**——这部分由游戏源码的 `CharacterModel` 基类管理，BaseLib 专注于：
- 视觉资源路径
- 动画状态机
- 能量计数器
- 注册到 `ModelDb`

**初始遗物/卡组由游戏原生逻辑控制**，需要通过其他方式设置（参见下一节）。

---

## 四、游戏源码 CharacterModel 中的字段

根据 STS2 反编译源码分析，`CharacterModel` 中控制初始模板的关键字段：

### 遗物相关

| 字段/属性 | 说明 |
|----------|------|
| `StarterRelic` | 初始遗物 `RelicModel` 实例 |
| `StarterRelicId` | 初始遗物 ID 字符串（如 `"BURNING_BLOOD"`） |
| `SharedStarterRelic` | 是否为共享遗物 |

### 卡组相关

| 字段/属性 | 说明 |
|----------|------|
| `StarterDeck` | `CardModel[]` 数组，初始牌组 |
| `StarterDeckIds` | `string[]` 数组，初始牌组 ID |
| `GetStarterDeck()` | 方法，返回初始牌组 |

### 生命值

| 字段/属性 | 说明 |
|----------|------|
| `MaxHP` | 最大生命值（`virtual`，子类可覆盖） |
| `StartingGold` | 初始金币 |

---

## 五、字段命名对照表

| 用途 | player_template.json | CharacterModel（C#） | CardModel |
|------|---------------------|----------------------|-----------|
| 角色 ID | `character_id` | `Id` / `CharacterId` | — |
| 角色名 | `name` | `DisplayName` | — |
| 最大 HP | `max_hp` | `MaxHP` | — |
| 初始遗物 | `starter_relic` | `StarterRelic` / `StarterRelicId` | — |
| 初始牌组 | `starter_deck[]` | `StarterDeck[]` / `StarterDeckIds[]` | — |
| 卡牌 ID | — | — | `Id` |
| 卡牌类型 | — | — | `Type`（攻击/技能/能力等） |
| 卡牌稀有度 | — | — | `Rarity` |
| 目标类型 | — | — | `Target` |

---

## 六、MP_PlayerManager 模板系统建议

当前 `player_template.json` 格式与游戏内 `CharacterModel` 字段命名保持一致，可直接扩展：

### 扩展方案（未来可能需要）

```json
{
    "character_id": "CHARACTER.WATCHER",
    "name": "观者",
    "max_hp": 72,
    "starting_gold": 99,
    "starter_relic": "RELIC.PURE_WATER",
    "starter_deck": [
        "CARD.STRIKE_P",
        "CARD.DEFEND_P",
        "CARD.ERUPTION_P",
        "CARD.VIGILANCE_P",
        "CARD.GREED_P"
    ],
    "visual_path": "res://...",
    "icon_path": "res://...",
    "notes": "观者初始遗物为纯水（Pure Water），卡组包含 4 打击 4 防御 2 能力 1 贪婪"
}
```

### 关键命名约定

1. **卡牌 ID**：大写，`CARD.` 前缀，如 `CARD.STRIKE_P`、`CARD.ERUPTION_P`
2. **遗物 ID**：大写，`RELIC.` 前缀，如 `RELIC.PURE_WATER`、`RELIC.BURNING_BLOOD`
3. **角色 ID**：大写，`CHARACTER.` 前缀，如 `CHARACTER.WATCHER`、`CHARACTER.IRONCLAD`
4. **注意**：Python 脚本处理时，需根据 `CARD.`、`RELIC.`、`CHARACTER.` 前缀判断是 ID 还是名称字符串

---

## 七、相关资源索引

| 文件 | 路径 | 说明 |
|------|------|------|
| Watcher player_template.json | `mods/【0.99+版本支持】Watcher-STS2_0.99-0.4.6/player_template.json` | Mod 角色模板示例（已实测 11 张初始牌组） |
| MP_PlayerManager 内存文件 | `MP_PlayerManager/MEMORY.md` | 活跃版本状态，模板数据结构 |
| STS2 角色 ID 列表 | `VC_STS2_FULL_ID_LISTS.md` | 完整卡牌/遗物/能力 ID，含官方翻译 |
| BaseLib CustomCharacterModel | GitHub: `Abstracts/CustomCharacterModel.cs` | 自定义角色虚属性完整列表 |
| STS2 反编译源码 | `Tools/sts2_decompiled/` | `CharacterModel`、`CardModel`、`RelicModel` 完整定义 |
