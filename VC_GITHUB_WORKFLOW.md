# STS2_mod 工作区 · GitHub 管理准则

> 本准则规范所有 STS2_mod 工作区下的 Mod 项目的 GitHub 管理流程。
> 所有新建项目及现有项目均应遵循本准则。

---

## 一、核心原则

1. **不另建仓库** — 各项目模块统一存放在 STS2_mod 主仓库下，以子目录区分
2. **不强制开分支** — 直接提交到 master/main，除非变更规模过大需征求用户意见
3. **构建成功才上传** — 反复修改直至 `dotnet build` 通过，再执行 push
4. **变更说明优于代码** — 提交信息聚焦"做了什么、为什么"，而非仅列文件清单

---

## 二、新项目创建流程

### 2.1 触发时机

任何在 STS2_mod 工作区下新建 Mod 项目文件夹时，立即执行以下流程。

### 2.2 操作步骤

```
Step 1：搭建项目框架
  └─ 创建目录结构（参考 NoClientCheats）
  └─ 编写 .csproj 文件
  └─ 编写 ModInitializer.cs（含 ModConfig 注册）
  └─ 编写 README.md（项目说明 + 功能列表）

Step 2：初始化 Git 仓库（仅首次新建仓库时）
  └─ 仅当 STS2_mod 主仓库尚无 git 时才执行 git init
  └─ 主仓库已有 git 时跳过此步

Step 3：首次提交：格式「[Init] 项目名 — 初始框架」

Step 4：在项目 README.md 添加说明
  └─ 项目名称 + 一句话描述
  └─ 功能列表（含待实现项）
  └─ 配置说明（如有）

Step 5：构建并上传
  └─ dotnet build 确认通过
  └─ git add + commit + push
```

### 2.3 首次提交格式

```
[Init] RunHistoryAnalyzer — 初始框架

功能：
- ModInitializer 入口，ModManagerInitPostfix 初始化
- ModConfig 配置页（检测开关）
- README.md 项目文档

项目结构：
- RunHistoryAnalyzer/
  ├─ ModInitializer.cs
  ├─ ModConfig.cs
  ├─ RunHistoryAnalyzer.csproj
  └─ README.md
```

---

## 三、日常开发提交流程

### 3.1 触发时机

每次完成一组逻辑修改后，执行 `dotnet build`，通过后立即提交并推送。

### 3.2 流程

```
修改代码
  ↓
dotnet build
  ↓
┌─ 失败 → 继续修改代码，回到上一步
└─ 成功 → git add + git commit + git push
```

**关键规则：构建失败绝不提交。** 宁可分多次小提交，也不带失败代码上传。

### 3.3 判断标准：是否值得提交

| 变更类型 | 是否提交 | 理由 |
|---|---|---|
| 新增检测规则（GoldConservationRule 等） | ✅ 立即提交 | 核心功能里程碑 |
| 新增 UI 组件（分析按钮/结果窗口） | ✅ 立即提交 | 交互功能里程碑 |
| Bug 修复 | ✅ 立即提交 | 可独立回退 |
| 重构（不影响外部行为） | ⚠️ 累积后提交 | 避免碎片化提交 |
| 仅修改注释/文档 | ❌ 不提交 | 非功能性变更 |
| 仅修改 .gitignore | ❌ 不提交 | 非功能性变更 |

### 3.4 提交信息格式

采用 **conventional commit** 格式，便于后期生成 CHANGELOG：

```
<类型>(<范围>): <简短描述>

[可选正文：详细说明变更原因、变更内容、注意事项]

[可选脚注：关联的 issue 或准备工作]
```

**类型（必填）：**

| 类型 | 含义 | 示例 |
|---|---|---|
| feat | 新功能 | 新增金币守恒检测规则 |
| fix | Bug 修复 | 修复遗物来源追溯的空引用异常 |
| refactor | 重构（不影响功能） | 提取 IAnomalyRule 接口 |
| docs | 仅文档变更 | 更新 README 功能列表 |
| ui | UI/交互变更 | 添加分析按钮 |
| perf | 性能优化 | 优化 JSON 解析缓存逻辑 |
| chore | 构建/工具/依赖变更 | 更新目标框架为 net8.0 |

**范围（可选）：**

| 范围 | 含义 |
|---|---|
| detection | 检测层相关 |
| ui | UI层相关 |
| data | 数据层相关 |
| config | 配置系统相关 |
| infra | 基础设施（CI/CD/构建脚本） |

### 3.5 示例提交

**新增检测规则：**
```
feat(detection): 实现金币守恒定律检测

- GoldConservationRule：验证 初始+ΣGained-ΣSpent=最终CurrentGold
- 允许1金币误差（浮点运算精度问题）
- 无异常时返回空列表，不影响性能

关联准备：FEASIBILITY_ANALYSIS.md 中 P0 优先级定义
```

**新增 UI 按钮：**
```
feat(ui): 在历史记录详情面板添加分析按钮

- 按钮位于详情面板底部操作栏，文字「🔍 分析」
- 点击后调用 AnalyzeRunner，分析完成后弹出结果窗口
- 按钮状态：默认/分析中/已分析（无异常绿/有异常橙）
```

**Bug 修复：**
```
fix(detection): 修复 CardSourceTraceRule 中遗物判定逻辑错误

原因：混淆了 RelicChoices 和 BoughtRelics 的 ID 类型
修复：将 ChoiceHistoryEntry 字段访问改为 .Id.Entry 正确路径
```

**构建/依赖变更：**
```
chore: 更新目标框架为 net8.0，与游戏 Godot Mono 版本对齐

影响：所有 .csproj 的 TargetFramework 统一为 net8.0
```

---

## 四、分支策略

### 4.1 原则

**默认不创建分支**，所有变更直接提交到 master/main。

### 4.2 何时考虑短期分支

当单次变更规模过大，可能影响其他模块或造成冲突时，主动向用户确认：

```
变更规模评估：
  ├─ 仅影响单一项目目录（如仅修改 RunHistoryAnalyzer/）
  │    → 直接提交，无需分支
  │
  ├─ 涉及多个项目目录（RunHistoryAnalyzer/ + NoClientCheats/）
  │    → 询问用户是否开短期分支
  │
  └─ 涉及共享基础设施变更（如 ModManagerInitPostfix 框架改动）
       → 询问用户是否开短期分支
```

**询问话术**：

> 当前变更涉及 [具体模块]，可能影响其他项目，是否需要创建短期开发分支？

---

## 五、README.md 管理

每个项目根目录的 README.md 应保持最新，每次主要功能上线后同步更新。

### 5.1 必填内容

```
# 项目名称

> 一句话描述项目功能

## 功能列表

- [ ] 功能一（完成打勾）
- [ ] 功能二

## 使用方法

## 配置说明

## 更新日志
```

### 5.2 更新时机

| 变更类型 | README 更新 |
|---|---|
| 新增功能 | ✅ 在功能列表打勾，补充说明 |
| Bug 修复 | ✅ 在更新日志追加 |
| 重构/优化 | ⚠️ 酌情更新 |
| 仅构建脚本变更 | ❌ 不更新 |

---

## 六、GitHub Release 管理

### 6.1 Release 时机

- 完成一组有意义的功能迭代后
- 非强制，但推荐每个 Release 包含至少一个完整功能

### 6.2 Release 内容格式

参考 `NoClientCheats/release_body.md`：

```
## 本次更新

- 变更一
- 变更二

## 下载地址

[Download mod.zip](链接)

## 兼容性

- 游戏版本：x.x.x
- 依赖：无
```

### 6.3 发布标签

每次发布 Release 时打标签：

```
git tag -a v1.0.0 -m "v1.0.0 — 初始发布：金币守恒+HP守恒检测"
git push origin v1.0.0
```

---

## 七、准则附录

### 7.1 各项目版本状态（已发布的 Release）

| 项目 | 最新 Release | 最新 Tag |
|---|---|---|
| NoClientCheats | v1.1.2 | NCC_v1.1.2 |
| RichPing | v0.1.1 | v0.1.1 |
| MP_PlayerManager | v1.0.0 | MP_PLv_1.0.0 |
| MP_SavePlayerRemover | v1.1.0 | MP_SPR_v1.1.0 |
| HostPriority | 无 | 无 |
| ControlPanel | 无 | 无 |
| RunHistoryAnalyzer | 待建 | 待建 |

> HostPriority、ControlPanel 目录已存在于仓库中但尚未发布 Release。

### 7.2 常见问题

**Q：构建失败但急需提交部分代码怎么办？**
A：不应妥协。可将变更拆分成多个独立 commit，确保每个都能单独构建成功。

**Q：连续多次小改动可以合并吗？**
A：可以。将多次相关的小改动合并为一条 commit，但 commit message 中列明所有变更点。

**Q：忘了在构建后提交，几天后才发现怎么办？**
A：补充提交即可。commit message 中说明「补交：上次构建的变更」，不要强行修改历史。

### 7.3 mod_manifest.json 的 JSON 规范要求（2026-03-22 事故记录）

**错误症状：**

```
System.Text.Json.JsonException: '0x0A' is invalid within a JSON string.
Path: $.description | LineNumber: 6 | BytePositionInLine: 377.
```

游戏拒绝加载 mod，日志文件为 `C:\Users\<user>\AppData\Roaming\SlayTheSpire2\logs\godot.log`。

**根本原因：** `description` 字段值内含原始 0x0A 字节，而非 `\n` escape 序列。
JSON 规范不允许字符串值内出现 raw LF（0x0A），必须写成 `\n`（0x5C 0x6E）。

**正确生成方式：永远用序列化库，不要手动写 JSON。**

```bash
# ✅ 正确：用 Python json.dump
python -c "
import json
manifest = {
    'id': 'MyMod',
    'description': '第一行\n第二行\n第三行',
    ...
}
with open('mod_manifest.json', 'w', encoding='utf-8') as f:
    json.dump(manifest, f, ensure_ascii=False, indent=2)
"

# ✅ 正确：用 C# JsonSerializer
var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
File.WriteAllText("mod_manifest.json", json, Encoding.UTF8);

# ❌ 错误：手动写 JSON，\n 会被解释为真实换行符
```

**验证方法：**

```bash
# 检查 description 字段内是否还有 raw LF
python -c "
import json
with open('mod_manifest.json', 'rb') as f:
    raw = f.read()
lf = raw.count(b'\x0a')
# JSON 结构换行约 10 个（如大于 15 则 description 内部可能也有 raw LF）
print(f'LF bytes: {lf}')
# 用 json 解析验证合法性
with open('mod_manifest.json') as f:
    json.load(f)
print('Valid JSON')
"
```

---

## 八、参考文档

- 本地工作区总记忆：`K:\杀戮尖塔mod制作\STS2_mod\VC_SESSION_MEMORY.md`
- NoClientCheats Release 模板：`NoClientCheats/release_body.md`
- GitHub Release 指南：`K:\杀戮尖塔mod制作\STS2_mod\VC_GITHUB_RELEASE_GUIDE.md`
