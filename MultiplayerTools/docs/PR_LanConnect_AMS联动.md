## 改动内容

- 新增 `LanConnectAutoModSubscriberCompat.cs`：运行时检测 AutoModSubscriber (AMS) mod，检测到后注册 `ExternalDialogHandler` 回调接管 ModMismatch 弹窗的 UI
- 弹窗使用大厅风格深色配色（#0D1117 / #1A2A3A / #3B82F6），包含双区块：
  - **缺失 Mod 列表**：每行显示 mod 名 + 状态 + 进度条 + 单独订阅按钮 + 打开工坊按钮 + 全部自动订阅
  - **多余 Mod 列表**：每行带 checkbox（默认勾选）+ 一键禁用
- 核心逻辑（SteamUGC 订阅、mod 禁用、sidecar 映射）全部复用 AMS 的 public API，通过反射调用，**不增加硬依赖**
- `Entry.cs`：在 `LanConnectExternalModDetection.Detect()` 之后调用 `LanConnectAutoModSubscriberCompat.Initialize()`

## 原因

当客机加入房间时如果 mod 列表不一致，原版游戏只显示一个简单的 ModMismatch 错误弹窗。AMS 已经实现了自动订阅缺失 mod + 勾选禁用多余 mod 的功能，但用的是自己的 UI 组件。本 PR 让大厅 mod 接管 AMS 的弹窗 UI，使用大厅风格展示，用户体验更统一。

## 兼容性

- **未安装 AMS 时**：`Initialize()` 找不到 AMS 类型，静默跳过，行为完全不变
- **安装了 AMS 但未安装大厅时**：AMS 用自己的默认 UI，行为完全不变
- **两者都安装时**：大厅接管 UI，核心逻辑复用 AMS

## 依赖

- AMS 侧已暴露 `public static Func<ConnectionFailureExtraInfo, bool>? ExternalDialogHandler` 字段（[AutoModSubscriber commit 595356c](https://github.com/Jianbao233/AutoModSubscriber/commit/595356c)）
- 本 PR 通过反射访问该字段，不直接引用 AMS DLL

## 测试

- 未安装 AMS 时静默回退（代码逻辑保证）
- 安装 AMS 时弹窗 UI 接管 -- 需要联机测试

## 作者

@Bilibili我叫煎包