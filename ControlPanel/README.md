# ControlPanel · 控制面板 Mod

F7 快捷键快速打开/隐藏调试控制面板，支持：
- **卡牌**：搜索并生成卡牌到手牌
- **药水**：按作用分类（伤害/格挡/增益/减益/治疗/能量等）快速查找并生成
- **战斗**：选择遭遇战跳转（需局内进行中）

## 用法

1. 启动游戏，进入对局或主菜单
2. 按 **F7** 打开/关闭面板
3. 在对应标签页点击项目即可执行（等同于控制台 `card`/`potion`/`fight` 命令）

**命令生效条件**（与控制台一致）：
- **卡牌** / **药水**：必须在**对局中、战斗中**使用
- **战斗**（fight）：必须**已有对局进行中**（在地图或战斗中），用于跳转到指定遭遇

## 构建

```powershell
cd ControlPanel
.\build.ps1
```

需 Godot 4.5.1 Mono、.NET 8 SDK。

## 技术说明

- 依赖 Harmony 注入 `ModManager.Initialize` 后挂载面板
- 通过反射调用 `DevConsole.ProcessCommand` 执行控制台命令
- 药水分类、卡牌子集、遭遇列表见 `PotionAndCardData.cs`

## 排查

- 若列表仍为空：查看 Godot 输出是否有 `[ControlPanel] 列表已加载: 卡牌 X, 药水 Y...`
- 若点击无反应：查看是否有 `[ControlPanel] DevConsole 未找到` 或 `RunCommand failed`
- 若显示 FAIL：多半是命令上下文不对（如未在对局中执行 card/potion）
