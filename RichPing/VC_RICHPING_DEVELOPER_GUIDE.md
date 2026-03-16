# RichPing 第三方 Mod 角色 Ping 文本开发指南

> 面向**其他 Mod 的角色开发者**，指导如何让自定义角色在 RichPing 中展示专属 Ping 台词，以及接口说明与运用场景。

---

## 一、概述

### 1.1 Ping 文本是什么？

在 Slay the Spire 2 联机战斗中，玩家可点击 **Ping 按钮** 向队友发送一句短文本，用于催促「该你出牌了」或调侃「我死了」。游戏默认使用本地化 key 如 `IRONCLAD.banter.alive.endTurnPing` 获取文本。**RichPing** 通过 Harmony 拦截该逻辑，改为从 JSON 配置或 **IRichPingTextProvider** 接口获取自定义文本。

### 1.2 接入方式概览

| 方式 | 说明 |
|------|------|
| **IRichPingTextProvider** | 你的 Mod 实现该接口并注册，当玩家使用**你的角色**发送 Ping 时，RichPing 会优先向你查询文本 |
| **优先级** | 外部提供者 > 角色专属 JSON > 全局 JSON > 游戏原版 |
| **无需改 RichPing 源码** | 只需引用接口 DLL 或通过反射零依赖接入 |

---

## 二、接口定义与说明

### 2.1 IRichPingTextProvider

```csharp
namespace RichPing;

public interface IRichPingTextProvider
{
    /// <summary>
    /// 该提供者支持的角色 Entry 列表。
    /// 如 "MY_ICE_MAGE"。空列表表示支持所有角色（作为兜底，慎用）。
    /// </summary>
    IReadOnlyList<string> SupportedCharacterIds { get; }

    /// <summary>
    /// 获取指定角色、楼层、存活状态下的 Ping 文本。
    /// </summary>
    /// <param name="characterId">角色 Entry，如 IRONCLAD、你的角色 Id.Entry</param>
    /// <param name="actIndex">当前幕：0=第一幕，1=第二幕，2=第三幕</param>
    /// <param name="isDead">角色是否已死亡</param>
    /// <returns>自定义文本；返回 null/空 则交给 RichPing 默认逻辑</returns>
    string GetPingText(string characterId, int actIndex, bool isDead);
}
```

### 2.2 参数与返回值说明

| 参数 | 类型 | 说明 |
|------|------|------|
| characterId | string | 角色 Entry，游戏在 `Player.Character.Id.Entry` 中定义。你的角色使用自定义 Mod 中的 `Id.Entry` |
| actIndex | int | 当前幕索引。0=第一幕，1=第二幕，2=第三幕。可用于按进度切换不同调侃/催促 |
| isDead | bool | 角色是否已死亡。存活时为催促文本，死亡后为调侃文本 |
| **返回值** | string | 要显示的文本。null 或空字符串表示「不处理」，交给下一级（JSON 或游戏默认） |

### 2.3 接口调用时机

- **调用者**：RichPing 的 `GetCustomPingText`
- **调用顺序**：按 `RegisterExternalProvider` 注册顺序依次询问
- **首次非空返回**：即采用该文本，不再询问后续提供者

---

## 三、开发步骤

### 3.1 定义你的提供者类

```csharp
using System.Collections.Generic;

namespace MyCharacterMod;

public class MyCharacterPingProvider : RichPing.IRichPingTextProvider
{
    public IReadOnlyList<string> SupportedCharacterIds => new[] { "MY_ICE_MAGE" };

    public string GetPingText(string characterId, int actIndex, bool isDead)
    {
        if (characterId != "MY_ICE_MAGE") return null;

        if (isDead)
            return "[gray]……（冰霜已逝）[/gray]";

        return actIndex switch
        {
            0 => "快点，冰霜在等你。",
            1 => "第二幕了，别冻着了~",
            2 => "[sine]最后一击……[/sine]",
            _ => "到你了。"
        };
    }
}
```

### 3.2 注册提供者

在 Mod 加载后调用 `RichPingMod.RegisterExternalProvider`。建议**延迟一帧**，以免 RichPing 尚未初始化：

```csharp
[ModInitializer("ModLoaded")]
public static class MyCharacterMod
{
    public static void ModLoaded()
    {
        var tree = (Godot.SceneTree)Godot.Engine.GetMainLoop();
        tree?.ProcessFrame += () =>
        {
            RichPing.RichPingMod.RegisterExternalProvider(new MyCharacterPingProvider());
        };
    }
}
```

### 3.3 多角色 Mod

在 `SupportedCharacterIds` 返回所有角色 ID，在 `GetPingText` 中按 `characterId` 分支：

```csharp
public IReadOnlyList<string> SupportedCharacterIds => new[] { "MY_ICE_MAGE", "MY_FIRE_KNIGHT" };

public string GetPingText(string characterId, int actIndex, bool isDead)
{
    return characterId switch
    {
        "MY_ICE_MAGE" => GetIceMagePing(actIndex, isDead),
        "MY_FIRE_KNIGHT" => GetFireKnightPing(actIndex, isDead),
        _ => null
    };
}
```

---

## 四、富文本支持

返回的字符串可包含游戏富文本标签，例如：

| 标签 | 效果 |
|------|------|
| `[sine]...[/sine]` | 晃动 |
| `[jitter]...[/jitter]` | 抖动 |
| `[red]...[/red]` | 红色 |
| `[gray]...[/gray]` | 灰色（死亡调侃常用） |

---

## 五、零依赖接入（不引用 RichPing.dll）

若不想在项目中引用 RichPing，可通过反射注册：

```csharp
var richPingMod = Type.GetType("RichPing.RichPingMod, RichPing");
var register = richPingMod?.GetMethod("RegisterExternalProvider",
    BindingFlags.Public | BindingFlags.Static);
// 创建实现 IRichPingTextProvider 的代理对象（需用 DynamicProxy 或手写适配类）
register?.Invoke(null, new object[] { yourProxyProvider });
```

更推荐**直接引用 RichPing 项目或 DLL**，实现更简单。

---

## 六、游戏内置角色 Entry 参考

| 角色 | Entry |
|------|-------|
| 铁甲战士 | IRONCLAD |
| 静默猎手 | THE_SILENT |
| 故障机器人 | DEFECT |
| 观者 | WATCHER / THE_HIEROPHANT |
| 储君 | THE_REGENT |
| 亡灵契约师 | THE_NECROBINDER |

自定义角色使用你在角色定义中设置的 `Id.Entry`。

---

## 七、文本选取优先级

1. **外部提供者**（按注册顺序）：`SupportedCharacterIds` 包含当前角色或为空，且 `GetPingText` 返回非空 → 使用
2. **角色专属**：`ping_messages.json` 中 `characters.{角色}.default/stages/dead/dead_stages`
3. **全局阶段**：`ping_messages.json` 中 `stages` / `dead_stages`
4. **全局默认**：`messages` / `dead_messages`
5. **游戏原版**：上述均无时使用游戏默认

---

## 八、常见问题

| 问题 | 建议 |
|------|------|
| 文本没显示 | 检查 `SupportedCharacterIds` 是否包含正确角色 Entry；`GetPingText` 是否返回非空 |
| 注册时机 | 使用 `SceneTree.ProcessFrame` 延迟 1–2 帧再调用 `RegisterExternalProvider` |
| 联机别人看不到我的文本 | 联机时文本由**各客户端本地**生成，对方需装 RichPing 且使用自己的配置；无法通过网络发送你的原句 |

---

## 九、联系与更新

- RichPing 仓库 / 更新说明见 Mod 发布页
- 接口以语义化版本维护，向后兼容的扩展会保留旧接口
