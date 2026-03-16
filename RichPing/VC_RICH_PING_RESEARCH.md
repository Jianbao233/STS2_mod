# RichPing 相关调研 · 可行方案

> 基于 Web 搜索整理的替代方案与参考资料。

---

## 一、关键发现：补丁目标线索

从 SL2 项目结构中可锁定以下与 Ping 相关的类，可作为 Harmony 补丁的候选：

| 类型 / 文件 | 用途推测 |
|-------------|----------|
| **EndTurnPingMessage** | 很可能为“结束回合 Ping”的网络/消息结构，序列化或构造时可能包含文本 |
| **NPingButton** | UI 上的 Ping 按钮，点击时发送 Ping |
| **MapPingMessage** | 地图 Ping（可能与本 Mod 无关） |

建议反编译 `sts2.dll` 时优先搜索：`EndTurnPingMessage`、`NPingButton`。

---

## 二、BaseLib-StS2：官方生态库

- **仓库**：https://github.com/Alchyr/BaseLib-StS2  
- **Wiki**：https://alchyr.github.io/BaseLib-Wiki/

BaseLib 已在使用 Harmony 做补丁，例如 `GetCustomLocKey`、`TheBigPatchToCardPileCmdAdd` 等。

**可选方案**：将 RichPing 作为 BaseLib 的依赖 Mod，参考其补丁写法，对 `EndTurnPingMessage` 或相关类进行 Patch，减少自行摸索成本。

**注意**：BaseLib 当前要求 Steam 测试分支。

---

## 三、ModTemplate-StS2：项目模板

- **仓库**：https://github.com/Alchyr/ModTemplate-StS2  
- **Wiki**：https://github.com/Alchyr/ModTemplate-StS2/wiki

模板中包含：

- 依赖 BaseLib 的 Mod 项目
- 使用 NuGet 配置 `Alchyr.Sts2.Templates`
- 利用 Rider 的 Publish 流程一键生成并发布 Mod

建议：以 ModTemplate 为起点，新建项目，再在此基础上实现 RichPing 逻辑与 Patch。

---

## 四、Harmony 在 Godot 导出后失效问题

**现象**：编辑器内 Harmony 正常，导出后打补丁失败。

**常见原因**：

1. Godot 导出后使用独立的 AssemblyLoadContext，Harmony 生成的动态模块未被加载到同一上下文；
2. 不同平台（如 Linux）可能触发 `DllNotFoundException` 等。

**参考处理**：

- 使用 `AssemblyLoadContext.LoadFromStream` 替代 `Assembly.Load`；
- Harmony 官方在 2024 年左右已合入相关修复（见 [Harmony #572](https://github.com/pardeike/Harmony/issues/572)）；
- 确保使用较新版本的 Harmony / Lib.Harmony（NuGet）。

---

## 五、其他 SL2 Mod 参考

| Mod | 说明 |
|-----|------|
| Minty Spire 2 | QoL 合集，包含治疗预览、伤害指示等 |
| STS2RouteSuggest | 路径建议 |
| DamageMeter (Skada) | 伤害统计（你已安装） |
| ModConfig | 通用 Mod 配置 UI（你已安装） |

这些 Mod 的实现思路和依赖管理可作参考，尤其是 ModConfig 的集成方式。

---

## 六、推荐实施路径

1. **确定补丁目标**  
   用 dnSpy / ILSpy 打开 `sts2.dll`，搜索 `EndTurnPingMessage`、`banter`、`endTurnPing`，找到构造或序列化 Ping 文本的逻辑。

2. **考虑使用 BaseLib**  
   将 RichPing 改为依赖 BaseLib，在其基础上写一个专门针对 Ping 文本的 Harmony 补丁（类似 `TheBigPatchToCardPileCmdAdd`）。

3. **使用 ModTemplate 搭建**  
   用 ModTemplate 创建新 Mod 项目，再在项目中加入 RichPing 的 Patch 和配置逻辑。

4. **验证导出后 Patch 是否生效**  
   构建 Mod 后，在导出版本中实测 Ping 文本是否被替换，若失败再检查 AssemblyLoadContext 与 Harmony 版本。

---

## 七、相关链接

- BaseLib: https://github.com/Alchyr/BaseLib-StS2  
- BaseLib Wiki: https://alchyr.github.io/BaseLib-Wiki/  
- ModTemplate: https://github.com/Alchyr/ModTemplate-StS2  
- ModTemplate Setup: https://github.com/Alchyr/ModTemplate-StS2/wiki/Setup  
- Harmony #572（导出相关修复）: https://github.com/pardeike/Harmony/issues/572  
