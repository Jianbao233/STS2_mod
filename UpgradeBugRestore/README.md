# UpgradeBugRestore · 升级 Bug 恢复

恢复官方已修复的火堆升级 UI Bug：允许在休息点铁匠（Smith）升级卡牌选择面板里**一次选择多张卡牌升级**。

原版在弹出升级预览面板时禁用了卡牌网格的焦点路由，本 mod 把这条限制还原，玩家可以连续点选多张牌并一次性升级。

## 信息

| 项 | 值 |
|---|---|
| manifest id | `UpgradeBugRestore` |
| 版本 | 1.0.0 |
| Workshop ID | 3750854057 |
| 依赖 | 无（纯 DLL mod） |
| 作者 | @Bilibili我叫煎包 |

## 构建

```powershell
.\build.ps1
```

产物走 `torelease/`（staging）→ `_workshop_workspaces/UpgradeBugRestore/`（上传）。

## 技术说明

两个 Harmony Patch（`src/Patches/`）：

- `DeckUpgradeFocusPatch.cs`：还原升级预览打开时卡牌网格的焦点路由，使多选可用。
- `ModManagerInitPatch.cs`：mod 初始化挂载。

## 注意

- `mod_manifest.json` 中 `affects_gameplay: false`（纯 UI/交互修复，不改对局数值）。
- 本 mod 与官方行为相反，是**有意的还原**，仅当玩家希望恢复旧行为时使用。