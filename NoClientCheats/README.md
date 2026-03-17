# No Client Cheats / 禁止客机作弊

Mod for Slay the Spire 2. Blocks client players from using dev console cheat commands (gold, relic, card, potion, etc.) in multiplayer. **Host-only install** — clients do not need this mod.

多人联机时禁止客机（非房主）使用控制台作弊指令（如 gold、relic、card、potion 等）。**仅房主需安装**，客机无需安装。

---

## Features / 功能

| 功能 | 说明 |
|------|------|
| **Block Client Cheats** | 房主启用时，客机发出的作弊指令被静默丢弃，不入队、不生效 |
| **Hide from Mod List** | 从联机 Mod 列表中移除本 Mod，客机无法检测到（参考 sts2-heybox-support） |
| **ModConfig** | 游戏内「模组配置」可开关以上两项 |

---

## Requirements / 依赖

- **Slay the Spire 2**（Steam 正式版）
- **ModConfig**（推荐，用于游戏内配置；未安装时使用默认开启）
- **Harmony**（游戏内置，无需额外安装）

---

## Installation / 安装

1. 找到游戏目录，例如：`Steam\steamapps\common\Slay the Spire 2`
2. 确保存在 `mods` 文件夹（与 `SlayTheSpire2.exe` 同级）
3. 从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载最新版 `NoClientCheats-vX.X.X.zip`
4. 解压到 `mods` 文件夹内，确保目录结构为：
   ```
   mods/
   └── NoClientCheats/
       ├── NoClientCheats.dll
       ├── NoClientCheats.pck
       └── mod_manifest.json
   ```
5. 启动游戏，仅**房主**需安装；客机无需安装本 Mod

---

## Configuration / 配置

在游戏主菜单 → **模组配置** → **禁止客机作弊** 中可调整：

| 选项 | 默认 | 说明 |
|------|------|------|
| Block Client Cheats | 开 | 禁止客机作弊指令 |
| Hide from Mod List | 开 | 从联机 Mod 列表隐藏本 Mod |

---

## Build from Source / 从源码构建

```powershell
cd NoClientCheats
.\build.ps1
```

需要：.NET 8 SDK、Godot 4.5.1 Mono。构建产物会复制到游戏 `mods\NoClientCheats\` 目录。

---

## Thanks / 致谢

- **sts2-heybox-support**（小黑盒官方支持）：屏蔽 Mod 检测实现参考
- **皮一下就很凡** @ B站（DamageMeter 作者）：Mod 开发参考

---

## License

MIT
