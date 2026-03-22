using Godot;
using System;
using System.Threading.Tasks;
using Control = Godot.Control;

namespace RunHistoryAnalyzer.UI;

/// <summary>
/// 分析按钮：点击后执行分析并弹出结果窗口。
/// 当前为简化实现：按钮固定显示在屏幕右下角。
/// 后续可优化为注入 NRunHistory 详情面板底部操作栏。
/// </summary>
public partial class AnalyzeButton : CanvasLayer
{
    private Button _btn = null!;
    private string _currentFilePath = "";
    private AnalyzeResult? _cachedResult;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        Layer = 500;
        _BuildButton();
        Hide();
    }

    private void _BuildButton()
    {
        _btn = new Button
        {
            Text = "🔍 分析",
            CustomMinimumSize = new Vector2(110, 36),
            TooltipText = "分析选中的历史记录是否异常"
        };
        _btn.Pressed += _OnAnalyzePressed;
        _btn.AddThemeFontSizeOverride("font_size", 13);

        // 样式
        var normalStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.22f, 0.14f, 0.95f),
            BorderColor = new Color(0.2f, 0.5f, 0.3f, 1f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12f,
            ContentMarginRight = 12f,
            ContentMarginTop = 8f,
            ContentMarginBottom = 8f
        };
        var hoverStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.18f, 0.35f, 0.22f, 0.98f),
            BorderColor = new Color(0.3f, 0.7f, 0.45f, 1f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12f,
            ContentMarginRight = 12f,
            ContentMarginTop = 8f,
            ContentMarginBottom = 8f
        };

        _btn.AddThemeStyleboxOverride("normal", normalStyle);
        _btn.AddThemeStyleboxOverride("hover", hoverStyle);
        _btn.AddThemeStyleboxOverride("pressed", normalStyle);
        _btn.AddThemeStyleboxOverride("disabled", new StyleBoxFlat
        {
            BgColor = new Color(0.12f, 0.12f, 0.15f, 0.8f),
            BorderColor = new Color(0.25f, 0.25f, 0.28f, 1f),
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 12f,
            ContentMarginRight = 12f,
            ContentMarginTop = 8f,
            ContentMarginBottom = 8f
        });

        // 右下角固定尺寸 — 勿用 FullRect，否则会铺满整个视口导致「巨型按钮」
        const float margin = 16f;
        const float bw = 110f;
        const float bh = 36f;
        _btn.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _btn.OffsetLeft = -bw - margin;
        _btn.OffsetTop = -bh - margin;
        _btn.OffsetRight = -margin;
        _btn.OffsetBottom = -margin;
        _btn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
        _btn.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;

        AddChild(_btn);
    }

    /// <summary>
    /// 当选中某条历史记录时，调用此方法传入文件路径。
    /// </summary>
    public void UpdateFilePath(string filePath)
    {
        _currentFilePath = filePath;
        _cachedResult = null;
        ApplyToolbarVisibility();
    }

    /// <summary>
    /// 根据当前路径与 <see cref="RunHistoryAnalyzerMod.AnalyzerToolbarVisible"/> 决定按钮是否可见。
    /// </summary>
    public void ApplyToolbarVisibility()
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            Hide();
            return;
        }

        if (!RunHistoryAnalyzerMod.AnalyzerToolbarVisible)
        {
            Hide();
            return;
        }

        Show();
        _SetButtonState(AnalyzeBtnState.Default);
    }

    private void _SetButtonState(AnalyzeBtnState state)
    {
        switch (state)
        {
            case AnalyzeBtnState.Default:
                _btn.Text = "🔍 分析";
                _btn.Disabled = false;
                _btn.Modulate = new Color(1f, 1f, 1f, 1f);
                break;
            case AnalyzeBtnState.Loading:
                _btn.Text = "分析中...";
                _btn.Disabled = true;
                _btn.Modulate = new Color(0.7f, 0.7f, 0.7f, 1f);
                break;
            case AnalyzeBtnState.NoAnomaly:
                _btn.Text = "✓ 未发现异常";
                _btn.Disabled = false;
                _btn.Modulate = new Color(0.5f, 0.95f, 0.5f, 1f);
                break;
            case AnalyzeBtnState.HasAnomaly:
                _btn.Text = "⚠ 存在异常";
                _btn.Disabled = false;
                _btn.Modulate = new Color(1f, 0.8f, 0.3f, 1f);
                break;
            case AnalyzeBtnState.Error:
                _btn.Text = "✗ 分析失败";
                _btn.Disabled = false;
                _btn.Modulate = new Color(1f, 0.4f, 0.4f, 1f);
                break;
        }
    }

    private async void _OnAnalyzePressed()
    {
        if (string.IsNullOrEmpty(_currentFilePath)) return;

        _SetButtonState(AnalyzeBtnState.Loading);

        var result = await Task.Run(() => RunHistoryAnalyzerCore.Analyze(_currentFilePath));
        _cachedResult = result;

        if (result.HasError)
        {
            _SetButtonState(AnalyzeBtnState.Error);
        }
        else if (result.HasAnomalies)
        {
            _SetButtonState(AnalyzeBtnState.HasAnomaly);
        }
        else
        {
            _SetButtonState(AnalyzeBtnState.NoAnomaly);

            await ToSignal(GetTree().CreateTimer(3.0f), Godot.Timer.SignalName.Timeout);
            if (GodotObject.IsInstanceValid(_btn))
                _SetButtonState(AnalyzeBtnState.Default);
        }

        RunHistoryAnalyzerMod.ResultWindow?.ShowResult(result);
    }

    private enum AnalyzeBtnState
    {
        Default,
        Loading,
        NoAnomaly,
        HasAnomaly,
        Error
    }
}
