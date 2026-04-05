# Slay the Spire 2 篝火升级/变换 UI Bug（Mythic Pyre Bug）

## 别名

- **Mythic Pyre Bug**（神话火堆 Bug）
- **Campfire Multi-Select Bug**（篝火多选 Bug）
- **Keyboard+Mouse Conflict Bug**（键鼠冲突 Bug）

## 现象

在篝火（Campfire）进行**卡牌升级（Upgrade）**或**卡牌变换（Transform）**时，**两张以上的卡牌被同时升级/变换**，而正常的游戏机制每次只允许升级/变换一张卡。

## 根本原因

游戏在篝火选卡界面存在**键盘和鼠标选择状态不同步**的 Bug：

- 鼠标点击一张卡 → 卡被"选中"（视觉上高亮）
- 方向键 + ENTER 选择另一张卡 → 第二张卡也被标记为"选中"
- 两种选择方式**分别维护各自的选择状态**
- 最终用鼠标点击"确认"按钮 → **两种选择状态都被提交**，导致多张卡同时被处理

```
选卡界面状态机：

鼠标选中状态 ──┐
               ├──→ 确认按钮 ──→ 两套状态都被提交
键盘选中状态 ──┘

正常预期：
鼠标选中状态 ──→ 确认按钮 ──→ 只处理 1 张卡
键盘选中状态 ──→ 确认按钮 ──→ 只处理 1 张卡
```

## 触发条件

1. 玩家必须处于**篝火（Campfire）场景**
2. 选择类型必须是**卡牌升级（Upgrade）** 或**卡牌变换（Transform）**
3. 必须**同时使用鼠标和键盘**两种选择方式：
   - 步骤 A：鼠标点击一张卡（不按确认）
   - 步骤 B：用方向键 + ENTER 选择另一张卡
   - 步骤 C：用鼠标点击确认/完成按钮
4. **结果**：两张卡都被升级/变换

## 具体操作步骤

### 升级多张卡（Upgrade）—— 橙型香盒（Pomander of Upgrade）

1. 到达篝火，选择"升级卡牌"（Use Upgrade）
2. 用鼠标点击第一张要升级的卡（不按确认）
3. 用方向键移动到第二张卡
4. 按 ENTER 选中第二张卡
5. 用鼠标点击"确认"按钮
6. **结果**：两张卡都被升级

### 变换多张卡（Transform）—— 橙型变换（Lantern）或其他 Transform

1. 到达篝火，选择"变换卡牌"（Transform Card）
2. 用鼠标点击第一张要变换的卡（不按确认）
3. 用方向键移动到第二张卡
4. 按 ENTER 选中第二张卡
5. 用鼠标点击"确认"按钮
6. **结果**：两张卡都被变换

## NCC 检测策略

### 重要结论：此行为在双端**表现一致**，是游戏本身 Bug，不是作弊

当此 Bug 触发时：
- 副机：确实有多张卡被变换
- 主机：收到 `OnReceivePlayerChoice` 且 `choiceCallCount >= 2`
- 副机发过来的 `NetPlayerChoiceResult` 中包含多张卡的选择
- **主机和副机显示完全一致**（都是变化后的卡牌）

### 关键日志特征

```
[PlayerChoice] [POSTFIX] NetPlayerChoiceResult.type=DeckCard
[PlayerChoice] [POSTFIX] indexes count=-1
[PlayerChoice] [POSTFIX] canonicalCards empty  ← 无 canonicalCards（游戏引擎不发快照）
[PlayerChoice] [POSTFIX] deckCards=8C           ← 卡数（可能变多了或不变）
[PlayerChoice] [POSTFIX] choiceCallCount now=2  ← 被调用 2 次
```

### 误判风险

NCC 的以下检测逻辑**可能被此 Bug 触发误报**：

| 检测逻辑 | 是否会误报 | 原因 |
|---------|-----------|------|
| `choiceCallCount >= 2 && delta == 0` | ⚠️ 可能误报 | Bug 导致 choiceCallCount 增加，但卡数变化是合法的 |
| `delta != expected` | ⚠️ 可能误报 | Bug 变换多张时卡数变化符合预期 |
| `canonicalCards 对比` | ❌ 不会误报 | 正常无 canonicalCards 时跳过 |

### 正确处理方式

**不检测**：因为这是游戏本身的 Bug，双端表现一致，不属于作弊。

在检测决策逻辑中，对于篝火升级/变换场景：
- 如果 `choiceCallCount >= 2` 但 `delta` 符合变换逻辑（变换 N 张 = 删除 N 张 + 增加 N 张，卡数不变）
- 视为 **Mythic Pyre Bug**，不触发作弊警告
- 记录日志：`Mythic Pyre Bug detected: choiceCalls=N delta=M`

### 判断 Mythic Pyre Bug 的条件

```csharp
// Mythic Pyre Bug 判断条件（全部满足）：
// 1. choiceCallCount >= 2        —— 同一选卡操作被多次调用
// 2. delta == 0                 —— 卡数没变（变换 N 张 = 删除 N + 增加 N）
// 3. canonicalCards 为空        —— 游戏引擎没有发作弊前快照
// 4. 无 GameActionHook 日志     —— 没有捕获到 EnqueueAction（变换在游戏内部处理）
```

## 相关游戏方法（用于 Hook 分析）

| 类 | 方法 | 用途 |
|---|------|------|
| `RestSiteSynchronizer` | `ChooseOption` | 篝火选项选择 |
| `PlayerChoiceSynchronizer` | `OnReceivePlayerChoice` | 选卡结果接收 |
| `ActionQueueSynchronizer` | `EnqueueAction` | GameAction 入队 |

## 修复建议（给游戏开发者）

此 Bug 应在游戏侧修复，NCC 作为反作弊mod不应拦截此合法游戏行为。

可能的修复方向：
1. 统一鼠标和键盘的选择状态为单一状态
2. 在确认时检查是否只有一种选择方式被使用
3. 禁止在鼠标选中状态下使用键盘选择（或反之）
