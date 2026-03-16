# Slay the Spire 2 控制台使用指南

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
| `room` | `<id:string>` | 跳转到指定房间。 |
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

## 五、参数格式约定

| 符号 | 含义 |
|------|------|
| `<x>` | 必填参数 |
| `[x]` | 可选参数 |
| `a\|b` | 二选一 |
| `string` | 字符串，卡牌/遗物/药水等 ID 通常为 SCREAMING_SNAKE_CASE |
| `int` | 整数 |

---

## 六、常见用法示例

```text
# 获得 999 金币
gold 999

# 抽 5 张牌
draw 5

# 添加遗物
relic add GOLDEN_IDOL

# 添加卡牌到手牌
card BODY_SLAM

# 跳转到指定战斗
fight SENTINELS

# 立即获胜
win

# 开启旅行模式，在地图直接选房间
travel

# 打开存档目录
open saves
```

---

## 七、源码参考路径

| 组件 | 路径 |
|------|------|
| 控制台 UI | `sts2.dll\MegaCrit\sts2\Core\Nodes\Debug\NDevConsole.cs` |
| 命令逻辑 | `sts2.dll\MegaCrit\sts2\Core\DevConsole\DevConsole.cs` |
| 命令基类 | `sts2.dll\MegaCrit\sts2\Core\DevConsole\AbstractConsoleCmd.cs` |
| 具体命令 | `sts2.dll\MegaCrit\sts2\Core\DevConsole\ConsoleCommands\*.cs` |
