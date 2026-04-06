using Godot;
using MultiplayerTools.Panel;
using MultiplayerTools.Platform;

namespace MultiplayerTools.UI
{
    /// <summary>
    /// Floating button: PanelContainer + Label (classic Godot UI).
    /// Top-center by default; long-press or move &gt; threshold then drag to reposition.
    /// Short release without drag toggles the panel.
    /// Hidden after scene change (leaving main menu / entering lobby or run).
    /// </summary>
    internal partial class MpFloatingButton : Control
    {
        private Label? _label;
        private bool _dragging;
        private bool _pressing;
        private Vector2 _dragStartGlobal;
        private Vector2 _pressStartGlobal;
        private float _pressTimer;

        private bool _sceneChanged;
        private Node? _menuParent;

        private const float LongPressThreshold = 0.3f;
        private const float TapMoveThreshold = 8f;

        private float BtnW => PlatformInfo.IsMobile ? 140f : 120f;
        private float BtnH => PlatformInfo.IsMobile ? 56f : 48f;

        public override void _Ready()
        {
            var w = BtnW;
            var h = BtnH;
            CustomMinimumSize = new Vector2(w, h);
            Size = new Vector2(w, h);
            MouseFilter = MouseFilterEnum.Stop;
            FocusMode = FocusModeEnum.None;

            var root = GetTree()?.Root;
            float rw = root?.Size.X ?? 1920f;
            GlobalPosition = new Vector2(rw * 0.5f - w * 0.5f, 20f);

            TooltipText = $"多人工具 (Hotkey: {Config.ToggleHotkey})";

            var bg = new PanelContainer();
            bg.SetAnchorsPreset(LayoutPreset.FullRect);
            bg.OffsetLeft = bg.OffsetTop = bg.OffsetRight = bg.OffsetBottom = 0;

            var bgStyle = new StyleBoxFlat
            {
                BgColor = new Color(0.16f, 0.25f, 0.48f, 0.92f),
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8
            };
            bg.AddThemeStyleboxOverride("panel", bgStyle);
            AddChild(bg);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            bg.AddChild(margin);

            _label = new Label
            {
                Text = Loc.Get("settings.open_panel", "多人工具"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _label.AddThemeFontSizeOverride("font_size", PlatformInfo.IsMobile ? 18 : 16);
            _label.AddThemeColorOverride("font_color", new Color(0.98f, 0.98f, 1f, 1f));
            margin.AddChild(_label);

            GetTree()?.Connect("tree_changed", Callable.From(OnTreeChanged));
        }

        private void OnTreeChanged()
        {
            if (_menuParent != null && !GodotObject.IsInstanceValid(_menuParent))
                _sceneChanged = true;
        }

        public override void _EnterTree()
        {
            Visible = !MpPanel.IsOpen;
            _sceneChanged = false;
            _menuParent = GetParent();
        }

        public override void _Process(double delta)
        {
            if (Visible == MpPanel.IsOpen)
                Visible = !MpPanel.IsOpen;

            if (_sceneChanged)
            {
                if (Visible)
                    Visible = false;
                return;
            }

            if (_pressing && !_dragging)
                _pressTimer += (float)delta;
        }

        /// <summary>
        /// GUI events only fire when the pointer is over this control (correct Size).
        /// </summary>
        public override void _GuiInput(InputEvent @event)
        {
            if (_sceneChanged)
                return;

            if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
            {
                if (mb.Pressed)
                {
                    _pressing = true;
                    _dragging = false;
                    _pressTimer = 0f;
                    _pressStartGlobal = mb.GlobalPosition;
                    _dragStartGlobal = mb.GlobalPosition;
                }
                else
                {
                    if (!_pressing)
                        return;
                    float moveDist = (mb.GlobalPosition - _pressStartGlobal).Length();
                    bool wasDragging = _dragging;
                    _pressing = false;
                    _pressTimer = 0f;
                    _dragging = false;

                    if (!wasDragging && moveDist <= TapMoveThreshold)
                        MpPanel.Toggle();
                }
                return;
            }

            if (@event is InputEventScreenTouch st && st.Index == 0)
            {
                if (st.Pressed)
                {
                    _pressing = true;
                    _dragging = false;
                    _pressTimer = 0f;
                    _pressStartGlobal = st.Position;
                    _dragStartGlobal = st.Position;
                }
                else
                {
                    if (!_pressing)
                        return;
                    float moveDist = (st.Position - _pressStartGlobal).Length();
                    bool wasDragging = _dragging;
                    _pressing = false;
                    _pressTimer = 0f;
                    _dragging = false;

                    if (!wasDragging && moveDist <= TapMoveThreshold)
                        MpPanel.Toggle();
                }
                return;
            }

            if (_pressing && @event is InputEventMouseMotion mm)
            {
                HandleDrag(mm.GlobalPosition);
                return;
            }

            if (_pressing && @event is InputEventScreenDrag sd)
            {
                HandleDrag(sd.Position);
            }
        }

        private void HandleDrag(Vector2 globalPos)
        {
            var diff = globalPos - _dragStartGlobal;
            var rootSize = GetTree()?.Root?.Size ?? new Vector2(1920, 1080);
            float w = BtnW;
            float h = BtnH;

            if (!_dragging && (diff.Length() > TapMoveThreshold || _pressTimer >= LongPressThreshold))
                _dragging = true;

            if (_dragging)
            {
                GlobalPosition = new Vector2(
                    Mathf.Clamp(globalPos.X - w * 0.5f, 0, rootSize.X - w),
                    Mathf.Clamp(globalPos.Y - h * 0.5f, 0, rootSize.Y - h));
            }
        }
    }
}
