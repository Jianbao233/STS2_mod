# BaseLib-StS2 可借鉴之处分析报告

> 资料来源：[Alchyr/BaseLib-StS2](https://github.com/Alchyr/BaseLib-StS2)（⭐ 105）  
> 分析日期：2026-03-20  
> 报告目的：为 STS2_mod 工作区（RichPing、ControlPanel、NoClientCheats、HostPriority 等项目）提供可借鉴的架构思路与实现参考

---

## 目录

1. [项目概述](#1-项目概述)
2. [目录结构总览](#2-目录结构总览)
3. [核心技术模块详解](#3-核心技术模块详解)
   - 3.1 [内容注册体系（Abstracts）](#31-内容注册体系abstracts)
   - 3.2 [配置系统（Config）](#32-配置系统config)
   - 3.3 [补丁体系（Patches）](#33-补丁体系patches)
   - 3.4 [扩展方法（Extensions）](#34-扩展方法extensions)
   - 3.5 [IL 补丁工具（Utils/Patching）](#35-il-补丁工具utilspatching)
   - 3.6 [Mod 互联互操作（ModInterop）](#36-mod-互联互操作modinterop)
   - 3.7 [日志系统（NLogWindow）](#37-日志系统nlogwindow)
4. [对工作区项目的具体借鉴点](#4-对工作区项目的具体借鉴点)
5. [总结与优先级建议](#5-总结与优先级建议)

---

## 1. 项目概述

**BaseLib-StS2** 是 Slay the Spire 2 的一个**基础库型 Mod**（类似 STS1 的 Basemod），设计目标是为其他 Mod 提供通用的基础设施，避免重复造轮子。其核心理念：

- **提供抽象基类**：自定义卡牌、角色、遗物、药水、能力等均有标准化抽象
- **统一内容注册**：通过 `ICustomModel` 接口体系，将 mod 内容自动注册到游戏数据库
- **配置系统抽象**：为所有 mod 提供统一、美观的游戏内配置 UI
- **IL 补丁工具**：降低 Harmony 高级用法（尤其是 Transpiler）的门槛
- **Mod 互操作**：定义标准跨 mod 调用协议（`[ModInterop]` 特性）

**技术栈**：.NET 9、C# 12、Godot 4.5.1 Mono SDK、Harmony（Lib.Harmony 2.x）、MegaDot

**文件组成**：约 60 个 .cs 文件 + Godot 场景（.tscn）+ 本地化 JSON + Godot 项目文件（project.godot）

---

## 2. 目录结构总览

```
BaseLib-StS2/
├── MainFile.cs                          # 入口：ModInitializer + Harmony 初始化
├── Abstracts/                            # 核心抽象类层
│   ├── ICustomModel.cs                   # 所有自定义内容的统一接口
│   ├── CustomCharacterModel.cs            # 自定义角色（最复杂）
│   ├── CustomCardModel.cs                # 自定义卡牌
│   ├── CustomRelicModel.cs               # 自定义遗物
│   ├── CustomPotionModel.cs              # 自定义药水
│   ├── CustomPowerModel.cs               # 自定义能力
│   ├── CustomPile.cs                     # 自定义牌堆
│   ├── CustomCardPoolModel.cs            # 自定义卡池
│   ├── CustomRelicPoolModel.cs           # 自定义遗物池
│   ├── CustomEnergyCounter.cs             # 自定义能量图标
│   ├── IHealAmountModifier.cs           # 治疗量修改接口
│   └── PlaceholderCharacterModel.cs     # 占位角色模型
├── Config/                               # 配置系统
│   ├── ModConfig.cs                      # 抽象基类（序列化/反序列化/UI 创建）
│   ├── SimpleModConfig.cs               # 通用简单实现（auto-generate UI）
│   ├── BaseLibConfig.cs                 # BaseLib 自身配置（示例）
│   ├── ConfigAttributes.cs              # 配置属性（Section/SliderRange/HoverTip…）
│   ├── ModConfigRegistry.cs             # 全局配置注册表
│   └── UI/                              # Godot UI 控件层
│       ├── NConfigTickbox.cs
│       ├── NConfigSlider.cs
│       ├── NConfigDropdown.cs
│       ├── NConfigOptionRow.cs
│       └── NModConfigPopup.cs
├── Patches/                             # Harmony 补丁
│   ├── Compatibility/                   # 兼容性补丁
│   │   ├── MissingLocPatch.cs           # 缺失本地化兜底
│   │   └── UnknownCharacterPatches.cs   # 未知角色兜底
│   ├── Content/                         # 内容注册补丁
│   │   ├── ContentPatches.cs            # 自定义内容注册核心
│   │   ├── CustomEnums.cs              # 自定义枚举值生成
│   │   ├── CustomPilePatches.cs       # 自定义牌堆（最复杂，16KB）
│   │   ├── AddAncientDialogues.cs     # 古遗物对话
│   │   ├── CustomAnimationPatch.cs     # 自定义动画
│   │   └── StarterUpgradePatches.cs   # 初始升级
│   ├── Features/                       # 功能特性补丁
│   │   ├── ExhaustivePatch.cs          # 穷举变量
│   │   ├── LogPatch.cs                 # 日志功能
│   │   ├── PersistPatch.cs            # 持久变量
│   │   ├── RefundPatch.cs             # 退款变量
│   │   ├── SelfApplyDebuffPatch.cs    # 自我debuff
│   │   └── ModInteropPatch.cs        # Mod互操作（核心，16KB）
│   ├── Hooks/                          # Hook补丁
│   │   └── ModifyHealAmountPatches.cs  # 治疗量修改hook
│   └── UI/                            # UI相关补丁
│       ├── CustomCompendiumPatch.cs    # 百科大全
│       ├── CustomEnergyIconPatches.cs # 能量图标
│       ├── ExtraTooltips.cs           # 额外悬浮提示
│       └── AutoKeywordText.cs         # 自动关键词文本
├── Extensions/                          # C# 扩展方法
│   ├── DynamicVarExtensions.cs         # 动态变量（核心！）
│   ├── ControlExtensions.cs            # Godot Control 扩展
│   ├── FloatExtensions.cs              # 浮点扩展
│   ├── HarmonyExtensions.cs           # Harmony 扩展
│   ├── IEnumerableExtensions.cs        # 集合扩展
│   └── TypeExtensions.cs              # 类型扩展
├── Utils/                               # 工具类
│   ├── Patching/                       # IL 补丁工具
│   │   ├── InstructionPatcher.cs       # 指令匹配/插入（核心）
│   │   ├── InstructionMatcher.cs      # 指令匹配器（链式API）
│   │   └── IMatcher.cs                # 匹配器接口
│   ├── ModInterop/                    # Mod互操作
│   │   ├── ModInteropAttributes.cs    # [ModInterop] 等特性
│   │   └── InteropClassWrapper.cs    # 包装器基类
│   ├── GodotUtils.cs                  # Godot工具
│   ├── AncientDialogueUtil.cs         # 古遗物对话工具
│   ├── WeightedList.cs                # 加权随机列表
│   └── GeneratedNodePool.cs           # 节点池
├── BaseLib/                            # Godot 资源（随mod发布）
│   ├── scenes/LogWindow.tscn           # 日志窗口场景
│   └── localization/eng/              # 英文本地化
│       ├── settings_ui.json
│       └── static_hover_tips.json
├── BaseLibScenes/                      # Godot脚本（C#）
│   └── NLogWindow.cs                   # 日志窗口控件
├── BaseLib.csproj                      # 项目文件
├── project.godot                       # Godot项目文件
├── Notes.txt                          # 大量技术笔记（CardPileCmd.Add 等）
└── README.md
```

---

## 3. 核心技术模块详解

### 3.1 内容注册体系（Abstracts）

BaseLib 建立了完整的内容抽象层次，所有自定义内容实现对应的抽象类/接口后，自动注册到游戏数据库。

#### ICustomModel —— 所有自定义内容的根接口

```csharp
public interface ICustomModel { }
```

极为简单，但作为**标记接口**，让补丁能统一识别所有自定义内容。

#### CustomCharacterModel —— 最复杂的抽象类

这是 BaseLib 中体积最大（17.5KB）、最核心的类，体现了完整的角色定制体系：

```csharp
public abstract class CustomCharacterModel : CharacterModel, ICustomModel {
    public virtual string? CustomVisualPath => null;           // 角色立绘路径
    public virtual string? CustomTrailPath => null;          // 拖尾效果
    public virtual string? CustomIconTexturePath => null;     // 小图标
    public virtual string? CustomIconPath => null;            // 大图标
    public virtual CustomEnergyCounter? CustomEnergyCounter => null;
    public virtual string? CustomEnergyCounterPath => null;
    public virtual string? CustomRestSiteAnimPath => null;   // 休息站动画
    public virtual string? CustomMerchantAnimPath => null;    // 商店动画
    public virtual string? CustomAttackSfx => null;           // 攻击音效
    public virtual string? CustomDeathSfx => null;            // 死亡音效

    // 角色选择界面
    public virtual string? CustomCharacterSelectBg => null;
    public virtual string? CustomCharacterSelectIconPath => null;

    // 默认值
    public override int StartingGold => 99;
    public override float AttackAnimDelay => 0.15f;

    public virtual NCreatureVisuals? CreateCustomVisuals() { ... }
    public virtual CreatureAnimator? SetupCustomAnimationStates(...) { ... }

    // 工具方法：批量配置 Spine 动画状态机
    public static CreatureAnimator SetupAnimationState(
        MegaSprite controller,
        string idleName,
        string? deadName = null, bool deadLoop = false,
        string? hitName = null, bool hitLoop = false,
        string? attackName = null, bool attackLoop = false,
        ...
    ) { ... }
}
```

**关键设计**：

- **虚属性覆盖**：每种资源（视觉、图标、动画、声音）都是 `virtual`，子类按需覆盖
- **SetupAnimationState 工厂方法**：用 fluent API 配置 Spine 动画状态机，统一管理 Idle/Dead/Hit/Attack/Cast/Relaxed 六个状态
- **自定义能量计数器**：`CustomEnergyCounter` 结构含 `LayerImagePath(int layer)` 委托 + 颜色配置，支持完全自定义能量显示
- **内嵌 Harmony Patch**：同一个文件里包含了 `EnergyCounterOutlineColorPatch`、`EnergyCounterPatch`（替换能量图标资源）、`ModelDbCustomCharacters`（注册角色到数据库）等多个补丁类

#### CustomCardModel

```csharp
public abstract class CustomCardModel : CardModel, ICustomModel {
    public CustomCardModel(int baseCost, CardType type, CardRarity rarity,
                           TargetType target, bool showInCardLibrary = true,
                           bool autoAdd = true) : base(...) {
        if (autoAdd) CustomContentDictionary.AddModel(GetType());
    }

    public virtual Texture2D? CustomFrame => null;      // 自定义卡牌边框
    public virtual string? CustomPortraitPath => null; // 自定义卡牌立绘
}
```

自动注册 + 虚属性定制框架简洁有效。

#### CustomPile —— 自定义牌堆

这是最有趣的抽象之一，允许 mod 创建全新的牌堆类型：

```csharp
public abstract class CustomPile : CardPile {
    public CustomPile(PileType pileType) : base(pileType) { }

    // 决定某张卡是否应显示在此堆中
    public abstract bool CardShouldBeVisible(CardModel card);

    // 目标位置（用于动画）
    public abstract Vector2 GetTargetPosition(CardModel model, Vector2 size);

    // 可选：自定义卡牌节点
    public virtual NCard? GetNCard(CardModel card) => null;

    // 可选：自定义移动动画
    public virtual bool CustomTween(Tween tween, CardModel card,
                                    NCard cardNode, CardPile oldPile) => false;
}
```

---

### 3.2 配置系统（Config）

BaseLib 的配置系统是其最具参考价值的子系统之一，分为三层：

#### 第一层：ModConfig（抽象基类）

```csharp
public abstract class ModConfig {
    // 自动扫描 static 属性 → 序列化为 JSON 到 mod_configs/ 目录
    public void Save();
    public void Load();

    // 创建 Godot UI 控件（raw，不含布局）
    protected NConfigTickbox CreateRawTickboxControl(PropertyInfo property);
    protected NConfigSlider   CreateRawSliderControl(PropertyInfo property);
    protected NDropdownPositioner CreateRawDropdownControl(PropertyInfo property);

    // 事件：值变化时通知
    public event EventHandler? ConfigChanged;
}
```

**核心设计**：

- **约定优于配置**：所有 `public static` 属性自动成为配置项，无需手动声明
- **类型安全**：`TypeDescriptor.GetConverter()` 将任意类型转为/从字符串序列化
- **文件路径安全**：用命名空间生成配置文件名，中文命名空间会自动去掉非法字符
- **损坏保护**：JSON 解析失败时 `_savingDisabled = true`，防止覆盖用户手动编辑
- **灵活 UI**：只提供 raw 控件，具体布局由子类决定

#### 第二层：SimpleModConfig（通用实现）

```csharp
public class SimpleModConfig : ModConfig {
    public override void SetupConfigUI(Control optionContainer) {
        // 自动遍历所有 static 属性
        // bool → Toggle  ;  double → Slider  ;  enum → Dropdown
        // 按 [ConfigSection] 属性分组，加 Section Header
        // 自动添加 HoverTip（class 有 [HoverTipsByDefault] 或属性有 [ConfigHoverTip]）
        GenerateOptionsForAllProperties(options);
    }
}
```

**效果**：**子类只需要写配置属性，无需重写任何 UI 代码**，就能得到完整美观的支持 HoverTip 的配置界面。

#### 第三层：ConfigAttributes（属性标记）

```csharp
[ConfigSection("FirstSection")]         // 分组标题
[ConfigHoverTip]                        // 该属性需要悬浮提示
[ConfigHoverTip(false)]                 // 明确禁用悬浮提示（即使 class 有默认值）
[SliderRange(0.1, 4.0, 0.05)]          // Slider 范围和步长
[SliderLabelFormat("{0:0.00}x")]         // 标签格式化
```

#### 示例配置类

```csharp
[HoverTipsByDefault]  // 全局：所有属性都自动加 HoverTip
internal class BaseLibConfig : SimpleModConfig {
    [ConfigSection("FirstSection")]
    public static bool EnableDebugLogging { get; set; } = false;
    public static bool AllowDuplicateRelics { get; set; } = false;

    [ConfigSection("SecondSection")]
    [SliderRange(0.1, 4.0, 0.05)]
    [SliderLabelFormat("{0:0.00}x")]
    public static double EnemyDamageMultiplier { get; set; } = 1.25;

    [SliderRange(-50, 50, 5)]
    [SliderLabelFormat("{0:+0;-0;0} HP")]  // 强制显示 + 号
    [ConfigHoverTip(false)]                 // 该属性不加 HoverTip
    public static double StartingHealthOffset { get; set; } = -10;
}
```

**对工作区的借鉴**：当前各 mod 的配置都是自己手写 UI 或用 ModConfig API 硬编码。BaseLib 的 `SimpleModConfig` + 属性标记方案可以将配置开发效率提升 10 倍以上——只需声明属性、添加几行 attribute，就能自动生成完整 UI。

---

### 3.3 补丁体系（Patches）

BaseLib 的补丁组织极为系统化，按功能分为四大类：

#### ContentPatches —— 内容注册核心

`ContentPatches.cs`（10KB）负责将所有 `ICustomModel` 实现类注册到游戏数据库：

- 扫描所有 mod 程序集中的 `ICustomModel` 子类
- 按类型分别注册到 `CardModel`、`RelicModel`、`PotionModel`、`CharacterModel` 等
- 处理 `CustomContentDictionary` 的内容合并

#### CustomEnums —— 自定义枚举值

这是 BaseLib 最有技术含量的部分之一。游戏中的枚举值（如 `CardKeyword`、`PileType`）是硬编码的整数，mod 无法在编译期知道自己的值应该是什么。

BaseLib 的解决方案：

```csharp
// mod 中声明字段并标记特性
public static class MyKeywords {
    [CustomEnum]           // 自动生成值（+1 递增）
    public static CardKeyword MyNewKeyword;

    [CustomEnum("MY_MOD_PREFIX")]           // 指定前缀
    [KeywordProperties(AutoKeywordPosition.Before)]  // 自动插入描述文本
    public static CardKeyword AnotherKeyword;
}
```

**实现机制**（`GenEnumValues` Patch）：
1. 在 `ModelDb.Init` 之前扫描所有标记了 `[CustomEnum]` 的字段
2. 按类型分组，统一分配不冲突的整数值
3. 对 `CardKeyword` 额外处理：注册到 `CustomKeywords.KeywordIDs`，支持自动插入描述文本
4. 对 `PileType` 额外处理：注册到 `CustomPiles`，使自定义牌堆可被游戏识别

#### CustomPilePatches —— 自定义牌堆（最复杂）

16.7KB 的 `CustomPilePatches.cs` 包含大量 IL Transpiler，修改游戏内部 `CardPileCmd` 的逻辑：
- 自定义牌的移动动画
- 自定义堆的可视性
- 战斗结束时的清理行为

#### ModInteropPatch —— Mod 互操作（16KB）

详见 3.6 节。

---

### 3.4 扩展方法（Extensions）

BaseLib 提供了大量精心设计的扩展方法，降低了游戏 API 的使用门槛。

#### DynamicVarExtensions —— 战斗变量扩展

```csharp
public static class DynamicVarExtensions {
    // 替代直接访问 BaseValue，自动处理战斗状态
    public static decimal CalculateBlock(this DynamicVar var, Creature creature,
                                        ValueProp props, CardPlay? cardPlay = null,
                                        CardModel? cardSource = null) {
        if (!CombatManager.Instance.IsInProgress) return amount;
        if (CombatManager.Instance.IsEnding) return amount;

        CombatState? combatState = creature.CombatState;
        if (combatState == null) return amount;

        amount = Hook.ModifyBlock(combatState, creature, amount, props,
                                   cardSource, cardPlay, out var modifiers);
        return Math.Max(amount, 0m);
    }

    // 为动态变量附加悬浮提示
    public static DynamicVar WithTooltip(this DynamicVar var,
                                          string? locKey = null,
                                          string locTable = "static_hover_tips") {
        // 自动生成 key = "PREFIX-VARIABLE_NAME"
        // 从 static_hover_tips.json 读取 .title 和 .description
        DynamicVarTips[var] = () => {
            LocString locString = new(locTable, key + ".title");
            LocString locString2 = new(locTable, key + ".description");
            return new HoverTip(locString, locString2);
        };
        return var;
    }
}
```

**`WithTooltip` 设计极为精妙**：它将本地化 key 生成规则标准化——变量类名自动映射到 `PREFIX-VARIABLE_NAME` 格式，只需在 JSON 中按约定填写 key 即可，无需在代码中硬编码字符串。

#### 其他扩展

| 文件 | 提供的扩展 |
|------|-----------|
| `ControlExtensions.cs` | `DrawDebug()` — 绘制调试矩形框 |
| `HarmonyExtensions.cs` | Harmony Patch 相关辅助方法 |
| `IEnumerableExtensions.cs` | LINQ 增强 |
| `TypeExtensions.cs` | 类型反射增强 |
| `FloatExtensions.cs` | 浮点数学扩展 |
| `StringExtensions.cs` | 字符串扩展 |
| `PublicPropExtensions.cs` | 公开属性访问器 |

---

### 3.5 IL 补丁工具（Utils/Patching）

这是 BaseLib 对 STS2 mod 生态最重要的贡献之一。Harmony 的 Transpiler 功能强大但门槛很高，BaseLib 提供了链式 API 大幅降低了使用难度。

#### InstructionMatcher —— 指令序列匹配

```csharp
// 链式 API 匹配一段 IL 指令
new InstructionPatcher(instructions)
    .Match(
        new InstructionMatcher()
            .ldc_i4_0()           // push 0
            .opcode(OpCodes.Conv_I8)  // 转换为 int64
            .callvirt(null)       // 调用虚方法
            .stloc_0()            // 存储到局部变量 0
    )
```

`InstructionMatcher` 为每种常见 IL 指令模式提供了语义化的方法（`.ldc_i4_0()`, `.stloc_0()`, `.ret()` 等），比直接写 `OpCodes.Ldc_I4_0` 直观得多。

#### InstructionPatcher —— 指令序列操作

```csharp
var patcher = new InstructionPatcher(instructions)
    .Match(new InstructionMatcher()...)   // 找到目标位置
    .Insert([                              // 在当前位置前插入指令
        CodeInstruction.LoadLocal(0),
        CodeInstruction.LoadArgument(0),
        CodeInstruction.Call(typeof(MyClass), nameof(MyMethod)),
        CodeInstruction.StoreLocal(0),
    ]);
```

支持 `.Match()`、`.Step()`、`.Insert()`、`.Replace()`、`.GetOperand()`、`.GetLabels()`、`.PrintResult()` 等操作，并自带指令日志记录。

**BaseLib 自用的 `QuickTranspiler`**：一个极简静态包装器，用于生成方法末尾的固定补丁模式（加载参数 → 调用目标 → 返回）。

**对工作区的直接借鉴**：当前 ControlPanel 等项目如果要写 Transpiler，都是手写 `OpCodes`。使用 `InstructionPatcher` + `InstructionMatcher` 可以显著减少 IL 调试时间。

---

### 3.6 Mod 互联互操作（ModInterop）

这是 BaseLib 最具创新性的设计，允许不同 mod 之间进行**类型安全的跨程序集调用**，无需双方在编译期互相引用。

#### 核心场景

假设 Mod A 想提供一个 API 给 Mod B 调用：
- Mod A 定义接口 `interface IMyApi { void DoSomething(); }`
- Mod B 想调用 `IMyApi.DoSomething()`

传统方案：Mod B 直接引用 Mod A 的程序集 → 版本耦合、循环依赖。

BaseLib 方案：`[ModInterop]` 特性 + 运行时动态生成 IL 代码。

#### 用法示例

```csharp
// Mod B 中定义"代理接口"并标记
[ModInterop(ModId = "ModA")]
public static class ModAInterop {
    // 声明要调用的静态方法（不必真的存在）
    // 运行时 BaseLib 自动在 ModA 程序集中找到同名同参数的方法
    // 并生成 IL 代码直接调用它
    [ModInteropMethod(Type = "MegaCrit.Sts2.Core.Models.ModelDb")]
    public static extern bool MethodInAnotherMod(string arg);
}
```

#### 实现机制（ModInteropPatch）

1. 读取 `[ModInterop(ModId = "xxx")]` 找到目标 mod 的 Assembly
2. 扫描接口中的方法和属性
3. 对每个方法：用 IL Transpiler 动态生成调用目标 mod 中同名方法的指令序列
4. 对属性：生成 getter/setter 的 IL 调用
5. 对包装类型：生成构造函数 + 字段访问的 IL 代码

这本质上是**编译期多态**的运行时版本——不需要接口继承，只需要方法签名匹配。

---

### 3.7 日志系统（NLogWindow）

BaseLib 提供了一个完整的**滚动日志窗口**，作为 Godot `.tscn` 场景 + C# 脚本：

```csharp
[GlobalClass]
public partial class NLogWindow : Window {
    private static readonly LimitedLog _log = new(256);  // 最多 256 条
    private static readonly List<NLogWindow> _listeners = [];

    public static void AddLog(string msg) {  // 全局静态方法，任何地方可调用
        _log.Enqueue(msg);
        foreach (var window in _listeners)
            window.Refresh();
    }

    // 有限队列，超过上限自动删除最旧条目
    // GetTail() 支持滚动偏移量
    public string GetTail(int offset, int lineCount) { ... }
}
```

**借鉴点**：
- **全局静态 API**：`NLogWindow.AddLog("msg")` 从任何地方调用，无跨线程问题（Godot 单线程）
- **多窗口同步**：多个 LogWindow 实例可同时存在，自动同步更新
- **滚动支持**：鼠标滚轮支持日志滚动

**对比当前工作区**：NoClientCheats 用 `CheatNotification` 做临时提示，没有持久日志窗口。BaseLib 的 `NLogWindow` 提供了更完整的日志持久化方案。

---

## 4. 对工作区项目的具体借鉴点

### 4.1 配置系统（最高优先级）

**现状**：RichPing、NoClientCheats、HostPriority 各自用 ModConfig API 手写配置控件。

**可借鉴**：
```
当前：12 行 ModConfig 代码 + 12 行 UI 代码 → 1 个开关
目标：3 行属性声明 + 2 行 attribute → 1 个开关 + 自动 UI + HoverTip
```

**实施方式**：
1. 引入 BaseLib 作为依赖（或抽取 `Config/` 目录到工作区
2. 配置类继承 `SimpleModConfig`
3. 用属性标记替代手写 UI

**预期收益**：
- RichPing 的 20+ 配置项（全局开关、各角色存活/死亡开关等）代码量减少 70%
- NoClientCheats 的 Duration Slider、History Dropdown 等配置用 `SimpleModConfig` 一行搞定

### 4.2 自定义枚举（`[CustomEnum]`）

**现状**：STS2 的 `CardKeyword`、`PileType` 等枚举值硬编码，mod 无法添加新值。

**可借鉴**：参考 `CustomEnums.cs` 的 `GenEnumValues` Patch，实现自己的枚举扩展机制。例如：
- RichPing 如果要添加新的 Ping 类型枚举，可复用该模式
- MP_PlayerManager 如果要定义新的玩家状态枚举（超出 offline/alive/dead 之外）

**注意**：`CustomEnums` 需要修改 `ModelDb.Init`，与现有 mod 可能存在 patch 冲突问题。

### 4.3 扩展方法层

**`DynamicVarExtensions.CalculateBlock`**：在 ControlPanel 的伤害/格挡显示逻辑中，可直接用 `.CalculateBlock()` 替代手写 `if (CombatManager.Instance.IsInProgress)` 判断。

**`DynamicVarExtensions.WithTooltip`**：如果未来要给 RichPing 的 Ping 文本添加悬浮提示说明，可以用类似的模式，从 JSON 加载说明文本并自动关联到对应变量。

### 4.4 扩展方法层（UI Debug）

**`ControlExtensions.DrawDebug`**：在 ControlPanel 开发 UI 布局时，临时添加 `.DrawDebug()` 调用可以快速看到控件边界，比 GDscript 的调试方法更直接。

### 4.5 IL 补丁工具

**当前需求**：ControlPanel 的 `SpawnEnemyHelper` 使用反射调用 `CreatureCmd.Add`，没有 Transpiler。但如果未来需要更深度地修改战斗逻辑（如修改 `CardPileCmd.Add` 的行为），`InstructionPatcher` + `InstructionMatcher` 可以大幅简化 IL 代码编写。

**优先级**：低，当前反射方案已够用。

### 4.6 ModInterop —— 长期目标

**潜力场景**：如果未来多个 mod 要互相通信（如 RichPing 想让其他 mod 注册自己的角色 Ping 文本），`[ModInterop]` 比接口继承更灵活。但目前各 mod 规模较小，暂无此需求。

### 4.7 日志窗口

**现状**：各 mod 只有临时通知（`CheatNotification`），无持久日志。

**可借鉴**：为 ControlPanel 或 NoClientCheats 添加 `NLogWindow`，让用户能看到所有历史操作记录。

---

## 5. 总结与优先级建议

### 借鉴价值排名

| 优先级 | 模块 | 理由 |
|--------|------|------|
| ⭐⭐⭐⭐⭐ | **SimpleModConfig 配置系统** | 代码量减少 70%，直接提升开发效率，无需额外学习成本 |
| ⭐⭐⭐⭐ | **ConfigAttributes 属性体系** | `[SliderRange]`、`[ConfigSection]`、`[HoverTipsByDefault]` 等零成本增强 UI |
| ⭐⭐⭐ | **DynamicVarExtensions** | 统一的战斗状态检查模式，ControlPanel 可直接使用 |
| ⭐⭐⭐ | **CustomCharacterModel** | 为未来制作自定义角色 mod 奠定基础（STS2 mod 生态的主流需求） |
| ⭐⭐ | **IL 补丁工具** | 需要一定学习成本，但长远收益高 |
| ⭐ | **ModInterop** | 当前项目规模暂不需要；适合生态成熟后的跨 mod 协作 |
| ⭐ | **NLogWindow** | 有一定参考价值，但 Godot 场景文件迁移成本较高 |

### 实施路线图

**第一阶段（立即可做）**：
1. 将 BaseLib 的 `Config/` 目录抽取为工作区的共享库 `STS2_mod/SharedConfig/`
2. 修改 RichPing 的 `RichPingConfig` 继承 `SimpleModConfig`，用属性标记替换手写 UI

**第二阶段（下次迭代）**：
3. 将 `DynamicVarExtensions.cs` 引入 ControlPanel，规范化伤害/格挡显示
4. 将 `ConfigAttributes.cs` 扩展到 NoClientCheats

**第三阶段（长期）**：
5. 考虑基于 BaseLib 架构重建 ControlPanel，使用 `CustomCharacterModel` 体系让卡牌管理更标准化
6. 引入 `InstructionPatcher` 用于高级补丁编写

### 与工作区现有工作的关系

| BaseLib 特性 | 工作区已有替代 | 整合建议 |
|-------------|------------|--------|
| ModConfig | 各 mod 各自实现 | 抽取 SimpleModConfig 统一 |
| 反射工具 | ControlPanel GameStateHelper | BaseLib 的反射工具更系统，可参考 |
| 日志 | godot.log + CheatNotification | NLogWindow 更结构化，但迁移成本高 |
| 自定义内容注册 | 无 | 未来做自定义角色时直接继承 BaseLib 体系 |

### 补充说明：Notes.txt 的技术价值

BaseLib 仓库中的 `Notes.txt`（8KB）是作者对 STS2 内部机制的详细笔记，特别是：
- **`CardPileCmd.Add` 的完整 IL 分析**（游戏内部卡牌移动逻辑）
- **`NCombatUi` 的战斗 UI 渲染流程**
- **`Hook.ModifyCardPlayResultPileTypeAndPosition`** 等高级 Hook 位置

这些笔记对于深入理解 STS2 战斗系统、编写高级补丁极具参考价值，建议作为 STS2_mod 的参考文档存档。
