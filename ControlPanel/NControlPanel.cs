using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ControlPanel;

/// <summary>
/// 控制面板：CanvasLayer + Panel，DamageMeter 风格浮窗，含卡牌/药水/战斗等标签页。
/// F7 由 F7InputLayer 处理。
/// </summary>
public partial class NControlPanel : CanvasLayer
{
    private static readonly Color GoldColor = new Color(1f, 0.84f, 0f, 1f);
    private static readonly Color GoldBorder = new Color(1f, 0.84f, 0f, 0.3f);

    private Panel _mainPanel;
    private TabContainer _tabs;
    private Control _cardTab;
    private Control _potionTab;
    private Control _fightTab;
    private VBoxContainer _cardListContainer;  // 卡牌按钮列表（替代 ItemList）
    private VBoxContainer _potionListContainer;
    private VBoxContainer _fightListContainer;
    private LineEdit _cardSearch;
    private OptionButton _potionCategory;
    private LineEdit _potionSearch;
    private Button _titleBtn;
    private bool _isDragging;
    private Vector2 _dragOffset;
    private bool _titleDragPending;
    private Vector2 _titleDragStart;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 100; // 置顶

        // DamageMeter 风格浮窗：深色半透明背景 + 金色描边
        var viewportSize = GetViewport().GetVisibleRect().Size;
        var panelW = 420f;
        var panelH = 520f;
        var posX = (viewportSize.X - panelW) / 2f;
        var posY = (viewportSize.Y - panelH) / 2f;

        _mainPanel = new Panel
        {
            Position = new Vector2(posX, posY),
            CustomMinimumSize = new Vector2(panelW, panelH),
            ClipContents = true
        };

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.05f, 0.05f, 0.12f, 0.9f),
            BorderColor = GoldBorder
        };
        style.BorderWidthBottom = style.BorderWidthTop = style.BorderWidthLeft = style.BorderWidthRight = 1;
        style.CornerRadiusTopLeft = style.CornerRadiusTopRight = style.CornerRadiusBottomLeft = style.CornerRadiusBottomRight = 8;
        _mainPanel.AddThemeStyleboxOverride("panel", style);
        AddChild(_mainPanel);

        var margin = new MarginContainer
        {
            OffsetLeft = 12,
            OffsetTop = 12,
            OffsetRight = -12,
            OffsetBottom = -12
        };
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        _mainPanel.AddChild(margin);

        var vbox = new VBoxContainer();
        margin.AddChild(vbox);

        // 可拖拽标题栏（DamageMeter 同款）
        _titleBtn = new Button
        {
            Text = "控制面板 v2 · 卡牌/药水/战斗  [F7 关闭]",
            Flat = true,
            FocusMode = Control.FocusModeEnum.None,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        _titleBtn.AddThemeColorOverride("font_color", GoldColor);
        _titleBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.95f, 0.5f, 1f));
        _titleBtn.AddThemeColorOverride("font_pressed_color", GoldColor);
        _titleBtn.AddThemeFontSizeOverride("font_size", 15);
        _titleBtn.GuiInput += OnTitleGuiInput;
        vbox.AddChild(_titleBtn);

        // 分隔线
        var sep = new HSeparator();
        sep.AddThemeStyleboxOverride("separator", new StyleBoxFlat { BgColor = new Color(1f, 0.84f, 0f, 0.25f) });
        sep.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(sep);

        _tabs = new TabContainer();
        vbox.AddChild(_tabs);

        BuildCardTab();
        BuildPotionTab();
        BuildFightTab();
        // 同步加载列表（不再用 CallDeferred，避免某些环境下延迟回调未执行）
        try
        {
            LoadCardItems();
            LoadPotionItems();
            LoadFightItems();
            GD.Print($"[ControlPanel] v2 列表已加载: 卡牌 {_cardListContainer.GetChildCount()}, 药水 {_potionListContainer.GetChildCount()}, 遭遇 {_fightListContainer.GetChildCount()}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ControlPanel] 列表加载失败: {e.Message}\n{e.StackTrace}");
        }
        // F7 由 CreateAndAttachPanel 中单独挂载的 F7InputLayer 处理（作为 Root 子节点，确保面板隐藏时仍能接收输入）
    }

    private void BuildCardTab()
    {
        _cardTab = new VBoxContainer();
        _tabs.AddChild(_cardTab);
        _tabs.SetTabTitle(0, "卡牌");

        _cardSearch = new LineEdit { PlaceholderText = "搜索卡牌名 / ID...", ClearButtonEnabled = true };
        _cardSearch.TextChanged += _ => FilterCardList();
        _cardTab.AddChild(_cardSearch);

        var cardScroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 180),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _cardListContainer = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        cardScroll.AddChild(_cardListContainer);
        _cardTab.AddChild(cardScroll);
        // 列表由 DeferredLoadAllLists 统一加载
    }

    private void BuildPotionTab()
    {
        _potionTab = new VBoxContainer();
        _tabs.AddChild(_potionTab);
        _tabs.SetTabTitle(1, "药水");

        var topRow = new HBoxContainer();
        _potionCategory = new OptionButton();
        _potionCategory.ItemSelected += _ => FilterPotionList();
        topRow.AddChild(_potionCategory);
        _potionSearch = new LineEdit { PlaceholderText = "搜索...", ClearButtonEnabled = true, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _potionSearch.TextChanged += _ => FilterPotionList();
        topRow.AddChild(_potionSearch);
        _potionTab.AddChild(topRow);

        var potionScroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 180),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _potionListContainer = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        potionScroll.AddChild(_potionListContainer);
        _potionTab.AddChild(potionScroll);
        // 列表由 DeferredLoadAllLists 统一加载
    }

    private void BuildFightTab()
    {
        _fightTab = new VBoxContainer();
        _tabs.AddChild(_fightTab);
        _tabs.SetTabTitle(2, "战斗");

        var hint = new Label { Text = "点击遭遇进入战斗（需进行中局内）" };
        _fightTab.AddChild(hint);

        var fightScroll = new ScrollContainer
        {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 180),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _fightListContainer = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        fightScroll.AddChild(_fightListContainer);
        _fightTab.AddChild(fightScroll);
        // 列表由 DeferredLoadAllLists 统一加载
    }

    private string[] _cardIds = Array.Empty<string>();
    private string[] _cardZhs = Array.Empty<string>();

    private void LoadCardItems()
    {
        _cardIds = PotionAndCardData.CardIds ?? Array.Empty<string>();
        _cardZhs = PotionAndCardData.CardZhs ?? Array.Empty<string>();
        if (_cardIds.Length == 0)
            GD.Print("[ControlPanel] 警告: 卡牌数据为空");
        RefreshCardList();
    }

    private void FilterCardList()
    {
        RefreshCardList();
    }

    private void RefreshCardList()
    {
        foreach (var c in _cardListContainer.GetChildren())
            c.QueueFree();
        var q = _cardSearch?.Text?.Trim().ToLowerInvariant() ?? "";
        for (int i = 0; i < _cardIds.Length; i++)
        {
            var id = _cardIds[i];
            var zh = i < _cardZhs.Length ? _cardZhs[i] : "";
            if (!string.IsNullOrEmpty(q) &&
                !id.ToLowerInvariant().Contains(q) &&
                !(zh?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                continue;
            var btn = new Button { Text = string.IsNullOrEmpty(zh) ? id : $"{zh} ({id})", Flat = true };
            var idCopy = id;
            btn.Pressed += () => RunCommand($"card {idCopy}");
            _cardListContainer.AddChild(btn);
        }
    }

    private (string id, string zh, string cat)[] _potionData = Array.Empty<(string, string, string)>();

    private void LoadPotionItems()
    {
        _potionData = PotionAndCardData.PotionData ?? Array.Empty<(string, string, string)>();
        _potionCategory?.Clear();
        foreach (var cat in PotionAndCardData.PotionCategories)
            _potionCategory?.AddItem(cat);
        if (_potionData.Length == 0)
            GD.Print("[ControlPanel] 警告: 药水数据为空");
        FilterPotionList();
    }

    private void FilterPotionList()
    {
        foreach (var c in _potionListContainer.GetChildren())
            c.QueueFree();
        var catIdx = _potionCategory?.Selected ?? 0;
        var catFilter = catIdx <= 0 ? "" : _potionCategory.GetItemText(catIdx);
        var q = _potionSearch?.Text?.Trim().ToLowerInvariant() ?? "";
        foreach (var (id, zh, cat) in _potionData)
        {
            if (!string.IsNullOrEmpty(catFilter) && cat != catFilter) continue;
            if (!string.IsNullOrEmpty(q) &&
                !id.ToLowerInvariant().Contains(q) &&
                !(zh?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false))
                continue;
            var btn = new Button { Text = string.IsNullOrEmpty(zh) ? id : $"{zh} ({id})", Flat = true };
            var idCopy = id;
            btn.Pressed += () => RunCommand($"potion {idCopy}");
            _potionListContainer.AddChild(btn);
        }
    }

    private (string id, string zh)[] _fightData = Array.Empty<(string, string)>();

    private void LoadFightItems()
    {
        _fightData = PotionAndCardData.FightData ?? Array.Empty<(string, string)>();
        foreach (var c in _fightListContainer.GetChildren())
            c.QueueFree();
        foreach (var (id, zh) in _fightData)
        {
            var btn = new Button { Text = string.IsNullOrEmpty(zh) ? id : $"{zh} ({id})", Flat = true };
            var idCopy = id;
            btn.Pressed += () => RunCommand($"fight {idCopy}");
            _fightListContainer.AddChild(btn);
        }
    }

    /// <summary>通过 DevConsole 执行命令（需进行中局内：卡牌/药水在战斗中，fight 需在地图/局内）</summary>
    private void RunCommand(string cmd)
    {
        try
        {
            object devConsole = GetDevConsole();
            if (devConsole == null)
            {
                GD.PrintErr($"[ControlPanel] DevConsole 未找到，无法执行: {cmd}");
                return;
            }

            var t = devConsole.GetType();
            var method = t.GetMethod("ProcessCommand", new[] { typeof(string) })
                ?? t.GetMethod("ProcessCommand", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string) }, null);
            if (method == null)
            {
                GD.PrintErr($"[ControlPanel] 未找到 ProcessCommand(string) 方法");
                return;
            }

            var result = method.Invoke(devConsole, new object[] { cmd });
            if (result != null)
            {
                var success = result.GetType().GetProperty("Success")?.GetValue(result) as bool?
                    ?? result.GetType().GetProperty("success")?.GetValue(result) as bool?;
                var msg = result.GetType().GetProperty("Msg")?.GetValue(result) as string
                    ?? result.GetType().GetProperty("msg")?.GetValue(result) as string;
                GD.Print($"[ControlPanel] {cmd} => {(success == true ? "OK" : "FAIL")} {msg ?? ""}");
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"[ControlPanel] RunCommand failed: {e.Message}\n{e.StackTrace}");
        }
    }

    private static readonly string[] NDevConsoleTypeNames = {
        "MegaCrit.Sts2.Core.Nodes.Debug.NDevConsole",
        "Sts2.Core.Nodes.Debug.NDevConsole",
        "NDevConsole"
    };

    private static readonly string[] DevConsoleTypeNames = {
        "MegaCrit.Sts2.Core.DevConsole.DevConsole",
        "Sts2.Core.DevConsole.DevConsole",
        "DevConsole"
    };

    private static object GetDevConsole()
    {
        // 1) 通过 NDevConsole.Instance._devConsole 获取（游戏单例，可能尚未创建则 Instance 会抛异常）
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                foreach (var typeName in NDevConsoleTypeNames)
                {
                    var ndevType = asm.GetType(typeName);
                    if (ndevType != null)
                    {
                        var instance = ndevType.GetProperty("Instance")?.GetValue(null);  // 可能抛 InvalidOperationException
                        if (instance != null)
                        {
                            var devField = ndevType.GetField("_devConsole", BindingFlags.NonPublic | BindingFlags.Instance);
                            var dev = devField?.GetValue(instance);
                            if (dev != null) return dev;
                        }
                        break;
                    }
                }
            }
            catch (InvalidOperationException) { /* Instance 尚未创建 */ }
            catch { /* 跳过 */ }
        }

        // 2) 创建 DevConsole 实例（mod 加载时 allowDebug=true）
        bool allowDebug = true;  // ModManager.LoadedMods.Count > 0 时等效
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                Type devType = null;
                foreach (var name in DevConsoleTypeNames)
                {
                    devType = asm.GetType(name);
                    if (devType != null) break;
                }
                if (devType != null)
                {
                    var dev = Activator.CreateInstance(devType, allowDebug);  // DevConsole(bool shouldAllowDebugCommands)
                    if (dev != null) return dev;
                }
            }
            catch { /* 跳过 */ }
        }
        return null;
    }

    private void OnTitleGuiInput(InputEvent ev)
    {
        if (ev is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _titleDragPending = true;
                    _titleDragStart = mb.GlobalPosition;
                }
                else
                {
                    if (_isDragging)
                        _isDragging = false;
                    _titleDragPending = false;
                }
            }
            return;
        }
        if (ev is InputEventMouseMotion mm && _titleDragPending && _mainPanel != null)
        {
            if (!_isDragging && _titleDragStart.DistanceTo(mm.GlobalPosition) > 4f)
            {
                _isDragging = true;
                _dragOffset = _mainPanel.Position - mm.GlobalPosition;
                _titleBtn.ReleaseFocus();
            }
            if (_isDragging)
            {
                _mainPanel.Position = mm.GlobalPosition + _dragOffset;
                GetViewport().SetInputAsHandled();
            }
        }
    }

}
