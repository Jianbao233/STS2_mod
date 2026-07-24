# 次元旅人

《杀戮尖塔 2》角色 Mod 的 `0.1.0` 开发版本。当前完整卡牌实现阶段已收口：45 张奖励卡、27 个药剂模型、五条构筑轴的单人闭环和双人 host-drive 冒烟均已完成自动验收。后续工作不属于本阶段卡牌实现范围，主要是药剂背包 UI、原生药水萃取、遗物体系、表现资源和真实双进程多人专项。

## 当前状态

### 已实现

- 次元旅人角色、12 张起始普通卡、起始遗物，以及不进入奖励池的兼容基础打防。
- 生机、挥发、腐化三类基础原理，以及催化、扩散、回响三类特殊原理。
- 战斗期药剂背包、永久系统牌 `药剂包`、动态主原理支付和按玩家隔离的隐藏战斗状态。
- 9 个药剂家族的普通、精制、杰作模型及各自 `+` 升级，共 27 个药剂模型。
- 45 张奖励卡，分布为普通 10 张、蓝色 18 张、金色 17 张。
- 显式生产、品质转化、局部／完整扩散、回响重放、回响派生药剂和实验记录链路。
- 8 张原生选择界面使用的原理 Token。Token 可由 `ModelDb` 解析，但不进入奖励池、起始套牌或正式内容计数。
- 中英文角色、卡牌、能力、遗物与悬浮提示本地化。

### 已验证

- 游戏/API 基线为 `0.109.0`；当前代码在 RitsuLib `0.109.0` 兼容分支下编译通过，0 警告、0 错误。
- 运行时目录契约：正式内容 87 个模型、选择 Token 8 个、`ModelDb` 可解析总数 95；其中 45 张奖励卡为普通 10、蓝色 18、金色 17，药剂模型为 27 个。
- 中英文各 6 份本地化 JSON 均可解析；卡牌表各 202 个键、键集合一致，95 个可解析卡牌模型均有标题。
- 单人自动验收覆盖基础设施、配方支付、生产、品质操作、催化、扩散、回响和实验转化，共 16/16 通过。
- 双人 host-drive 冒烟形成同一 `RunState` 中 `NetId 1/1001` 两位次元旅人，并验证：
  - 炼金原理、回合状态和药剂背包按玩家隔离；
  - 能量与次级资源只由出牌所有者支付；
  - 队友目标按指定战斗 ID 结算，双方快照观察到一致结果；
  - 原生选择的 `choiceId`、远端等待和远端提交均归属于正确玩家，主机不会代交本地选择。
- 多人同步会序列化配方临时身份和药剂来源；继承模型的 `[SavedProperty]` 具备原生反序列化可调用的 setter，`EchoDerived` 来源恢复时同步恢复其费用不变量。

### 自动验收

统一入口会为每次运行生成独立报告到 `tests/acceptance/reports/acceptance_*.json`，并在项目根目录生成对应 `_runtime_acceptance_*.log`。

```powershell
# 五套单人验收，当前共 16 个用例
.\tests\acceptance\Invoke-Acceptance.ps1 `
  -Suite infrastructure,formulas,production,operations,special-axes

# 双人 host-drive 冒烟，当前 1 个端到端用例
.\tests\acceptance\Invoke-Acceptance.ps1 -Suite coop-smoke
```

`-SkipBuild` 只适用于已明确部署当前 DLL/PCK 的重复探针；正式收口应省略该参数，让运行器先重建并部署正式 Mod 与测试适配器。

### 本阶段之外

- 药剂背包查看 UI、原生药水萃取、R01～R09 遗物、Epoch、角色事件、美术、动画和 VFX 尚未实现。
- 当前双人自动化是同进程伪联机的 host-drive 冒烟，不等同于真实双进程环境下的掉线、延迟、重连、客机 UI 与长局摘要压力测试。
- 正式显示名、文案润色和表现设计继续暂停；当前功能名与冻结规则用于开发验证。

## 依赖边界

- **运行时依赖**：已订阅的 `STS2-RitsuLib`，兼容游戏 `0.109.0` 分支。
- **测试基础设施**：实际加载 KitLib `0.31.1`，用于游戏内调试、MCP 自动化和伪联机宿主。双人入口通过反射校验 KitLib 内部伪联机契约，正式 Mod 不新增 KitLib 编译或运行时依赖，也不修改 KitLib 源码。

## 源码职责

- `src/Alchemy/Backpack/`：药剂背包状态、选择流程和系统牌保护。
- `src/Alchemy/Production/`：显式生产、催化增幅与最终生产快照。
- `src/Alchemy/Resolution/`：药剂目标、扩散、回响和派生来源结算。
- `src/Alchemy/State/`：按玩家隔离的战斗状态与摘要。
- `src/Content/`：卡牌、能力、遗物及角色卡池模型。
- `src/Resources/`：炼金原理的注册、获取与支付。
- `src/Bootstrap/`：RitsuLib 与内容程序集初始化。
- `test-adapter/`：仅用于验收的夹具、快照、支付／选择审计和伪联机控制。
- `tests/acceptance/`：PowerShell 验收 DSL、套件与 JSON 报告。
- `DimensionalTraveler/localization/`：英文与简体中文本地化。

## 本地构建与部署

运行以下命令会编译 DLL、导出 PCK，并同步到项目 `torelease/` 和游戏的 `mods/DimensionalTraveler` 目录。

```powershell
.\build.ps1
```

构建缓存、PCK、`torelease/`、`release/`、验收报告和 `_runtime_acceptance_*` 运行日志均不纳入 Git。
