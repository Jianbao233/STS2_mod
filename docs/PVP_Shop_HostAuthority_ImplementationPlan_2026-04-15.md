# PVP Shop 主机权威同步落地步骤文档（第一版）

- 生成时间：2026-04-15
- 目标项目：`STS2_mod/PVP_ParallelTurn`
- 目标范围：`PvpShopEngine` 联机主机权威同步第一版
- 当前状态：**仅产出步骤文档，不执行代码落地**

---

## 1. 文档目标

本文件用于把“商店引擎按战斗 PvP 的主机权威同步思路补全”的代码落地步骤拆清楚，供确认后按步骤实施。

本次目标不是把商店系统做成完整产品，而是先完成一版**可进入双机测试的主机权威同步骨架**。

### 本次要达成的目标
- 商店状态由 **Host 唯一权威维护**
- Client 不再本地决定刷新结果或购买结果
- Client 只发送 **请求**，并应用 Host 返回的 **authoritative state**
- 回合开始 / 回合结束的开店关店流程与现有 PvP 战斗回合同步机制对齐
- 为后续 UI 接入与联机测试提供稳定同步基线

### 本次明确不做
- 不做完整商店 UI
- 不做第二模式
- 不做复杂断线恢复细节
- 不做 fixed seed 回归脚本
- 不做 lock slot / 复杂交互扩展

---

## 2. 参考基线（来自现有战斗引擎）

商店同步第一版直接参考现有 PvP 战斗引擎的主机权威模式。

### 已存在、可复用的战斗同步思路
1. **Host authoritative state broadcast**
   - `RoundState`
   - `PlanningFrame`
   - `RoundResult`
2. **Client -> Host request / submission**
3. **ACK / NACK**
4. **revision / snapshotVersion 去重与过期保护**
5. **roomSessionId / topology 上下文校验**
6. **Client 只应用 authoritative state，不自己决定最终结果**

### 商店同步要复用的核心原则
- `ShopIntent(Refresh/Purchase)` 只由客户端发请求
- Host 校验、执行并更新 `ShopState`
- Host 广播 `ShopState`
- Client 只读应用 `ShopState`
- 断线恢复先保留接口和状态补齐入口，不在第一版做复杂恢复行为

---

## 3. 当前商店引擎现状

### 已有能力
- `PvpShopEngine` 已具备本地核心逻辑：
  - `TryOpenRound`
  - `TryGetView`
  - `TryRefresh`
  - `TryPurchase`
  - `TryCloseRound`
  - `ApplyAuthoritativeState`
- `PvpShopBridgePatches` 已接上回合开始开店、回合结束关店
- `PvpShopRoundState / PvpShopPlayerState / PvpShopOffer` 已具备商店快照基础数据

### 当前缺口
- 没有 Shop 专用网络消息
- 没有 Shop 请求 revision / stale / duplicate 校验
- 没有 Shop ACK / NACK
- 没有 Host 广播 Shop authoritative snapshot 的通路
- 没有 Client 收到 Shop snapshot 后的统一应用逻辑
- 当前客户端被排除在商店开关流程之外，不具备同等级联机同步能力

---

## 4. 落地总策略

建议按“先协议、再桥、再请求、再应用、最后补观测”的顺序落地。

### 总体流程图
1. 定义 Shop 网络协议
2. 定义 Shop runtime 版本控制与去重状态
3. 新增 `PvpShopNetBridge`
4. Host 开店时广播 `ShopState`
5. Client 收到后只读应用 `ShopState`
6. Client 发 `Refresh/Purchase request`
7. Host 校验并执行 `PvpShopEngine`
8. Host 回 ACK/NACK
9. Host 广播最新 `ShopState`
10. Client 应用最新 `ShopState`
11. 回合结束 Host 广播 `ShopClosed`

---

## 5. 代码落地步骤

## 步骤 A：冻结第一版同步协议

### 目标
先把商店同步消息与字段边界定死，避免边写边改。

### 需要新增的消息
建议在 `src/ParallelTurnPvp/Core/PvpMessages.cs` 内新增以下消息：

#### A1. `PvpShopStateMessage`
用途：Host 广播当前商店 authoritative state

建议字段：
- `roomSessionId`
- `roomTopology`
- `roundIndex`
- `snapshotVersion`
- `shopStateVersion`
- `modeId`
- `modeVersion`
- `strategyPackId`
- `strategyVersion`
- `rngVersion`
- `players[]`
  - `playerId`
  - `gold`
  - `refreshCount`
  - `playerStateVersion`
  - `statusText`
  - `offers[]`
    - `slotIndex`
    - `slotKind`
    - `cardId`
    - `displayName`
    - `price`
    - `available`

#### A2. `PvpShopRequestMessage`
用途：Client 向 Host 发刷新/购买请求

建议字段：
- `roomSessionId`
- `roomTopology`
- `roundIndex`
- `snapshotVersion`
- `shopStateVersion`
- `requestRevision`
- `playerId`
- `requestKind`
  - `Refresh`
  - `Purchase`
- `refreshType`
- `slotIndex`

#### A3. `PvpShopRequestAckMessage`
用途：Host 返回请求结果

建议字段：
- `roomSessionId`
- `roomTopology`
- `roundIndex`
- `snapshotVersion`
- `shopStateVersion`
- `playerId`
- `requestRevision`
- `accepted`
- `note`

#### A4. `PvpShopClosedMessage`
用途：Host 显式广播关店

建议字段：
- `roomSessionId`
- `roomTopology`
- `roundIndex`
- `snapshotVersion`
- `shopStateVersion`

### 验收标准
- Shop 第一版消息结构冻结
- 明确 `snapshotVersion / shopStateVersion / requestRevision` 的职责

---

## 步骤 B：补商店同步所需的最小运行时状态

### 目标
让 Shop 也具备和战斗同步相同的“去重 / 过期保护 / 主机广播标记”能力。

### 建议新增内容
建议在 `PvpShopEngine` 旁边补一层 runtime 同步状态，最小需要：

- `LastBroadcastShopStateVersion`
- `LastReceivedShopStateVersion`
- `LastProcessedRequestRevisionByPlayer`
- `LastAckedRequestRevisionByPlayer`
- `CurrentShopRoundIndex`
- `CurrentSnapshotVersion`

### 需要新增的能力
- `TryMarkShopStateBroadcast(...)`
- `TryMarkShopStateReceived(...)`
- `TryAcceptShopRequestRevision(playerId, revision, payloadSignature)`
- `TryGetLastProcessedRequest(...)`

### 说明
这里不要把所有同步状态硬塞进 `PvpShopRoundState` 里。

建议区分：
- **业务状态**：商店里实际显示与购买相关的数据
- **同步状态**：广播去重、请求去重、ACK 处理、网络时序保护

### 验收标准
- shop snapshot 可以判定 duplicate / stale
- request revision 可以判定 stale / duplicate / conflicting payload

---

## 步骤 C：新增独立的 `PvpShopNetBridge`

### 目标
不要继续把商店同步逻辑堆进 `PvpNetBridge`。新增独立桥接层，降低耦合。

### 建议新增文件
- `src/ParallelTurnPvp/Core/PvpShopNetBridge.cs`

### 主要职责
- `EnsureRegistered()`
- `BroadcastShopState(...)`
- `BroadcastShopClosed(...)`
- `SendShopRequest(...)`
- `HandleShopStateMessage(...)`
- `HandleShopRequestMessage(...)`
- `HandleShopRequestAckMessage(...)`
- `HandleShopClosedMessage(...)`
- `ValidateRoomContext(...)`

### 与现有战斗桥接的边界
- 共享 `RunState / roomSessionId / topology / current round context`
- 不共享具体的 message handler 逻辑
- 战斗同步仍由 `PvpNetBridge` 负责
- 商店同步完全收敛到 `PvpShopNetBridge`

### 验收标准
- Shop 网络注册与消息处理入口独立
- 商店同步代码不继续扩散到战斗同步类中

---

## 步骤 D：Host 开店广播 authoritative `ShopState`

### 目标
让现有 `TryOpenRound(...)` 不再只是 Host 本地行为，而是同步起点。

### 需要修改的位置
- `PvpShopBridgePatches.cs`
- `PvpShopBridge` / `PvpShopRuntimeRegistry` 相关入口

### 代码落地动作
1. Host 在回合开始 `TryOpenRound(...)` 成功后：
   - 生成当前 shop snapshot
   - 调 `PvpShopNetBridge.BroadcastShopState(...)`
2. 广播前做去重保护：
   - 避免同一 `shopStateVersion` 重复广播
3. 日志打印：
   - `roundIndex`
   - `snapshotVersion`
   - `shopStateVersion`
   - `modeId`
   - `players summary`

### Client 行为
- Client 不再尝试本地开店
- Client 必须等 Host 的 `PvpShopStateMessage`

### 验收标准
- Host 开店后 Client 能拿到首份 authoritative shop state
- 双方 round / snapshot 上下文一致

---

## 步骤 E：Client 应用 `ShopState`，进入只读商店状态

### 目标
Client 不再本地推导 offers，只应用 Host 广播结果。

### 代码落地动作
在 `HandleShopStateMessage(...)` 中：

1. 校验：
   - `roomSessionId`
   - `topology`
   - `roundIndex`
   - `snapshotVersion`
2. 判定：
   - duplicate / stale shop state 直接忽略
3. 构造本地可应用的 `PvpShopRoundState`
4. 调 `PvpShopEngine.ApplyAuthoritativeState(...)`
5. 刷新只读 view cache（如有）
6. 打日志

### 注意事项
- Client 本地不得重新生成 offers
- Client 只消费 `message -> state -> ApplyAuthoritativeState`
- 如果当前 UI 未接入，至少保证数据层状态正确

### 验收标准
- Client 接收后本地商店状态与 Host 一致
- 同一快照重复到达不会重复应用

---

## 步骤 F：打通 `Refresh/Purchase` 请求链路

### 目标
把商店行为从“本地调用引擎”改成“Client 请求 -> Host 执行 -> Host 回推状态”。

### 需要支持的请求类型
#### F1. Refresh Request
- Client 发：`requestKind=Refresh`
- Host 校验 `refreshType`
- Host 调 `TryRefresh(...)`
- Host 回 ACK/NACK
- Host 广播新 `ShopState`

#### F2. Purchase Request
- Client 发：`requestKind=Purchase`
- Host 校验 `slotIndex`
- Host 调 `TryPurchase(...)`
- Host 回 ACK/NACK
- Host 广播新 `ShopState`

### Host 侧最小校验
- 当前必须是 Host
- shop 必须 open
- `playerId == senderPlayerId`
- `roundIndex` 匹配
- `snapshotVersion` 匹配
- `shopStateVersion` 不落后
- `requestRevision` 不过期
- `Refresh` 的 `refreshType` 合法
- `Purchase` 的 `slotIndex` 合法
- 不允许 conflicting payload on same revision

### 验收标准
- Client 发请求不会直接修改本地 shop state
- Host 成为唯一修改商店状态的执行者
- 每次成功修改后都会广播新的 authoritative shop state

---

## 步骤 G：补 ACK / NACK / 幂等处理

### 目标
防止重复点击、重复包、重发导致重复扣费或重复购买。

### 需要实现的最小能力
#### G1. ACK / NACK
Host 对每个请求都返回：
- `accepted=true/false`
- `note=accepted/rejected/already_applied/stale/conflict`

#### G2. 幂等
按 `playerId + requestRevision` 处理：
- **相同 payload**：重复请求 -> 不重复执行，直接 `already_applied`
- **不同 payload**：同 revision 冲突 -> reject
- **更旧 revision**：reject stale

#### G3. Client pending 状态
Client 侧保留最小 pending request 状态：
- 最近一次请求 revision
- 等待 ACK 中
- ACK 超时后可记录 warning

### 第一版先不做
- 不做完整自动 retry pump
- 不做复杂窗口退避
- 不做多请求并发队列调度

### 验收标准
- 连点刷新不会多次扣费
- 购买请求重复包不会重复购入
- stale request 能被显式拒绝

---

## 步骤 H：Host 显式广播 `ShopClosed`

### 目标
把当前 Host 本地关店升级成双方一致的 authoritative 结束事件。

### 代码落地动作
1. Host 在回合切换前 `TryCloseRound()` 成功后：
   - 广播 `PvpShopClosedMessage`
2. Client 收到后：
   - 清空本地 shop state
   - 调 `ApplyAuthoritativeState(null)` 或等价关闭逻辑
3. 日志记录：
   - `roundIndex`
   - `snapshotVersion`
   - `shopStateVersion`

### 验收标准
- Host 关店后 Client 同步关闭
- 不会残留上一轮 shop state 到下一轮

---

## 步骤 I：补最小断线恢复占位点

### 目标
第一版不做完整 D2 恢复，但要把“恢复入口”留好，避免之后推翻同步结构。

### 第一版只做占位
- 保留 `ShopState` 可完整序列化/重建的能力
- 预留 `RequestShopResumeState` / `ShopResumeState` 消息位或 TODO
- 断线重连后如果没有恢复逻辑，至少保证：
  - 不会继续接受过期请求
  - 当前 shop 状态可被 Host 再次广播覆盖

### 验收标准
- 结构上允许后续加 resume
- 第一版不因缺少 resume 而破坏现有主链路

---

## 步骤 J：补最小 telemetry 与排错日志

### 目标
为你后面开始双机测试提供足够的排错证据。

### 至少需要记录的日志
#### 请求日志
- `roomSessionId`
- `roundIndex`
- `snapshotVersion`
- `shopStateVersion`
- `playerId`
- `requestRevision`
- `requestKind`
- `refreshType / slotIndex`
- `accepted`
- `note`

#### 状态广播日志
- `modeId`
- `modeVersion`
- `strategyPackId`
- `strategyVersion`
- `rngVersion`
- `shopStateVersion`
- `players summary`
- `offers summary`

#### 应用日志
- Client 应用的 `shopStateVersion`
- duplicate/stale ignore 原因
- 关闭事件是否成功消费

### 验收标准
- 双机日志中能串出完整链路：
  - client request
  - host validate
  - host execute
  - host ack
  - host broadcast
  - client apply

---

## 6. 实施顺序建议

建议严格按下面顺序做，不建议乱序。

1. **步骤 A：冻结协议**
2. **步骤 B：补 shop runtime 同步状态**
3. **步骤 C：新增 `PvpShopNetBridge`**
4. **步骤 D：Host 开店广播 `ShopState`**
5. **步骤 E：Client 应用 `ShopState`**
6. **步骤 F：打通 Refresh/Purchase 请求链路**
7. **步骤 G：补 ACK/NACK/幂等**
8. **步骤 H：Host 广播 `ShopClosed`**
9. **步骤 I：补最小 resume 占位点**
10. **步骤 J：补 telemetry**

---

## 7. 第一版完成定义（DoD）

当以下条件全部满足时，可认为“Shop 主机权威同步第一版”代码落地完成：

1. Host 开店后，Client 能收到并应用同一份商店状态
2. Client 不再本地生成刷新/购买结果
3. Client 的刷新请求只能由 Host 执行
4. Client 的购买请求只能由 Host 执行
5. Host 执行成功后会广播新的 authoritative state
6. Client 能应用新状态，不发生本地分叉
7. Host 关店后 Client 会同步关闭商店状态
8. duplicate / stale request 不会重复扣费或重复购买
9. 双机日志里可以定位一次完整请求链路

---

## 8. 源码落地后再进入的测试建议

本文件不执行测试，只定义“代码落地后优先测什么”。

### T1. Host 开店广播
- Host 开店
- Client 收到初始 `ShopState`
- 双方 `roundIndex/snapshotVersion/shopStateVersion` 一致

### T2. Refresh 请求链路
- Client 发普通刷新
- Host 扣费并广播新状态
- 双方金币、刷新次数、offers 一致

### T3. Purchase 请求链路
- Client 购买某一槽
- Host 扣费并标记售出
- 双方状态一致

### T4. 重复请求保护
- Client 连点刷新或重复发包
- 不出现重复扣费

### T5. 过期状态保护
- 用旧 `shopStateVersion` 发请求
- Host 正确 reject stale request

### T6. 关店同步
- 回合结束关店
- Client 同步清空当前商店状态

---

## 9. 当前建议

如果你确认要开始代码落地，建议第一轮就只做：

- 协议
- `PvpShopNetBridge`
- Host 开店广播
- Client 应用状态
- Refresh/Purchase 请求 + ACK/NACK + 幂等
- ShopClosed 广播

也就是先把 **D 里程碑的最小闭环** 做出来。

等这一步稳定后，再进入：
- UI 接入
- 测试脚本
- 第二模式
- 恢复增强

---

## 10. 结论

当前最合理的代码落地路线不是继续增强本地 `PvpShopEngine` 算法，而是先把它纳入与战斗引擎同等级的 **Host authoritative sync pipeline**。

这一步完成后，项目状态会从：
- “商店引擎本地核心已成型”
推进到：
- “商店引擎联机同步第一版可测”

后续所有 UI、回归、双机排错、恢复增强，都会建立在这条同步主干上。