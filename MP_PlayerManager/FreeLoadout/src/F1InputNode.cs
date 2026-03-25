using Godot;

namespace MP_PlayerManager
{
    /// <summary>
    /// F1 全局输入节点：永远用 _Process 轮询，不阻断游戏输入。
    /// 与 NoClientCheats.InputHandlerNode 完全一致的轮询模式。
    /// </summary>
    public partial class F1InputNode : Node
    {
        public F1InputNode()
        {
            Name = "MPF1Input";
            ProcessMode = ProcessModeEnum.Always;
        }

        public override void _EnterTree()
        {
            SetProcess(true);
        }

        public override void _Process(double delta)
        {
            if (Input.IsKeyPressed(Key.F1) && !_prevDown)
            {
                LoadoutPanel.Toggle();
                _prevDown = true;
            }
            else if (!Input.IsKeyPressed(Key.F1))
            {
                _prevDown = false;
            }
        }

        private bool _prevDown;
    }
}
