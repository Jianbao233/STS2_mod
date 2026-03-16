# ControlPanel · 控制面板 Mod

F7 快捷键快速打开/隐藏调试控制面板，支持：
- **卡牌**：搜索并生成卡牌到手牌
- **药水**：按作用分类（伤害/格挡/增益/减益/治疗/能量等）快速查找并生成
- **战斗**：选择遭遇战跳转（需局内进行中）

## 用法

1. 启动游戏，进入对局或主菜单
2. 按 **F7** 打开/关闭面板
3. 在对应标签页点击项目即可执行（等同于控制台 `card`/`potion`/`fight` 命令）

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
