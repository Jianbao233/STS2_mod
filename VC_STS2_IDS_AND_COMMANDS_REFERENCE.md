# Slay the Spire 2 · ID 与指令参考大全

> 整合游戏内各类 ID、target-index 规则、资源说明、指令使用场景及多人联机适用性。用于控制台指令与 Mod 开发参考。

---

## 一、ID 格式与获取方式

### 1.1 通用规则：Slugify

所有 Model ID 的 **Entry** 由类名经 `StringHelper.Slugify` 生成：

- CamelCase → UPPER_SNAKE_CASE  
- 例：`BodySlam` → `BODY_SLAM`，`EntropicBrew` → `ENTROPIC_BREW`

### 1.2 获取 ID 的途径

| 方式 | 说明 |
|------|------|
| **`dump`** | 将 Model ID 数据库输出到控制台与日志（需 Debug 模式） |
| **Tab 补全** | 输入命令后按 Tab，可补全卡牌、遗物、药水、遭遇、事件等 ID |
| **反编译源码** | `Models/` 下各子目录中的类名，按 Slugify 规则转换 |

---

## 二、ID 分类汇总

### 2.1 角色 ID（CharacterModel）

| 类名 | 控制台 ID | 说明 |
|------|-----------|------|
| Ironclad | IRONCLAD | 铁甲战士 |
| Silent | SILENT | 静默猎手 |
| Defect | DEFECT | 故障机器人 |
| Necrobinder | NECROBINDER | 亡灵契约师 |
| Regent | REGENT | 储君 |
| Deprived | DEPRIVED |  deprived |
| RandomCharacter | RANDOM_CHARACTER | 随机角色 |

### 2.2 卡牌 ID（CardModel）

- **来源**：`Models/Cards/*.cs`，约 584 个
- **格式**：`BODY_SLAM`、`WELL_LAID_PLANS`、`ASCENDERS_BANE`
- **用法**：`card <id>`、`remove_card <id>`
- **示例**：`BODY_SLAM`、`NEUTRALIZE`、`ZAP`、`DARK_EMBRACE`、`RAISE_DEAD`

### 2.3 药水 ID（PotionModel）

- **来源**：`Models/Potions/*.cs`，约 64 个
- **格式**：`ENTROPIC_BREW`、`STRENGTH_POTION`、`FIRE_POTION`
- **用法**：`potion <id>`
- **示例**：`FIRE_POTION`、`ENERGY_POTION`、`BLOCK_POTION`、`DEXTERITY_POTION`、`WEAK_POTION`、`VULNERABLE_POTION`、`GIGANTIFICATION_POTION`、`FAIRY_IN_A_BOTTLE`

### 2.4 遗物 ID（RelicModel）

- **来源**：`Models/Relics/*.cs`，约 290 个
- **格式**：`VAJRA`、`GOLDEN_IDOL`、`SNECKO_EYE`
- **用法**：`relic add <id>`、`relic remove <id>`
- **示例**：`VAJRA`、`ORNAMENTAL_FAN`、`WHITE_BEAST_STATUE`、`TOXIC_EGG`、`THE_BOOT`

### 2.5 能力 ID（PowerModel）

- **来源**：`Models/Powers/*.cs`（排除 Mocks），约 265 个
- **格式**：`STRENGTH_POWER`、`PLATED_ARMOR_POWER`、`VULNERABLE_POWER`
- **用法**：`power <id> <amount> <target-index>`
- **示例**：`STRENGTH_POWER`、`DEXTERITY_POWER`、`INTANGIBLE_POWER`、`NOXIOUS_FUMES_POWER`、`MACHINE_LEARNING_POWER`

### 2.6 附魔 ID（EnchantmentModel）

| 类名 | 控制台 ID |
|------|-----------|
| Adroit | ADROIT |
| Clone | CLONE |
| Corrupted | CORRUPTED |
| Favored | FAVORED |
| Glam | GLAM |
| Goopy | GOOPY |
| Imbued | IMBUED |
| Instinct | INSTINCT |
| Momentum | MOMENTUM |
| Nimble | NIMBLE |
| PerfectFit | PERFECT_FIT |
| RoyallyApproved | ROYALLY_APPROVED |
| Sharp | SHARP |
| Slither | SLITHER |
| SlumberingEssence | SLUMBERING_ESSENCE |
| SoulsPower | SOULS_POWER |
| Sown | SOWN |
| Spiral | SPIRAL |
| Steady | STEADY |
| Swift | SWIFT |
| TezcatarasEmber | TEZCATARAS_EMBER |
| Vigorous | VIGOROUS |

### 2.7 强化/异常 ID（AfflictionModel）

| 类名 | 控制台 ID |
|------|-----------|
| Bound | BOUND |
| Entangled | ENTANGLED |
| Galvanized | GALVANIZED |
| Hexed | HEXED |
| Ringing | RINGING |
| Smog | SMOG |

### 2.8 遭遇战 ID（EncounterModel）

- **来源**：`Models/Encounters/*.cs`（排除 Mocks），约 88 个
- **格式**：全大写，如 `SENTINELS`、`SLIME_BOSS`、`THE_ARCHITECT_EVENT_ENCOUNTER`
- **用法**：`fight <id>`
- **示例**：`SENTINELS`、`QUEEN_BOSS`、`VANTOM_BOSS`、`WATERFALL_GIANT_BOSS`、`PHANTASMAL_GARDENERS_ELITE`、`TWO_TAILED_RATS_NORMAL`

### 2.9 事件 ID（EventModel）

- **来源**：`Models/Events/*.cs`（排除 Mocks），约 67 个
- **格式**：`RELIC_TRADER`、`PUNCH_OFF`、`THE_ARCHITECT`
- **用法**：`event <id>`
- **示例**：`RELIC_TRADER`、`POTION_COURIER`、`WELCOME_TO_WONGOS`、`MORPHIC_GROVE`、`BUGSLAYER`

### 2.10 敌人/怪物 ID（MonsterModel）

- **来源**：`Models/Monsters/*.cs`（排除 Mocks），约 121 个
- **说明**：用于 `unlock monsters` 等；战斗内目标用 **target-index**，不用 Monster ID
- **示例**：`ZAPBOT`、`VANTOM`、`QUEEN`、`SOUL_FYSH`、`TERROR_EEL`

### 2.11 房间 / 场景 ID（RoomType）

`room` 命令使用 **RoomType 枚举名**（非 Slugify）：

| 枚举值 | 说明 |
|--------|------|
| Monster | 普通战斗 |
| Elite | 精英战斗 |
| Boss | Boss 战斗 |
| Treasure | 宝箱房 |
| Shop | 商店 |
| Event | 事件房 |
| RestSite | 休息点 |
| Map | 地图 |

**用法**：`room Monster`、`room Elite`、`room Shop` 等（区分大小写，与枚举一致）。

---

## 三、target-index 与战斗内目标

### 3.1 Creature 顺序

战斗内 `CombatState.Creatures` 顺序：

- **0**：玩家（Player）
- **1、2、3...**：敌人（按场上从左到右）

### 3.2 使用 target-index 的指令

| 指令 | target-index 含义 |
|------|-------------------|
| `damage <amount> [target-index]` | 无 index：所有敌人；有 index：指定 Creature（0=玩家） |
| `block <amount> [target-index]` | 0=玩家，1+=敌人 |
| `heal <amount> [index]` | 使用 Allies 列表的 index，非 Creatures |
| `power <id> <amount> <target-index>` | 0=玩家，1+=敌人 |
| `kill [target-index]\|all` | 无参数：第一个敌人；数字：指定敌人；`all`：全部敌人 |

### 3.3 敌人 ID 的获取

- 战斗内：用 **target-index**（1、2、3...）指定目标
- `Monster.Id.Entry` 对应 MonsterModel 的 ID（如 `ZAPBOT`、`SLIME`）
- 控制台不直接接收 Monster ID 选敌，统一用 target-index

---

## 四、卡牌属性（CardModel 相关）

控制台不直接改卡牌属性，但了解结构有助于 Mod 开发：

| 属性 | 说明 |
|------|------|
| CanonicalEnergyCost | 基础费用 |
| Type | 卡牌类型（Attack/Skill/Power/Status/Curse） |
| Rarity | 稀有度 |
| TargetType | 目标类型 |
| Id.Entry | 卡牌 ID（UPPER_SNAKE_CASE） |
| Pool | 所属卡池 |
| Afflictions / Enchantments | 强化与附魔列表 |

---

## 五、游戏内资源与相关指令

| 资源 | 修改指令 | 说明 |
|------|----------|------|
| 金币 | `gold <amount>` | 可为负数 |
| 能量 | `energy <amount>` | 当前回合能量 |
| 星星 | `stars <amount>` | 星星数 |
| 血量 | `heal <amount> [index]` | 治疗，无 index 默认玩家 |
| 格挡 | `block <amount> [target-index]` | 0=玩家 |
| 抽牌 | `draw <count>` | 抽若干张牌 |
| 药水槽 | `potion <id>` | 添加药水到腰带 |
| 遗物 | `relic add/remove <id>` | 添加或移除遗物 |

---

## 六、指令使用场景分类

### 6.1 战斗内可用（需 `CombatManager.IsInProgress`）

| 指令 | 说明 |
|------|------|
| damage | 造成伤害 |
| block | 给予格挡 |
| heal | 治疗（Allies 列表） |
| power | 施加能力 |
| afflict | 对手牌施加强化 |
| enchant | 对手牌施加附魔 |
| kill | 击杀敌人 |
| win | 立即获胜 |
| godmode | 切换无敌（需先有 run） |
| card | 添加卡牌到手牌（run+combat 均可） |
| remove_card | 移除卡牌 |
| upgrade | 升级手牌中的卡 |
| draw | 抽牌 |
| energy | 增加能量 |

### 6.2 战斗外 / 跑图可用（需 `RunManager.IsInProgress`，非战斗）

| 指令 | 说明 |
|------|------|
| gold | 修改金币 |
| potion | 添加药水 |
| relic | 添加/移除遗物 |
| stars | 增加星星 |
| room | 跳转到指定房间类型 |
| event | 跳转到指定事件 |
| fight | 跳转到指定遭遇战 |
| act | 跳转幕 |
| travel | 开启/关闭地图旅行模式 |
| ancient | 打开远古事件 |

### 6.3 事件 / 地图相关（跑图或事件内）

| 指令 | 适用场景 | 说明 |
|------|----------|------|
| event &lt;id&gt; | 跑图中 | 进入指定事件 |
| ancient &lt;id&gt; &lt;choice&gt; | 跑图中 | 打开远古事件并选选项 |
| room &lt;RoomType&gt; | 跑图中 | 进入商店、宝箱、休息等 |
| fight &lt;id&gt; | 跑图中 | 直接进入指定遭遇 |
| travel | 地图界面 | 切换旅行模式，可点任意房间跳转 |
| act &lt;int\|string&gt; | 跑图中 | 跳幕或替换当前幕 |

### 6.4 无 Run 要求（任意主菜单/游戏内）

| 指令 | 说明 |
|------|------|
| achievement | 解锁/撤销成就 |
| unlock | 解锁发现物 |
| cloud | 删除 Steam 云存档 |
| getlogs | 收集日志 |
| log | 设置日志级别 |
| open | 打开系统目录 |
| dump | 输出 Model ID（需 Debug） |
| help | 帮助 |

---

## 七、多人联机与 IsNetworked

### 7.1 联机可同步执行（IsNetworked = true）

下列指令在多人模式下会通过 `ActionQueueSynchronizer` 排队同步执行：

- act, afflict, ancient, block, card, damage, draw, energy, enchant, event, fight, godmode, gold, heal, kill, potion, power, relic, remove_card, room, stars, travel, upgrade, win

### 7.2 联机不可用（IsNetworked = false）

仅本地生效，不会同步给其他玩家：

- achievement, unlock, cloud, getlogs, log, open, multiplayer, trailer, leaderboard, sentry, log-history, instant, art, dump

### 7.3 多人逻辑

- 单人 / 假多人：直接执行
- 真实多人 + IsNetworked：入队等待同步执行

---

## 八、ID 来源路径速查

| 类型 | 源码路径 |
|------|----------|
| 卡牌 | `Models/Cards/*.cs` |
| 药水 | `Models/Potions/*.cs` |
| 遗物 | `Models/Relics/*.cs` |
| 能力 | `Models/Powers/*.cs`（排除 Mocks） |
| 附魔 | `Models/Enchantments/*.cs` |
| 强化 | `Models/Afflictions/*.cs` |
| 遭遇 | `Models/Encounters/*.cs` |
| 事件 | `Models/Events/*.cs` |
| 角色 | `Models/Characters/*.cs` |
| 怪物 | `Models/Monsters/*.cs` |
| 房间类型 | `Rooms/RoomType` 枚举 |
| 地图点类型 | `Map/MapPointType` 枚举 |

---

## 九、常用示例

```text
# 添加卡牌
card BODY_SLAM
card ZAP Hand

# 添加遗物和药水
relic add GOLDEN_IDOL
potion ENTROPIC_BREW

# 战斗内：对玩家加格挡、对 1 号敌人造成伤害
block 20 0
damage 50 1

# 施加能力（0=玩家，1=第一个敌人）
power STRENGTH_POWER 5 0
power VULNERABLE_POWER 2 1

# 跳转
fight SENTINELS
event RELIC_TRADER
room Shop
travel

# 资源
gold 999
draw 5
energy 10
heal 30
```

---

*文档基于 sts2 反编译源码整理，以实际游戏版本为准。*
