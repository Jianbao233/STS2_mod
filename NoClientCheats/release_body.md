## 禁止客机作弊 v1.1.2 — 最终版

### 新增

- **历史面板快捷键改为按键绑定**：可在 ModConfig 设置页自由配置（默认 F6），点击按钮即可绑定任意键

### 修复内容

- 修复 `mod_manifest.json` 中 `description` 字段未转义换行符导致的 JSON 解析错误，游戏无法识别 mod
- DLL 重新从源码构建（AssemblyVersion 1.1.2.0）
- 修复初始化时机：静态构造函数 + 两帧延迟，三重保险确保 `LocManager` 就绪后才初始化

### 如何确认生效

游戏日志（`AppData\Roaming\SlayTheSpire2\logs\godot.log`）中应出现：

```
[NoClientCheats] Loaded. Block=True Hide=True Notify=True ...
[NoClientCheats] Harmony patches applied.
```

### 依赖

- ModConfig（可选，用于快捷键配置）：[下载地址](https://github.com/xhyrzldf/ModConfig-STS2)

### 致谢

- sts2-heybox-support（小黑盒官方支持）
- 皮一下就很凡 @ B站（ModConfig / DamageMeter 作者）
