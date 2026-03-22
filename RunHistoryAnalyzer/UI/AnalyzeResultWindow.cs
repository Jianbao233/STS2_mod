using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Control = Godot.Control;
using SizeFlags = Godot.Control.SizeFlags;

namespace RunHistoryAnalyzer.UI;

/// <summary>
/// 分析结果窗口：显示所有检测到的异常，支持导出报告。
/// 尺寸 560×420，模态，ESC 关闭。
/// </summary>
public partial class AnalyzeResultWindow : Window
{
    private const float WINDOW_WIDTH = 560f;
    private const float WINDOW_HEIGHT = 420f;

    private Label _titleLabel = null!;
    private Label _summaryLabel = null!;
    private VBoxContainer _anomalyList = null!;
    private Button _exportButton = null!;
    private Button _closeButton = null!;

    private AnalyzeResult _result = null!;

    public override void _Ready()
    {
        // 避免在游戏开场/主菜单阶段以空白窗口形式闪现；仅在分析完成后 ShowResult 显示。
        Visible = false;

        CloseRequested += () => Hide();
        Title = "分析报告";
        Size = new Vector2I((int)WINDOW_WIDTH, (int)WINDOW_HEIGHT);
        MinSize = new Vector2I(400, 300);

        // 居中显示
        var screenSize = DisplayServer.WindowGetSize();
        Position = (screenSize - Size) / 2;

        _BuildUI();
        Hide();
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            if (Visible)
            {
                Hide();
                GetViewport().SetInputAsHandled();
            }
        }
    }

    public void ShowResult(AnalyzeResult result)
    {
        _result = result;
        _RefreshContent();
        Show();
    }

    private void _BuildUI()
    {
        var root = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        root.AddThemeConstantOverride("separation", 4);
        // 填满窗口客户区；勿设 OffsetRight/Bottom 为正像素值，否则会撑破布局导致控件溢出
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.OffsetLeft = 0;
        root.OffsetTop = 0;
        root.OffsetRight = 0;
        root.OffsetBottom = 0;
        AddChild(root);

        // ── 标题栏 ──
        _titleLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 36),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _titleLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.95f, 1f));
        _titleLabel.AddThemeFontSizeOverride("font_size", 16);
        _titleLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _titleLabel.AddThemeStyleboxOverride("normal", _MakeTitleStyle());
        root.AddChild(_titleLabel);

        // ── 摘要栏 ──
        _summaryLabel = new Label
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 32),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        _summaryLabel.AddThemeFontSizeOverride("font_size", 14);
        _summaryLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _summaryLabel.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        root.AddChild(_summaryLabel);

        // ── 分隔线 ──
        var sep1 = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 1)
        };
        sep1.AddThemeStyleboxOverride("panel", _MakeSeparatorStyle());
        root.AddChild(sep1);

        // ── 异常列表（滚动）──
        var scroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        scroll.ClipContents = true;
        root.AddChild(scroll);

        _anomalyList = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        _anomalyList.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        scroll.AddChild(_anomalyList);

        // ── 分隔线 ──
        var sep2 = new Control
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 1)
        };
        sep2.AddThemeStyleboxOverride("panel", _MakeSeparatorStyle());
        root.AddChild(sep2);

        // ── 底部操作栏 ──
        var bottomBar = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 44),
            Alignment = HBoxContainer.AlignmentMode.End
        };
        bottomBar.AddThemeConstantOverride("separation", 8);
        root.AddChild(bottomBar);

        bottomBar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _exportButton = new Button { Text = "导出报告", CustomMinimumSize = new Vector2(90, 28) };
        _exportButton.Pressed += _OnExportPressed;
        bottomBar.AddChild(_exportButton);

        _closeButton = new Button { Text = "关闭", CustomMinimumSize = new Vector2(70, 28) };
        _closeButton.Pressed += () => Hide();
        bottomBar.AddChild(_closeButton);
    }

    private void _RefreshContent()
    {
        if (_result.HasError)
        {
            _titleLabel.Text = "分析失败";
            var err = _result.ErrorMessage ?? "未知错误";
            // 摘要一行说明，长错误正文只在下方区域展示，避免重复 + 窄宽度竖排字
            _summaryLabel.Text = "解析或分析过程出错，详见下方";
            _summaryLabel.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.45f, 1f));
            _ClearAnomalyList();
            _AddCenteredScrollMessage(err, isError: true);
            _exportButton.Disabled = true;
            return;
        }

        var history = _result.History;
        if (history != null)
        {
            var charName = history.Players.Count > 0 ? _GetCharacterName(history.Players[0].Character) : "未知";
            _titleLabel.Text = $"分析报告 — {charName}  {history.GetDifficulty()}";
        }
        else
        {
            _titleLabel.Text = "分析报告";
        }

        if (_result.HasAnomalies)
        {
            _summaryLabel.Text = $"⚠  检测到 {_result.Anomalies.Count} 项异常";
            var color = _result.MaxLevel switch
            {
                Models.AnomalyLevel.High => new Color(0.9f, 0.3f, 0.2f, 1f),
                Models.AnomalyLevel.Medium => new Color(0.95f, 0.65f, 0.1f, 1f),
                Models.AnomalyLevel.Low => new Color(0.2f, 0.85f, 0.4f, 1f),
                _ => new Color(0.7f, 0.7f, 0.75f, 1f)
            };
            _summaryLabel.AddThemeColorOverride("font_color", color);
            _exportButton.Disabled = false;
        }
        else
        {
            _summaryLabel.Text = "✓  未发现异常";
            _summaryLabel.AddThemeColorOverride("font_color", new Color(0.2f, 0.85f, 0.4f, 1f));
            _exportButton.Disabled = true;
        }

        _ClearAnomalyList();

        if (_result.HasAnomalies)
        {
            foreach (var anomaly in _result.Anomalies)
                _AddAnomalyRow(anomaly);
        }
        else
        {
            _AddCenteredScrollMessage("该局历史记录未检测到明显异常", isError: false, useOkIcon: true);
        }
    }

    private void _ClearAnomalyList()
    {
        foreach (var child in _anomalyList.GetChildren())
            child.QueueFree();
    }

    private void _AddAnomalyRow(Models.Anomaly anomaly)
    {
        var color = anomaly.Level switch
        {
            Models.AnomalyLevel.High => new Color(0.85f, 0.2f, 0.2f, 1f),
            Models.AnomalyLevel.Medium => new Color(0.95f, 0.6f, 0.1f, 1f),
            Models.AnomalyLevel.Low => new Color(0.2f, 0.8f, 0.4f, 1f),
            _ => new Color(0.7f, 0.7f, 0.75f, 1f)
        };

        var barColor = anomaly.Level switch
        {
            Models.AnomalyLevel.High => new Color(0.9f, 0.25f, 0.2f, 1f),
            Models.AnomalyLevel.Medium => new Color(0.95f, 0.6f, 0.1f, 1f),
            Models.AnomalyLevel.Low => new Color(0.2f, 0.8f, 0.4f, 1f),
            _ => new Color(0.5f, 0.5f, 0.55f, 1f)
        };

        var levelTag = anomaly.Level switch
        {
            Models.AnomalyLevel.High => "高",
            Models.AnomalyLevel.Medium => "中",
            Models.AnomalyLevel.Low => "低",
            _ => "?"
        };

        var row = new PanelContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 0)
        };

        var rowStyle = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.08f, 0.1f, 0.95f),
            BorderColor = barColor,
            BorderWidthLeft = 3,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 10f,
            ContentMarginTop = 6f,
            ContentMarginRight = 10f,
            ContentMarginBottom = 6f
        };
        row.AddThemeStyleboxOverride("panel", rowStyle);
        _anomalyList.AddChild(row);

        var content = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 2);
        row.AddChild(content);

        var titleRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        content.AddChild(titleRow);

        var levelDot = new Label
        {
            Text = "●",
            SizeFlagsHorizontal = SizeFlags.ShrinkEnd
        };
        levelDot.AddThemeColorOverride("font_color", color);
        levelDot.AddThemeFontSizeOverride("font_size", 14);
        titleRow.AddChild(levelDot);

        var titleText = new Label
        {
            Text = $"【{levelTag}】{anomaly.Title}",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        titleText.AddThemeColorOverride("font_color", color);
        titleText.AddThemeFontSizeOverride("font_size", 13);
        titleText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        titleRow.AddChild(titleText);

        var descText = new Label
        {
            Text = anomaly.Description,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        descText.AddThemeColorOverride("font_color", new Color(0.75f, 0.75f, 0.8f, 1f));
        descText.AddThemeFontSizeOverride("font_size", 12);
        descText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(descText);

        if (!string.IsNullOrWhiteSpace(anomaly.Detail))
        {
            var detailText = new Label
            {
                Text = anomaly.Detail.Trim(),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            detailText.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.55f, 1f));
            detailText.AddThemeFontSizeOverride("font_size", 11);
            detailText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            content.AddChild(detailText);
        }
    }

    /// <summary>在滚动区内横向铺满、文字居中换行（避免 CenterContainer+Label 宽度过窄导致一字一行）。</summary>
    private void _AddCenteredScrollMessage(string message, bool isError, bool useOkIcon = false)
    {
        var margin = new MarginContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 80)
        };
        margin.AddThemeConstantOverride("margin_left", 12);
        margin.AddThemeConstantOverride("margin_right", 12);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        _anomalyList.AddChild(margin);

        var label = new Label
        {
            Text = useOkIcon ? $"✓  {message}" : message,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        label.AddThemeFontSizeOverride("font_size", 13);
        if (isError)
            label.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.5f, 1f));
        else
            label.AddThemeColorOverride("font_color", new Color(0.45f, 0.9f, 0.5f, 1f));

        margin.AddChild(label);
    }

    private void _OnExportPressed()
    {
        if (_result == null) return;

        var history = _result.History;
        var charName = history?.Players.Count > 0 ? _GetCharacterName(history.Players[0].Character) : "未知";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var defaultFileName = $"runhistory_report_{charName}_{timestamp}.txt";

        var dialog = new FileDialog
        {
            FileMode = (global::Godot.FileDialog.FileModeEnum)(int)4, // FILE_MODE_SAVE_FILE
            Access = FileDialog.AccessEnum.Filesystem,
            Filters = new string[] { "*.txt" }
        };

        var desktop = Godot.OS.GetEnvironment("USERPROFILE");
        if (desktop.Length > 0)
            dialog.CurrentPath = desktop + "/Desktop/" + defaultFileName;
        else
            dialog.CurrentPath = defaultFileName;

        dialog.FileSelected += path =>
        {
            try
            {
                System.IO.File.WriteAllText(path, _result.ToExportText());
                Godot.GD.Print($"[RunHistoryAnalyzer] 报告已导出至：{path}");
            }
            catch (System.Exception ex)
            {
                Godot.GD.PushError($"[RunHistoryAnalyzer] 导出失败：{ex.Message}");
            }
            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();

        GetTree().Root.AddChild(dialog);
        dialog.Popup();
    }

    private static string _GetCharacterName(string characterId) => characterId switch
    {
        "CHARACTER.IRONCLAD" => "铁甲战士",
        "CHARACTER.SILENT" => "静默猎手",
        "CHARACTER.DEFECT" => "故障机器人",
        "CHARACTER.NECROMANCER" => "亡灵契约师",
        "CHARACTER.HEXAGUARD" => "储君",
        "MOD.WATCHER" => "观者",
        _ => characterId
    };

    private static StyleBoxFlat _MakeTitleStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.07f, 0.09f, 1f),
            ContentMarginLeft = 8f,
            ContentMarginTop = 8f,
            ContentMarginRight = 8f,
            ContentMarginBottom = 8f
        };
    }

    private static StyleBoxFlat _MakeSeparatorStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(0.18f, 0.18f, 0.22f, 1f)
        };
    }
}
