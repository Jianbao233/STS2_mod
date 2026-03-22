using Godot;

namespace RunHistoryAnalyzer;

/// <summary>
/// 全局热键：在 ModConfig 中配置的快捷键用于显示/隐藏分析工具栏。
/// </summary>
public partial class RunHistoryAnalyzerHotkey : Node
{
    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey key || !key.Pressed || key.Echo)
            return;

        if (key.Keycode != RunHistoryAnalyzerMod.ToggleToolbarKey)
            return;

        RunHistoryAnalyzerMod.ToggleAnalyzerToolbar();
        GetViewport().SetInputAsHandled();
    }
}
