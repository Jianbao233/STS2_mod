# Slay the Spire 2 控制台与指令参考

> 合并自 VC_STS2_CONSOLE_GUIDE.md + VC_STS2_IDS_AND_COMMANDS_REFERENCE.md（2026-08-10 文档整理）；ID 数据以 reference/ID数据源/ 为准。

> 根据 sts2 反编译源码整理的开发者控制台文档，涵盖打开方式、快捷键、可用指令及参数说明。

---

## 一、控制台可用条件

控制台在以下**任一**情况成立时可用：

| 条件 | 说明 |
|------|------|
| `OS.HasFeature("editor")` | 在 Godot 编辑器中运行 |
| `TestMode.IsOn` | 测试模式开启 |
| `ModManager.LoadedMods.Count > 0` | 任意 Mod 已加载 |
| `SaveManager.Instance.SettingsSave.FullConsole` | 设置中开启「完整控制台」 |

**说明**：有 Mod 加载或开启 FullConsole 时，会以 `shouldAllowDebugCommands = true` 初始化，此时**所有**控制台命令（含 DebugOnly）均可用；否则仅显示 `cloud`、`getlogs`、`log`、`open` 四个正式版命令。

---

## 二、打开方式与快捷键

### 2.1 打开 / 关闭控制台

按下下列任一键可打开或关闭控制台：

| 按键 | 说明 |
|------|------|
| `'` | 单引号 |
| `` ` `` | 反引号（键盘左上角） |
| `^` | 脱字符 |
| `*` | Shift + 8（星号） |

**注意**：若当前焦点在 `TextEdit` 或 `LineEdit` 上，则不会打开控制台。

### 2.2 控制台内快捷键

| 快捷键 | 功能 |
|--------|------|
| **Esc** | 退出选择模式；或关闭控制台 |
| **F11** | 切换全屏/半屏 |
| **Tab** | 命令补全；多候选时按 Enter 选择 |
| **↑ / ↓** | 浏览历史命令 |
| **Enter** | 执行当前命令 |

### 2.3 特殊指令（不经过命令系统）

| 指令 | 功能 |
|------|------|
| `clear` | 清空输出区 |
| `exit` | 关闭控制台 |

### 2.4 Readline 风格快捷键（Ctrl+ 组合）

| 快捷键 | 功能 |
|--------|------|
| Ctrl+A | 光标移到行首 |
| Ctrl+E | 光标移到行尾 |
| Ctrl+C | 清空输入行 |
| Ctrl+D | 关闭控制台 |
| Ctrl+L | 清空输出区 |
| Ctrl+U | 删除光标前到行首（内容进入剪贴板） |
| Ctrl+K | 删除光标到行尾 |
| Ctrl+W | 删除光标前一个词 |
| Ctrl+Y | 粘贴（Yank） |

---

## 三、内置帮助命令

| 用法 | 说明 |
|------|------|
| `help` | 列出所有已注册命令及简短描述 |
| `help <cmd>` | 显示指定命令的详细用法（含 Args 与 Description） |

---

## 四、命令分类与用法

### 4.1 成就 / 进度

| 命令 | 参数 | 描述 |
|------|------|------|
| `achievement` | `<operation:string> [id:string]` | 解锁或撤销成就。无 id 时对所有成就生效。operation 通常为 unlock / revoke。 |
| `unlock` | `<type:string>` | 标记为已发现。type 可为：`cards`、`potions`、`relics`、`monsters`、`events`、`epochs`、`ascensions`、`all`（全解锁）。可带具体 id 列表。 |

---

### 4.2 地图 / 导航

| 命令 | 参数 | 描述 |
|------|------|------|
| `act` | `<int\|string: act>` | 跳转到指定幕。整数为幕编号；字符串为替换当前幕。 |
| `room` | `<id:string>` | 跳转到指定房间（RoomType 枚举名，区分大小写，如 `Monster`、`Elite`、`Shop`）。 |
| `event` | `<id:string>` | 跳转到指定事件。 |
| `fight` | `<id:string>` | 跳转到指定遭遇战（Encounter）。id 需大写，如 `SENTINELS`。 |
| `ancient` | `<id:string> <choice:string>` | 打开远古事件并选择指定选项。 |
| `travel` | （无参数） | 开关「旅行模式」，允许在地图上直接跳转到任意房间。 |

---

### 4.3 战斗

| 命令 | 参数 | 描述 |
|------|------|------|
| `damage` | `<amount:int> [target-index:int]` | 造成伤害。无 target-index 时对全体敌人生效；0 = 玩家，1+ = 敌人索引。 |
| `block` | `<amount:int> [target-index:int]` | 给予格挡。0 = 玩家。 |
| `heal` | `<amount:int> [index:int]` | 治疗指定目标。index 为盟友列表索引。 |
| `power` | `<id:string> <amount:int> <target-index:int>` | 对指定目标施加能力。target-index 为 Creature 列表索引（0 通常为玩家）。 |
| `afflict` | `<id:string> [amount:int] [hand-index:int]` | 对手牌中指定位置的卡牌施加 Affliction。 |
| `kill` | `<target-index:int>\|'all'` | 击杀目标。指定索引杀单个，`all` 杀全部敌人，无参数杀第一个。 |
| `win` | （无参数） | 立即赢得战斗。 |
| `godmode` | （无参数） |  toggle 无敌模式。 |

---

### 4.4 卡牌

| 命令 | 参数 | 描述 |
|------|------|------|
| `card` | `<card-id:string> [pileName:string]` | 生成卡牌到指定牌堆。默认手牌。ID 使用 SCREAMING_SNAKE_CASE（如 `BODY_SLAM`）。 |
| `remove_card` | `<id:string> [pileName:string]` | 从手牌或牌库移除卡牌。 |
| `upgrade` | `<hand-index:int>` | 升级手牌中指定位置的卡（0 为最左）。 |
| `enchant` | `<id:string> [amount:int] [hand-index:int]` | 对手牌中指定位置的卡牌施加附魔。 |
| `draw` | `<count:int>` | 抽 X 张牌。 |

---

### 4.5 物品 / 资源

| 命令 | 参数 | 描述 |
|------|------|------|
| `gold` | `<amount:int>` | 修改金币（可为负数）。 |
| `energy` | `<amount:int>` | 增加能量。 |
| `stars` | `<amount:int>` | 增加星星。 |
| `potion` | `<id:string>` | 添加药水到腰带。ID 如 `ENTROPIC_BREW`。 |
| `relic` | `[add\|remove] <relic-id:string>` | 添加/移除遗物，默认 add。 |

---

### 4.6 系统 / 工具（正式版可用）

以下命令在**无 Mod、未开 FullConsole** 时也可用：

| 命令 | 参数 | 描述 |
|------|------|------|
| `cloud` | `delete` | 删除 Steam 云存档。需连按两次确认。 |
| `getlogs` | `<name:string>` | 收集日志，打包为含 name 的 zip，并打开所在目录。 |
| `log` | `[type:string] <level:string>` | 设置日志级别。type 见 LogType 枚举，level 见 LogLevel 枚举。 |
| `open` | `logs\|saves\|root\|build-logs\|loc-override` | 在系统文件管理器中打开对应目录。 |

---

### 4.7 开发 / 调试（仅 DebugOnly 开启时）

| 命令 | 参数 | 描述 |
|------|------|------|
| `dump` | （无参数） | 将 Model ID 数据库输出到控制台和日志。 |
| `art` | `<type:string>` | 列出缺失美术资源的条目。type：`affliction`、`card`、`enchantment`、`power`、`relic`。 |
| `instant` | （无参数） | 开启即时模式（跳过动画等）。 |
| `multiplayer` | `[test]` | 打开多人菜单；或 test 打开测试场景。 |
| `trailer` | （无参数） | 切换 0–9 与 +- 键显示/隐藏 UI 元素（宣传片模式）。 |
| `leaderboard` | `[option] [name] <score> [count]` | 上传分数。option：`upload` 或 `random`。 |
| `sentry` | `<test\|message\|exception\|crash\|status> [text]` | 测试 Sentry 错误上报。`crash confirm` 会导致原生崩溃并退出。 |
| `log-history` | （无参数） | 保存命令历史并打开所在目录。 |

---

## 五、target-index 与战斗内目标

### 5.1 Creature 顺序

战斗内 `CombatState.Creatures` 顺序：

- **0**：玩家（Player）
- **1、2、3...**：敌人（按场上从左到右）

### 5.2 使用 target-index 的指令

| 指令 | target-index 含义 |
|------|-------------------|
| `damage <amount> [target-index]` | 无 index：所有敌人；有 index：指定 Creature（0=玩家） |
| `block <amount> [target-index]` | 0=玩家，1+=敌人 |
| `heal <amount> [index]` | 使用 Allies 列表的 index，非 Creatures |
| `power <id> <amount> <target-index>` | 0=玩家，1+=敌人 |
| `kill [target-index]\|all` | 无参数：第一个敌人；数字：指定敌人；`all`：全部敌人 |

### 5.3 敌人 ID 的获取

- 战斗内：用 **target-index**（1、2、3...）指定目标
- `Monster.Id.Entry` 对应 MonsterModel 的 ID（如 `ZAPBOT`、`SLIME`）
- 控制台不直接接收 Monster ID 选敌，统一用 target-index

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
| event <id> | 跑图中 | 进入指定事件 |
| ancient <id> <choice> | 跑图中 | 打开远古事件并选选项 |
| room <RoomType> | 跑图中 | 进入商店、宝箱、休息等 |
| fight <id> | 跑图中 | 直接进入指定遭遇 |
| travel | 地图界面 | 切换旅行模式，可点任意房间跳转 |
| act <int\|string> | 跑图中 | 跳幕或替换当前幕 |

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

## 八、ID 数据与获取

### 8.1 ID 格式与获取方式

**通用规则（Slugify）**：所有 Model ID 的 **Entry** 由类名经 `StringHelper.Slugify` 生成——CamelCase → UPPER_SNAKE_CASE。例：`BodySlam` → `BODY_SLAM`，`EntropicBrew` → `ENTROPIC_BREW`。

获取 ID 的途径：

| 方式 | 说明 |
|------|------|
| **`dump`** | 将 Model ID 数据库输出到控制台与日志（需 Debug 模式） |
| **Tab 补全** | 输入命令后按 Tab，可补全卡牌、遗物、药水、遭遇、事件等 ID |
| **反编译源码** | `Models/` 下各子目录中的类名，按 Slugify 规则转换 |

**完整 ID 列表见 reference/ID数据源/VC_STS2_FULL_ID_LISTS.md**（卡牌/药水/遗物/能力/附魔/强化/遭遇/事件/怪物/角色/房间类型等全部 ID 以此为准）。

> 注 1：卡牌数以 ID数据源 的 **576** 为准（原 IDS_AND_COMMANDS_REFERENCE 记为 584，为口径差异）。
> 注 2：药水 ID 以 ID数据源 的 **`FAIRY_IN_ABOTTLE`** 为准（原参考文档曾写作 `FAIRY_IN_A_BOTTLE`，二者不一致时以 ID数据源 为准）。

### 8.2 卡牌属性（CardModel 相关）

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

## 九、参数格式约定

| 符号 | 含义 |
|------|------|
| `<x>` | 必填参数 |
| `[x]` | 可选参数 |
| `a\|b` | 二选一 |
| `string` | 字符串，卡牌/遗物/药水等 ID 通常为 SCREAMING_SNAKE_CASE |
| `int` | 整数 |

---

## 十、常见用法示例

```text
# 获得 999 金币
gold 999

# 抽 5 张牌
draw 5

# 添加遗物
relic add GOLDEN_IDOL

# 添加卡牌到手牌
card BODY_SLAM
card ZAP Hand

# 添加药水
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
energy 10
heal 30

# 立即获胜
win

# 开启旅行模式，在地图直接选房间
travel

# 打开存档目录
open saves
```

---

## 十一、源码参考路径速查

### 11.1 控制台实现（sts2.dll）

| 组件 | 路径 |
|------|------|
| 控制台 UI | `sts2.dll\MegaCrit\sts2\Core\Nodes\Debug\NDevConsole.cs` |
| 命令逻辑 | `sts2.dll\MegaCrit\sts2\Core\DevConsole\DevConsole.cs` |
| 命令基类 | `sts2.dll\MegaCrit\sts2\Core\DevConsole\AbstractConsoleCmd.cs` |
| 具体命令 | `sts2.dll\MegaCrit\sts2\Core\DevConsole\ConsoleCommands\*.cs` |

### 11.2 ID 数据模型（Models/）

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

*文档基于 sts2 反编译源码整理，以实际游戏版本为准。*