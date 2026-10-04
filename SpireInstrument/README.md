# SpireInstrument / 尖塔乐器

游戏内可演奏的**多乐器浮窗**。等队友出牌、火堆休整的空档，按 `P` 唤出随手弹两句 —— 联机的队友能同步听到。

- 依赖：`STS2-RitsuLib`
- 最低游戏版本：`0.107.1`（正式版与 public-beta 均可）
- 工坊条目：待抢注

## 玩法（目标形态）

| 层 | 说明 |
|---|---|
| 自由演奏 | 键盘两排 13 键（下排低八度 / 上排高八度），或鼠标/触屏点琴键 |
| 自动演奏 | 选内置曲目或导入自己的 `.mid`，一键播放；带自动伴奏 |
| 跟弹引导 | 照着下落的方块按键，漏按有「辅助补音」兜底，带命中/连击/准确率 |

**导入自己的曲子**：面板上点 📂 打开 `%APPDATA%\SlayTheSpire2\songs\`，把 `.mid` 丢进去，曲目列表里就会出现。

**不懂乐器也能玩**：默认开启「五声音阶吸附」，琴键映射到 C D E G A 两个多八度，怎么按都不会难听；需要完整半音时可在面板上一键关闭。

## 状态

**M1 已完成（工具链与出声）**：可编译工程、`[ModInitializer]` 入口、浮窗骨架（迷你/标准/大 + 音符图标收起 + `P` 唤出）、五声/半音键盘、音量与八度。

**M2 已完成（音源升级 + 延音类 + 设置）**：**9 种音色** —— 竖琴/拨弦（Karplus-Strong 物理建模）、八音盒、马林巴、大钢琴、芯片音、钟琴，以及 3 件**延音类**（口风琴 / 管风琴 / 弦乐，按住持续、松键淡出）。全部由代码在运行时合成：零素材、零版权风险、离线可构建。设置存盘并接入 **RitsuLib 原生设置页**（游戏内「设置 → 模组」可改：音阶吸附 / 音量 / 八度 / 默认音色 / 浮窗尺寸 / 唤出按键 / 伴奏 / 辅助补音）。

**M3 已完成（曲库 / 导入 / 自动演奏 / 跟弹）**：6 首内置曲（全部公有领域）+ 导入自己的 `.mid`（丢进 `%APPDATA%\SlayTheSpire2\songs\`）、自动演奏（带自动伴奏）、跟弹引导（下落方块 + 命中/连击/准确率 + 辅助补音）。

**M4 已完成（联机）**：音符走 **Unreliable**（实测 4 字节/音符，丢包不影响手感）、曲谱走 **Reliable + 统一起拍**（各端本机合成 ⇒ 齐奏）。**观众侧**已有队友演奏音量与一键静音（静音时仍亮键提示）。链路已**自动化验收**：自检在进程内起一对真实 ENet 主机/客户端，双向收发实测通过（`host=2 client=2`）。真实房间的端到端体感实测步骤见 **[MULTIPLAYER_TEST.md](MULTIPLAYER_TEST.md)**。

**M5 已就绪（工坊发布）**：工坊 workspace、中英文案、真封面、审核报告初稿全部完成（见 [DESIGN.md](DESIGN.md) §14）。**UI 文案本地化完整**（静态 40 条表 + 动态 9 处模板，游戏内强制英文实测通过）。剩余：**首次上传抢 ID**（需 Steam 前台登录）与转 `public`。

```
dotnet build                     → 0 警告 0 错误
.\build.ps1 -SelfTestOnly        → 2066 项离线断言全通过（音频/响度配平/和弦余量/音阶/设置/曲库/MIDI/判定/本地化）
.\build.ps1                      → 编译 + 自检 + torelease + 打包自检 + 本地部署
.\build.ps1 -StageWorkshop       → 额外同步到工坊 workspace 的 content/
.\verify-in-game.ps1             → 游戏内无头自检（挂载/9 音色/延音/设置/联机消息/ENet 回环/曲目调度/英文验收/压力边界/整曲收尾）
.\verify-in-game.ps1 -Capture    → 游戏内截图巡游（8 张真实渲染图，含跟弹轨道与英文界面）
.\preflight-workshop.ps1         → 工坊上传前预检（6 节 go/no-go，退出码＝失败项数）
```

设置文件：`%APPDATA%\SlayTheSpire2\mods_settings\SpireInstrument.json`

## 文档索引

| 文档 | 内容 |
|---|---|
| [DESIGN.md](DESIGN.md) | 完整设计：设计决策 D1–D9、UI 迭代记录、各里程碑实现与验收证据、§17 现状与交接 |
| [REQUIREMENTS.md](REQUIREMENTS.md) | **需求对照表**：最初需求/设计约束/锁定项逐条对照实现与证据（验收用） |
| [MULTIPLAYER_TEST.md](MULTIPLAYER_TEST.md) | 双人联机实测手册（6 节 + 日志判据 + 已知限制） |
| `preflight-workshop.ps1` | 工坊上传前预检（上传不可逆，先跑这个） |

## 已知特性与取舍（不是 bug）

* **拨弦（竖琴）稳态响度比持续音低约 11 dB**：拨弦是瞬态乐器，已顶到峰值上限 0.98，再响就必须削顶 —— 听感上拨弦靠起音被感知（见 DESIGN §16.3）；
* **和弦按发声数做线性补偿**（`1/N`）：保证不破音，代价是和弦整体略保守，可用音量滑块补回（见 DESIGN 和弦余量一节）；
* **联机曲谱用固定起拍 + 可调补偿**：跨地域若听出固定偏移，用面板"联机同步补偿"（0–500ms）自行校准；
* **SMPTE 时间基准的 MIDI** 按 480 tick/拍近似处理（真实文件极少用）。

## 构建

```powershell
# 只跑离线自检（不需启动游戏）
.\build.ps1 -SelfTestOnly

# 完整流程：编译 + 自检 + 组装 torelease + 部署到游戏 mods 目录
.\build.ps1

# 只出包不部署
.\build.ps1 -NoLocalDeploy

# 换游戏分支编译（例如正式版 0.107.1）
.\build.ps1 -Sts2DataDir "F:\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64"

# 游戏内无头自检（需先关闭游戏）
.\verify-in-game.ps1

# 游戏内截图巡游：窗口化跑一遍，产出 8 张真实渲染截图（UI 迭代用）
.\verify-in-game.ps1 -Capture
```

构建依赖（本机已就绪）：.NET SDK 9、NuGet 缓存含 `Godot.NET.Sdk 4.5.1`、
游戏程序集目录 `F:\Steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64`、
工坊 RitsuLib 目录（含 `RitsuLib.References.props`）。

## 目录

```
src/SpireInstrumentMod.cs     入口（[ModInitializer]）
src/Ui/PanelHost.cs           挂载 + 唤出键 + 输入独占
src/Ui/InstrumentPanel.cs     浮窗 UI 与演奏输入
src/Ui/Localizer.cs           建好 UI 后整树翻译（本地化）
src/Audio/InstrumentAudio.cs  声部池 + 采样缓存 + 多声部余量（SFX 总线）
src/Audio/PcmRenderer.cs      纯 C# 合成（可离线自检）
src/Audio/ToneSynth.cs        PCM → AudioStreamWav
src/Audio/InstrumentLibrary.cs 乐器定义（9 音色 + 响度配平系数）
src/Core/ScaleMath.cs         纯 C# 音阶/吸附数学（可离线自检）
src/Core/MusicScale.cs        键位表
src/Core/Strings.cs           文案表（中→英，可离线自检）
src/Core/SpireInstrumentSettings.cs 设置模型（纯 POCO，可离线自检）
src/Core/SettingsStore.cs     设置落盘（user://mods_settings/）
src/Core/ModSettings.cs       设置门面（防抖落盘）
src/Songs/SongModel.cs        记谱解析 + 编曲 + 键盘折叠（可离线自检）
src/Songs/SongLibrary.cs      6 首内置曲（公有领域）
src/Songs/MidiFileParser.cs   SMF 解析（可离线自检）
src/Songs/LearnJudge.cs       跟弹判定（可离线自检）
src/Songs/SongPlayer.cs       前视调度播放器
src/Songs/SongFolder.cs       user://songs/ 扫描与加载
src/Net/NetMessages.cs        联机消息（音符 4 字节 / 曲谱）
src/Net/InstrumentNet.cs      注册/收发/回环自检
src/Net/NetLoopbackProbe.cs   进程内 ENet 回环探针
src/Settings/SettingsPage.cs  RitsuLib 原生设置页注册
src/SelfTest.cs               游戏内自检链（20 步）
tests/SpireInstrument.Tests/  离线自检（2066 项断言）
```


## 作者

`@Bilibili我叫煎包`
