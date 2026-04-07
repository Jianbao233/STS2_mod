# 项目日志 (Changelog)

---

## 2026-04-07 — MultiplayerTools v0.2.0 安卓存档适配与主菜单安全限制

### 变更内容

1. **工具入口限制为主菜单首页**
   - 悬浮按钮离开主菜单首页时自动隐藏，返回主页面时自动恢复
   - 快捷键 `F1` 仅在主菜单首页生效
   - 工具面板本体增加守卫，避免在游戏过程中被误打开导致风险操作

2. **新增安卓端存档路径适配**
   - 存档扫描不再只依赖 PC 的 `steam/{id}` 目录
   - 现可同时识别 `steam` / `default` / `editor` 账户目录
   - 兼容安卓实际目录结构：`user://default/{localPlayerId}/[modded/]profileN/saves/`

3. **新增安卓端本地玩家 ID 适配**
   - 移动端不再显示“Steam ID unavailable”占位
   - 改为读取游戏本体的本地玩家 ID 作为当前身份
   - 在当前测试设备上，本地玩家 ID 为 `1`

4. **设置页与备份扫描同步适配安卓路径**
   - 设置页显示的 Save root / Backup root 改为实际运行平台路径
   - 备份页的 legacy backup 扫描逻辑同步支持非 Steam 账户目录

5. **实机验证通过**
   - 已将 PC 端多人联机存档改写为安卓本地玩家 ID 后部署到手机
   - 手机端可以成功读取该多人存档
   - 说明安卓端多人联机存档格式与 PC 端一致，之前的核心问题是路径与本地 ID 适配

---

## 2026-04-02 — MultiplayerTools 删除玩家后存档列表不刷新

### 根因

`RemovePlayerPage` 和 `AddPlayerPage` 成功修改存档文件后，调用 `ReloadSave()` 刷新内存数据，但 **没有重新扫描 `AllProfiles`**。

`SaveSelectPage` 的存档列表数据源是 `MpSessionState.AllProfiles`（启动时一次性扫描缓存），其中每个 `SaveProfile` 的 `PlayerCount` 字段在启动时从 `current_run_mp.save` 读取。删除玩家后，这个缓存值仍然是 2，导致工具的 SaveSelect 列表无法反映最新状态。

此外，删除成功后切换到 Takeover 页面（`SwitchPage(PAGE_TAKEOVER)`），如果此时 AllProfiles 缓存未刷新，Takeover 页面的玩家列表也可能显示旧数据。

### 修复

1. **`ReloadSave()` 新增 `RefreshProfiles()` 调用** — `LoadSave` 成功后立即重新扫描所有 profile，使 `SaveProfile.PlayerCount` 反映磁盘最新值。`RefreshProfiles()` 会触发 `ProfilesChanged` 事件，`MpPanel.OnSessionProfilesChanged` 自动重建 SaveSelect 页面。

2. **`RemovePlayerPage` 成功后改为 `RefreshCurrentPage()`** — 不再跳转到 Takeover 页面（避免 AllProfiles 未刷新的窗口期），直接重建当前页面，`Build()` 会重新从 `MpSessionState.GetPlayers()` 读取新的玩家数量。

3. **`AddPlayerPage` 同样改为 `RefreshCurrentPage()`** — 保持一致。

4. **`EnumerateSaveRoots()` 改为 `internal`** — `BackupPage.cs` 需要访问。

5. **`WriteSaveFile()` 新增 `makeBackup` 参数** — 向后兼容。

6. **`SaveManagerHelper.DebugNdjsonLogPath`** — 新增 internal 属性，供调试日志使用。

---

## 2026-04-01 — NCC v1.3.0 卡组状态回滚检测

### 新增：卡组状态回滚检测

多人联机时，客机可能在休息点/事件中利用 UI bug（键盘方向键多选卡牌）超额升级、删除或转化卡牌。

**检测机制**：主机在执行休息点/事件选项后记录卡组快照（`RestSiteSynchronizer.ChooseOption` Postfix），收到客机同步消息时对比（`CombatStateSynchronizer.OnSyncPlayerMessageReceived` Prefix），不一致则强制回滚。

**覆盖场景**：
- 篝火升级（SMITH）—— 可检测超额升级
- 篝火烹饪（COOK，删2张）—— 可检测超额删除
- 所有涉及卡牌选择的遗物触发（`Pomander`、`PrecariousShears`、`NewLeaf` 等）
- 所有涉及卡牌选择的活动事件（`Trial`、`ZenWeaver` 等）

**不覆盖**：战斗中的卡牌操作（Entropower、TriBoomerang 等）—— 无合适的休息点同步时机。

**回滚方式**：通过 `INetGameService.SendMessage<SyncPlayerDataMessage>` 向作弊客机定向发送正确卡组状态，`SyncWithSerializedPlayer` 自动重建卡组。

**新增文件**：`NoClientCheats/DeckSyncPatches.cs`

---

## 2026-04-01 — MultiplayerTools 面板不可见 Bug 修复

### 问题描述
按 F1 后，面板完全不显示（无报错，无任何 UI）。

### 根本原因

`MpPanel.Build()` 中创建了 `CanvasLayer`，但**从未将其加入场景树**：

```csharp
// 之前 — _layer 创建后直接 AddChild 子节点，但 _layer 本身不在树中
_layer = new CanvasLayer { Layer = 200, Name = "MpPanel" };
_layer.AddChild(backdrop);   // backdrop 的父节点存在，但 _layer 不在树中
_layer.AddChild(_panel);
```

结果：`_layer` 及其全部子节点在 Godot 渲染阶段完全不可见，因为 Godot 的渲染器只遍历场景树中的节点。

### 调试过程

1. **误判阶段** — 检查了锚点、偏移量、`UiFont.ApplyTo`、样式覆盖等，均未发现问题
2. **关键发现** — 尝试用 `Timer` 延迟打印调试信息时，Godot 报错：`Unable to start the timer because it's not inside the scene tree`
3. **推论** — 该报错暗示 `_layer` 在 F1 触发时不在场景树中，进而怀疑 `CanvasLayer` 从未被加入根节点
4. **确认** — 检查 `Build()` 代码，发现 `_layer = new CanvasLayer(...)` 后没有 `AddChild(_layer)` 调用

### 修复内容 (`MpPanel.cs`)

1. **核心修复** — 在 `Build()` 中，`_layer` 创建后立即加入场景树：
   ```csharp
   _layer = new CanvasLayer { Layer = 200, Name = "MpPanel" };
   (Engine.GetMainLoop() as SceneTree)?.Root?.AddChild(_layer, false, Node.InternalMode.Disabled);
   ```

2. **backdrop 锚点补全** — 确保全屏遮罩正确填满：
   ```csharp
   var backdrop = new ColorRect {
       AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1,
       OffsetRight = 0, OffsetBottom = 0,
       Color = Panel.Styles.Backdrop,
       MouseFilter = Control.MouseFilterEnum.Ignore
   };
   ```

3. **_panel 偏移补全** — 响应锚点的 `OffsetRight`/`OffsetBottom`：
   ```csharp
   _panel = new Panel {
       AnchorLeft = 0.5f, AnchorRight = 0.5f,
       AnchorTop = 0.5f, AnchorBottom = 0.5f,
       OffsetRight = 500, OffsetBottom = 600,
       OffsetLeft = -250, OffsetTop = -300,
       MouseFilter = Control.MouseFilterEnum.Ignore
   };
   ```

4. **_mainVBox 锚点补全** — 确保 `VBoxContainer` 填满面板：
   ```csharp
   _mainVBox = new VBoxContainer {
       AnchorLeft = 0, AnchorRight = 1, AnchorTop = 0, AnchorBottom = 1,
       OffsetRight = 0, OffsetBottom = 0
   };
   ```

### 经验教训

1. **Godot 中节点必须入树才能工作** — 创建节点只是内存分配，渲染、输入、生命周期全部依赖 `GetTree() != null`
2. **日志会说谎** — `_layer.Visible = true` 在日志中出现不代表节点在正确的父节点下。只说明赋值语句执行了
3. **调试 Godot UI 时，第一步永远是检查 `node.GetTree() != null`**
4. **CanvasLayer 是独立的层** — 它本身需要通过 `SceneTree.Root.AddChild()` 显式加入根节点，不能依赖 Godot 的场景合并机制
