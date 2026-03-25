# MP_PlayerManager_v2 开发记录

> 本文件记录开发过程中的重要决策、问题与解决方案。

---

## 2026-03-25

### 2. FreeLoadout Mod Templates Tab 开发

**背景**：用户要求保留卡牌修改功能，方便模板内卡牌配置。经过分析，该功能实现于 FreeLoadout Mod 的游戏内 UI 中（C# Godot），而非 Python 外部工具。

**新增文件**（`FreeLoadout/src/`）：

| 文件 | 用途 |
|------|------|
| `TemplateData.cs` | 数据模型：`CardConfig`（卡牌配置）、`CharacterTemplate`（角色模板）、`TemplateStorage`（JSON 根容器） |
| `TemplateStorage.cs` | 持久化：`user_templates.json` 读写，增删改查，路径优先 Mod 目录，回退游戏 mods 目录 |
| `Tabs/TemplatesTab.cs` | 核心 UI：左右分栏布局，左侧模板列表，右侧详情（角色/HP/金币/卡组） |
| `Tabs/CardBrowserPanel.cs` | 卡牌浏览器弹窗：搜索过滤 + 四种排序 + 缩略图网格（60张/页），点击选卡后打开配置面板 |
| `Tabs/CardConfigPanel.cs` | 卡片配置面板：升级等级（0/+/++）、能量费用（0-5/X/默认）、附魔类型+数量、关键词开关 |
| `Tabs/DialogPanel.cs` | 通用文本输入对话框：用于重命名模板等场景 |

**修改文件**（`FreeLoadout/src/LoadoutPanel.cs`）：

- 新增 `using MP_PlayerManager.Tabs;`
- Tab 数组增加 `"tab.templates"` → **第 8 个 Tab**（索引 7）
- `RefreshCurrentTab` 中 `case 7: TemplatesTab.Build(...)` 

**国际化**（`FreeLoadout/localization/en/ui.json`）：新增 Templates Tab 相关英文文本

**模板数据结构**（`CardConfig`）：

```csharp
CardConfig {
    Id: string              // 卡牌 ID，如 "CARD.STRIKE_IRONCLAD"
    UpgradeLevel: int        // 升级等级 0/1/2
    BaseCost: int           // 基础费用（-1=不修改）
    CostsX: bool            // 是否为 X 费用
    EnchantmentType: string // 附魔类型名（空=无）
    EnchantmentAmount: int   // 附魔层数
    AddKeywords: List<string>    // 添加的关键词
    RemoveKeywords: List<string>  // 移除的关键词
}
CharacterTemplate {
    Id: string
    Name: string
    CharacterId: string     // 角色 ID
    MaxHp: int             // 起始 HP
    Gold: int              // 起始金币
    Cards: List<CardConfig> // 卡组
    StartingRelic: string   // 起始遗物
    StartingPotions: List<string> // 起始药水
}
```

---

### 1. 复制玩家与 v1 对齐：深拷贝 + map 注入 + 药水列表

**问题**：复制模式用 `_build_new_player` 拼装玩家，丢失大量字段（如 `potions` 被写成 `null`、金币被固定为 100），且未向 `map_point_history` 注入新 `net_id` 的 `player_stats`，易导致游戏无法读档。

**修复**（`core.py`）：

- 新增 `inject_player_into_map_history`（与 v1 逻辑一致，且用 `setdefault` 写回 `player_stats`）。
- `add_player_copy` 改为 `deepcopy` 源玩家，满血、`potions=[]`，保留金币与其余进度字段；追加后调用注入。
- `add_player_fresh` 追加玩家后同样注入。
- `take_over_player` 在更换 `net_id` 时调用 `remap_player_id_in_map_history`，避免地图上仍引用旧 ID。
- `_build_new_player` 默认 `potions` 由 `null` 改为 `[]`。

**UI**（`main.py`）：Steam 昵称与本地化角色名相同时，列表标题只显示一段，避免「帕瓦五号 帕瓦五号」类重复。

---

### 2. 修复：存档写入格式错误（CRLF 明文 JSON）

**根本原因**（`save_io.write_save`）：工具将存档以 **GZIP 压缩格式**写入 `current_run_mp.save`，而**游戏原始存档格式为 CRLF 明文 JSON**（`{\r\n  "key"...`，Magic `0x7b0d0a20`）。

游戏读档时遇到 `0x1f8b` 开头会解压 GZIP，但明文 JSON 不是有效的 GZIP 数据，解压出乱码，存档直接损坏。

**修复**（`save_io.py`）：`json.dumps(indent=2)` 后将 `\n` 替换为 `\r\n`，以 CRLF 格式写入，与游戏原始存档逐字节一致。

**涉及文件**：`K:\杀戮尖塔mod制作\STS2_mod\MP_PlayerManager_v2\save_io.py`

---

## 2026-03-24

### 1. 修复：非房主卡片昵称显示重复、角色名被覆盖

**问题现象**

夺舍页面中，非房主玩家卡片的标题变成了：

```
[2] 帕瓦五号  帕瓦五号  [7656119...]
```

Steam 昵称「帕瓦五号」出现了两次，角色名（如「铁甲战士」）完全消失。

**根本原因**

代码中同时调用了两个方法得到同一段昵称：

```python
pname = self._player_heading_name(pl)  # 查到 Steam 昵称时返回昵称
...
text=f"[{i + 1}] {pname}  {self._steam_name_str(net_id)}  [{net_id}]"
#                  ^ 已经是昵称          ^ 又查了一次昵称
# 第二段本应是角色名，却被重复的昵称占据了
```

`_steam_name_str` 在 `steam_names` 字典存在该 ID 时同样返回昵称，导致同一内容写两遍。

**修复方案**

引入两个专用辅助方法，语义明确且不会出错：

| 方法 | 用途 |
|------|------|
| `_player_row_title_with_id(i, pl)` | 夺舍/移除玩家的卡片标题 |
| `_player_row_copy_mode_label(i, pl, hp, gold)` | 添加玩家·复制模式的单选行 |

两者逻辑统一：

```
有 Steam 昵称时：  [序号] 昵称  角色名  [ID]
无 Steam 昵称时：  [序号] 角色名  [ID]
```

同时去掉副标题里多余的 `角色：{char_cn}`，避免与标题再次重复。

涉及文件：`main.py`（`_page_takeover`、`_build_copy_panel`、`_page_remove_player` 三处）

---

### 2. 移除 friends.json 功能

**背景**

`friends.json` 是工具早期版本的临时方案，允许用户手动维护一份 `id → name` 映射文件。

随着 `steam_api.get_all_contacts()` 已能从 `localconfig.vdf`（当前账户）或 Steam WebAPI 自动获取好友列表和昵称，该文件已冗余。

**同时引发的隐私讨论**

用户曾担心遍历所有 `userdata` 文件夹会读取其他 Steam 账户的私人好友数据。该方案后来改为仅读取 **ActiveUser 注册表指向的当前账户** 的 `localconfig.vdf`，不再跨账户遍历。

> **提醒**：开源工具应避免读取其他 Steam 账户的私人数据（如其他人的好友列表）。任何好友信息获取都应仅限当前登录账户。

**移除内容**

- `characters.py`：`load_friends_json()` 函数及其实现
- `main.py` 设置页：friends.json 格式示例 UI（含标签、JSON 示例、代码框）
- 相关未使用导入：`os`、`field`（`dataclasses`）

---

### 3. 好友列表刷新频率说明

**获取时机**

- 每次打开「选择 Steam 玩家」弹窗时重新读取
- **不依赖**内部 5 分钟缓存（`_CACHE` 仅用于 WebAPI 请求）

**数据来源优先级**

1. `localconfig.vdf` friends 区段（通过 ActiveUser 注册表定位当前账户）
2. Steam WebAPI GetFriendList（需 API Key）
3. `loginusers.vdf` 本机账户信息

**为何新加好友搜不到**

`localconfig.vdf` 由 Steam 客户端写入。Steam 并非实时写入，通常在：

- 重启 Steam
- 打开好友界面刷新
- 客户端定时同步

之后才写入新好友。

**建议**：重启 Steam 后再打开好友选择弹窗，或直接手动输入对方的 Steam64 位 ID。

---

### 4. 开发规范反思

**问题1：重复命名导致语义模糊**

`_player_heading_name` 的命名暗示「玩家在列表/标题中显示的名字」，但实现上它根据是否有 Steam 昵称来决定返回哪个——这个多义性让调用处很容易出错。

**改进**：方法名应反映其返回值（如 `_primary_display_name`），或在其 docstring 中明确列出所有调用点，防止被错误使用两次。

**问题2：未使用的变量残留**

`char_cn` 曾在循环顶部计算，但修改逻辑时没有被清理，导致代码冗余。

**改进**：每次修改卡片渲染逻辑后，检查循环内所有变量的使用情况。

**问题3：功能扩展时的临时方案堆积**

`friends.json` 作为早期临时方案引入，当正式方案（`localconfig.vdf` + WebAPI）实现后未及时清理，导致两份功能共存。

**改进**：建立功能对照表，每个新能力上线后记录「替代了哪个旧方案」，在发布前统一清理。
