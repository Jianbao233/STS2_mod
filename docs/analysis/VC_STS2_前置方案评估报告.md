# STS2 前置方案评估报告（面向“卡牌/药水/遗物数据 Mod”）

- 评估时间：2026-04-08
- 仓库基线：`K:\Dev\BaseLib-StS2`、`K:\Dev\STS2-RitsuLib`、`K:\Dev\ModTemplate-StS2`、`K:\Dev\SlayTheSpire2ModdingTutorials`、`K:\Dev\sts2-quickRestart`
- 说明：以上仓库已按完整历史拉取（非浅克隆）

## 1. 结论先行

1. 对“新增内容型 Mod（卡/药/遗物）”，不建议纯手写 self-hook 作为主路线。
2. 必须二选一时，当前更推荐 BaseLib 作为默认依赖（教程和模板链路更一致，接入复杂度更低）。
3. “前置维护慢”的担忧可以理解，但按当前数据，两库都高频更新；主要风险是 API 漂移而非停更。
4. 最稳妥策略是“可控依赖”：固定版本 + 自己的适配层（Facade）+ 升级回归。

## 2. 两条路线本质差异

| 路线 | 你会得到 | 你会承担 |
|---|---|---|
| 借鉴前置并自己 hook | 最高控制权、无运行时前置依赖 | 高维护成本、兼容和回归全自担 |
| 直接使用前置（BaseLib/RitsuLib） | 开发快、内容注册与工具链现成 | 版本耦合、升级节奏受上游影响 |

## 3. 量化基线（本地完整仓库）

| 项目 | BaseLib-StS2 | STS2-RitsuLib |
|---|---:|---:|
| 最近提交 | 2026-04-07 | 2026-04-08 |
| 提交总数 | 329 | 219 |
| 近一周提交（自 2026-04-01） | 94 | 81 |
| Tag 数量 | 18 | 41 |
| 最新 Tag | v0.2.8 | v0.0.43 |
| C# 文件数 | 126 | 324 |
| C# 行数 | 13,517 | 42,995 |

解读：两库都活跃；RitsuLib 覆盖面更大、体量更重；BaseLib 更轻、上手更直接。

## 4. 结构与维护性观察

### BaseLib

- 入口清晰：`BaseLibMain` 初始化 + `Harmony PatchAll`。
- 内容模型抽象直接：`CustomCardModel / CustomRelicModel / CustomPotionModel`。
- 注册链路直观：`PoolAttribute + AddModelToPool + ModelDb` 补丁。
- 更贴合“先做内容扩展再逐步深入”的路径。

### RitsuLib

- 框架完整：内容包、生命周期、补丁系统、持久化、设置 UI、关键词、时间线、兼容层。
- 工程化强：显式注册、冻结机制、可组合注册条目。
- 支持与 BaseLib 并存和桥接。
- 代价：学习面和升级跟踪成本更高。

### 纯 self-hook

- 技术可行，尤其对小范围 patch。
- 但在 EA 高频变更阶段，内容注册体系（池、ID、本地化、解锁、联机一致性）自维护成本很高。

## 5. 多维评分（1-5）

| 维度 | 自己 hook | BaseLib | RitsuLib |
|---|---:|---:|---:|
| 内容型开发速度 | 2 | 5 | 4 |
| 控制权 | 5 | 3 | 3 |
| 抗版本变更 | 2 | 4 | 4 |
| 心智负担 | 2 | 4 | 2 |
| 多人/兼容基础设施复用 | 2 | 4 | 5 |
| 长期维护总成本 | 2 | 4 | 3 |
| 适配“卡/药/遗物”目标 | 2 | 5 | 4 |

## 6. 推荐落地方案

1. 默认依赖 BaseLib。
2. 固定前置版本（不要 `*`）。
3. 自建 `MyMod.Framework` 适配层，业务代码只调用适配层。
4. 前置升级时只改适配层并做回归。

## 7. 何时不该用前置

1. 只做小 patch（UI、菜单、命令）且不新增大量内容。
2. 团队愿意长期维护自己的注册/兼容系统。
3. 有能力持续跟进 `ModelDb/Pool/Loc/Unlock/MP` 的版本变化。

## 8. PVP Mod 前置选择（新增）

### 8.1 结论

- **重底层 PVP（同步、判定、回滚、防作弊）**：优先“自建 Core（sts2 + Harmony）”，前置只做可选能力。
- **中等复杂 PVP（以内容扩展为主，少量联机逻辑）**：可先用 BaseLib，保持依赖最小化。
- **需要完整框架能力（设置页、生命周期、内容包、大规模扩展）**：再评估 RitsuLib。

### 8.2 原因

- PVP 最敏感点是“确定性 + 同步一致性 + 补丁冲突控制”。
- 前置越重，介入面越广，冲突面越大；排障复杂度随之提升。
- 先把 PVP 核心逻辑隔离在自有 Core 层，能最大化可控性。

### 8.3 推荐架构

1. `MyPvpMod.Core`：仅依赖 `sts2 + Harmony`，承载网络协议、状态机、同步、权威判定。
2. `MyPvpMod.ContentAdapter`：可选接 BaseLib（内容注册/UI便利）。
3. `MyPvpMod.Tools`：日志、诊断、回放验证。

这样可做到：
- 去前置可行（替换适配层而非重写核心）
- 与其他 Mod 的冲突面可控
- 联机一致性排查边界清晰

## 9. Sources

- https://github.com/Alchyr/BaseLib-StS2
- https://github.com/Alchyr/BaseLib-StS2/blob/master/BaseLibMain.cs
- https://github.com/Alchyr/BaseLib-StS2/blob/master/Patches/Content/ContentPatches.cs
- https://github.com/Alchyr/BaseLib-StS2/blob/master/Patches/Content/PrefixIdPatch.cs
- https://github.com/Alchyr/BaseLib-StS2/blob/master/Extensions/TypePrefix.cs
- https://github.com/Alchyr/ModTemplate-StS2
- https://github.com/Alchyr/ModTemplate-StS2/blob/master/content/ModTemplate/ModTemplate.csproj
- https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials
- https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials/blob/master/Basics/03%20-%20BaseLib%E6%8E%A5%E5%8F%A3/README.md
- https://github.com/BAKAOLC/STS2-RitsuLib
- https://github.com/BAKAOLC/STS2-RitsuLib/blob/main/Docs/zh/GettingStarted.md
- https://github.com/BAKAOLC/STS2-RitsuLib/blob/main/Docs/zh/FrameworkDesign.md
- https://github.com/BAKAOLC/STS2-RitsuLib/blob/main/Docs/zh/ContentPacksAndRegistries.md
- https://github.com/BAKAOLC/STS2-RitsuLib/blob/main/RitsuLibFramework.cs
- https://github.com/BAKAOLC/STS2-RitsuLib/blob/main/RitsuLibFramework.PatcherSetup.cs
- https://github.com/freude916/sts2-quickRestart
- https://github.com/freude916/sts2-quickRestart/blob/main/README.md
