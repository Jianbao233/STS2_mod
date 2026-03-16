# VC 会话记忆 · 工作流承接

> 新对话时请先阅读本文，以延续开发上下文。

---

## 项目工作区约定

- **STS2_mod**：`K:\杀戮尖塔mod制作\STS2_mod\` — Mod 工作区根目录，包含 RichPing 等子项目
- **RichPing**：`K:\杀戮尖塔mod制作\STS2_mod\RichPing\` — RichPing Mod 项目根目录
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
| 2025-03-16 | 将此次项目生成的文件都转移至RichPing，以后本mod项目在此文件夹内施工 | 移动 杀戮尖塔*.md 至 RichPing；VC_SESSION_MEMORY 迁入 RichPing |
| 2025-03-16 | 依旧报错，效仿其他 mod 解决 | 采用 jiegec/STS2FirstMod：sts2.dll 复制到项目根；建 build.ps1；后因 Godot .NET 8/9 冲突改 Sts2Stubs |
| 2025-03-16 | build.ps1 报错（字符串终止符/PowerShell 编码） | 脚本中文改英文，避免编码解析错误 |
| 2025-03-16 | Godot not found | build.ps1 默认 GodotExe 改为相对路径 `..\..\Godot_v4.5.1\...` |
| 2025-03-16 | System.Runtime Version=9.0.0.0 找不到、Failed to load project assembly | Sts2Stubs 方案：net8.0、移除 sts2 引用、Sts2Stubs.cs 存根；build 用 dotnet build 替代 Godot --build-solutions |
| 2025-03-16 | Mod 能加载但 Ping 文本未变更，ModConfig 无配置项 | 1) 改用 [HarmonyPatch]+TargetMethod 让 PatchAll 发现；2) EndTurnPingPrefix 静态构造中调度 2 帧延迟 EnsureInitialized，解决 ModConfig 注册过早问题 |
| 2025-03-16 | 检索 ModConfig/教程、总结失败点 | 创建 VC_STS2_MOD_SUMMARY.md；确认 BaseLib 非必需；记录 ModConfig 两帧延迟、ModInitializer 类型需来自 sts2 |
| 2025-03-17 | 参考 DamageMeter 解包改进 | ModConfig 检测改为遍历程序集按 FullName 查找；Register 支持 4 参数；AccessTools.TypeByName 加固 Harmony TargetMethod |
| 2025-03-17 | 模组配置依旧无、要打开游戏不游玩即可配置 | 新增 **ModManagerInitPostfix**：Patch ModManager.Initialize 的 Postfix，在其完成后调度 init，保证主菜单出现前已注册 |
| 2025-03-17 | 严格按 ModConfig-STS2 原仓库 README 重写 | Type.GetType 为主、程序集遍历回退；两帧延迟；`Array.CreateInstance(_entryType)` 构造 ConfigEntry[]；**模组配置功能恢复正常** ✓ |
| 2025-03-17 | 丰富模组配置、个性化选择、禁止个别文本、各角色存活催促文本 | 扩展 ModConfig：存活/死亡分离、角色开关、排除文本(Input/Text)、角色专属；丰富 ping_messages.json 各角色存活催促文本；Pick() 过滤排除词 |
| 2025-03-17 | 你进行构筑，别忘了记忆过程 | 执行 build.ps1：dotnet build → Godot 导出 PCK → 复制到 mods/RichPing；**构筑成功** ✓ |
| 2025-03-17 | 配置分大类、逐条解释、控件列举 | 创建 VC_MODCONFIG_CONTROLS.md 控件清单；ModConfig 分 5 组（Header+Separator）；每项加 Description/Descriptions |
| 2025-03-17 | 角色不同文本单独列出是否使用、大类区分、调控目标解释、开发者指南、登记记忆 | 角色存活/死亡分别开关（char_*_alive、char_*_dead）；6 大类（全局开关、全局文本类别、选取行为、过滤、角色存活、角色死亡）；每项加「调控目标」解释；重写 VC_RICHPING_DEVELOPER_GUIDE |
| 2025-03-17 | 脱离 richping 项目，把记忆文本往父一级挪；解析控制台指令并总结文档 | VC_SESSION_MEMORY 移至 STS2_mod；创建 VC_STS2_CONSOLE_GUIDE.md |
| 2025-03-17 | 查找游戏内各类卡牌药水遗物等 ID，整合文档；战斗内外指令、多人联机 | 创建 VC_STS2_IDS_AND_COMMANDS_REFERENCE.md |
| 2025-03-17 | 罗列所有省略的大型数组（卡牌/药水/遗物/能力/附魔/强化）、附中文、多人玩家与敌方 ID | 创建 VC_STS2_FULL_ID_LISTS.md；记录完整药水(63)、附魔(21)、强化(6)及多人 NetId/CombatId 说明 |
| 2025-03-17 | 完整列表脚本爬取、官方中文汉化来源、解包提取 | **extract_sts2_ids.py** 从 Models 爬取卡(576)/药(63)/遗(289)/能(260)/附(22)/强(6)；GodotPckTool 解包 SlayTheSpire2.pck 获取 res://localization/zhs；**VC_STS2_FULL_ID_LISTS.md** 已更新完整 ID+官方翻译(约 97.8%)；**extract_localization_from_pck.md** 解包说明 |

---

## 报错特征速查（精简）

| 特征 | 含义 | 处理方向 |
|------|------|----------|
| `can_instantiate: ... class could not be found` | C# 脚本无类定义或类名与文件名不符 | 补全类定义，保证类名与文件名一致 |
| `A compatible .NET SDK was not found` | global.json 要求的 SDK 版本与已安装不符 | 安装对应版本或将 global.json 改为已安装版本 |
| `mod_mainfest.json` | 拼写错误（应为 manifest） | 重命名为 `mod_manifest.json` |
| `CS1705` / `Failed to load project assembly` / `System.Runtime Version=9.0.0.0` 找不到 | Godot 4.5.1 为 .NET 8，sts2 为 .NET 9，直接引用导致加载失败 | **Sts2Stubs 方案**：net8.0 + Sts2Stubs.cs 存根，移除 sts2 引用；Harmony 运行时反射访问游戏 |
| Ping 文本未替换 / ModConfig 无配置项 | ModLoaded 未调用；Register 时机太晚或未调用 | **ModManagerInitPostfix** 在 ModManager.Initialize 完成后调度 init；**严格按 [ModConfig-STS2](https://github.com/xhyrzldf/ModConfig-STS2) README**：Type.GetType + 程序集回退；2 帧延迟；`Array.CreateInstance(_entryType)` 构造 ConfigEntry[] |

---

## 下一段工作目标（待承接）

1. ~~分析 sts2 反编译源码~~：已完成。
2. ~~确定 Harmony Patch 目标~~：已实现，补丁 `LocString.GetFormattedText`。
3. ~~ModConfig 集成~~：已实现，反射零依赖；2 帧延迟；**ModManagerInitPostfix 保证主菜单前完成注册** ✓
4. ~~多阶段 / 多角色 / 死亡 Ping~~：已实现。
5. ~~Godot 导出~~：dotnet build + Godot --export-pack，build.ps1 一键构建。
6. ~~实机验证~~：Ping 文本替换 ✓、模组配置显示 ✓

---

## ModConfig 集成 · 反思与记忆

| 要点 | 说明 |
|------|------|
| **时机** | 必须在 ModConfig 加载后、用户打开设置前完成 Register。**ModManagerInitPostfix** 在 `ModManager.Initialize()` 返回时调度，保证早于主菜单。 |
| **官方 README 优先** | 严格按 [ModConfig-STS2](https://github.com/xhyrzldf/ModConfig-STS2) README 实现，避免过度“优化”（ALC 遍历、IsModConfigLoaded 等曾引入复杂性和潜在 bug）。 |
| **ConfigEntry[] 类型** | 反射 Invoke 时须传正确的 `ConfigEntry[]`，用 `Array.CreateInstance(_entryType, n)` 构造，不能用 `object[]`。 |
| **两帧延迟** | README 明确要求：`tree.ProcessFrame += () => { tree.ProcessFrame += () => Register(); }`，确保 ModConfig 完全就绪。 |
| **Type.GetType 回退** | `Type.GetType("ModConfig.ModConfigApi, ModConfig")` 可能因 ALC 返回 null；回退：遍历 `AppDomain.CurrentDomain.GetAssemblies()` 按 FullName 查找。 |

---

## ModConfig 可调选项（2025-03-17 重构）

| 大类 | Key | 调控目标 | 默认 |
|------|-----|----------|------|
| 全局开关 | use_custom_ping | 总开关：关=游戏原版；开=RichPing | true |
| 全局文本类别 | use_alive_ping | 存活时催促文本是否自定义 | true |
| | use_dead_ping | 死亡后调侃文本是否自定义 | true |
| 选取行为 | random_pick | 随机 vs 顺序轮转 | true |
| | use_stages | 是否按幕(0/1/2)切换 | true |
| | use_character_specific | 角色专属优先 vs 仅全局 | true |
| 过滤 | excluded_messages | 黑名单：包含即不发送 | "" |
| 角色-存活 | char_*_alive | 各角色存活时是否用专属 | true |
| 角色-死亡 | char_*_dead | 各角色死亡后是否用专属 | true |

---

## 构筑流程（build.ps1）

| 步骤 | 命令 | 输出 |
|------|------|------|
| 1 | `dotnet build -c Debug` | `.godot\mono\temp\bin\Debug\RichPing.dll` |
| 2 | `Godot --path . --export-pack "Windows Desktop" RichPing.pck --headless` | `RichPing.pck`（含 DLL、ping_messages.json、mod_manifest 等） |
| 3 | 复制到 `{游戏}\mods\RichPing\` | RichPing.dll、RichPing.pck、mod_manifest.json |

**用法**：项目根目录执行 `.\build.ps1`。需 Godot 4.5.1 Mono（PATH 或 -GodotExe）。

---

## 项目产出文件（RichPing 内）

| 文件 | 说明 |
|------|------|
| `build.ps1` | 构建脚本：dotnet build → Godot 导出 PCK → 复制到 mods/RichPing/（避免 Godot --build-solutions 的 .NET 9 依赖） |
| `RichPing.sln` | C# 解决方案，供 dotnet build 使用 |
| `杀戮尖塔角色背景故事与人物分析.md` | 1/2 代角色背景、经历、性格 |
| `杀戮尖塔角色梗与游戏梗合集.md` | 角色梗、遗物梗、Boss 梗、社区文化梗 |
| `杀戮尖塔角色死亡Ping文本_幽默梗风格.md` | 6 角色死亡 Ping 文本（按幕分类） |
| `ping_messages.json` | 配置：stages、characters（default/stages 存活催促、dead/dead_stages 死亡调侃）；各角色个性化文本 |
| `RichPingMod.cs` | Mod 入口、配置加载、文本选取 |
| `HarmonyPatcher.cs` | **ModManagerInitPostfix**（ModManager.Initialize 后调度 init）+ EndTurnPingPrefix（LocString.GetFormattedText）；AccessTools.TypeByName 加固 |
| `ModConfigIntegration.cs` | ModConfig 反射接入；6 大类；角色 char_*_alive/char_*_dead 分别开关；AddCharToggles；每项调控目标描述 |
| `RichPingExternalProvider.cs` | IRichPingTextProvider 接口 |
| `VC_RICHPING_DEVELOPER_GUIDE.md` | 第三方 Mod 角色 Ping 文本开发指南：接口 IRichPingTextProvider、参数、注册、富文本、优先级、零依赖 |
| `VC_RICH_PING_README.md` | 项目说明 |
| `VC_RICH_PING_RESEARCH.md` | 调研与替代方案 |
| `VC_MODCONFIG_CONTROLS.md` | ModConfig 控件清单（Header/Separator/Toggle/Slider/Dropdown/Input/KeyBind）与 ConfigEntry 属性 |
| `VC_STS2_MOD_SUMMARY.md` | STS2 Mod 要点、失败反思、ModConfig/BaseLib 说明 |
| `Sts2Stubs.cs` | ModInitializer/Log 编译期存根，解决 net8/net9 冲突 |

---

## 下次对话可用的快速指令

- 「继续 RichPing」：在 RichPing 文件夹内施工
- 「我遇到了 [报错特征]」：可引用报错速查表
- 「查 ID 列表」：参考 VC_STS2_FULL_ID_LISTS.md（药水/附魔/强化完整表；卡牌遗物能力见生成脚本）

---

## ID 文档与多人联机记忆

| 文档 | 内容 |
|------|------|
| VC_STS2_IDS_AND_COMMANDS_REFERENCE.md | ID 分类、target-index、指令场景、IsNetworked |
| VC_STS2_FULL_ID_LISTS.md | **完整 ID 列表**：卡(576)、药(63)、遗(289)、能(260)、附(22)、强(6) + 官方中文；**多人 NetId/CombatId** 说明；由 extract_sts2_ids.py 生成 |
| extract_sts2_ids.py | 从 Models 爬取 ID；自动合并 %APPDATA%\localization_override\zhs 或解包后的 zhs；输出 JSON + MD |
| Tools\extract_localization_from_pck.md | GodotPckTool 解包、用户覆盖、Weblate 等官方中文获取方式 |
