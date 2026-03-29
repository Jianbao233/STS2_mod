using Godot;
using System;

namespace NoClientCheats;

/// <summary>
/// 临时悬浮通知：作弊拦截时在屏幕顶部居中弹出，N 秒后自动淡出消失。
/// 多条垂直堆叠，不合并冷却（每次都弹）。
/// </summary>
public partial class CheatNotification : CanvasLayer
{
    const float ANIM_DURATION = 0.3f;
    const int MAX_VISIBLE = 4;

    static CheatNotification _instance;

    readonly System.Collections.Generic.List<_Item> _visible = new();
    float _processDelta;
    bool _hasScheduledCleanup;

    /// <summary>显示一条临时拦截通知。格式：Steam名 | 角色 | 指令。</summary>
    public static void Show(string senderName, string characterName, string cheatCommand)
    {
        if (_instance == null) return;
        _instance._Enqueue(senderName, characterName ?? "", cheatCommand);
    }

    public override void _Ready()
    {
        _instance = this;
        Layer = 900;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        _processDelta += (float)delta;
        if (_processDelta < 0.05f) return;
        _processDelta = 0;
        float dt = 0.05f;

        for (int i = _visible.Count - 1; i >= 0; i--)
        {
            var item = _visible[i];
            item.Remaining -= dt;

            if (item.Remaining <= 0)
            {
                _AnimateOutAndRemove(item);
                _visible.RemoveAt(i);
            }
            else if (item.Remaining < ANIM_DURATION && !item.FadingOut)
            {
                item.FadingOut = true;
                _AnimateOut(item);
            }
        }

        if (_visible.Count == 0 && !_hasScheduledCleanup)
        {
            _hasScheduledCleanup = true;
            CallDeferred(nameof(_DelayedStop));
        }
    }

    void _DelayedStop()
    {
        _hasScheduledCleanup = false;
        if (_visible.Count == 0) SetProcess(false);
    }

    void _Enqueue(string senderName, string characterName, string cheatCommand)
    {
        if (_visible.Count >= MAX_VISIBLE) return;

        var displayChar = CheatLocHelper.GetCharacterDisplayName(characterName ?? "");
        var displayCmd = CheatLocHelper.LocalizeCheatCommand(cheatCommand ?? "");

        var screenW = (float)DisplayServer.WindowGetSize().X;
        int index = _visible.Count;
        float baseY = 80f;
        float itemH = 40f;
        float y = baseY + index * (itemH + 6f);

        var root = GetTree().Root;

        var panel = new Panel
        {
            Name = "CheatNotifyPopup",
            CustomMinimumSize = new Vector2(640, 0),
            Size = new Vector2(640, itemH),
            GlobalPosition = new Vector2((screenW - 640) / 2f, y)
        };
        panel.Modulate = new Color(1, 1, 1, 0);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.01f, 0.01f, 0.95f),
            BorderColor = new Color(0.9f, 0.08f, 0.08f, 1.0f),
            BorderWidthLeft = 2,
            BorderWidthRight = 2,
            BorderWidthTop = 2,
            BorderWidthBottom = 2,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6
        };
        panel.AddThemeStyleboxOverride("panel", style);

        // 内容行：左侧文字 + 右侧按钮
        var row = new HBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
            Alignment = BoxContainer.AlignmentMode.Center
        };
        row.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        row.AddThemeConstantOverride("separation", 12);
        panel.AddChild(row);

        var mid = string.IsNullOrEmpty(displayChar) ? "" : $"  |  {Localization.Tr("label_role")}：{displayChar}  |  ";
        var label = new Label
        {
            Text = $"{Localization.Tr("notify_blocked")}  {senderName}{mid}  {displayCmd}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeColorOverride("font_color", new Color(1f, 0.25f, 0.25f, 1f));
        label.AddThemeFontSizeOverride("font_size", 14);
        row.AddChild(label);

        // 查看历史按钮
        var histBtn = new Button
        {
            Text = Localization.Tr("btn_history"),
            Flat = true,
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd,
            SizeFlagsVertical = Control.SizeFlags.Fill,
            CustomMinimumSize = new Vector2(100, 0),
            TooltipText = Localization.Tr("tooltip_history")
        };
        histBtn.AddThemeColorOverride("font_color", new Color(0.85f, 0.75f, 0.55f, 1f));
        histBtn.AddThemeColorOverride("font_hover_color", new Color(1f, 0.9f, 0.6f, 1f));
        histBtn.Pressed += () => NoClientCheatsMod.ShowHistoryPanelUI();
        row.AddChild(histBtn);

        root.AddChild(panel);

        var item = new _Item
        {
            Panel = panel,
            Remaining = NoClientCheatsMod.NotificationDuration,
            FadeTween = null,
            FadingOut = false
        };
        _visible.Add(item);
        SetProcess(true);
        _hasScheduledCleanup = false;

        // 立即显示
        var t = CreateTween();
        t.TweenProperty(panel, "modulate:a", 1f, ANIM_DURATION)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        t.Play();
    }

    void _AnimateOut(_Item item)
    {
        if (item.FadeTween != null && item.FadeTween.IsValid()) item.FadeTween.Kill();
        if (item.Panel == null || !GodotObject.IsInstanceValid(item.Panel)) return;
        var t = CreateTween();
        t.TweenProperty(item.Panel, "modulate:a", 0f, ANIM_DURATION)
            .SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
        t.Play();
        item.FadeTween = t;
    }

    void _AnimateOutAndRemove(_Item item)
    {
        if (item.FadeTween != null && item.FadeTween.IsValid()) item.FadeTween.Kill();
        if (item.Panel != null && GodotObject.IsInstanceValid(item.Panel))
            item.Panel.QueueFree();
    }

    class _Item
    {
        public Panel Panel;
        public float Remaining;
        public Tween FadeTween;
        public bool FadingOut;
    }
}
