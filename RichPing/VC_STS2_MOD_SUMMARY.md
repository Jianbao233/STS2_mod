# STS2 Mod 制作要点 · 失败反思与总结

> 基于 ModConfig-STS2、ModTemplate-StS2、GlitchedReme 教程等资料整理。

---

## 一、参考仓库与流程

| 来源 | 用途 |
|------|------|
| [ModConfig-STS2](https://github.com/xhyrzldf/ModConfig-STS2) | 通用配置框架，零依赖反射接入 |
| [ModTemplate-StS2](https://github.com/Alchyr/ModTemplate-StS2) | 官方风格模板，需 sts2 引用 |
| [BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2) | 内容 Mod 基础库，**需 Steam 测试分支** |
| [SlayTheSpire2ModdingTutorials](https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials) | 中文教程 |

---

## 二、核心机制：ModInitializer vs PatchAll

游戏在 `ModManager.TryLoadModFromPck` 中：

1. 查找 `[ModInitializer]`：`assembly.GetTypes().Where(t => t.GetCustomAttribute<ModInitializerAttribute>() != null)`
2. **属性类型必须来自 sts2 程序集**：`GetCustomAttribute<ModInitializerAttribute>()` 的泛型在游戏代码里解析，只识别 `MegaCrit.Sts2.Core.Modding.ModInitializerAttribute, sts2`
3. 若未找到：执行 `Harmony.PatchAll(assembly)`，只处理带 `[HarmonyPatch]` 的类

**RichPing 用 Sts2Stubs 的 ModInitializer 不被识别 → 只能依赖 PatchAll 发现 `[HarmonyPatch]`**

---

## 三、ModConfig 接入要点（来自官方 README）

1. **延迟注册**：用 **两帧** 回调，否则 ModConfig 可能尚未加载
   ```csharp
   tree.ProcessFrame += () => { tree.ProcessFrame += () => ModConfigBridge.Register(); };
   ```
2. **反射零依赖**：`Type.GetType("ModConfig.ModConfigApi, ModConfig")`，不引用 DLL
3. **注册时机**：必须在 Mod 加载后尽早调用 `Register()`，不能等到首次 Ping

---

## 四、RichPing 失败点反思

| 问题 | 原因 | 处理 |
|------|------|------|
| Ping 文本未替换 | PatchAll 能发现 `[HarmonyPatch]`，但初始化太晚 | 在 patch 类**静态构造**中调度 2 帧延迟的 EnsureInitialized |
| ModConfig 无配置项 | `Register()` 仅在 `GetCustomPingText` 内通过 EnsureInitialized 调用 | 同上：加载时执行 2 帧延迟 init，不要等首次 Ping |
| 之前用 Sts2Stubs | Godot 4.5.1 为 .NET 8，sts2 为 .NET 9 | 继续用 net8.0 + Sts2Stubs，避免直接引用 sts2 |

---

## 五、BaseLib 是否更适合？

- **BaseLib**：面向角色/卡牌等内容 Mod，需 **Steam 测试分支**，对纯 Harmony  patch 型 Mod 非必需
- **RichPing**：仅需 Harmony 补丁 + 反射，**无需 BaseLib**
- **ModTemplate**：直接引用 sts2、net9.0，适合能跑正式/测试版的开发环境

---

## 六、推荐工作流

1. **目标**：Steam 正式版 → 用 net8.0 + Sts2Stubs，不引用 sts2
2. **补丁发现**：`[HarmonyPatch]` + `TargetMethod()` 反射，交给 `PatchAll`
3. **ModConfig**：加载时 2 帧延迟调用 `Register()`
4. **构建**：`dotnet build` + Godot `--export-pack`，脚本见 `build.ps1`

---

## 七、DamageMeter 参考要点（解包分析）

| 项目 | DamageMeter | RichPing 对应调整 |
|------|-------------|------------------|
| 目标框架 | .NET 9.0，直接引用 sts2 | net8.0 + Sts2Stubs（因 Godot 冲突） |
| 入口 | `[ModInitializer("Initialize")]` 被游戏识别 | 依赖 PatchAll 发现 [HarmonyPatch] |
| ModConfig 检测 | 先 `LoadedMods.Any(m => pckName=="ModConfig")` | 同：反射检查 LoadedMods |
| 类型查找 | `GetAssemblies().SelectMany(GetTypes()).FirstOrDefault(FullName)` | 同：按 FullName 遍历，替代 Type.GetType |
| 注册延迟 | **一帧** | 改为一帧 |
| Register API | 取参数最多的重载，支持 3/4 参数 | 同 |
| Harmony | 不使用，纯 Public API | 用 AccessTools.TypeByName 加固 TargetMethod |

## 八、相关链接

- ModConfig 接入：https://github.com/xhyrzldf/ModConfig-STS2  
- Harmony 文档：https://harmony.pardeike.net/articles/intro.html  
- Godot 4.x：https://docs.godotengine.org/zh-cn/4.x/  
