# StandardSeedHost / 标准房种子

> **状态**：STS2 主仓内项目（非独立 Git 仓）｜v0.1.0｜已可用（2026-08-17 测试通过）｜**未发布 / 未上传工坊**

在 STS2 **标准模式（Standard）多人开房**与**单机开局**的选人界面加入一个种子输入框（视觉复刻自定义模式种子框），房主可指定本局种子。

## 功能

- 房主 / 单机：输入种子后开局即用该种子——自动经过游戏原生 `SeedHelper.CanonicalizeSeed`（大写、O→0、I→1、去空白）；留空 = 随机种子。
- 客机：只读显示房主当前种子，不可编辑；房主修改实时同步（原生 `LobbySeedChangedMessage`）。
- 种子最终由房主经 `LobbyBeginRunMessage` 下发给全队，**无需全员安装本 mod**（客机不装也能正常游玩）。

## 原理（Harmony 三补丁，仅改 NCharacterSelectScreen）

| 补丁 | 作用 |
|---|---|
| `_Ready` Postfix | 代码动态构建种子框（HBoxContainer + Label + NMegaLineEdit），复用游戏字体 / 本地化键（`CUSTOM_RUN_SCREEN.SEED_LABEL` / `SEED_RANDOM_PLACEHOLDER`）；`ConditionalWeakTable` 防重复构建 |
| `SeedChanged` Prefix | 替换标准房原生 `NotImplementedException`（不改必崩）；**主机/单机不回写文本**（避免光标归零 bug），客机刷新只读显示 |
| `OnSubmenuOpened` Postfix | 刷新种子显示 + 按主机/客机锁定 `Editable`；把种子行定位到屏幕左下角 |

关键实现事实（供后续维护参考）：

- **复用依据**：自定义房种子框是 `custom_run_screen.tscn` 内联节点（`SeedContainer` → `SeedLabel` + `SeedInput`），不是独立场景；mod 不能改游戏打包 tscn，因此用代码动态构建同款节点。
- **光标 bug 根因**：Godot `LineEdit.set_text()` 无条件 `caret_column = 0`（`line_edit.cpp`）。主机每次按键回写文本会把光标拉回第一位，故主机侧不回写。
- **定位 bug 根因**：`Control.position/size` 设值器会按锚点换算 offsets（`control.cpp _compute_offsets`），锚点非默认时坐标会被算错（如 BottomLeft + `Position=(24,-64)` → 出屏）。定位使用默认锚点 + 视口绝对坐标（`PositionRow`）。
- **权限**：`StartRunLobby.SetSeed()` 仅 Host / Singleplayer 可调（Client 调用会 throw），所以客机必须锁输入。

## 安装 / 构建

```
cd K:/杀戮尖塔mod制作/STS2_mod/StandardSeedHost
powershell -ExecutionPolicy Bypass -File build.ps1
```

`build.ps1` 自动完成：`dotnet build` → 部署 `StandardSeedHost.dll` + `mod_manifest.json` 到 `K:/SteamLibrary/steamapps/common/Slay the Spire 2/mods/StandardSeedHost/` → 快照 `torelease/`。

也可手动拷贝：把 `torelease/`（或构建产物）里的 dll + manifest 放入游戏 mods 目录下的 `StandardSeedHost/` 文件夹。

依赖：.NET 9 SDK、Godot 4.5.1 Mono；编译期引用 `data_sts2_windows_x86_64/sts2.dll`（**当前 v0.111.0**，游戏更新后需用新版 sts2.dll 重新构建）。

## 联机说明

- 仅房主需要安装；客机可选（装了会多一个只读种子显示）。
- manifest `affects_gameplay=false`：不改协议、不改变原版可表达的状态（自定义房本来就能指定种子），无 desync 风险。

## 测试记录（2026-08-17）

1. **manifest JSON 损坏**：description 换行被写坏成真实 0x0A 导致解析失败、mod 未被识别 → 用 `JSON.stringify` 重建，三处（源码/部署/快照）修复。
2. **光标永远在第一位**：主机回写 `Input.Text` 触发 `set_text` 光标归零 → 主机/单机跳过回写。
3. **种子行消失**：`SetAnchorsPreset(BottomLeft)` + `Position` 被锚点换算推出屏幕 → 改用默认锚点 + 视口绝对坐标。
4. **位置**：左下角（距左 24px、距底 64px、440×52），避开人物介绍面板与返回按钮。

## 已知限制

- 读档多人房（MP_LOAD）不支持修改种子（种子固化在存档 RNG 中，改种子会破坏全队同步）。
- 自定义房 / 每日房种子行为由游戏原生逻辑负责，本 mod 不干预。
- 客机加入瞬间显示为空（占位"随机"）属正常：标准房加入时原生同步消息不带种子，房主修改后即实时显示。

## 目录结构

```
StandardSeedHost/
├── project.godot              # Godot 4.5 C# 项目配置
├── StandardSeedHost.csproj    # net9.0 + Lib.Harmony 2.3.3，引用游戏 sts2.dll
├── mod_manifest.json          # mod 清单（作者 @Bilibili我叫煎包）
├── build.ps1                  # 一键构建/部署/快照
├── README.md
├── src/
│   ├── StandardSeedHostMod.cs           # 入口：EnsureInitialized / ApplyHarmonyPatches
│   └── Patches/
│       ├── ModManagerInitPatch.cs       # 启动调度（ModManager.Initialize + 两帧兜底）
│       └── CharacterSelectSeedPatch.cs  # 功能核心（三补丁 + 定位）
├── torelease/                 # 发布快照（gitignore，不入库）
└── .godot/                    # 构建产物（gitignore，不入库）
```
