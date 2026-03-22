// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  SharedConfig Stubs — 编译用类型占位符
//  仅定义 StS2 游戏专有类型（MegaCrit.Sts2.*），不重新定义 Godot 基础类型。
//
//  注意：
//  - StS2 的自定义类（NDropdown, NSettingsTickbox 等）继承 Godot 类，必须声明为 partial。
//  - 所有 Godot API 类型（Control, Node, RichTextLabel, Vector2, MarginContainer 等）由 GodotSharp.dll 提供。
//  - Extensions_ 和 TransferAllNodes 等实用方法来自 SharedConfig.Utils 和 SharedConfig.Extensions。
//
//  若 dotnet build 报 "cannot find type"，
//  优先在此添加缺失的 StS2 专有类型 stub。
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;
using SharedConfig.Extensions;
using SharedConfig.Utils;

// ── StS2 游戏专有类型（这些是 Godot SDK 中没有的）──────────────

namespace MegaCrit.Sts2.addons.mega_text
{
    // MegaRichTextLabel 是游戏自定义的 RichTextLabel，继承 Godot.RichTextLabel
    // partial 必须（Godot C# 要求）
    public partial class MegaRichTextLabel : Godot.RichTextLabel
    {
        public bool AutoSizeEnabled;
        public new void SetText(string text) { Text = text; }
        public void SetTextAutoSize(string text) { Text = text; }
    }
}

namespace MegaCrit.Sts2.Core.Assets
{
    public class Cache_
    {
        public static Cache_ Instance = null!;
        public Node GetScene(string p) => null!;
        // GetAsset 返回 object，因为可能返回 Font、Theme、Texture2D 等多种类型
        public object GetAsset(string p) => null!;
    }

    public static class PreloadManager
    {
        public static Cache_ Cache => Cache_.Instance;
    }
}

namespace MegaCrit.Sts2.Core.Combat { }
namespace MegaCrit.Sts2.Core.Entities.Cards { }
namespace MegaCrit.Sts2.Core.Entities.Creatures { }

namespace MegaCrit.Sts2.Core.Helpers
{
    public static class SceneHelper
    {
        public static string GetScenePath(string key) => key;
    }
}

namespace MegaCrit.Sts2.Core.Hooks { }

namespace MegaCrit.Sts2.Core.HoverTips
{
    public class HoverTip
    {
        public HoverTip(object? title, object? description) { }
        public HoverTip(object description) { }
    }
}

namespace MegaCrit.Sts2.Core.Localization
{
    public class LocString
    {
        public LocString(string table, string key) { }
        public void Add(object? obj) { }
        public static bool Exists(string table, string key) => false;
        public static LocString? GetIfExists(string table, string key) => null;
        public string GetFormattedText() => "";
        public string GetRawText() => "";
    }
}

namespace MegaCrit.Sts2.Core.Localization.DynamicVars { }
namespace MegaCrit.Sts2.Core.Models { }

namespace MegaCrit.Sts2.Core.Modding
{
    public static class ModManager
    {
        public static IEnumerable<ModInfo> LoadedMods => System.Linq.Enumerable.Empty<ModInfo>();
    }

    public class ModInfo
    {
        public string? id;
        public System.Reflection.Assembly? assembly;
        public object? manifest;
    }
}

namespace MegaCrit.Sts2.Core.Nodes.CommonUi
{
    using MegaCrit.Sts2.addons.mega_text;

    // partial 必须（Godot C# 要求）
    public partial class NDropdown : Godot.Control
    {
        // StS2 中这个字段是 VBoxContainer，不是 Array
        public Godot.Control? _dropdownItems;
        public Godot.Control? _dropdownContainer;
        public MegaRichTextLabel _currentOptionLabel = null!;
        protected void ConnectSignals() { }
        public void ClearDropdownItems() { }
        public void CloseDropdown() { }
    }

    // partial 必须（Godot C# 要求）
    public partial class NDropdownItem : Godot.Control
    {
        public static new class SignalName_
        {
            public static Godot.StringName Pressed => "pressed";
        }
        public SharedConfig.Config.UI.ConfigDropdownItem? Data;
        public int DisplayIndex;
        public MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel? _label;
        public void Init(int index) { }
    }

    // partial 必须（Godot C# 要求）
    public partial class NDropdownPositioner : Godot.Control
    {
        public Node? _dropdownNode;
    }
}

namespace MegaCrit.Sts2.Core.Nodes.GodotExtensions
{
    // NSlider 是 StS2 自定义的 Slider 类，继承自 Godot.Control（包含 Range 的功能）
    // partial 必须（Godot C# 要求）
    public partial class NSlider : Godot.Control
    {
        // 这些属性来自 Godot.Range，供 NConfigSlider 使用
        public double MinValue { get; set; }
        public double MaxValue { get; set; }
        public double Step { get; set; }
        public double Value { get; set; }
        public void SetValueWithoutAnimation(double value) { Value = value; }
    }

    // SizeFlags_ 在 Godot 4 中不再是 Control 的嵌套类
    // StS2 仍然使用它作为布局标志，这里提供兼容定义
    public static class SizeFlags_
    {
        public const Godot.Control.SizeFlags Fill = Godot.Control.SizeFlags.Fill;
        public const Godot.Control.SizeFlags ShrinkEnd = Godot.Control.SizeFlags.ShrinkEnd;
        public const Godot.Control.SizeFlags ShrinkBegin = Godot.Control.SizeFlags.ShrinkBegin;
        public const Godot.Control.SizeFlags Expand = Godot.Control.SizeFlags.Expand;
        public const Godot.Control.SizeFlags ExpandFill = Godot.Control.SizeFlags.ExpandFill;
    }

    public static class Extensions_
    {
        public static T AddChildSafely<T>(this Godot.Node parent, T child) where T : Godot.Node => child;
        public static void QueueFreeSafely(this Godot.Node? node) { }
        public static void RefreshLayout(this Godot.Node node) { }
    }
}

namespace MegaCrit.Sts2.Core.Nodes.HoverTips
{
    public class HoverTip
    {
        public HoverTip(object? title, object? description) { }
    }

    // partial 必须（Godot C# 要求）
    public partial class NHoverTipSet : Godot.Control
    {
        public static NHoverTipSet CreateAndShow(MarginContainer source, HoverTip tip) => null!;
        public static void Remove(MarginContainer source) { }
    }
}

namespace MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen
{
    // partial 必须（Godot C# 要求）
    public partial class NModdingScreen : Godot.Control { }
    public partial class NConfigButton : Godot.Control { public bool IsConfigOpen; }

    // partial 必须（Godot C# 要求）
    public partial class NClickableControl : Godot.Control
    {
        public event Action? ReleaseEvent;
        protected virtual void OnRelease() { }
    }

    // partial 必须（Godot C# 要求）
    public partial class NScrollableContainer : Godot.Control
    {
        public void DisableScrollingIfContentFits() { }
        public void InstantlyScrollToTop() { }
    }

    // partial 必须（Godot C# 要求）
    public partial class NScrollbar : Godot.Control { }
}

namespace MegaCrit.Sts2.Core.Nodes.Screens.Settings
{
    // partial 必须（Godot C# 要求）
    public partial class NSettingsTickbox : Godot.Control
    {
        public bool IsTicked { get; set; }
        protected virtual void OnTick() { }
        protected virtual void OnUntick() { }
        protected void ConnectSignals() { }
    }

    // partial 必须（Godot C# 要求）
    public partial class NSettingsDropdown : Godot.Control { }
}

namespace MegaCrit.Sts2.Core.Nodes.Combat
{
    // partial 必须（Godot C# 要求）
    public partial class NCreatureVisuals : Godot.Node { }
}

namespace MegaCrit.Sts2.Core.ValueProps
{
    public class ValueProp { }
}

namespace MegaCrit.Sts2.Core.Nodes.Localization
{
    public static class StringHelper
    {
        public static string Slugify(string input) =>
            System.Text.RegularExpressions.Regex.Replace(
                input.Replace("_", " ").Replace("-", " "), @"\s+", "_")
            .ToLowerInvariant();
    }
}
