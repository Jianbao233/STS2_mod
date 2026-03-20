## 禁止客机作弊 v1.1.2

### 重要提示

> 如发现面板无法唤起，更新最新的 ModConfig（[B站 @皮一下就很凡](https://github.com/piyixiajiuhenfen/STS2_mod/releases)）

### 修复内容

- **修复初始化时机 Bug**：原版本在 `ModInitializer` 阶段加载配置时 `LocManager` 尚未初始化，导致配置加载失败。v1.1.2 采用静态构造函数 + 两帧延迟方案，三重保险确保在 `LocManager` 就绪后才初始化，完全兼容 ModConfig v0.1.5 及更新版本
- 实测验证：玩家（RTX 3060 + ModConfig v0.1.5）正常加载，三重保险全部触发

### 如何确认生效

游戏日志（`AppData\Roaming\SlayTheSpire2\logs\godot.log`）中应出现：
```
[NoClientCheats] Loaded. Block=True Hide=True Notify=True ...
[NoClientCheats] Harmony patches applied.
```
联机时客机发送作弊指令，房主机日志会显示：
```
[NoClientCheats] Blocked client cheat: 'gold 1000' from 玩家名
```
