using Godot;
using System;
using System.Collections.Generic;

namespace NoClientCheats;

/// <summary>
/// 左下角作弊拦截历史记录面板。
/// 默认隐藏；热键（F9）呼出/隐藏（由 InputHandlerNode 轮询触发）。
/// 可拖拽标题栏移动、拖拽边缘调整大小，内容随窗口自适应。
/// 窗口按需延迟创建（首次 Toggle/Show 时）。
/// </summary>
public partial class CheatHistoryPanel : CanvasLayer
{
    const float EdgeThreshold = 8f;
    const float MinWidth = 320f;
    const float MinHeight = 200f;
    const float MaxWidth = 800f;
    const float MaxHeight = 600f;

    [Flags]
    enum ResizeEdge
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8
    }

    PanelContainer _titleBar;

    Panel _window;
    VBoxContainer _vbox;
    VBoxContainer _listContainer;
    Label _emptyLabel;
    Label _titleLabel;
    Label _hintLabel;
    int _totalCount;
    bool _isVisible;

    bool _isResizing;
    ResizeEdge _resizeEdges;
    Vector2 _resizeStartPos;
    Vector2 _resizeStartPanelPos;
    float _resizeStartWidth;
    float _resizeStartHeight;

    bool _isDragging;
    Vector2 _dragOffset;
    bool _titleDragPending;
    Vector2 _titleDragStart;

    const float TitleBarHeight = 32f;

    public override void _Ready()
    {
        EnsureWindowBuilt(); // 构建窗口 UI
    }

    public override void _Input(InputEvent ev)
    {
        if (_window == null || !GodotObject.IsInstanceValid(_window) || !_window.Visible) return;

        // ── 左键按下：判断是拖拽标题栏还是缩放边缘 ──
        if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left && mb.Pressed)
        {
            var mousePos = mb.GlobalPosition;
            if (_IsOverTitleBar(mousePos))
            {
                _titleDragPending = true;
                _titleDragStart = mousePos;
            }
            else
            {
                var local = _window.GetLocalMousePosition();
                var edge = _DetectEdges(local);
                if (edge != ResizeEdge.None)
                {
                    _isResizing = true;
                    _resizeEdges = edge;
                    _resizeStartPos = mousePos;
                    _resizeStartPanelPos = _window.Position;
                    _resizeStartWidth = _window.Size.X;
                    _resizeStartHeight = _window.Size.Y;
                }
            }
            return;
        }

        // ── 左键抬起：结束拖拽或缩放 ──
        if (ev is InputEventMouseButton mb2 && mb2.ButtonIndex == MouseButton.Left && !mb2.Pressed)
        {
            if (_isResizing) { _isResizing = false; _resizeEdges = ResizeEdge.None; }
            if (_isDragging || _titleDragPending) { _isDragging = false; _titleDragPending = false; }
            return;
        }

        // ── 鼠标移动：执行拖拽 / 缩放；动态更新光标 ──
        if (ev is InputEventMouseMotion mm)
        {
            var mousePos = mm.GlobalPosition;

            // 缩放
            if (_isResizing)
            {
                _ApplyResize(mousePos);
                return;
            }

            // 拖拽
            if (_titleDragPending)
            {
                if (!_isDragging && _titleDragStart.DistanceTo(mousePos) > 4f)
                {
                    _isDragging = true;
                    _dragOffset = _window.Position - mousePos;
                }
                if (_isDragging)
                    _window.Position = mousePos + _dragOffset;
                return;
            }

            // 鼠标悬停：更新光标形状（边缘=resize，标题栏=move，其他=默认）
            var localPos = _window.GetLocalMousePosition();
            if (localPos.Y >= 0 && localPos.Y < TitleBarHeight)
            {
                _window.MouseDefaultCursorShape = Control.CursorShape.Move;
            }
            else
            {
                var edge = _DetectEdges(localPos);
                _window.MouseDefaultCursorShape = edge != ResizeEdge.None
                    ? _GetResizeCursor(edge)
                    : Control.CursorShape.Arrow;
            }
        }
    }

    bool _IsOverTitleBar(Vector2 globalPos)
    {
        if (_titleBar == null || !GodotObject.IsInstanceValid(_titleBar)) return false;
        var titleBarGlobalY = _titleBar.GetGlobalPosition().Y;
        // 标题栏高度固定 TitleBarHeight
        return globalPos.Y >= titleBarGlobalY && globalPos.Y < titleBarGlobalY + TitleBarHeight;
    }

    // ── 显示/隐藏（由 InputHandlerNode 或按钮触发）───────────────────────
    public void TogglePanel()
    {
        if (_isVisible) HidePanel();
        else ShowPanel();
    }

    public void ShowPanel()
    {
        if (!IsInsideTree())
        {
            CallDeferred(nameof(ShowPanel));
            return;
        }
        EnsureWindowBuilt(); // 首次 Show 时若 _Ready 尚未执行，先构建窗口
        if (_window == null || !GodotObject.IsInstanceValid(_window)) return;
        _window.Visible = true;
        _isVisible = true;
        RefreshList();
    }

    public void HidePanel()
    {
        if (_window == null || !GodotObject.IsInstanceValid(_window)) return;
        _window.Visible = false;
        _isVisible = false;
    }

    /// <summary>
    /// 确保窗口 UI 已构建（可被 ShowPanel 提前调用，以防 _Ready 尚未执行）。
    /// 加 _uiBuilt 保护，重复调用安全。
    /// </summary>
    bool _uiBuilt;
    void EnsureWindowBuilt()
    {
        if (_window != null && GodotObject.IsInstanceValid(_window)) { _uiBuilt = true; return; }
        _BuildUI();
        _uiBuilt = _window != null && GodotObject.IsInstanceValid(_window);
    }

    /// <summary>将窗口居中到屏幕中央。</summary>
    public void CenterWindow()
    {
        if (_window == null || !GodotObject.IsInstanceValid(_window)) return;
        var screen = (Engine.GetMainLoop() as SceneTree)?.Root?.GetViewport();
        if (screen == null) return;
        var rect = screen.GetVisibleRect();
        _window.Position = new Vector2(
            (rect.Size.X - _window.Size.X) / 2f,
            (rect.Size.Y - _window.Size.Y) / 2f
        );
    }

    // ── 刷新 ───────────────────────────────────────────────────────────
    public void RefreshList()
    {
        if (_listContainer == null || !GodotObject.IsInstanceValid(_listContainer)) return;

        foreach (var child in _listContainer.GetChildren())
        {
            if (child == _emptyLabel) continue;
            child.QueueFree();
        }

        _totalCount = NoClientCheatsMod.GetHistoryCount();

        var records = NoClientCheatsMod.GetHistoryRecords();
        if (records.Count == 0)
        {
            _emptyLabel.Visible = true;
            return;
        }

        _emptyLabel.Visible = false;

        int start = Math.Max(0, records.Count - NoClientCheatsMod.HistoryMaxRecords);
        for (int i = start; i < records.Count; i++)
        {
            var rec = records[i];
            var row = _MakeRow(rec.Time, rec.SenderName, rec.CharacterName, rec.Command);
            _listContainer.AddChild(row);
        }

        if (_titleLabel != null && GodotObject.IsInstanceValid(_titleLabel))
            _titleLabel.Text = $"  {Localization.Trf("panel_title", _totalCount)}";
        if (_hintLabel != null && GodotObject.IsInstanceValid(_hintLabel))
            _hintLabel.Text = $"  {Localization.Trf("hint_row", NoClientCheatsMod.GetHistoryKeyDisplayName(), _totalCount)}";
    }

    // ── 缩放边缘检测 ────────────────────────────────────────────────────
    ResizeEdge _DetectEdges(Vector2 localPos)
    {
        if (_window == null) return ResizeEdge.None;
        var size = _window.Size;
        var e = ResizeEdge.None;
        if (localPos.X < EdgeThreshold) e |= ResizeEdge.Left;
        else if (localPos.X > size.X - EdgeThreshold) e |= ResizeEdge.Right;
        if (localPos.Y < EdgeThreshold) e |= ResizeEdge.Top;
        else if (localPos.Y > size.Y - EdgeThreshold) e |= ResizeEdge.Bottom;
        return e;
    }

    static Control.CursorShape _GetResizeCursor(ResizeEdge edges)
    {
        bool h = (edges & (ResizeEdge.Left | ResizeEdge.Right)) != ResizeEdge.None;
        bool v = (edges & (ResizeEdge.Top | ResizeEdge.Bottom)) != ResizeEdge.None;
        if (h && v)
            return (edges & ResizeEdge.Left) != ResizeEdge.None && (edges & ResizeEdge.Top) != ResizeEdge.None
                || (edges & ResizeEdge.Right) != ResizeEdge.None && (edges & ResizeEdge.Bottom) != ResizeEdge.None
                ? Control.CursorShape.Fdiagsize
                : Control.CursorShape.Bdiagsize;
        if (h) return Control.CursorShape.Hsize;
        if (v) return Control.CursorShape.Vsize;
        return Control.CursorShape.Arrow;
    }

    void _ApplyResize(Vector2 globalPos)
    {
        if (_window == null) return;
        var delta = globalPos - _resizeStartPos;
        var pos = _resizeStartPanelPos;
        var w = _resizeStartWidth;
        var h = _resizeStartHeight;
        if ((_resizeEdges & ResizeEdge.Right) != ResizeEdge.None) w = _resizeStartWidth + delta.X;
        if ((_resizeEdges & ResizeEdge.Left) != ResizeEdge.None) { w = _resizeStartWidth - delta.X; pos.X = _resizeStartPanelPos.X + delta.X; }
        if ((_resizeEdges & ResizeEdge.Bottom) != ResizeEdge.None) h = _resizeStartHeight + delta.Y;
        if ((_resizeEdges & ResizeEdge.Top) != ResizeEdge.None) { h = _resizeStartHeight - delta.Y; pos.Y = _resizeStartPanelPos.Y + delta.Y; }
        w = Math.Clamp(w, MinWidth, MaxWidth);
        h = Math.Clamp(h, MinHeight, MaxHeight);
        if ((_resizeEdges & ResizeEdge.Left) != ResizeEdge.None) pos.X = _resizeStartPanelPos.X + (_resizeStartWidth - w);
        if ((_resizeEdges & ResizeEdge.Top) != ResizeEdge.None) pos.Y = _resizeStartPanelPos.Y + (_resizeStartHeight - h);
        _window.Size = new Vector2(w, h);
        _window.Position = pos;
    }

    // ── 面板鼠标事件（缩放）─────────────────────────────────────────────
    // 已迁移到 _Input 统一处理，此处保留空函数供外部引用
    void _OnPanelGuiInput(InputEvent ev) { }

    // ── 标题栏鼠标事件（拖拽移动）───────────────────────────────────────
    // 已迁移到 _Input 统一处理，此处保留空函数供外部引用
    void _OnTitleGuiInput(InputEvent ev) { }

    // ── UI 构建（必须在场景树线程中调用）────────────────────────────────
    void _BuildUI()
    {
        if (!IsInsideTree())
        {
            CallDeferred(nameof(EnsureWindowBuilt));
            return;
        }
        if (!GodotObject.IsInstanceValid(this)) return;
        SceneTree sceneTree;
        try { sceneTree = GetTree(); }
        catch (ObjectDisposedException) { sceneTree = null; }
        catch { sceneTree = null; }
        sceneTree ??= Engine.GetMainLoop() as SceneTree;
        if (sceneTree?.Root == null) return;
        var root = sceneTree.Root;
        var screenH = (float)DisplayServer.WindowGetSize().Y;

        _window = new Panel
        {
            Name = "CheatHistoryWindow",
            CustomMinimumSize = new Vector2(MinWidth, MinHeight),
            Size = new Vector2(440, 340),
            Position = new Vector2(16, screenH - 340 - 16),
            ClipContents = true
        };

        var winStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.08f, 0.96f),
            BorderColor = new Color(0.2f, 0.2f, 0.25f, 1.0f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8
        };
        _window.AddThemeStyleboxOverride("panel", winStyle);
        // 拖拽/缩放统一在 _Input 处理，删除 GuiInput 注册

        _vbox = new VBoxContainer { Name = "VBox", MouseFilter = Control.MouseFilterEnum.Ignore };
        _vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _vbox.OffsetLeft = 8;
        _vbox.OffsetTop = 8;
        _vbox.OffsetRight = -8;
        _vbox.OffsetBottom = -8;
        _vbox.AddThemeConstantOverride("separation", 4);
        _window.AddChild(_vbox);

        // ── 标题栏 ──
        var titleBar = new PanelContainer { Name = "TitleBar", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titleBar.CustomMinimumSize = new Vector2(0, 32);
        _vbox.AddChild(titleBar);
        _titleBar = titleBar;

        var titleStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.16f, 1.0f),
            BorderColor = new Color(0.2f, 0.2f, 0.25f, 1.0f),
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8
        };
        titleBar.AddThemeStyleboxOverride("panel", titleStyle);

        var titleRow = new HBoxContainer { Name = "TitleRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titleBar.AddChild(titleRow);

        _titleLabel = new Label
        {
            Name = "Title",
            Text = $"  {Localization.Trf("panel_title", 0)}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            TooltipText = Localization.Tr("tooltip_move")
        };
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f, 1f));
        _titleLabel.AddThemeFontSizeOverride("font_size", 14);
        _titleLabel.MouseDefaultCursorShape = Control.CursorShape.Move;
        // 拖拽在 CanvasLayer._Input 统一处理，不再注册 GuiInput
        titleRow.AddChild(_titleLabel);

        // 居中按钮
        var centerBtn = new Button
        {
            Text = "⊡",
            Flat = true,
            CustomMinimumSize = new Vector2(28, 32),
            TooltipText = Localization.Tr("tooltip_center")
        };
        centerBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f, 1f));
        centerBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.85f, 0.4f, 1f));
        centerBtn.Pressed += () => CenterWindow();
        titleRow.AddChild(centerBtn);

        // 清空按钮
        var clearBtn = new Button
        {
            Text = Localization.Tr("btn_clear"),
            Flat = true,
            CustomMinimumSize = new Vector2(50, 32),
            TooltipText = Localization.Tr("tooltip_clear")
        };
        clearBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f, 1f));
        clearBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.5f, 0.2f, 1f));
        clearBtn.Pressed += () => { NoClientCheatsMod.ClearHistory(); RefreshList(); };
        titleRow.AddChild(clearBtn);

        // 关闭按钮
        var closeBtn = new Button
        {
            Text = "✕",
            Flat = true,
            CustomMinimumSize = new Vector2(32, 32),
            TooltipText = string.Format(Localization.Tr("tooltip_close"), NoClientCheatsMod.GetHistoryKeyDisplayName())
        };
        closeBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f, 1f));
        closeBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.3f, 0.3f, 1f));
        closeBtn.Pressed += () => HidePanel();
        titleRow.AddChild(closeBtn);

        // 呼出按钮（在关闭前，点此按钮确保面板可见）
        var showBtn = new Button
        {
            Text = Localization.Tr("btn_show"),
            Flat = true,
            CustomMinimumSize = new Vector2(50, 32),
            TooltipText = Localization.Tr("tooltip_show")
        };
        showBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f, 1f));
        showBtn.AddThemeColorOverride("font_hover_color", new Color(0.4f, 0.9f, 0.5f, 1f));
        showBtn.Pressed += () => ShowPanel();
        titleRow.AddChild(showBtn);

        // ── 快捷键提示 ──
        var hintRow = new HBoxContainer { Name = "HintRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        hintRow.CustomMinimumSize = new Vector2(0, 20);
        _vbox.AddChild(hintRow);

        _hintLabel = new Label
        {
            Name = "Hint",
            Text = $"  {Localization.Trf("hint_row", NoClientCheatsMod.GetHistoryKeyDisplayName(), 0)}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd
        };
        _hintLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.5f, 1f));
        _hintLabel.AddThemeFontSizeOverride("font_size", 11);
        hintRow.AddChild(_hintLabel);

        // ── 列表区域（随窗口高度自适应）──
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 80)
        };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        _vbox.AddChild(scroll);

        _listContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill
        };
        scroll.AddChild(_listContainer);

        // ── 空状态提示 ──
        _emptyLabel = new Label
        {
            Text = $"  {Localization.Tr("empty")}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        _emptyLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.5f, 1f));
        _emptyLabel.AddThemeFontSizeOverride("font_size", 13);
        _listContainer.AddChild(_emptyLabel);

        root.AddChild(_window);
    }

    HBoxContainer _MakeRow(string time, string senderName, string characterName, string command)
    {
        var displayChar = CheatLocHelper.GetCharacterDisplayName(characterName ?? "");
        var displayCmd = CheatLocHelper.LocalizeCheatCommand(command ?? "");

        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 24)
        };

        var timeLabel = new Label
        {
            Text = time,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            SizeFlagsVertical = Control.SizeFlags.Fill,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        timeLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.5f, 1f));
        timeLabel.AddThemeFontSizeOverride("font_size", 12);
        timeLabel.CustomMinimumSize = new Vector2(64, 0);
        row.AddChild(timeLabel);

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd, CustomMinimumSize = new Vector2(6, 0) });

        var playerLabel = new Label
        {
            Text = senderName,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            SizeFlagsVertical = Control.SizeFlags.Fill
        };
        playerLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.75f, 1.0f, 1f));
        playerLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(playerLabel);

        if (!string.IsNullOrEmpty(displayChar))
        {
            var roleLabel = new Label
            {
                Text = $" ({displayChar})",
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
                SizeFlagsVertical = Control.SizeFlags.Fill
            };
            roleLabel.AddThemeColorOverride("font_color", new Color(0.55f, 0.65f, 0.8f, 1f));
            roleLabel.AddThemeFontSizeOverride("font_size", 12);
            row.AddChild(roleLabel);
        }

        var arrow = new Label { Text = " → ", SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd, SizeFlagsVertical = Control.SizeFlags.Fill };
        arrow.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f, 1f));
        arrow.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(arrow);

        var cmdLabel = new Label
        {
            Text = displayCmd,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        cmdLabel.AddThemeColorOverride("font_color", new Color(1f, 0.3f, 0.3f, 1f));
        cmdLabel.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(cmdLabel);

        return row;
    }
}
