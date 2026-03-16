# RichPing 第三方 Mod 角色文本接入指南

本文档面向**其他 Mod 的角色开发者**，指导如何让自己的自定义角色在 RichPing 中展示专属的 Ping 台词。

---

## 一、接入方式概览

RichPing 提供 `IRichPingTextProvider` 接口。你的 Mod 实现该接口并注册后，当玩家使用**你的角色**发送 Ping 时，RichPing 会优先向你查询文本；若你返回有效文本，则使用你的；否则回退到 RichPing 的默认/配置文本。

**无需修改 RichPing 源码**，只需引用 RichPing 的接口（或通过反射零依赖接入）。

---

## 二、接口定义

```csharp
namespace RichPing;

public interface IRichPingTextProvider
{
    /// <summary>
    /// 该提供者支持的角色 Entry 列表（如 "MY_MOD_CHARACTER"）。
    /// 若为空列表，表示支持所有角色（作为兜底提供者）。
    /// </summary>
    IReadOnlyList<string> SupportedCharacterIds { get; }

    /// <summary>
    /// 获取指定角色、楼层、存活状态下的 Ping 文本。
    /// </summary>
    /// <param name="characterId">角色 Entry，如 IRONCLAD、THE_HIEROPHANT、你的角色 ID</param>
    /// <param name="actIndex">当前幕索引：0=第一幕，1=第二幕，2=第三幕</param>
    /// <param name="isDead">角色是否已死亡</param>
    /// <returns>自定义文本；若返回 null 或空字符串，交给 RichPing 默认逻辑</returns>
    string GetPingText(string characterId, int actIndex, bool isDead);
}
```

---

## 三、实现示例

### 1. 定义你的提供者类

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

### 2. 在 Mod 加载时注册

在 `ModLoaded` 或等效入口中，**在 RichPing 加载之后**调用：

```csharp
[ModInitializer("ModLoaded")]
public static class MyCharacterMod
{
    public static void ModLoaded()
    {
        RichPing.RichPingMod.RegisterExternalProvider(new MyCharacterPingProvider());
    }
}
```

**注意**：Mod 加载顺序由游戏决定。若 RichPing 比你晚加载，你的注册会在 RichPing 的 `ModLoaded` 之前执行，此时 RichPing 可能尚未准备好接收提供者。建议使用**延迟注册**（如 `SceneTree.ProcessFrame` 延迟一帧）以保证 RichPing 已初始化。

---

## 四、零依赖接入（不引用 RichPing.dll）

若你不想在项目中引用 RichPing，可通过反射实现接口并注册：

```csharp
// 1. 在运行时通过反射创建“实现了 IRichPingTextProvider 的代理”
// 2. 调用 RichPingMod.RegisterExternalProvider(provider)

var richPingMod = Type.GetType("RichPing.RichPingMod, RichPing");
var registerMethod = richPingMod?.GetMethod("RegisterExternalProvider",
    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
var providerType = Type.GetType("RichPing.IRichPingTextProvider, RichPing");
// 创建实现 providerType 的代理对象，然后：
registerMethod?.Invoke(null, new object[] { yourProxyProvider });
```

更推荐直接引用 RichPing 项目或 DLL，以简化实现。

---

## 五、游戏内置角色 Entry 参考

| 角色           | Entry            |
|----------------|------------------|
| 铁甲战士       | IRONCLAD         |
| 静默猎手       | THE_SILENT       |
| 故障机器人     | DEFECT           |
| 观者 / 主教    | WATCHER / THE_HIEROPHANT |
| 储君           | THE_REGENT       |
| 亡灵契约师     | THE_NECROBINDER  |
| （其他角色见游戏本地化 characters 表） | ... |

自定义角色使用你在角色定义中设置的 `Id.Entry`。

---

## 六、富文本支持

返回的字符串可包含游戏富文本标签，例如：

- `[sine]晃动文字[/sine]`
- `[jitter]抖动文字[/jitter]`
- `[red]红色文字[/red]`
- `[gray]灰色文字[/gray]`

具体标签以游戏文档为准。

---

## 七、优先级与回退

RichPing 选取文本的优先级：

1. **外部提供者**（按注册顺序）：若 `SupportedCharacterIds` 包含当前角色或为空，且 `GetPingText` 返回非空，则使用该文本。
2. **角色专属配置**：`ping_messages.json` 中 `characters.{角色}.stages` 或 `default`。
3. **全局阶段配置**：`ping_messages.json` 中 `stages.{actIndex}`。
4. **全局默认**：`ping_messages.json` 中 `messages`。
5. **游戏原版**：上述均无时使用游戏默认角色台词。

---

## 八、常见问题

| 问题             | 建议                                       |
|------------------|--------------------------------------------|
| 我的文本没显示   | 检查 `SupportedCharacterIds` 是否包含正确角色 Entry；`GetPingText` 是否返回非空。 |
| 注册时机不确定   | 使用 `SceneTree.ProcessFrame` 延迟 1–2 帧再调用 `RegisterExternalProvider`。      |
| 多角色 Mod       | 在 `SupportedCharacterIds` 中返回所有角色 ID；在 `GetPingText` 中按 `characterId` 分支。 |

---

## 九、联系与更新

- RichPing 仓库 / 更新说明见 Mod 发布页。
- 接口以语义化版本维护，向后兼容的扩展会保留旧接口。
