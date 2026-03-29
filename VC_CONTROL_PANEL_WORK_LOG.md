# ControlPanel 工作日志与注意事项

> 记录面板 UI/功能完善计划与开发进展。

---

## 当前状态（2025-03-17）

- **三栏布局**：左大类 | 次左功能项 | 右功能区 ✓
- **可调宽度**：HSplitContainer DraggerVisibility=Visible；标题下新增 尺寸 SpinBox 可调面板最小尺寸 ✓
- **面板尺寸**：默认 960x620，最小 700x450，用户可调 ✓
- **实时检测**：删除卡牌、已拥有遗物、药水腰带均从 GameStateHelper 反射获取实时数据 ✓
- **卡牌角色**：CardCharacterHelper 按铁甲/寂静/故障/亡灵/储君正确筛选，修复「只剩打击防御」 ✓
- **遗物**：按稀有度分类，图标网格，左键直接添加/删除 ✓
- **药水删除**：实时显示腰带药水，左键尝试 potion remove ✓
- **能力/Buff**：PotionAndCardData.PowerData 表，点击施加，无需手动输入 ID ✓
- **生成敌人**：战斗控制新增「生成敌人」，预设遭遇列表，fight \<id\> 跳转 ✓
- **标题**：ControlPanel v0.2.0 | 作者：煎包 / bili@我叫煎包 / claude-4.6-sonnet ✓

---

## ControlPanel 面板 UI/功能 完善 Plan（2025-03 已实施）

| 项 | 实现 |
|---|------|
| 1 图标 | IconLoader.cs 反射 AtlasManager / ResourceLoader；卡牌/遗物/药水 TextureRect |
| 2 卡牌 | 牌堆选择 Hand/Draw/Discard/Deck；按 ID 添加；删除为 remove_card（手牌索引）|
| 3 遗物 | 按稀有度筛选；已拥有→删除，未拥有→获得 |
| 4 遭遇战 | 按普通/精英/Boss 分类，fight &lt;id&gt; |
| 5 事件 | event &lt;id&gt; 列表 |
| 6 战斗控制 | 伤害/格挡/回血（数值+目标）、能量/抽牌、能力施加、击杀 |
| 7 药水 | 生成 potion &lt;id&gt;；删除暂无控制台命令 |
| 8 标题 | ReleaseInfoManager 反射获取游戏版本；Mod 0.2.0；作者固定 |
| 9 布局 | HSplitContainer 三栏 |
| 10 悬浮 | 所有命令按钮 TooltipText / MouseEntered 显示将执行命令 |

---

## UI 完善（2025-03 图2 模板）

| 问题 | 处理 |
|------|------|
| 栏目不可调大小 | HSplitContainer.DraggerVisibility = Visible；面板 920x600 |
| 选项文本截断 | 列表 Button SizeFlagsHorizontal = ExpandFill |
| 统一 UI 结构 | 按图2：筛选 → 列表(左) \| 预览+指令(右) → 执行按钮 |

---

## 新增文件

- **GameStateHelper.cs**：反射 CombatManager/RunManager 获取牌堆卡牌、遗物、药水
- **CardCharacterHelper.cs**：卡牌角色归属（ID 后缀 + 静态表），供角色筛选

## 注意事项

- 修改后需运行 `运行构建.bat` 或 `.\build.ps1` 部署
- 部署后需**完全重启游戏**方能加载新 DLL
- 标题含「v2」可验证新版本是否生效
- 命令生效条件：卡牌/药水需在**对局战斗内**；fight 需**局内进行中**
