using Godot;
using System;
using System.Collections.Generic;

namespace NoClientCheats;

/// <summary>
/// 左下角作弊拦截历史记录面板。
/// 默认隐藏；按快捷键（F6）呼出/隐藏。
/// 可拖拽标题栏移动、拖拽边缘调整大小，内容随窗口自适应（参考伤害统计 mod 窗口）。
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

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 800;
        _BuildUI();
        HidePanel();
        SetProcessInput(true);
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (NoClientCheatsMod.ShowHistoryPanel && key.Keycode == NoClientCheatsMod.HistoryToggleKey)
            {
                GetViewport().SetInputAsHandled();
                TogglePanel();
            }
            return;
        }

        // 拖拽/缩放时在全局接收鼠标移动与释放，避免移出窗口后丢失
        if (_window == null || !GodotObject.IsInstanceValid(_window) || !_window.Visible) return;
        if (ev is InputEventMouseButton mb && !mb.Pressed && mb.ButtonIndex == MouseButton.Left)
        {
            if (_isResizing || _isDragging)
            {
                _isResizing = false;
                _isDragging = false;
                _resizeEdges = ResizeEdge.None;
                _titleDragPending = false;
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        if (ev is InputEventMouseMotion mm)
        {
            if (_isResizing)
            {
                _ApplyResize(mm.GlobalPosition);
                GetViewport().SetInputAsHandled();
            }
            else if (_isDragging)
            {
                _window.Position = mm.GlobalPosition + _dragOffset;
                GetViewport().SetInputAsHandled();
            }
        }
    }

    /// <summary>切换显示/隐藏。</summary>
    public void TogglePanel()
    {
        if (_isVisible) HidePanel();
        else ShowPanel();
    }

    public void ShowPanel()
    {
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

    /// <summary>刷新列表内容（从全局历史队列读取）。</summary>
    public void RefreshList()
    {
        if (_listContainer == null || !GodotObject.IsInstanceValid(_listContainer)) return;

        // 只移除动态行，保留 _emptyLabel，避免访问已释放节点导致异常
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

        // 取最近的 N 条（最新在上）
        int start = Math.Max(0, records.Count - NoClientCheatsMod.HistoryMaxRecords);
        for (int i = start; i < records.Count; i++)
        {
            var rec = records[i];
            var row = _MakeRow(rec.Time, rec.SenderName, rec.CharacterName, rec.Command);
            _listContainer.AddChild(row);
        }

        if (_titleLabel != null && GodotObject.IsInstanceValid(_titleLabel))
            _titleLabel.Text = $"  作弊拦截记录  ({_totalCount} 条)";
        if (_hintLabel != null && GodotObject.IsInstanceValid(_hintLabel))
            _hintLabel.Text = $"  F6 呼出/隐藏  |  记录保存本局  |  总计 {_totalCount} 条";
    }

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

    void _OnPanelGuiInput(InputEvent ev)
    {
        if (_window == null) return;
        if (ev is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex != MouseButton.Left) return;
            if (!mb.Pressed)
            {
                if (_isResizing) { _isResizing = false; _resizeEdges = ResizeEdge.None; }
                return;
            }
            var local = _window.GetLocalMousePosition();
            var edge = _DetectEdges(local);
            if (edge != ResizeEdge.None)
            {
                _isResizing = true;
                _resizeEdges = edge;
                _resizeStartPos = (ev as InputEventMouseButton).GlobalPosition;
                _resizeStartPanelPos = _window.Position;
                _resizeStartWidth = _window.Size.X;
                _resizeStartHeight = _window.Size.Y;
            }
            return;
        }
        if (ev is InputEventMouseMotion mm)
        {
            var edge = _DetectEdges(_window.GetLocalMousePosition());
            _window.MouseDefaultCursorShape = edge != ResizeEdge.None ? _GetResizeCursor(edge) : Control.CursorShape.Arrow;
            if (_isResizing) _ApplyResize(mm.GlobalPosition);
        }
    }

    void _OnTitleGuiInput(InputEvent ev)
    {
        if (_window == null) return;
        if (ev is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            if (mb.Pressed)
            {
                _titleDragPending = true;
                _titleDragStart = mb.GlobalPosition;
            }
            else
            {
                if (_isDragging) { }
                _isDragging = false;
                _titleDragPending = false;
            }
            return;
        }
        if (ev is InputEventMouseMotion mm && _titleDragPending)
        {
            if (!_isDragging && _titleDragStart.DistanceTo(mm.GlobalPosition) > 4f)
            {
                _isDragging = true;
                _dragOffset = _window.Position - mm.GlobalPosition;
            }
            if (_isDragging)
                _window.Position = mm.GlobalPosition + _dragOffset;
        }
    }

    void _BuildUI()
    {
        var root = GetTree().Root;
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
        _window.GuiInput += _OnPanelGuiInput;

        // 内容区：锚定满矩形 + 边距，随窗口大小自适应
        _vbox = new VBoxContainer { Name = "VBox", MouseFilter = Control.MouseFilterEnum.Ignore };
        _vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _vbox.OffsetLeft = 8;
        _vbox.OffsetTop = 8;
        _vbox.OffsetRight = -8;
        _vbox.OffsetBottom = -8;
        _vbox.AddThemeConstantOverride("separation", 4);
        _window.AddChild(_vbox);

        var titleBar = new PanelContainer { Name = "TitleBar", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        titleBar.CustomMinimumSize = new Vector2(0, 32);
        _vbox.AddChild(titleBar);

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
            Text = "  作弊拦截记录  (0 条)",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            TooltipText = "拖拽移动  |  边缘拖拽调整大小"
        };
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.85f, 0.85f, 0.9f, 1f));
        _titleLabel.AddThemeFontSizeOverride("font_size", 14);
        _titleLabel.MouseDefaultCursorShape = Control.CursorShape.Move;
        _titleLabel.GuiInput += _OnTitleGuiInput;
        titleRow.AddChild(_titleLabel);

        var closeBtn = new Button
        {
            Text = "✕",
            Flat = true,
            CustomMinimumSize = new Vector2(32, 32),
            TooltipText = "关闭（F6 重新呼出）"
        };
        closeBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f, 1f));
        closeBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.3f, 0.3f, 1f));
        closeBtn.Pressed += () => HidePanel();
        titleRow.AddChild(closeBtn);

        var clearBtn = new Button
        {
            Text = "清空",
            Flat = true,
            CustomMinimumSize = new Vector2(50, 32),
            TooltipText = "清空历史记录"
        };
        clearBtn.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.65f, 1f));
        clearBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.5f, 0.2f, 1f));
        clearBtn.Pressed += () => { NoClientCheatsMod.ClearHistory(); RefreshList(); };
        titleRow.AddChild(clearBtn);

        // ── 快捷键提示 ──
        var hintRow = new HBoxContainer { Name = "HintRow", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        hintRow.CustomMinimumSize = new Vector2(0, 20);
        _vbox.AddChild(hintRow);

        _hintLabel = new Label
        {
            Name = "Hint",
            Text = "  F6 呼出/隐藏  |  记录保存本局  |  总计 0 条",
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
            Text = "  暂无拦截记录",
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
