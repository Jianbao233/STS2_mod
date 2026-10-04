# SpireInstrument / 尖塔乐器 —— 设计与计划

> 游戏内多乐器演奏浮窗：**等队友的时候，随手弹两下，队友能听见。**
> 依赖 `STS2-RitsuLib`。工坊条目待抢注。

---

## 1. 定位与目标场景

| 项 | 内容 |
|---|---|
| 一句话 | 一个「不懂乐器也能玩、会乐器更好玩」的多人可听演奏浮窗 |
| 核心场景 | 出完牌等队友、火堆休整、等其他人的空档（**不是战斗中**） |
| 目标玩家 | ① 完全不懂乐器的玩家（多数）② 想随手弹两句的玩家 ③ 想给队友表演的玩家 |
| 明确不做 | 回合检测 / 「轮到我了」自动收起（用户明确否决）；战斗中主动出声 |

## 2. 已锁定的设计决策

| # | 决策 | 依据 | 状态 |
|---|---|---|---|
| D1 | 依赖 RitsuLib | 用户指定；且 RitsuLib 提供 compat 双分支程序集（0.107.1 / 0.111.0） | ✅ 已落地 |
| D2 | **默认音色＝竖琴/拨弦** | 单音自然衰减：不需要踏板、不需要力度、乱按也像在弹琴；钢琴需要踏板与力度控制，新手会弹得又糊又断 | ✅ 已落地 |
| D3 | **五声音阶吸附默认开** | 不懂乐器的人按错音最劝退；吸附后任意组合都协和。这是"新手能不能玩下去"的单一最关键设计 | ✅ 已落地 |
| D4 | 三层可用性：导入 MIDI 自动演奏 → 跟弹引导（带辅助补音）→ 自由演奏 | 让第一分钟就有正反馈 | M3 |
| D5 | 浮窗形态：迷你/标准/大 + 收起为**音符小图标** + `P` 唤出 | 用户指定；迷你条就是给"等队友"场景用的 | ✅ 已落地 |
| D6 | 不抢焦点、不挡出牌（只有琴键吃点击） | 等待场景需要边等边看牌桌 | ✅ 已落地（UI 层 MouseFilter） |
| D7 | 联机：自由演奏逐音符走 **Unreliable**；自动演奏走**「发曲谱 + 统一起拍」** | 逐音符同步必然抖动；共享时钟才能齐奏。已核对 `NetTransferMode.Unreliable → ENet 通道 1`，与游戏状态同步（通道 0）隔离，不会队头阻塞 | M4 |
| D8 | 观众侧：每个队友可**单独**静音我（默认开）、演奏者头顶气泡、鼓掌反馈 | 这条决定 mod 口碑，不是附加项 | M4 |
| D9 | 曲库只用自编/公有领域曲目 | 工坊发布红线，避免版权风险 | M3 |
| D10 | **不内嵌浏览器** | 见 §6 评估结论 | ✅ 已决定 |

## 3. 技术选型与架构

### 3.1 为什么走原生 Godot UI（而不是内嵌浏览器）

用户给的逃生通道是"UI 难写就把浏览器界面内嵌进游戏"。**评估结论：不走。** 理由：

1. Godot 4 没有内置 WebView；要用第三方 GDExtension（CEF/WebView2 封装），等于给 mod 塞进 100MB+ 的浏览器运行时；
2. 工坊分发体积与审核风险陡增，移动端（STS2 有移动版与第三方启动器生态）直接不可用；
3. 引入的崩溃面、输入焦点问题、与游戏渲染线程的交互，都远超"UI 调不好看"这点成本。

**替代路径（已采用）**：
* HTML 原型只作为**视觉规范**，其设计令牌（配色、圆角、间距）已 1:1 抄进 C# 常量（`InstrumentPanel` 顶部的 `Col*`）；
* UI 由代码构建，改版成本极低（改一个方法即可重排）；
* 若后续确需美术级表现，下一步是**用 `.tscn` + 原生 Theme**（工作区规则本就要求 UI 优先复用原生零件），而不是浏览器。

### 3.2 分层

```
SpireInstrumentMod            [ModInitializer] 入口
  └─ Ui/PanelHost             CanvasLayer 挂到场景树根；P 键唤出；浮窗开启时独占键盘
      ├─ Ui/InstrumentPanel   浮窗 UI（代码构建）+ 演奏输入
      └─ Audio/InstrumentAudio 声部池（24 路）+ 采样缓存（LRU 96）+ "SFX" 总线
            ├─ Audio/ToneSynth      纯包装：PCM → AudioStreamWav
            ├─ Audio/PcmRenderer    ★纯 C#：谐波叠加 + 起音噪声 + 归一 + 尾音淡出
            └─ Audio/InstrumentLibrary  乐器定义（泛音比、包络、时长…）
  └─ Core/MusicScale          Godot 侧：两排 13 键位表
      └─ Core/ScaleMath       ★纯 C#：五声/半音映射、音名、吸附
  └─ Core/ModSettings         运行时设置（M2 接 RitsuLib 设置页持久化）
```

★ = 不引用 Godot，因此可被 `tests/` 离线自检工程直接编译并跑断言。

### 3.3 关键 API 依据（已核对 v0.111.0 反编译）

| 用途 | 依据 |
|---|---|
| mod 入口 | `MegaCrit.Sts2.Core.Modding.ModInitializerAttribute(string initializerMethod)`；无该特性时游戏会 `Harmony.PatchAll` |
| manifest 字段 | `ModManifest`：`id/name/author/description/version/hasPck/hasDll/dependencies/affectsGameplay/minGameVersion`，`ModDependency{id,minVersion}` |
| 挂载浮窗 | `Engine.GetMainLoop() as SceneTree` → `Root.CallDeferred(Node.MethodName.AddChild, host)`（无需 Harmony 补丁） |
| 音量跟随 | 游戏把 `VolumeMaster/VolumeSfx` 写入 Godot 的 `Master`/`SFX` 总线（`NGame.cs`、`NSfxVolumeSlider.cs`），故声部播放器挂 `SFX` 总线即自动跟随 |
| 网络（M4） | `INetMessage` 支持 mod 子类型（`MessageTypes.Initialize()` 调 `ReflectionHelper.GetSubtypesInMods<INetMessage>()`）；`NetTransferMode.Unreliable → 通道 1` |

## 4. 里程碑与验收

| 阶段 | 交付物 | 验收方式 |
|---|---|---|
| **M1 工具链与出声**（本轮完成） | 可编译工程、入口、浮窗骨架、6 音色合成音源、五声/半音键盘、音量/八度 | ✅ 编译 0 警告 0 错误；✅ 离线自检 637 项断言全通过；⏳ 游戏内人工确认 |
| M2 音色与手感 | 采样音源（替换/补充合成音）、延音类 note-off、RitsuLib 设置页持久化、键位自定义 | 自检扩展到采样层；游戏内盲测手感 |
| M3 曲目 | 曲库（自编/公有领域）、MIDI 导入解析、自动演奏（带伴奏）、跟弹引导 + 辅助补音 | MIDI 解析器离线自检（SMF 0/1、running status、tempo）；引导判定逻辑自检 |
| M4 联机 | 音符 Unreliable 广播、曲谱+统一拍、观众静音/气泡/鼓掌 | 双开真机联机；消息往返与丢包行为日志 |
| M5 打磨与发布 | 迷你条/移动端触屏、原生零件复用、i18n、工坊条目与审核 | 03-发布审核 checklist 全过；双分支（0.107.1 / 0.111.0）实测 |

## 5. 验收标准

### 5.1 可自动化（纳入 `build.ps1` 自检，任一失败即中断）

**音频**（每音色 × 每测试音高）
- A1 采样长度 == `Duration × MixRate`
- A2 无 `NaN`/`Inf`
- A3 峰值归一在 [0.80, 0.90]
- A4 无削波（|x| ≤ 1.0）
- A5 直流偏移 |mean| < 2e-3
- A6 包络衰减：末段 RMS < 首段 RMS × 0.5
- A7 尾音淡出：|末样本| < 0.02（无爆音）
- A8 **音高正确**：Goertzel 扫频能量峰落在期望基频 ±3% 内
- A9 渲染确定性：同参数两次渲染逐样本一致（保证缓存与联机一致性）

**音阶**
- B1 槽位数：五声 13 / 半音 25
- B2 五声每个键都落在音阶内（"怎么按都不难听"的形式化保证）
- B3 五声往返映射 `MidiToSlot(SlotToMidi(s)) == s`
- B4 半音槽位为连续半音
- B5 上排 == 下排 +12 半音
- B6 半音折叠对 MIDI 24..108 全都不越界（含旧版死循环回归）
- B7 五声吸附对 MIDI 0..127 全部落在合法键且结果为音阶音
- B8 五声音阶相邻级差只允许 2/3 半音（无小二度/三全音）

### 5.2 需人工（游戏内）

- 进游戏按 `P` 能唤出浮窗，`—` 收起为右下角音符图标，再按 `P` 或点图标能回来
- 键盘/鼠标点击都能出声，快速连弹不断音（24 声部内）
- 音量跟随游戏「音效」滑块；切后台静音
- 开/关音阶吸附能明显听出"乱按是否难听"的差别
- 拖动标题栏可移动浮窗；迷你档位适合等队友时用
- 不遮挡/不吞掉游戏本身的出牌操作（浮窗关闭时）

## 6. 风险与对策

| 风险 | 影响 | 对策 |
|---|---|---|
| **手感天花板**：Godot 音频输出延迟约 15ms + 帧输入延迟，合计 25–40ms | 到不了专业软音源（2–5ms），只能"跟手" | 已做：本地即时发声、预生成采样、对象池零分配；后续：`Input.UseAccumulatedInput=false`、采样预热点 |
| **键盘 6KRO**：普通薄膜键盘同时按 6 键丢键 | 十指和弦必炸 | 五声吸附降低所需按键数、自动伴奏、竖琴类单音色；M5 评估外接 MIDI 键盘（`winmm midiin`） |
| 采样版权 | 工坊发布红线 | D9：只用自编/公有领域；M2 采样来源须逐条记录授权 |
| 合成音色"电子味" | 影响"好听" | M2 换采样音源；架构已把音源与播放层解耦，替换不动 UI 与网络层 |
| 游戏版本分裂（正式版 0.107.1 / beta 0.111.0） | 整包被拒载 | `min_game_version=0.107.1`（填最低支持版本）；必要时用 `tools/ModVersionLoader` 双分支分发 |
| 与聊天类 mod 的 Enter 键冲突 | 演奏时误触发聊天 | 浮窗开启时独占输入（已实现 `SetInputAsHandled`）；M2 做键位自定义 |

## 7. 工程约定（衔接工作区规则）

- 工程位置：`STS2_mod/SpireInstrument/`（主仓内项目，未建独立仓）
- 作者字段：`@Bilibili我叫煎包`（manifest 已在 `build.ps1` 里做断言）
- `torelease/`＝staging（dll + manifest），`release/`＝历史发布包归档，两者均被 `.gitignore` 忽略
- `min_game_version` 一律填**最低**支持版本
- 上传工坊前必须先读 `创意工坊/AGENTS.md` → `01-上传流程.md` / `02-多语言文案.md` / `03-发布审核.md`，并回写 `工坊条目台账.md`
- 工作区结构变动需在 `工作区台账.md` 追加变更日志

## 8. 当前状态（M1 完成，已通过两层自动化验收）

```
dotnet build                 → 0 警告 0 错误（52 KB dll）
build.ps1 -SelfTestOnly      → 637 项离线断言全通过
build.ps1                    → 编译 + 自检 + torelease + 打包自检 + 本地部署（全绿）
verify-in-game.ps1           → 游戏内无头自检 PASS failures=0
```

**游戏内自检实测输出（真机、无头、自动退出）**：

```
[INFO] Found mod manifest file ...\mods\SpireInstrument\mod_manifest.json
[INFO] Loading assembly DLL ...\SpireInstrument.dll
[INFO] Calling initializer method of type SpireInstrument.SpireInstrumentMod
[SpireInstrument] v0.1.0 initialized.
[SpireInstrument] audio ready: 24 voices on bus 'SFX', mix 22050 Hz.
[SpireInstrument] panel host ready.
[SpireInstrument] [SELFTEST] host=1
[SpireInstrument] [SELFTEST] panel=True keys=13/13 insts=6 voices=3 played=3 ex=false snap=True oct=4
[SpireInstrument] [SELFTEST] RESULT=PASS failures=0
```

即已验证：mod 被游戏加载 → `[ModInitializer]` 被调用 → 浮窗挂进游戏场景树 → 面板构建无崩溃 → 琴键数(13)与音阶模式一致 → 6 个音色齐全 → **试奏产生 3 个在响声部（游戏内音频链路真的通）** → 无异常 → 干净退出（退出码 0）。

**待办**：
1. 人工验收手感与观感（进游戏按 `P`）；
2. M2：采样音源 + 延音类 note-off + RitsuLib 设置页持久化 + 键位自定义。

### 无头自检踩到的四个坑（全部已修，留档）

1. **不能用环境变量**当开关 —— PowerShell 设的 `$env:` 在游戏进程里读不到 → 改用 `%TEMP%\spireinstrument-selftest` 标记文件；
2. **不能用 `_Process` / `SceneTree.ProcessFrame` 驱动** —— 启动期会被暂停导致自检静默卡死 → 用 `SceneTreeTimer` 链；
3. **游戏启动时会轮转日志**（`godot.log` → `godot<时间戳>.log`）→ 判定必须扫"本次启动后被写过的日志文件"，只盯 `godot.log` 会假阴性；
4. **相邻两次运行的日志会同时落在扫描窗口内** → 必须按 `[SELFTEST] marker found` 切分，只分析最后一次会话，否则会把上一轮的 FAIL 混进来。

另：自检断言写反过一次（`ex` 表示"抛异常"，成功条件是 `ex=false`，却断言成 `ex=true`）—— 这正是自检存在的意义：它把"看起来跑通了"变成"逐条可判定"。

---

## 9. UI 自主迭代（本轮新增能力 + 已完成的迭代）

### 9.1 让 UI 迭代也能自动化：截图巡游

`verify-in-game.ps1 -Capture` 会**窗口化**启动游戏（无头模式没有渲染器，截不了图），
mod 内部按 `SceneTreeTimer` 链依次切档位/模式并调用 `Viewport.GetTexture().GetImage().SavePng()`，
产出 6 张真实渲染截图后自行退出，脚本再收进 `D:\A-Developing\tmp\spireinstrument-shots\`：

| 图 | 覆盖 |
|---|---|
| `01_standard_pentatonic` | 标准档 + 五声吸附（默认形态，含按下态与最近演奏） |
| `02_large_piano` | 大档 + 大钢琴 |
| `03_mini` | 迷你条（等队友随手弹两下） |
| `04_standard_chromatic` | 半音 25 键（真钢琴几何） |
| `05_standard_chip` | 芯片音 |
| `06_dock_collapsed` | 收起态（右下角音符小图标） |

这样"UI 好不好看"第一次变成**能自己看、自己改、自己复验**的闭环，不再依赖人眼转述。

### 9.2 本轮完成的 UI 迭代（每条都由截图驱动）

| # | 问题（截图所见） | 修法 |
|---|---|---|
| 1 | 琴键又高又窄，像管风琴管 | 键高改固定值（不再被 `ExpandFill` 撑满），窗口高度按内容收紧，消掉底部死空间 |
| 2 | 半音档是"深色条纹格"，不是钢琴 | 弃用 `HBoxContainer` 平铺，改普通 `Control` + 归一化坐标手动排布：白键满高、黑键 62% 高压在白键交界（`ZIndex` 置顶） |
| 3 | 13 键挤在中栏，键宽只有 ~34px | 键盘改**通栏**（占满窗口宽度），键宽升到 ~73px，接近真钢琴比例 |
| 4 | 中栏大片空白、右栏"状态"是开发笔记 | 中栏加「最近演奏」音符带；右栏改玩家向信息（音阶/音色/复音/八度）；八度加数值标签 |
| 5 | 迷你档同时显示通栏键盘（回归） | 档位切换纳入新增的 `_keyWrap` / `_keyHint` 两个区块 |
| 6 | 按下的琴键没有高亮 | `ButtonPressed` 对非 `ToggleMode` 的 Button 不改变外观 → 改用 `Modulate` 染金 |
| 7 | 收起态右下角**没有**音符图标 | 锚点定位不可靠（根 `Control` 在 `CanvasLayer` 下拿不到父尺寸，实测被算到 `-78,-78` 屏幕外）→ 改按视口尺寸绝对定位，并在 `NotificationResized` 时重排 |

### 9.3 自检在本轮抓到的真 bug（玩家会遇到）

1. **收起浮窗必崩**：`InstrumentAudio.ReleaseAll()` 边遍历 `_active` 边 `Remove` → `InvalidOperationException`（集合已修改）。
   触发条件＝有音在响时按 `—`/`✕` 收起，也就是**每次正常收起都会命中**。修法：先快照再释放。
   这个 bug 是截图巡游新增"收起态"步骤时暴露的；否则会一直潜伏到玩家手里。
2. **自检链断裂表现为卡死**：任何一步抛异常会让定时器链静默中断 → 游戏不退出 → 脚本 180 秒超时。
   现在 `Schedule` 用 try/catch 包裹：记一次失败并直接收尾，结果落成 `FAIL` 而不是挂死。
3. **无头模式假 FAIL**：无头没有渲染器，6 个截图步骤必然失败。现在检测到 `headless` 就跳过巡游。

### 9.4 当前验收状态（两条路径都跑）

```
.\verify-in-game.ps1            → 无头自检：PASS failures=0（挂载/面板/琴键/音色/试奏 + 延音）
.\verify-in-game.ps1 -Capture   → 截图巡游：PASS failures=0 + 6 张 PNG
```

---

## 10. M2：音源升级 + 延音类乐器

### 10.1 一处设计改动（需要你知情）

原计划 M2 是"**采样音源**"（换成录制的乐器采样）。实际改为 **物理建模 + 增强合成**，理由：

| | 采样音源 | 物理建模/合成（本轮采用） |
|---|---|---|
| 素材 | 需要录制或购买采样库 | **零素材** |
| 版权 | 工坊发布有授权风险（红线） | **零风险** |
| 构建 | 需 pck + 导入管线 | 离线可构建、可离线自检 |
| 听感 | 上限最高 | 明显优于纯叠加合成，但不如真采样 |
| 体积 | 每音色数 MB | 运行时生成，0 字节 |

结论：**先做到"零风险 + 明显更好听"**。音源层已隔离（`PcmRenderer` → `ToneSynth` → `InstrumentAudio`），
若后续要上真采样，只替换 `ToneSynth` 一层，UI/网络/设置全不动。

### 10.2 本轮交付

| 项 | 内容 |
|---|---|
| **音色 6 → 9** | 新增 3 件**延音类**：口风琴、管风琴、弦乐 |
| **竖琴改物理建模** | Karplus-Strong 拨弦（小数延迟保证音准 + 一阶低通阻尼 + 激励去直流），比原叠加合成更像真拨弦 |
| **延音实现** | 循环段取**整数个基频周期** → 接缝无缝；`AudioStreamWav` 设 `LoopMode=Forward` + 循环点；松键用 Tween 把 `VolumeDb` 拉到 -60dB 后 `Stop()`（Godot 的 AudioStreamPlayer 没有 ADSR，自己补） |
| **声部句柄** | `Play()` 返回 `VoiceHandle`，新增 `Release()`（淡出）/`StopNow()`（立即）/`PlayPreview()`（试听自动释放，避免延音类一直响） |
| **面板接线** | 按住记句柄、松键按音色类型分派：延音类 `Release`、一次性类自然衰减 |

### 10.3 自检新增的验收项（离线 793 项，比上轮 +156）

**延音类专用**（每音色 × 每测试音高）：
- 循环区间合法（`LoopBegin > 0`、`LoopEnd == 采样末尾`、长度 > 0.25s）
- 循环段不静音（RMS > 0.05）、起音段确实渐入
- **接缝无缝**：`|buf[LoopEnd-1] - buf[LoopBegin]|` 不超过环内相邻样本平均落差的 4 倍
  —— 循环长度只要不是整数个基频周期，这里就会报"会听到咔哒声"
- 音高（在循环段上做 Goertzel 扫频）、无 NaN、峰值归一、直流、渲染确定性

**游戏内新增**：延音真的"按住持续、松键停下"，用**基线对比**判定（避免把仍在衰减的试奏音算进来）：

```
sustain=started  voices=4 baseline=3     ← 延音音色占了一个声部
sustain-hold     voices=4 baseline=3     ← 1.2 秒后仍在响 ⇒ 循环生效
sustain-end      voices=3 baseline=3     ← 释放后回到基线 ⇒ 淡出释放生效
```

### 10.4 自检抓到的真缺陷

* **Karplus-Strong 直流偏移 2.6%–4.2%**：低通环路会把激励噪声的直流分量一直循环下去，浪费动态余量。
  修法：激励先去均值 + 输出侧 `RemoveDc()` 兜底。**这是新增物理建模时自检当场抓到的**。
* **（我自己写错的断言，非产品 bug）** 延音释放后断言"声部数 == 0"，但试奏的一次性音还在衰减 →
  改为与基线对比。留档：断言要考虑测试自身的副作用。

---

## 11. M2 收尾：设置持久化 + 原生设置页 + 键位

### 11.1 分层（为什么不是"直接写文件"那么简单）

```
SettingsPage (RitsuLib 原生设置页)     ← 游戏内「设置 → 模组」里能改
      │  ModSettingsCallbackValueBinding（读写都回调到下面）
ModSettings（门面 façade）              ← 调用点仍是 ModSettings.ScaleSnap 这样的简单属性
      │  防抖 500ms
SettingsStore（Godot I/O）              ← user://mods_settings/SpireInstrument.json
      │  System.Text.Json
SpireInstrumentSettings（纯 POCO）      ← ★不引用 Godot，可离线自检
```

三个关键决策：

1. **一份真相**：设置页与浮窗面板改的是同一份数据、同一个文件 ——
   设置页用 `ModSettingsCallbackValueBinding`（RitsuLib 官方给"自有存储"场景准备的绑定类型），
   而不是 RitsuLib 自带的数据存储，避免出现两套持久化互相覆盖。
2. **门面而非散点**：所有调用点继续写 `ModSettings.Octave = 5`，落盘时机集中在一处防抖
   （拖音量滑块会连续触发几十次写入，不防抖就是几十次磁盘写）。
3. **纯 POCO 可离线验**：`SpireInstrumentSettings` 不引用 Godot，因此默认值、越界夹取、
   JSON 往返、脏数据容错、键名归一都能在离线自检里跑（C 组）。

### 11.2 设置项

| 设置项 | 类型 | 默认 |
|---|---|---|
| 音阶吸附 | 开关 | 开（五声音阶，怎么按都不难听） |
| 音量 | 0–100 滑块 | 80 |
| 八度 | 2–6 滑块 | C4 |
| 默认音色 | 9 选 1（步进器） | 竖琴 / 拨弦 |
| 浮窗尺寸 | 迷你/标准/大 | 标准 |
| **唤出按键** | **键位捕获行** | `P` |
| 自动伴奏 / 跟弹辅助补音 | 开关 | 开（M3 用） |

键位行可能写入 `Ctrl+P` 这类组合写法，因此 `NormalizeKeyName()` 会取最后一段作为实际键名，
再由 `OS.FindKeycodeFromString` 解析成 Godot 键值（解析失败回退 `P`，绝不因为一个键名让浮窗唤不出来）。

### 11.3 验收证据

**离线（817 项，+24）**：默认值、越界夹取（八度 99→6、音量 −5→0、未知尺寸名→Standard、空键名→P）、
JSON 往返字段一致、缺字段保留默认、脏类型抛异常由存储层兜底、键名归一（`Ctrl+Shift+P`→`P`）。

**游戏内（无头自检）**：

```
[SpireInstrument] settings loaded: inst=harp snap=True oct=4 vol=80 size=Standard visible=True key=P
                                  path=.../mods_settings/SpireInstrument.json
[SpireInstrument] settings page registered (RitsuLib).
[SpireInstrument] [SELFTEST] settings save=True roundtrip=True ...
```

**跨进程持久化（我手写文件 → 重启游戏 → 读回）**：写入 `organ / snap=false / oct=5 / vol=55 / Large / K`，
重启后日志 `settings loaded: inst=organ snap=False oct=5 vol=55 size=Large visible=True key=K`，
且自检探针串显示 `keys=25/25 snap=False oct=5` —— **说明设置不只是被读出来，而是真的改变了行为**
（25 键＝半音模式、八度 5）。验证完已把文件恢复为默认值。

**落盘文件**（`%APPDATA%\SlayTheSpire2\mods_settings\SpireInstrument.json`，与 MerchantBlacklist 同目录约定）：

```json
{ "Version": 1, "InstrumentId": "harp", "ScaleSnap": true, "Octave": 4, "VolumePercent": 80,
  "PanelSize": "Standard", "PanelVisible": true, "SummonKey": "P", "Accompany": true, "Assist": true }
```

> 待人工确认：RitsuLib 设置页**注册成功**（日志确认），但页面在游戏内「设置 → 模组」里的实际排版需要人眼过一遍。

---

## 12. M3：曲库 / MIDI 导入 / 自动演奏 / 跟弹引导

### 12.1 分层（延续"纯逻辑可离线自检"的做法）

```
Songs/SongModel.cs      ★ 记谱解析 + 编曲（伴奏）+ 键盘折叠
Songs/SongLibrary.cs      6 首内置曲（全部公有领域，带 Credit 标注）
Songs/MidiFileParser.cs ★ 标准 MIDI 文件解析（格式 0/1、running status、tempo）
Songs/LearnJudge.cs     ★ 跟弹判定（±0.30s 命中 / ±0.09s Perfect / 辅助补音）
Songs/SongPlayer.cs       前视调度播放（Godot）
Songs/SongFolder.cs       user://songs/ 目录扫描与加载（Godot）
```
★ = 纯逻辑，进离线自检。

### 12.2 几个设计决定

1. **导入走固定目录而不是文件对话框**：玩家把 `.mid` 丢进 `user://songs/`（UI 上有 📂 按钮直接打开该目录），
   列表自动出现。游戏内弹 Godot 自带对话框体验差、还容易和游戏输入打架；这也与 CustomBGM Player 等 mod 的做法一致。
2. **伴奏是"每小节取旋律首音下移两个八度"**：永远协和，不需要玩家懂乐理 —— 这是"不懂乐器也能弹"的一部分。
3. **内置曲全部标注 `PublicDomain` 与 `Credit`**，并在离线自检里断言 —— 工坊发布红线（D9）落到代码里，而不是靠记忆。
4. **自适应前视调度**：Godot 不能"预约未来时刻播放"，只能在到点时立刻播。因此前视至少覆盖一帧
   （`clamp(delta × 1.25, 20ms, 150ms)`）：宁可稳定地早一点点，也不要长帧时成串迟到。
5. **判定状态放在曲子上**：`LearnJudge` 把音符标成 Hit/Auto/Missed，所以每次开始播放都要重置（`SongPlayer.Start` 会重置）。

### 12.3 验收

**离线 1969 → 1981 项**（新增 D 组）：记谱解析（含 `C-1`/`G9`/非法音名）、6 首内置曲可展开且版权标注齐全、
伴奏成对出现且低于旋律、播放序按时间有序、五声/半音折叠槽位全合法、**手工构造 MIDI 字节流**验证解析器
（基础/ running status / velocity=0 当 note-off / tempo 元事件 / 多轨合并 / 密度限流 / 损坏与截断文件不抛异常）、
跟弹判定（Perfect / Good / 超窗口 None / 连击与断连击 / 辅助补音 / Finish 收尾）。

**游戏内**（无头自检）：

```
song start: 小星星 mode=Auto notes=42 dur=28.3s imported=False
song=stopped scheduled=9 err=85.1ms worst=341.3ms fps=23
```

3 秒内排出 9 个音（104BPM 的旋律＋伴奏）⇒ 调度器生效。
`err=85ms / fps=23` 是**启动预载期**的实测值（日志里有 `Preloading 'Common' assets... 3,601ms`）；
自适应前视把平均误差从 355ms 压到 85ms，最坏值来自预载停顿。真机对局中（60fps）误差应在 ~25ms 内。

### 12.4 本轮修掉的问题

* `FileAccess` 在 Godot 与 `System.IO` 间歧义（第二次遇到，`SongFolder` 同样加别名）；
* **我自己写错的 4 条断言**：① 密集 MIDI 用例只关了 1 个音，其余未闭合所以根本没进入限流；
  ②③④ 多个 `LearnJudge` 共用同一个 `Song` 对象，而判定会把状态写回曲子 → 后续用例找不到待弹音符。
  改成每个用例一份新曲。留档：**测试要尊重被测对象的可变状态**。

### 12.5 未完成（下一轮）

* ~~跟弹轨道的视觉验证~~ → 本轮已完成（第 7 张截图 + 数值验证，见 §13.3）。
* 曲目播放的**联机同步**（发曲谱 + 统一起拍）→ 本轮已实现，见 §13。

---

## 13. M4：联机同步（本轮完成核心链路）

### 13.1 两条消息，两种传输

| 消息 | 内容 | 传输 | 体积 |
|---|---|---|---|
| `NoteMessage` | 按下/松开 + 音高 + 力度 + 序号 | **Unreliable**（ENet 通道 1） | **4 字节**（按位打包 1+7+7+16） |
| `SongSyncMessage` | 曲名 + 统一延迟起拍 + 整张曲谱 | Reliable | 每音 ~10 字节（42 音≈41 字节实测） |

设计要点：
* **音符走 Unreliable**：丢一两个音无所谓，但绝不能因为重传把后续音符堆在后面（那听起来像卡带）；
* **曲谱走 Reliable 且只发一次**：逐音符同步必然受各自延迟影响、多人合奏会"散"；
  发整张曲谱 + 约定同一个起拍时刻，各端本机合成 —— 才能真正齐奏；
* `ShouldBroadcast = true`：客机发出的消息由主机转发给所有其他客机，3 人以上房间也能互相听到；
* `ShouldBuffer = false`：实时事件进缓冲队列只会迟到。

### 13.2 联机的关键集成点（已实测）

mod 自定义的 `INetMessage` 必须被游戏的 `MessageTypes` 登记，否则**联机会完全收不到东西**。
游戏在 `MessageTypes.Initialize()` 里会调 `ReflectionHelper.GetSubtypesInMods<INetMessage>()` 扫描模组程序集，
所以这是"应该能行"，但必须实测。自检输出（游戏内）：

```
net noteBytes=4 noteRt=True songBytes=41 songRt=True registered=True
```

* `noteBytes=4` —— 按位打包生效，音符消息只占 4 字节；
* `noteRt/songRt=True` —— 用游戏自己的 `PacketWriter`/`PacketReader` 往返后字段一致；
* **`registered=True`** —— 游戏消息表确实登记了这两个 mod 消息类型。

网络服务实例会在"进房间 / 断线重连 / 退出对局"时更换，因此 `InstrumentNet.Update()` 每帧比对实例并重新注册
（否则会出现"第一局能听到、第二局听不到"这类幽灵问题）。

### 13.3 顺带修掉的真缺陷：整曲被强制居中

跟弹轨道截图暴露的：原实现一律把整曲"居中"到键盘中段，结果《小星星》（C4~A4，只跨 9 个半音）
被整整抬高一个八度，新手被推到高音区。改为 **`ChooseShift()`：放得下就不移调**，只有在超出键盘范围时才移八度
（范围比键盘还宽才退化为居中）。离线自检新增 5 条断言，游戏内数值验证：

```
learn=started notes=42 laneVisible=True slots=0..4 snapped=8
```

即《小星星》现在用 0~4 号键（13 键键盘的左半区），且轨道方块与琴键列对齐。

### 13.4 观众侧（本轮完成基础版）

DESIGN §2 D8 说"观众侧决定 mod 口碑"，本轮先落地最要紧的一半 —— **不想听就能不听**：

| 项 | 位置 | 说明 |
|---|---|---|
| 队友演奏音量 | 面板右栏 + RitsuLib 设置页 | 0–100%，只影响"别人弹给我听"的音量，与自己的音量分开 |
| 静音队友 | 面板右栏 + 设置页 | 一键全静音（此时远端音符仍会**亮键提示**，只是不出声） |
| 联机状态 | 面板右栏 | 已连接/未连接 · 收到音符数 · 发出音符数 |

实现上零新 API：远端音符播放时把力度乘以 `ModSettings.AudienceVolume`（静音时只做视觉提示）。

### 13.5 进程内 ENet 回环（本轮完成，联机链路已自动化验收）

自检此前只验证"消息类型被登记"和"序列化往返"——这两条都通过、实际收发仍可能是坏的。
本轮补上 **`Net/NetLoopbackProbe.cs`**：在同一进程里起一对真实的主机/客户端，让消息走完整条链路。

```
new NetHostGameService(PeerVersionInfo.LocalDefault()) → StartENetHost(47821, 2)
new NetClientGameService(PeerVersionInfo.LocalDefault()) → Initialize(new ENetClient(svc), PlatformType.None)
                                                         → ENetClient.ConnectToHost(2, "127.0.0.1", 47821)
```

自检实测输出：

```
loopback host=2 client=2 status=done hostConn=True clientConn=True err=''
```

即 **客户端→主机** 与 **主机→客户端** 两个方向都真发真收了（各 2 个音符，序列化→ENet→反序列化→处理器全链路）。

#### 顺带查明的一个关键机制（重要，避免以后误判）

主机的 `SendMessage(msg)` 广播**只发给 `readyForBroadcasting == true` 的 peer**，
而这个标志由**游戏大厅流程**设置。裸回环（无大厅）里它恒为 false，广播会被静默丢弃 ——
第一版探针就撞上了这个（`host=35 client=0`）。因此：

* mod 里用广播是对的（真机对局中大厅会置位该标志）；
* 回环探针里改用**定向发送** `SendMessage(msg, peerId)` 验证链路本身（该路径不受门控）；
* **想验证"主机转发其他客机"的路径需要一主机两客机**，属于双人真机实测的范畴。

### 13.6 未完成

* **双人真机端到端体感实测** —— 链路已自动化验证（§13.5），剩"真实房间听起来对不对"。
  已把步骤与观察清单写成可照做的手册：**`MULTIPLAYER_TEST.md`**（6 节：能听到 / 自动演奏同步 / 观众侧静音 /
  断线重连 / 压力稳定 / 记录表），并写明每条的**日志判据**（`net handlers registered`、
  `song broadcast`、`song start ... lead=`）与已知限制（固定 lead 未按 RTT 补偿等）。
* 演奏者头顶气泡（原生 `NSpeechBubbleVfx.Create(text, Creature, seconds, color)`）与鼓掌反馈 —— 需要玩家/生物身份映射。
* 战斗中自动静音策略（需要战斗状态判定）。

---

## 14. M5：工坊发布准备（本轮完成到"一条命令即可上传"）

按 `创意工坊/AGENTS.md` 的要求先读了 `01-上传流程.md`、`02-多语言文案.md`、`03-发布审核.md` 与三份模板，再动手。

### 14.1 已就绪

| 项 | 状态 |
|---|---|
| 工坊 workspace | `STS2_mod/_workshop_workspaces/SpireInstrument/`（按规则统一放这里，不污染源码仓） |
| `workshop.json` | 真实标题/描述、`visibility: private`（抢注阶段硬约束）、`dependencies: [3747602295]`（RitsuLib，已上线 public）、schinese 变体齐全；JSON 合法性已校验 |
| `content/` | 由 `torelease/` 同步，**逐文件 SHA-256 比对一致**（`mod_manifest.json` + `SpireInstrument.dll`） |
| `image.png` | 抢注阶段用上传器模板占位（规则 §4 允许）；真封面待做 |
| `i18n/english.md`、`i18n/schinese.md` | 完整 BBCode 文案，标签逐段对齐（规则 §4 要求） |
| `build.ps1 -StageWorkshop` | 默认路径已改为真实 workspace，全流程实测通过 |
| `mod_id.txt` | **尚未存在** —— 首次上传时由上传器写入（不可手造、不可删除） |

### 14.2 上传（需要人执行）

```powershell
cd "D:\A-Developing\repos\sts2-mod-uploader\artifacts\publish\ModUploader\release_win-x64"
.\ModUploader.exe upload -w "D:\A-Developing\main\sts2\STS2_mod\_workshop_workspaces\SpireInstrument"
```

前置（规则 §0）：Steam 前台登录且账号拥有 STS2。**首次上传会创建真实工坊条目**（private），
拿到 ID 后必须：① 记入 `创意工坊/工坊条目台账.md`；② 保留 `mod_id.txt` 永不删除。

### 14.3 尚未做（正式上线前必须完成）

* ~~`image.png` 真封面~~ → 本轮完成：由**真实界面截图**制作（`art/cover.png` → workspace `image.png`，512×512、83 KB、整块面板居中 + 金色边框 + 深色底）。图像生成 provider 均未登录，故未走 AI 生成；用真 UI 做封面反而更诚实。
* **`03-发布审核.md` 的审核报告** → 本轮完成初稿：`创意工坊/审核记录/2026-10-04_SpireInstrument_v0.1.0.md`（8 节齐全，**结论为"⚠️ 有保留 —— 抢注可执行，转 public 前必须补齐"**）。
* **切换 `visibility` 到 `public` 并 re-upload** —— 前置见审核报告 §1/§2/§6。
* ~~🔴 上线前最大阻塞：UI 文案目前是内置中文~~ → **本轮已解决**（见 §15）。仍有少量**动态/瞬时**文案未译（曲目信息行、跟弹成绩行、声部计数、部分状态提示），已在审核报告里如实标注。
* 文案里的 GitHub 仓库链接指向 `Jianbao233/STS2_mod` 主仓 —— 若后续把本 mod 拆成独立仓，需同步改文案与 `workshop.json`。

---

## 15. UI 文案本地化（本轮完成，解除上线阻塞）

### 15.1 做法：以中文原文为 key + 建好 UI 后整树翻译

```
Core/Strings.cs（★纯逻辑，可离线自检）   中文原文 → 英文 的表（40 条）+ 语言判定 + Tr/Pick
Ui/Localizer.cs（Godot 层）              注入 OS.GetLocale() + 建好 UI 后遍历控件树翻译静态文案
```

为什么这么设计：
* **改动面最小**：面板里所有中文标签保持原样，不需要逐个改成 `T("key")`；
* **覆盖最广**：一次树遍历把 Label / Button / OptionButton（含 tooltip）的静态文案全部翻掉；
* **可离线验**：语言判定与查表逻辑不依赖 Godot，进了离线自检；
* **动态文案**由生成函数显式调用 `Strings.Pick(zh, en)`（目前覆盖"状态"与"联机"两条主状态行）。

### 15.2 验收

**离线 1990 → 2023 项**（新增 E 组 33 条）：`zh_CN`/`zh-Hans`/`ZH_tw` → 中文，`en_US`/`ja`/空/null → 英文；
中文环境原样返回、英文环境查表命中、未命中回退原文（**不显示空白**）；
表规模与"面板上最显眼的一批文案必须都在表里"的覆盖度抽查。

**游戏内**（强制英文后重跑整树翻译，放在自检链最后一步以免影响中文截图）：

```
localize play='▶ Play' auto='Auto-play' status='Scale: Pentatonic snap (nothing sounds wrong)'
```

### 15.3 动态文案（本轮已补齐）

原先未译的动态拼接文案已全部补上英文模板，共 9 处：声部计数、跟弹成绩行、曲目信息行（音符/时长/音域/吸附/折叠/导入标记）、
音色与八度状态提示、队友开始演奏提示、导入失败提示、歌曲文件夹路径提示。
另外 `UpdateStatus()` 统一走查表（精确命中的消息自动翻译，无需逐条改）。

游戏内英文验收（强制英文后重读控件文案）：

```
localize play='▶ Play' auto='Auto-play'
         status='Scale: Pentatonic snap (nothing sounds wrong)'
         voices='Voices 0/24'
         song='Notes 42 · 28.3s · Range C4 ~ A4 | Snapped 8'
```

**UI 本地化至此完整**（静态 40 条表 + 动态 9 处模板 + 统一状态查表）。

---

## 16. 音色响度配平（本轮，客观可测的音质打磨）

### 16.1 问题：峰值归一 ≠ 听感一样响

`PcmRenderer.Normalize()` 一直把所有音色的**峰值**统一到 0.85。但峰值相同不代表响度相同：
拨弦峰值高、能量集中在起音（RMS 低，听着轻）；延音类持续输出（RMS 高，听着响）。

**实测（C4 的 0.02–0.30s 窗，跳过起音）**：切换音色的响度离散达 **12.5 dB**（0.6s 窗）
/ **7.7 dB**（0.3s 窗）—— 竖琴 −5.0 dB vs 管风琴 +2.6 dB，换音色会明显忽大忽小。

### 16.2 做法：按实测 RMS 给每种音色一个配平系数

* `InstrumentDef.Gain`（默认 1.0）—— 折进归一目标：`Normalize(buf, peakTarget * Gain)`，
  **峰值不会超 0.85 × Gain**，因此 Gain ≤ 1.15 时天然不削顶；
* 系数由**实测 RMS 反算**（目标 = 8 件稳态乐器的中位 RMS，上限 1.15 保峰值安全）：

  | 音色 | Gain | 音色 | Gain |
  |---|---|---|---|
  | 大钢琴 | 0.70 | 钟琴 | 1.09 |
  | 管风琴 | 0.71 | 芯片音 | 1.11 |
  | 口风琴 | 0.83 | 八音盒 | 1.03 |
  | 弦乐 | 0.96 | 马林巴 | 1.00（不动） |
  | **竖琴** | **1.00（刻意不动，见下）** | | |

### 16.3 结果与一个反直觉的发现

**稳态 8 件乐器的响度离散：7.7 dB → 0.2 dB**（几乎完全一致）。

**竖琴刻意不参与 RMS 配平**：实测给它 +1.15 增益后，RMS 不升反降（0.2254 → 0.1482）、
而峰值几乎不变 —— 说明**提升它等于把"咔哒"起音相对放大**（更刺耳）。
这是指标选错，不是代码错：**拨弦是瞬态乐器，稳态 RMS 天然低**，用稳态指标去配平它会把音色做坏。
故保留原状，测试里单独放宽到 ≤10 dB 并写明理由。

留档：**客观指标要选对；指标与听感冲突时，宁可记录差异也不要为了指标好看而做坏音色。**

### 16.4 验收

* 离线自检 **2023 → 2043 项**：新增 [A2] 组（9 音色 RMS 表 + 稳态离散 ≤2dB + 拨弦偏差 ≤10dB + 不削顶 + 不静音）；
  原有峰值断言改为"范围 [0.50,0.99]"（因 Gain 使峰值本就应有差异，精确平衡交给 [A2]）；
* 游戏内无头自检 PASS failures=0；`-StageWorkshop` 同步通过。












---

## 17. 现状与下一步（交接）

### 已完成并验收

| 里程碑 | 内容 | 验收证据 |
|---|---|---|
| M1 | 工程骨架、mod 加载、浮窗面板、无头自检 + 截图巡游闭环 | 游戏内自检 PASS |
| M2 | 9 音色（含 3 件延音类）、Karplus-Strong 物理建模拨弦、设置持久化 + RitsuLib 原生设置页 | 跨进程持久化实测；离线 817 项 |
| M3 | 6 首公有领域内置曲、MIDI 导入、自动演奏（自适应前视调度）、跟弹判定与下落轨道 | 手工构造 MIDI 字节流验证；截图 07 |
| M4 | 联机同步（音符 Unreliable 4 字节 / 曲谱 Reliable + 统一起拍）、观众侧音量与静音 | **进程内 ENet 回环双向收发** `host=2 client=2` |
| M5 | 工坊 workspace、中英文案、真封面、审核报告、UI 本地化 | 审核报告 8 节；强制英文实测 |
| 音质 | 9 音色响度配平 | 稳态离散 7.7dB → **0.2dB** |

累计：**离线 2043 项断言** · 游戏内无头自检（含 ENet 回环、英文验收、曲目调度）· **7 张真实渲染截图** · 工坊 workspace 就绪。

### 只差两步，都在人手上

1. **首次上传抢 ID**（不可逆外部动作，Agent 未擅自执行）：

   ```powershell
   cd "D:\A-Developing\repos\sts2-mod-uploader\artifacts\publish\ModUploader\release_win-x64"
   .\ModUploader.exe upload -w "D:\A-Developing\main\sts2\STS2_mod\_workshop_workspaces\SpireInstrument"
   ```

   前置：Steam 前台登录且账号拥有 STS2。拿到 ID 后：登记 `创意工坊/工坊条目台账.md`、补 tags、走审核后切 `public`。

2. **双人实测**：照 `MULTIPLAYER_TEST.md` 走 6 节，把 `[SpireInstrument]` 日志与复现步骤回传。

### 仍可做（按价值排序）

1. **演奏者头顶气泡 + 鼓掌反馈**（原生 `NSpeechBubbleVfx.Create(text, Creature, seconds, color)`）—— 需玩家/生物身份映射侦察；
2. **战斗中自动静音** —— 需战斗状态判定侦察（已知 `RunManager` 上只有 `IsInProgress`，那不是战斗状态）；
3. **联机同步按 RTT 动态补偿 lead** —— 若实测发现跨地域不齐，按 `GetStatsForPeer().PingMsec` 动态加 lead；
4. **手绘封面**替代当前"面板截图做底"的封面。

### 需要人耳确认的两点（Agent 无法判断）

* **竖琴的响度**：它作为瞬态拨弦被刻意排除在 RMS 配平之外（见 §16.3），稳态指标下比其它音色低约 7 dB。**听感上是否偏轻需要人耳定夺** —— 若偏轻，把它的 `Gain` 调到 1.10–1.15 即可（代价是起音"咔哒"更突出）。
* **演奏手感与延迟**：harness 无法采集音频，跟手程度只能由人手判断。

---

## 18. 会话续接：记住上次的演奏模式与曲目（本轮）

原先每次进游戏都回到"自由弹 + 第一首" —— 跟弹用户每次都要重设，属于体验缺口。

* 设置新增 `PlayMode`（"Free"/"Auto"/"Learn"）与 `LastSongId`；
* 面板：选曲时记 `LastSongId`，切模式时记 `PlayMode`，启动时恢复（曲目找不到就回第一首）；
* 容错：非法模式名回退 `Free`、`LastSongId` 为 null 回退空串（老存盘/手改文件都不会让面板崩）。

**验收**：离线 **2043 → 2048 项**（默认值 + 非法模式回退 + null 容错）；游戏内设置往返自检纳入这两个字段（PASS）；
**跨进程实测** —— 手写 `PlayMode=Learn / LastSongId=ode` → 重启游戏 → 文件仍为 `Learn/ode`（未被重置为 Free），
证明恢复路径读取并应用且不会冲掉设置。
