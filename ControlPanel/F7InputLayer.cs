using Godot;

namespace ControlPanel;

/// <summary>
/// 全屏透明层，仅响应 F7 切换控制面板。
/// </summary>
public partial class F7InputLayer : Control
{
    public override void _Ready()
    {
        SetProcessInput(true);
        SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _Input(InputEvent ev)
    {
        if (ev is InputEventKey key && key.Pressed && key.Keycode == ControlPanelMod.ToggleKey)
        {
            GetViewport().SetInputAsHandled();
            ControlPanelMod.TogglePanel();
        }
    }
}
