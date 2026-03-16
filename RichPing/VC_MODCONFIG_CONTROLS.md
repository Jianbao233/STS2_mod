# ModConfig-STS2 可用控件清单

> 来源：ModConfig v0.1.2 官方描述 + DamageMeter 解包代码实测

---

## 一、控件类型（ConfigType）

| 控件 | 英文名 | 用途 | 主要属性 | 示例 |
|------|--------|------|----------|------|
| **标题** | Header | 分组标题，不保存值 | Label, Labels | 「全局开关」「角色个性化」 |
| **分隔线** | Separator | 视觉分隔，无交互 | 无 | 一条横线 |
| **开关** | Toggle | 布尔开关 | Key, Label, DefaultValue (bool), Description | 开/关 |
| **滑条** | Slider | 数值范围选择 | Key, Label, DefaultValue (float), **Min**, **Max**, **Step**, **Format**, Description | 50~200，步进 10 |
| **下拉框** | Dropdown | 多选一（待实测） | Key, Label, DefaultValue, **Options** | 随机/顺序 |
| **文本输入** | Input / Text | 字符串输入（待实测） | Key, Label, DefaultValue (string) | 排除词列表 |
| **按键绑定** | KeyBind | 快捷键 | Key, Label, DefaultValue (long = Key 枚举值), Description | F7 |

---

## 二、ConfigEntry 通用属性

| 属性 | 类型 | 说明 |
|------|------|------|
| Key | string | 配置键，持久化用 |
| Label | string | 显示名（英文） |
| Labels | Dict&lt;string,string&gt; | 多语言，如 {"en":"x", "zhs":"y"} |
| Type | ConfigType | 控件类型 |
| DefaultValue | object | 默认值 |
| Description | string | 说明文案（英文） |
| Descriptions | Dict | 说明多语言 |
| OnChanged | Action&lt;object&gt; | 值变更回调 |

**Slider 专用**：Min, Max, Step, Format（如 "F0"）

---

## 三、已证实可用的控件

- **Header**：DamageMeter 用于「Display Settings」「Hotkeys」「Behavior」
- **Separator**：分组间横线
- **Toggle**：布尔
- **Slider**：Min/Max/Step/Format
- **KeyBind**：long

**待实测**：Dropdown（需 Options）、Input/Text（需确认 ConfigType 精确名称）

---

## 四、RichPing 配置分组与调控目标（2025-03-17 重构）

| 分组 | 配置项 | 控件 | 调控目标 |
|------|--------|------|----------|
| **一、全局开关** | use_custom_ping | Toggle | 总开关：关=游戏原版；开=RichPing |
| **二、全局文本类别** | use_alive_ping | Toggle | 存活时催促文本是否自定义 |
| | use_dead_ping | Toggle | 死亡后调侃文本是否自定义 |
| **三、选取行为** | random_pick | Toggle | 随机 vs 顺序轮转 |
| | use_stages | Toggle | 是否按幕(0/1/2)切换 |
| | use_character_specific | Toggle | 角色专属优先 vs 仅全局 |
| **四、过滤** | excluded_messages | Input/Text | 黑名单：包含即不发送 |
| **五、角色设置-存活** | char_*_alive | Toggle | 各角色存活时是否用专属文本 |
| **六、角色设置-死亡** | char_*_dead | Toggle | 各角色死亡后是否用专属文本 |

每组有 Header + Separator，每项有 Description/Descriptions 说明**实际调控目标**。
