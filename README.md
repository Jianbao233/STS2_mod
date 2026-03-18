# 杀戮尖塔2 Mod 制作 | Slay the Spire 2 Mods

Mods for Slay the Spire 2. Built with **vibe coding** — I'm a Chinese high school student who started learning this approach yesterday. I haven't studied any programming language systematically; these projects are created with AI assistance (Cursor / Composer).

杀戮尖塔 2 Mod 合集。采用 **vibe coding** 方式开发——我是一名中国高中生，昨天才开始接触并学习 vibe coding，没有系统性地学过任何一门编程语言，项目均由 AI 协助完成。

---

## Projects | 子项目

| Mod / Tool | 说明 | Links |
|------------|------|-------|
| **NoClientCheats** | 多人联机禁止客机作弊；仅房主需安装 | [README](NoClientCheats/README.md) · [Releases](https://github.com/Jianbao233/STS2_mod/releases) |
| **MP_SavePlayerRemover** | 多人存档移除断线玩家工具（独立 exe，读档前使用） | [README](MP_SavePlayerRemover/README.md) · [Releases](https://github.com/Jianbao233/STS2_mod/releases) |
| **ControlPanel** | F7 控制面板：卡牌/药水/遗物/战斗快捷 | [README](ControlPanel/README.md) |
| **RichPing** | 自定义联机 Ping 文本（存活催促/死亡调侃） | [README](RichPing/VC_RICH_PING_README.md) · [Releases](https://github.com/Jianbao233/STS2_mod/releases) |

---

## Quick Install | 快速安装

1. 找到游戏目录：`Steam\steamapps\common\Slay the Spire 2`
2. 在 `mods` 文件夹内创建对应 Mod 子目录（如 `mods\NoClientCheats\`）
3. 从 [Releases](https://github.com/Jianbao233/STS2_mod/releases) 下载 Mod 的 zip，解压到 `mods` 内

---

## Build from Source | 从源码构建

每个子项目有独立的 `build.ps1`：

```powershell
cd NoClientCheats
.\build.ps1
```

需要：.NET 8 SDK、Godot 4.5.1 Mono。

---

## License

MIT
