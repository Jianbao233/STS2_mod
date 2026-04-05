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
        GD.Print($"[NCCInputHandler] _EnterTree, HistoryToggleKey={NoClientCheatsMod.HistoryToggleKey}");
    }

    public override void _Ready()
    {
        GD.Print($"[NCCInputHandler] _Ready, hotkey={NoClientCheatsMod.GetHistoryKeyDisplayName()}, HistoryToggleKey={NoClientCheatsMod.HistoryToggleKey}");
    }

    public override void _Process(double delta)
    {
        // 处理延迟的 Player 刷新（地图阶段作弊检测后 Player 对象尚未加载）
        NoClientCheatsMod.ProcessPendingPlayerRefreshes();

        Key key = NoClientCheatsMod.HistoryToggleKey;
        bool down = Input.IsKeyPressed(key);
        if (down && !_prevDown)
        {
            GD.Print($"[NCCInputHandler] key pressed, toggling. key={key}({(int)key}), display={NoClientCheatsMod.GetHistoryKeyDisplayName()}");
            NoClientCheatsMod.ToggleHistoryPanel();
        }
        _prevDown = down;
    }

    public override void _ExitTree()
    {
        GD.Print("[NCCInputHandler] _ExitTree");
    }

    private bool _prevDown;
}
