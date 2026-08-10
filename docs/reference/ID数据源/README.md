# ID 数据源

> 本区存放游戏内容 ID 的机器可读数据与说明。**数据基于旧版反编译，仅作历史参考；游戏更新后需回到本区重新解析。**

## 文件

| 文件 | 内容 | 数据版本 |
|---|---|---|
| `VC_STS2_FULL_ID_LISTS.md` | 人类可读全表：Cards 576 / Potions 63 / Relics 289 / Powers 260 / Enchantments 22 / Afflictions 6，含官方 zhs 中文 | v0.99 反编译（2026-04 前生成） |
| `VC_STS2_FULL_IDS.json` | 同数据机器可读版（Id/ClassName/Zhs，1216 条） | 同上 |

## 更新策略

1. 游戏更新后，若发现代码引用模型 ID 不适用（如卡牌数变化、新内容缺失），运行 `Tools/extract_sts2_ids.py`（或 `.ps1`，含 zhs 合并）从当前反编译基线重新提取。
2. 更新后覆盖本区两个文件，并同步更新头部头注（数据版本 + 日期）与本 README。
3. 与 `docs/reference/VC_STS2_IDS_AND_COMMANDS_REFERENCE.md` 的历史口径差异（576 vs 584 卡牌数）以本区为准（本区是提取器直接产物）。

## 生成工具

- `Tools/extract_sts2_ids.py`
- `Tools/extract_sts2_ids.ps1`（PowerShell 版，含官方 zhs 本地化合并）
- 输入：反编译 Models 目录（当前基线 `Tools/sts.dll历史存档/sts2_decompiled_v0.110.1_20260809/`）