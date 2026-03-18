# VC 会话记忆 · 工作流承接

> 新对话时请先阅读本文，以延续开发上下文。

---

## 项目工作区约定

- **STS2_mod**：`K:\杀戮尖塔mod制作\STS2_mod\` — Mod 工作区根目录，包含 RichPing、ControlPanel 等子项目
- **RichPing**：`K:\杀戮尖塔mod制作\STS2_mod\RichPing\` — RichPing Mod 项目根目录
- **ControlPanel**：`K:\杀戮尖塔mod制作\STS2_mod\ControlPanel\` — 控制面板 Mod（F7 卡牌/药水/战斗快捷）
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
| 2025-03-17 | ControlPanel 卡牌/药水栏空、游戏内功能不实现 | 日志显示列表已加载但点击无反应；多次修复后 **最终方案**：ItemList → VBoxContainer+Button；同步加载替代 CallDeferred；版本标识 v2；运行构建.bat；**游戏内功能已实现** ✓ |
| 2025-03-17 | ControlPanel 总结反思、Plan 记录 | 见下方 ControlPanel 项目总结与反思；工作日志见 `VC_CONTROL_PANEL_WORK_LOG.md` |
| 2025-03-17 | ControlPanel 构建 7 错误修复、反思记录 | CS0136 pileRow 重复→改 removePileRow；CS0117 AutowrapModeOff→改 AutowrapMode.Off；已记入报错速查与错误反思 |
| 2025-03-17 | ControlPanel 大改：实时检测+角色分类+遗物图标+生成敌人+Power表+可调尺寸 | GameStateHelper 反射牌堆/遗物/药水；CardCharacterHelper 角色筛选；遗物图标网格左键添加/删除；药水实时显示；PowerData 表；生成敌人(fight)；尺寸 SpinBox；标题 ControlPanel+Composer 1.5 |
| 2025-03-16 | ControlPanel 10 项大改+构建错误修复 | 1)添加卡牌从 VC_STS2_FULL_IDS.json 加载；2)面板右下角拖拽改变大小；3)牌堆实时检测用 DebugOnlyGetState；4)遗物 RunState 从 CombatState/NRun 获取；5)图标用小写 ID；6)药水/事件右侧游戏内文本；7)战斗内生成敌人(SpawnEnemyHelper+CreatureCmd.Add)；8)能力从 JSON Powers 加载；9)事件场景文本；10)VC_STS2_FULL_IDS.json 复制到 mod 目录。**SpawnEnemyHelper 构建错误**：combatMgr 为 object 需 GetType() 再 GetProperty/GetMethod；modelDb.GetMethod 第二个参数不能为 int(会被当作 BindingFlags)，改用 GetMethods+FirstOrDefault 找泛型 GetById |
| 2025-03-16 | ControlPanel 10 项优化+卡顿修复 | 1)**卡牌角色归属**：CardPoolHelper 从 ModelDb.AllCardPools 反射；2)**卡顿**：延迟构建、占位、上限 200；3)**遗物**：图标下文字、稀有度缓存；4)**药水**：图标缩小、移除删除药水；5)**显示面积**：ExpandFill 自适应；6)**生成敌人**：CreateModelId 小写、Monsters 回退；7)**事件**：GetEventText options 文本；8)**百科大全**：数据爬取参考 |
| 2025-03-16 | ControlPanel 9 项修复与 UI 优化 | 1)**删除卡牌**：游戏 remove_card 仅支持 Hand/Deck，移除抽牌堆/弃牌堆选项；2)**遗物/能力**：网格 8→10 列、6 列，ExpandFill 自适应；3)**药水**：预览图标 40→28；4)**事件**：GetEventText 改用 pages.INITIAL.description + LocManager 枚举 options；5)**生成敌人**：CombatState 类型显式查找；6)**添加卡牌**：cardPreview 100x140、KeepAspectCentered；7)**能力**：单行显示、9 号字体、6 列；8)参考 STS-EUI、BaseLib-StS2 等 mod 仓库 |
| 2025-03-16 | ControlPanel 卡牌图/事件文本/UI 锐化 | 1)**卡牌缩略图**：IconLoader 使用 pool/id 路径 `packed/card_portraits/{pool}/{id}.png`，CardPoolHelper 映射角色；2)**事件文本**：LocString 失败时回退到 `extracted/localization/zhs|eng/events.json` 文件读取，BBCode  stripping；3)**EventData**：更新为与 events.json 一致的 ID（ABYSSAL_BATHS、BUGSLAYER、RELIC_TRADER 等）；4)**UI**：边框 2px/更亮、事件右侧 Panel 背景+内边距、卡牌预览 110x154、字体 12px |
| 2025-03-16 | ControlPanel 生成敌人/UI 自适应/ParasiteSpire 致谢 | 1)**生成敌人**：SpawnEnemyHelper 修复 ModelId 的 Category（MONSTER，从 ModelDb.Monsters 缓存）；2)**遗物**：网格 12 列、ScrollContainer ExpandFill；3)**药水**：右侧预览 32–48px 限制；4)**Buff**：scroll 120 最小高+ExpandFill，8 列；5)**感谢**：标题栏+mod 描述加入 ParasiteSpire 参考致谢 |
| 2025-03-17 | 禁止客机作弊 Mod 可行性分析 | 分析 ActionQueueSynchronizer 网络收发；结论：房主 Patch HandleRequestEnqueueActionMessage 可行 |
| 2025-03-17 | NoClientCheats Mod 实现与构建 | 创建 NoClientCheats 项目；Harmony Prefix + ModConfig；构建部署至 mods/NoClientCheats ✓ |
| 2025-03-17 | GitHub 发布 Mod：README + Release | 模仿 STS2 mod 仓库撰写 README.md；prepare-release.ps1 打包；VC_GITHUB_RELEASE_GUIDE.md 发布流程；记忆中补充 Release 学习与提示词 ✓ |
| 2025-03-17 | 仓库 README（vibe coding / 高中生 / AI）+ 构建打包 | 根目录 README 写明 vibe coding、中国高中生、昨天才开始、无系统编程经验、AI 协助；构建并打包 NoClientCheats；Releases 链接改为 Jianbao233/STS2_mod ✓ |
| 2026-03-18 | 自行查看 log，找到问题并解决；先本机正确加载再考虑 bat | 分析 godot.log；VC_STS2_FULL_IDS.json 移至游戏根；settings.json→settings.cfg；build.ps1 输出 JSON 到根目录；bat 增 fixdatafiles 选项 |
| 2026-03-18 | 依旧显示错误，仅 JSON 不适配，先修复本机 json | 标准化 manifest 格式；mod_mainfest→mod_manifest；视频格式：id、pck_name、无 dependencies、affects_gameplay 按类型 |
| 2026-03-18 | 查看 log 哪里出问题 | 定位 4 模组：heybox GetModNameList、NoClientCheats Harmony、DamageMeter pckName、ModConfig First()；均 DLL 代码，非 JSON |
| 2026-03-18 | 查看游戏源码 modid.json，分析问题怎么解决 | 源码无 modid.json；扫描所有 .json；检测到错误 = assemblyLoadedSuccessfully 任一 false；更新模组加载问题分析报告 |
| 2026-03-18 | NoClientCheats 更新 Harmony 目标方法并构建 | GetModNameList→GetGameplayRelevantModNameList（v0.99 更名）；ModListFilterPatch.TargetMethod 双方法 fallback |
| 2026-03-18 | 只有 NoClientCheats 报错，再次修改并构造 | 同上；manifest 需含 id，项目内 manifest 缺 id 会覆盖 mods 目录正确版本 |
| 2026-03-18 | 依旧未加载 NoClientCheats | 项目 mod_manifest.json 缺 id/has_pck/has_dll；build 复制会覆盖；修复项目 manifest 并重写 mods 目录 |
| 2026-03-18 | 作者改成煎包 / Cursor Composer 1.5，重新构造 | 更新 NoClientCheats manifest 作者字段 |
| 2026-03-18 | 三个项目作者统一、JSON 正确格式、版本号、构建打包发布 | ControlPanel/RichPing/NoClientCheats manifest 全字段+作者统一；RichPing 0.1.1、NoClientCheats 1.0.1；RichPing prepare-release.ps1；构建+打包 zip 供 GitHub Release |

---

## 数据来源约定 · 百科大全

**爬取/归类游戏数据时**，优先参考游戏内「百科大全」功能：官方已对卡牌、遗物、药水、怪物图鉴等做了细化分类。NBestiary 显示怪物；NCardLibrary 用 `c.Pool is IroncladCardPool` 等筛选卡牌。数据脚本与 Mod 可据此对齐分类。

---

## STS2 v0.99 模组 Manifest 与加载（2026-03-18）

### 官方 manifest 格式（必遵）

| 字段 | 必填 | 说明 |
|------|------|------|
| `id` | ✅ | 缺则直接拒绝加载；用于查找 {id}.dll / {id}.pck |
| `pck_name` | - | 旧版兼容，建议与 id 一致 |
| `name` `version` `author` `description` | - | 展示用 |
| `has_pck` `has_dll` | - | 布尔，不声明则默认 false，不加载资源 |
| `affects_gameplay` | - | 纯 UI/显示类 mod 建议 false |
| `dependencies` | - | 可选；视频示例未包含 |

**文件名**：`mod_manifest.json` 或 `{id}.json` 均可；游戏扫描所有 `.json`，**源码中无 modid.json 字样**。

### 「检测到错误」红字原因

`NDebugInfoLabelManager`：`hasError = ModManager.LoadedMods.Any(m => !m.assemblyLoadedSuccessfully)`。  
任一模组 `assemblyLoadedSuccessfully == false` 即显示红字，**与 manifest JSON 无关**，来自 DLL 初始化异常。

### v0.99 API 变更（模组需适配）

| 旧 API | 新 API | 影响模组 |
|--------|--------|----------|
| `ModManager.GetModNameList` | `ModManager.GetGameplayRelevantModNameList` | sts2-heybox-support、NoClientCheats ModListFilterPatch |
| `ModManifest.pckName` | 已移除，改用 `id` / `hasPck` | DamageMeter |

### 项目 manifest 与 build 关系

**重要**：`build.ps1` 会复制项目根目录的 `mod_manifest.json` 到 mods 目录。若项目内 manifest **缺少 id**，会覆盖 mods 下正确版本，导致游戏拒绝加载。三个项目 manifest 必须包含完整字段。

### GitHub Release 打包

1. `.\build.ps1` 构建并复制到 mods  
2. `.\prepare-release.ps1 -Version "x.y.z"` 打包 zip 到 `release/`  
3. RichPing / NoClientCheats 均有 prepare-release；ControlPanel 暂未单独发布

---

## 报错特征速查（精简）

**本次对话补充（2026-03-18）**：`Mod manifest ... is missing the 'id' field` → 项目 manifest 缺 id，build 会覆盖 mods；`assemblyLoadedSuccessfully` 红字「检测到错误」→ 任一模组 DLL 初始化失败，查 godot.log，v0.99 关注 GetModNameList→GetGameplayRelevantModNameList、ModManifest.pckName 已移除。

| 特征 | 含义 | 处理方向 |
|------|------|----------|
| `can_instantiate: ... class could not be found` | C# 脚本无类定义或类名与文件名不符 | 补全类定义，保证类名与文件名一致 |
| `A compatible .NET SDK was not found` | global.json 要求的 SDK 版本与已安装不符 | 安装对应版本或将 global.json 改为已安装版本 |
| `mod_mainfest.json` | 拼写错误（应为 manifest） | 重命名为 `mod_manifest.json` |
| `CS1705` / `Failed to load project assembly` / `System.Runtime Version=9.0.0.0` 找不到 | Godot 4.5.1 为 .NET 8，sts2 为 .NET 9，直接引用导致加载失败 | **Sts2Stubs 方案**：net8.0 + Sts2Stubs.cs 存根，移除 sts2 引用；Harmony 运行时反射访问游戏 |
| Ping 文本未替换 / ModConfig 无配置项 | ModLoaded 未调用；Register 时机太晚或未调用 | **ModManagerInitPostfix** 在 ModManager.Initialize 完成后调度 init；**严格按 [ModConfig-STS2](https://github.com/xhyrzldf/ModConfig-STS2) README**：Type.GetType + 程序集回退；2 帧延迟；`Array.CreateInstance(_entryType)` 构造 ConfigEntry[] |
| **CS0136** 无法在此范围中声明名为“X”的局部变量 | 同一方法内，外层与内层（if/else/循环）使用了相同变量名，即使路径互斥 | 内/外层变量改用不同名称，如 `removePileRow` vs `pileRow` |
| **CS0117** “TextServer”未包含“AutowrapModeOff” | Godot 4 API 误用：`AutowrapModeOff` 不存在 | 正确为 **`TextServer.AutowrapMode.Off`**（枚举 `AutowrapMode` 下的值 `Off`），不是 `AutowrapModeOff` |
| **CS1929** “object”不包含“GetProperty”/“GetMethod” | 反射时对 `object` 实例直接调用了 `GetProperty`/`GetMethod`（实为 `Type` 的扩展） | 先用 `obj.GetType()` 得到 Type，再 `type.GetProperty(...)` / `type.GetMethod(...)` |
| **CS1503** 参数 2: 无法从“int”转换为“BindingFlags” | `Type.GetMethod(name, 1, ...)` 中 `1` 被解析为 BindingFlags 参数 | 使用 `GetMethods(BindingFlags)...FirstOrDefault(m => m.Name=="..." && m.IsGenericMethodDefinition)` 等方式查找泛型方法 |

---

## 控制台作弊指令 · 多人联机与模组模式（2025-03-17）

| 场景 | 作弊指令是否可用 | 说明 |
|------|------------------|------|
| **模组模式**（ModManager.LoadedMods.Count > 0） | ✅ **可用** | 有 Mod 加载时，控制台会以 `shouldAllowDebugCommands=true` 初始化，所有指令（含 DebugOnly）均注册并可执行 |
| **多人联机**（真实多人，非单人/假多人） | ✅ **可用** | gold/relic/card/potion 等 IsNetworked 指令会通过 ActionQueueSynchronizer 入队同步执行，所有玩家同步生效 |
| **单人 / 假多人** | ✅ **可用** | 指令直接执行，无入队逻辑 |

**关键代码**：
- `NDevConsole._Ready`：`shouldAllowDebugCommands = OS.HasFeature("editor") \|\| TestMode.IsOn \|\| ModManager.LoadedMods.Count > 0 \|\| SaveManager.Instance.SettingsSave.FullConsole`
- `DevConsole.ProcessCommand`：真实多人且 IsNetworked 时 `ActionQueueSynchronizer.RequestEnqueue(ConsoleCmdGameAction)` 入队
- `RunManager.IsSinglePlayerOrFakeMultiplayer`：`IsInProgress && NetService.Type == NetGameType.Singleplayer`

**无 Mod 且未开 FullConsole 时**：部分 DebugOnly 指令（如 log、open、getlogs 等）不注册，但 gold/relic/potion/card 等游戏内作弊指令为默认注册，不受影响。

---

## NoClientCheats Mod · 禁止客机作弊（2025-03-17 · 2026-03-18 更新）

| 项目 | 说明 |
|------|------|
| **路径** | `K:\杀戮尖塔mod制作\STS2_mod\NoClientCheats\` |
| **功能** | 多人联机时禁止客机（非房主）使用控制台作弊指令（gold、relic、card 等） |
| **部署** | 仅房主需安装；客机无需安装 |
| **实现** | Harmony Prefix Patch `ActionQueueSynchronizer.HandleRequestEnqueueActionMessage`，当 `message.action` 为 `NetConsoleCmdGameAction` 且 cmd 在作弊列表中时跳过原方法 |
| **ModListFilterPatch** | Postfix `GetGameplayRelevantModNameList`（v0.99 原名 GetModNameList），从 Mod 列表移除 NoClientCheats 使客机不可见；TargetMethod 需优先找新名再 fallback 旧名 |
| **ModConfig** | `block_enabled` 开关，默认 true；需 ModConfig 方可配置 |
| **构建** | `运行构建.bat` 或 `.\build.ps1` → 复制到 `{游戏}\mods\NoClientCheats\` |

**v0.99 适配**：`ModManager.GetModNameList` 已更名为 `GetGameplayRelevantModNameList`，ModListFilterPatch.TargetMethod 已更新。

**manifest**：项目内 mod_manifest.json 必须含 id、has_pck、has_dll，否则 build 复制会覆盖 mods 下正确版本导致无法加载。

**网络逻辑**：客机输入作弊指令 → 发送 `RequestEnqueueActionMessage` 给房主 → 房主 `HandleRequestEnqueueActionMessage` 收到 → 若启用且为作弊 cmd 则静默丢弃，不入队、不广播 `ActionEnqueuedMessage`，客机不会执行。

**GitHub 发布**：README.md、prepare-release.ps1、VC_GITHUB_RELEASE_GUIDE.md；`.\prepare-release.ps1 -Version "1.0.1"` 生成 `release/NoClientCheats-v1.0.1.zip`。

---

## 下一段工作目标（待承接）

1. ~~分析 sts2 反编译源码~~：已完成。
2. ~~确定 Harmony Patch 目标~~：已实现，补丁 `LocString.GetFormattedText`。
3. ~~ModConfig 集成~~：已实现，反射零依赖；2 帧延迟；**ModManagerInitPostfix 保证主菜单前完成注册** ✓
4. ~~多阶段 / 多角色 / 死亡 Ping~~：已实现。
5. ~~Godot 导出~~：dotnet build + Godot --export-pack，build.ps1 一键构建。
6. ~~实机验证~~：Ping 文本替换 ✓、模组配置显示 ✓
7. ~~ControlPanel 功能扩大与 UI 改造~~：已实现（2025-03 计划，见 VC_CONTROL_PANEL_WORK_LOG.md）
8. ~~ControlPanel UI 可调宽度 + 图2 模板~~：栏目可拖拽调整；各功能统一为 列表\|预览+执行 结构
9. ~~ControlPanel 实时检测+角色筛选+遗物图标+生成敌人+Power表~~：GameStateHelper 实时牌堆/遗物/药水；CardCharacterHelper 卡牌角色；遗物按稀有度图标网格；能力表；**战斗内生成敌人**（SpawnEnemyHelper+CreatureCmd.Add，非 fight）

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

## GitHub Release 发布 Mod · 逻辑与流程（2025-03-17）

| 概念 | 说明 |
|------|------|
| **Tag** | 版本标签（如 v1.0.0），每个 Release 绑定一个 Tag |
| **Release** | 发行版页面，含版本说明和可下载附件（zip） |
| **附件 (Assets)** | 用户下载的 Mod 打包，如 NoClientCheats-v1.0.0.zip |

**流程**：1) `build.ps1` 构建 → 2) `prepare-release.ps1 -Version "1.0.0"` 打包 zip → 3) GitHub 网页或 `gh release create` 创建 Release 并上传 zip。

**文件**：`NoClientCheats/README.md`、`prepare-release.ps1`、`VC_GITHUB_RELEASE_GUIDE.md`。

---

## 提示词记录 · 发布相关

| 时间 | 提示词 | 结果概要 |
|------|--------|----------|
| 2025-03-17 | 把 mod 发布到 github，没有经验，模仿其他 mod 的 readme，实现发行版并讲述逻辑，学习发布，记入记忆 | 撰写 README.md（参考 Minty-Spire-2、StS2-Quick-Restart）；prepare-release.ps1 打包脚本；VC_GITHUB_RELEASE_GUIDE.md 发布指南；VC_SESSION_MEMORY 补充 Release 逻辑与流程 |
| 2025-03-17 | 仓库 README 表达 vibe coding、中国高中生、昨天才开始、无编程经验、AI 创建；构建打包禁止客机作弊 mod 供上传；记入记忆 | 根目录 README 中英双语说明；构建+prepare-release 生成 NoClientCheats-v1.0.0.zip 于 `NoClientCheats/release/`；Releases 指向 Jianbao233/STS2_mod |

---

## 下次对话可用的快速指令

- 「继续 RichPing」：在 RichPing 文件夹内施工
- 「我遇到了 [报错特征]」：可引用报错速查表
- 「查 ID 列表」：参考 VC_STS2_FULL_ID_LISTS.md（药水/附魔/强化完整表；卡牌遗物能力见生成脚本）
- 「ControlPanel 排查」：日志 `%APPDATA%\SlayTheSpire2\logs\godot.log`；工作日志 `VC_CONTROL_PANEL_WORK_LOG.md`
- 「发布 Mod 到 GitHub」：参考 VC_GITHUB_RELEASE_GUIDE.md；NoClientCheats/RichPing 均用 `prepare-release.ps1 -Version "x.y.z"` 打包
- 「模组加载/检测到错误」：参考 模组加载问题分析报告.md 第八、九节；manifest 需 id；红字来自 assemblyLoadedSuccessfully

---

## ControlPanel Mod · 项目总结与反思

### 实现要点

| 维度 | 要点 |
|------|------|
| **架构** | CanvasLayer + Panel 浮窗；Harmony ModManagerInitPostfix 挂载；F7InputLayer 独立于面板处理快捷键 |
| **UI 实现** | **VBoxContainer + Button** 替代 ItemList（ItemList 在 STS2 环境下不显示、不响应点击） |
| **命令执行** | 反射 DevConsole.ProcessCommand(string)；NDevConsole.Instance 未创建时用 Activator.CreateInstance(DevConsole, true) |
| **数据** | PotionAndCardData 静态类：约 80 卡牌、63 药水、30 遭遇；药水 ID 须与游戏一致（FAIRY_IN_ABOTTLE 等） |
| **加载时机** | **同步加载** 列表于 _Ready（CallDeferred 在某些环境下未执行导致列表始终为空） |
| **构建部署** | dotnet build → Godot 导出 PCK → 复制到 `{游戏}\mods\ControlPanel\`；`运行构建.bat` 一键执行 |

### 错误反思

| 问题 | 原因 | 正确做法 |
|------|------|----------|
| 列表始终为空 | 1) ItemList 在 Godot/STS2 下渲染或事件异常；2) CallDeferred 可能未执行 | 用 VBoxContainer+Button；同步加载 |
| 点击无反应 | ItemList.ItemClicked 未触发（selectable、渲染等问题） | Button.Pressed 可靠 |
| 修改后 UI 无变化 | 构建后未正确部署或游戏未重启加载新 DLL | 版本标识（v2）可验证；`运行构建.bat` 后需完全重启游戏 |
| 药水 ID 错误 | 使用 FAIRY_IN_A_BOTTLE 等，游戏实际为 FAIRY_IN_ABOTTLE | 以 VC_STS2_FULL_IDS.json 为准 |
| **CS0136** pileRow 重复声明 | 同一方法内 remove 分支与 add 分支都声明 `pileRow`，C# 不允许外层与内层同名 | 移除分支内改为 `removePileRow` |
| **CS0117** TextServer.AutowrapModeOff | Godot 4 中不存在该成员 | 使用 `TextServer.AutowrapMode.Off`（枚举值在 `AutowrapMode` 下） |

### 排查与记忆（保留）

| 要点 | 说明 |
|------|------|
| **日志路径** | `%APPDATA%\SlayTheSpire2\logs\godot.log` |
| **DevConsole** | NDevConsole.Instance 会抛 InvalidOperationException，需 catch 后 CreateInstance |
| **构建** | `运行构建.bat` 或 `.\build.ps1` |
| **版本标识** | 标题含「v2」则新版本已加载 |

---

## ID 文档与多人联机记忆

| 文档 | 内容 |
|------|------|
| VC_STS2_IDS_AND_COMMANDS_REFERENCE.md | ID 分类、target-index、指令场景、IsNetworked |
| VC_STS2_FULL_ID_LISTS.md | **完整 ID 列表**：卡(576)、药(63)、遗(289)、能(260)、附(22)、强(6) + 官方中文；**多人 NetId/CombatId** 说明；由 extract_sts2_ids.py 生成 |
| extract_sts2_ids.py | 从 Models 爬取 ID；自动合并 %APPDATA%\localization_override\zhs 或解包后的 zhs；输出 JSON + MD |
| Tools\extract_localization_from_pck.md | GodotPckTool 解包、用户覆盖、Weblate 等官方中文获取方式 |
