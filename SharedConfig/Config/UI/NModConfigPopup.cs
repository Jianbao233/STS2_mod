using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ModdingScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using SharedConfig.Config;
using SharedConfig.Extensions;
using SharedConfig.Utils;

// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  SharedConfig Stubs — UI 组件占位符
//  此文件中的实现为存根，编译用。实际逻辑在运行时 Patch。
//
//  原始实现在源文件 OldNModConfigPopup.cs.backup
// ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

namespace SharedConfig.Config.UI;

public partial class NModConfigPopup : NClickableControl
{
    // SpireField 的 key 必须是 NModConfigPopup，不能用 NModdingScreen
    private static readonly System.Collections.Generic.Dictionary<object, object?> _popupCache = new();

    public static void ShowModConfig(NModdingScreen screen, ModConfig config, NConfigButton opener)
    {
        if (!_popupCache.TryGetValue(screen, out var popup) || popup == null)
        {
            opener.IsConfigOpen = false;
            return;
        }
        ((NModConfigPopup?)popup)?.ShowMod(config, opener);
    }

    private ModConfig? _currentConfig;
    private NScrollableContainer _optionScrollContainer = null!;
    private VBoxContainer _optionContainer = null!;
    private NConfigButton? _opener;
    private double _saveTimer;
    private const double AutosaveDelay = 5;

    private NModConfigPopup(Control futureParent)
    {
        _saveTimer = -1;
        Size = futureParent.Size;
        MouseFilter = MouseFilterEnum.Ignore;

        _optionScrollContainer = new();
        _optionScrollContainer.MouseFilter = MouseFilterEnum.Stop;
        _optionScrollContainer.Size = new Vector2(
            x: System.Math.Max(480, Size.X * 0.5f),
            y: System.Math.Min(950, Size.Y * 0.95f)
        );

        AddChild(_optionScrollContainer);
        _optionScrollContainer.Owner = this;
        _optionScrollContainer.Position = Size * 0.5f - _optionScrollContainer.Size * 0.5f;

        NScrollbar scrollbar = (NScrollbar)(object)PreloadManager.Cache.GetScene(SceneHelper.GetScenePath("ui/scrollbar"));
        scrollbar.Name = "Scrollbar";
        _optionScrollContainer.AddChild(scrollbar);
        scrollbar.Owner = _optionScrollContainer;

        scrollbar.SetAnchorsAndOffsetsPreset(5 /* LayoutPreset.RightWide */);
        scrollbar.OffsetLeft = 0;
        scrollbar.OffsetRight = 48;
        scrollbar.OffsetTop = 32;
        scrollbar.OffsetBottom = -32;

        Godot.Control mask = new();
        mask.Name = "Mask";
        mask.Size = _optionScrollContainer.Size;
        mask.MouseFilter = MouseFilterEnum.Ignore;
        mask.ClipContents = true;

        _optionScrollContainer.AddChild(mask);
        mask.Owner = _optionScrollContainer;

        _optionContainer = new VBoxContainer();
        _optionContainer.Name = "Content";
        _optionContainer.CustomMinimumSize = new Vector2(mask.Size.X, 0);
        mask.MouseFilter = MouseFilterEnum.Ignore;

        mask.AddChild(_optionContainer);
        _optionContainer.Owner = mask;

        Hide();
        futureParent.AddChildSafely(this);
    }

    private void ShowMod(ModConfig config, NConfigButton opener)
    {
        _opener = opener;
        MouseFilter = MouseFilterEnum.Stop;

        try
        {
            config.SetupConfigUI(_optionContainer);
            _optionScrollContainer.DisableScrollingIfContentFits();
            _optionScrollContainer.InstantlyScrollToTop();
            _currentConfig = config;
            config.ConfigChanged += OnConfigChanged;
            Show();
        }
        catch (Exception e)
        {
            ModConfigLogger.Error(e.ToString());
            ClosePopup();
        }
    }

    private void ClosePopup()
    {
        if (_opener != null) _opener.IsConfigOpen = false;
        MouseFilter = MouseFilterEnum.Ignore;
        if (_currentConfig != null) _currentConfig.ConfigChanged -= OnConfigChanged;
        Hide();
        _optionContainer.FreeChildren();
        foreach (var child in _optionContainer.GetParent().GetChildren())
            if (child != _optionContainer)
                child.QueueFreeSafely();
    }

    private void OnConfigChanged(object? sender, System.EventArgs e)
    {
        _saveTimer = AutosaveDelay;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (_saveTimer > 0)
        {
            _saveTimer -= delta;
            if (_saveTimer <= 0)
            {
                SaveCurrentConfig();
            }
        }
    }

    protected override void OnRelease()
    {
        base.OnRelease();
        SaveCurrentConfig();
        ClosePopup();
    }

    private void SaveCurrentConfig()
    {
        _currentConfig?.Save();
        _saveTimer = -1;
    }

    // 简单的日志桩
    public static class ModConfigLogger
    {
        public static void Error(string message) { }
        public static void Warn(string message, bool showInGui = false) { }
    }
}

public class NHotkeyManager
{
    public static NHotkeyManager? Instance => null;
    public void AddBlockingScreen(object screen) { }
    public void RemoveBlockingScreen(object screen) { }
}
