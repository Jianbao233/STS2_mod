# RichPing Mod - 丰富 Ping 文本

在战斗回合中，玩家结束自己的回合后可使用游戏自带的 **Ping** 催促其他玩家。本 Mod 允许用更丰富的自定义文本替代默认角色台词。

---

## 项目结构

```
RichPing/
├── project.godot          # Godot 项目配置
├── mod_manifest.json      # Mod 元信息
├── RichPing.csproj        # C# 项目，需引用 sts2.dll
├── ping_messages.json     # 自定义 Ping 文本列表（可编辑）
├── RichPingMod.cs         # Mod 入口与逻辑
├── HarmonyPatcher.cs      # Harmony 补丁（需指定目标方法）
├── ModConfigIntegration.cs # ModConfig 集成占位
└── RichPing/
    └── mod_image.png      # Mod 图标（可选，需自行添加）
```

---

## 游戏内的 Ping 机制（已发现）

- 本地化 key：`{角色}.banter.alive.endTurnPing` / `{角色}.banter.dead.endTurnPing`
- 示例（铁甲战士）：`IRONCLAD.banter.alive.endTurnPing` → "快点。" / "Make haste."
- 支持富文本：`[sine]`、`[jitter]`、`[red]` 等

---

## 开发步骤

### 1. 准备环境

- Godot 4.5.1 Mono
- .NET 9 SDK（与游戏 sts2.dll 依赖一致，需单独安装）
- 从游戏 `data_sts2_windows_x86_64\` 目录复制 `sts2.dll` 到 RichPing 项目根目录

### 2. 编辑自定义消息

编辑 `ping_messages.json` 的 `messages` 数组，例如：

```json
{
  "messages": [
    "快点！",
    "到你了。",
    "[jitter]快——点——！！[/jitter]"
  ]
}
```

### 3. Harmony 补丁（已实现）

补丁目标：`MegaCrit.Sts2.Core.Localization.LocString.GetFormattedText`。

当 `LocTable=="characters"` 且 `LocEntryKey` 以 `.banter.alive.endTurnPing` 或 `.banter.dead.endTurnPing` 结尾时，返回 `ping_messages.json` 中的自定义文本。见 `HarmonyPatcher.cs`。

### 4. ModConfig 集成（可选）

参考 ModConfig 的 API，在 `ModConfigIntegration.cs` 中实现 `Register()`，使配置可在游戏设置中修改。

---

## 导出与安装

1. Godot：Build Project → 生成 `RichPing.dll`
2. Project → Export → 勾选 `mod_manifest.json`、`ping_messages.json`、`mod_image.png`（若有）→ 导出 `RichPing.pck`
3. 将 `RichPing.dll` 和 `RichPing.pck` 放入：
   ```
   <游戏目录>/mods/RichPing/
   ```

---

## 依赖

- **sts2.dll**：必需
- **ModConfig**：可选，用于在游戏内配置
- **Lib.Harmony**：已通过 NuGet 引用，用于运行时补丁
