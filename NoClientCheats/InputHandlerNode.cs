using Godot;

namespace NoClientCheats;

/// <summary>
/// 纯轮询式热键处理器。参考 DamageMeter InputHandler 模式：
/// - 不重写 _Input / _UnhandledKeyInput
/// - 不调用 SetInputAsHandled()
/// - 只在 _Process 里用 Input.IsKeyPressed() 轮询，100%可靠
/// </summary>
public partial class InputHandlerNode : Node
{
    public InputHandlerNode()
    {
        Name = "NCCInputHandler";
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _EnterTree()
    {
        SetProcess(true);
    }

    public override void _Ready()
    {
        GD.Print($"[NCCInputHandler] ready, hotkey={NoClientCheatsMod.GetHistoryKeyDisplayName()}");
    }

    public override void _Process(double delta)
    {
        Key key = NoClientCheatsMod.HistoryToggleKey;
        bool down = Input.IsPhysicalKeyPressed(key) || Input.IsKeyPressed(key);
        if (down && !_prevDown)
        {
            NoClientCheatsMod.ToggleHistoryPanel();
        }
        _prevDown = down;
    }

    public override void _ExitTree()
    {
        GD.Print("[NCCInputHandler] exited tree");
    }

    private bool _prevDown;
}
