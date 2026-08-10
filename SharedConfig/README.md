# SharedConfig · 共享配置框架库

> **不是独立 mod**：供各 mod 复用的配置框架库（编译期引用，不单独进工坊）。

## 定位

把"mod 配置"做成统一基建：JSON 持久化、变更事件、原生设置页 UI 控件、Harmony 反射集成，各 mod 继承即可获得一致的配置体验（与原生设置界面风格统一）。

## 组成

| 目录 | 内容 |
|---|---|
| `Config/ModConfig.cs` | 抽象基类：JSON 读写、`Changed` 变更事件、属性反射收集、保存节流 |
| `Config/SimpleModConfig.cs` | 简化配置实现（无 UI 的轻量场景） |
| `Config/ModConfigRegistry.cs` | 配置注册表（mod 前缀隔离） |
| `Config/UI/` | 原生设置页 UI 控件：`NConfigSlider` / `NConfigTickbox` / `NConfigDropdown` / `NModConfigPopup` / 行容器（复用 `settings_screen_line_header.tres` 主题） |
| `Extensions/` | `ControlExtensions` / `StringExtensions` / `TypeExtensions` |
| `Utils/` | `GodotUtils` / `SpireField` |
| `Stubs/` | 编译期桩 |

## 用法（消费 mod）

1. csproj 引用本库（ProjectReference）。
2. 配置类继承 `SharedConfig.Config.ModConfig`，属性加 `[ConfigOption]` 等特性（见 `ConfigAttributes.cs`）。
3. 注册到 `ModConfigRegistry`，UI 控件自动按属性生成。
4. 属性变更时调用 `Changed()` 触发持久化与事件。

## 参考实现

- `NoClientCheats/` 使用本库的配置面板。
- 技术决策记录见 `STS2_mod/MEMORY.md`（ModConfig 反射集成、`[ModuleInitializer]` 三重保险）。