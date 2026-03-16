# VC 会话记忆 · 工作流承接

> 新对话时请先阅读本文，以延续开发上下文。

---

## 项目工作区约定

**将此次项目生成的文件都转移至 RichPing，以后本 Mod 项目都在此文件夹内施工。**

- **RichPing**：`K:\杀戮尖塔mod制作\RichPing\` — Mod 项目根目录，所有产出文件均在此内
- **sts2 反编译源码**：`K:\杀戮尖塔mod制作\Tools\sts2_decompiled\`（外部参考）
- **游戏目录**：`K:\SteamLibrary\steamapps\common\Slay the Spire 2\`

---

## 用户画像

| 维度 | 描述 |
|------|------|
| **经验** | 初次 vibe coding，几乎无编程基础 |
| **目标** | 制作 Slay the Spire 2 的 Mod，在 Steam 正式版上使用 |
| **工作流** | 以 SL2 源码/资源为参考，在独立 Mod 项目中开发 |
| **偏好** | 中文交流；步骤需尽量清晰，避免过度技术细节 |

---

## 提示词记录

| 时间 | 提示词 | 结果概要 |
|------|--------|----------|
| 2025-03-16 | 简单分析一下 SL2 | 识别为 Godot 4.5 + C#，梳理核心目录和架构 |
| 2025-03-16 | 帮我安装开发前置 | 安装 .NET 9 → 后改为 .NET 8，创建 VC_DEVELOPMENT_SETUP.md |
| 2025-03-16 | 你确定是.NET 9 SDK吗 | 说明官方 vs 项目需求；实测 .NET 8 可运行 |
| 2025-03-16 | 你先试运行8 | 修改 global.json 为 8，Godot 无头运行成功 |
| 2025-03-16 | 改吧，顺便把所有此次对话生成的所有文件名前缀加一个VC_ | 文档重命名为 VC_*；更新为 .NET 8 说明 |
| 2025-03-16 | 启动游戏失败…（NOneTimeInitialization.cs 无类定义） | 补全 NOneTimeInitialization 的 C# 类定义 |
| 2025-03-16 | 算了我不需要能运行这个游戏，我只需要制作mod并在steam正式发行版本上的客户端能用就行 | 创建 VC_MOD_GUIDE.md；说明 Mod 开发流程 |
| 2025-03-16 | 阅读mods，这是我现在正在用的几个mod | 分析 4 个 Mod：Heybox、DamageMeter、ModConfig、RemoveMultiplayerPlayerLimit |
| 2025-03-16 | 项目后续的开发需能适配ModConfig，实现 Ping 发送更多丰富文本 | 创建 RichPing Mod 框架：ping_messages.json、Harmony 占位、ModConfig 扩展点 |
| 2025-03-16 | 生成一个记忆文本（Markdown形式），便于每次建立新对话时承接工作流… | 创建 VC_SESSION_MEMORY.md（本文档） |
| 2025-03-16 | 选择路径1，部署反编译程序 | 下载 dnSpyEx 至 Tools/，创建 VC_DNSPY_SETUP.md |
| 2025-03-16 | 我能把整个sts2.dll反编译源码出来让你分析吗，怎么做？ | 说明 dnSpy 导出步骤；用户已导出成功 |
| 2025-03-16 | 我导出成功了，此次对话即将结束，总结工作内容目标 | 更新 VC_SESSION_MEMORY；撰写下一段工作摘要 |
| 2025-03-16 | 搜索杀戮尖塔 1/2 背景故事…生成角色梗合集 | 创建 杀戮尖塔角色背景故事与人物分析.md、杀戮尖塔角色梗与游戏梗合集.md |
| 2025-03-16 | 摄政官→储君，死灵法师→亡灵契约师；生成各角色死亡后 Ping 文本… | 创建 杀戮尖塔角色死亡Ping文本_幽默梗风格.md；RichPing 新增 dead/dead_stages |
| 2025-03-16 | 将此次对话按照 VC_SESSION_MEMORY 记录上 | 更新本文档 |
| 2025-03-16 | 文本我已生成完毕，将文本接入。优化代码结构，注释提高可阅读性… | 文本写入 ping_messages.json；代码优化、注释、角色别名 |
| 2025-03-16 | 将此次项目生成的文件都转移至RichPing，以后本mod项目都在此文件夹内施工 | 移动 杀戮尖塔*.md 至 RichPing；VC_SESSION_MEMORY 迁入 RichPing |

---

## 报错特征速查（精简）

| 特征 | 含义 | 处理方向 |
|------|------|----------|
| `can_instantiate: ... class could not be found` | C# 脚本无类定义或类名与文件名不符 | 补全类定义，保证类名与文件名一致 |
| `A compatible .NET SDK was not found` | global.json 要求的 SDK 版本与已安装不符 | 安装对应版本或将 global.json 改为已安装版本 |
| `mod_mainfest.json` | 拼写错误（应为 manifest） | 重命名为 `mod_manifest.json` |

---

## 下一段工作目标（待承接）

1. ~~分析 sts2 反编译源码~~：已完成。Ping 文本由 `FlavorSynchronizer.CreateEndTurnPingDialogueIfNecessary` 通过 `LocString("characters","{角色}.banter.{alive|dead}.endTurnPing").GetFormattedText()` 获取。
2. ~~确定 Harmony Patch 目标~~：已实现。补丁 `LocString.GetFormattedText`。
3. ~~ModConfig 集成~~：已实现，反射零依赖接入。
4. ~~多阶段 / 多角色 / 死亡 Ping~~：已实现。**下一步：Godot 导出并测试**。

---

## 项目产出文件（RichPing 内）

| 文件 | 说明 |
|------|------|
| `杀戮尖塔角色背景故事与人物分析.md` | 1/2 代角色背景、经历、性格 |
| `杀戮尖塔角色梗与游戏梗合集.md` | 角色梗、遗物梗、Boss 梗、社区文化梗 |
| `杀戮尖塔角色死亡Ping文本_幽默梗风格.md` | 6 角色死亡 Ping 文本（按幕分类） |
| `ping_messages.json` | 配置：stages、characters、dead、dead_stages |
| `RichPingMod.cs` | Mod 入口、配置加载、文本选取 |
| `HarmonyPatcher.cs` | LocString.GetFormattedText 补丁 |
| `ModConfigIntegration.cs` | ModConfig 反射接入 |
| `RichPingExternalProvider.cs` | IRichPingTextProvider 接口 |
| `VC_RICHPING_DEVELOPER_GUIDE.md` | 第三方 Mod 角色接入指南 |
| `VC_RICH_PING_README.md` | 项目说明 |
| `VC_RICH_PING_RESEARCH.md` | 调研与替代方案 |

---

## 下次对话可用的快速指令

- 「继续 RichPing」：在 RichPing 文件夹内施工
- 「我遇到了 [报错特征]」：可引用报错速查表
