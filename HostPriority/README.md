# 房主优先 / Host Priority

多人联机时，让房主在遗物猜拳、地图路径、事件选项分歧中胜出。**仅房主需安装**，客机无需安装。

## 功能

- **遗物猜拳**：多人选择同一遗物时，房主获胜
- **地图路径**：玩家投票选择不同地图节点时，采用房主的选择
- **事件选项**：事件中玩家选择不同选项时，采用房主的选择

## 安装

1. 将 `HostPriority` 文件夹放入游戏的 `mods` 目录
2. 启动游戏，在模组管理中启用
3. **仅房主**需要安装，客机无需安装
4. 本 Mod 通过 `affects_gameplay: false` 及 Harmony Patch 从联机 Mod 列表中隐藏自身，客机无需安装即可加入

## 配置

若已安装 ModConfig，可在模组设置中开关「房主优先」功能，默认启用。

## 兼容性

- 需 Harmony（通常由其他模组提供）
- 可与 NoClientCheats、RemoveMultiplayerPlayerLimit 等模组同时使用
- 建议与 ModConfig 配合使用以便配置

## 已知现象（非本 Mod 实现范围）

以下为游戏本体多人地图投票的同步/UI 行为，本 Mod 仅干预「集齐票后的随机结果」，不修改投票收集与显示逻辑：

- **主机只显示客机第一次选择**：客机后续改票后，主机画面上可能仍显示客机第一次的投票（游戏内投票同步/网络处理）。
- **主机端图标重叠**：主机多次换选时，地图上可能出现多个选择图标重叠（游戏内 NMapScreen 对本地玩家投票图标的刷新逻辑）。
- 若出现「两边都不执行随机动画、无法继续」：多为双方票未在主机侧正确收齐（例如客机改票未同步到主机），可尝试双方先确定选择后再点继续，或向游戏官方反馈多人投票同步问题。

## 技术说明

通过 Harmony Patch 修改以下逻辑：

- `RelicPickingResult.GenerateRelicFight`：Postfix 强制 `result.player = 房主`
- `MapSelectionSynchronizer.MoveToMapCoord`：仅在集齐所有票、游戏做随机决定时干预，Prefix 强制采用房主所选道路（不改变投票收集与显示）
- `EventSynchronizer.ChooseSharedEventOption`：Prefix 优先采用房主投票

所有逻辑在房主端执行，客机无需安装即可同步。
