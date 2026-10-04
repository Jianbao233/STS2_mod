using System;
using Godot;
using SpireInstrument.Audio;

namespace SpireInstrument.Ui;

/// <summary>
/// 浮窗宿主：把演奏面板挂到场景树根上（独立 CanvasLayer，不随任何界面销毁）。
///
/// 选这条路而不是 Harmony 补丁的原因：mod 初始化时游戏场景可能还没建好，
/// 用 SceneTree.Root + CallDeferred 即可稳定挂载，且不需要补丁任何游戏方法。
/// </summary>
public partial class PanelHost : CanvasLayer
{
    private const int LayerIndex = 90;   // 高于游戏常规 UI，低于系统弹窗

    private static PanelHost? _s_instance;

    private InstrumentPanel? _panel;
    private InstrumentAudio? _audio;
    private Songs.SongPlayer? _player;
    private Net.InstrumentNet? _net;

    /// <summary>供无头自检使用。</summary>
    public static PanelHost? Instance => _s_instance;

    /// <summary>供自检使用：音频层（读声部数）。</summary>
    public InstrumentAudio? Audio => _audio;

    /// <summary>供无头自检使用：返回一行可解析的探测串。</summary>
    public string SelfTestProbe() => _panel != null ? _panel.SelfTestProbe() : "panel=missing";

    /// <summary>供截图巡游使用：把面板切到指定尺寸/音阶模式/音色。</summary>
    public void SelfTestConfigure(Core.PanelSize size, bool snap, string inst)
        => _panel?.SelfTestConfigure(size, snap, inst);

    /// <summary>供截图巡游使用：按下几个键，让截图里有"按下态 + 最近演奏"。</summary>
    public void SelfTestPlayDemo() => _panel?.SelfTestPlayDemo();

    /// <summary>供截图巡游使用：收起面板（验证右下角音符小图标）。</summary>
    public void SelfTestCollapse() => _panel?.SetPanelVisible(false);

    /// <summary>供自检使用：起一个延音音色（循环采样）。</summary>
    public string SelfTestSustainStart() => _panel?.SelfTestSustainStart() ?? "sustain=missing";

    /// <summary>供自检使用：释放延音音色。</summary>
    public string SelfTestSustainRelease() => _panel?.SelfTestSustainRelease() ?? "sustain=missing";

    /// <summary>供自检使用：当前声部数。</summary>
    public int SelfTestVoices() => _panel?.SelfTestVoices() ?? -1;

    /// <summary>供自检使用：延音开始前的基线声部数。</summary>
    public int SelfTestSustainBaseline() => _panel?.SelfTestSustainBaseline() ?? -1;

    /// <summary>供自检使用：开始播放内置曲目。</summary>
    public string SelfTestSongStart() => _panel?.SelfTestSongStart() ?? "song=missing";

    /// <summary>供自检使用：停止播放并汇报调度统计。</summary>
    public string SelfTestSongStop() => _panel?.SelfTestSongStop() ?? "song=missing";

    /// <summary>供自检/截图使用：切到跟弹模式并开始播放。</summary>
    public string SelfTestLearnStart() => _panel?.SelfTestLearnStart() ?? "learn=missing";

    /// <summary>供自检使用：强制英文并重跑整树翻译，返回关键控件文案。</summary>
    public string SelfTestLocalizeProbe() => _panel?.SelfTestLocalizeProbe() ?? "localize=missing";

    /// <summary>供自检使用：视口尺寸与 UI 缩放系数。</summary>
    public string SelfTestUiScale() => _panel?.SelfTestUiScale() ?? "ui=missing";

    /// <summary>供自检使用：压力边界（密集发声 / 缓存淘汰）。</summary>
    public string SelfTestStress() => _panel?.SelfTestStress() ?? "stress=missing";

    /// <summary>供自检使用：播一首短曲以覆盖整曲收尾路径。</summary>
    public string SelfTestShortSong() => _panel?.SelfTestShortSong() ?? "shortsong=missing";

    /// <summary>供自检使用：短曲是否已自然播完。</summary>
    public string SelfTestShortSongState() => _panel?.SelfTestShortSongState() ?? "shortsong=missing";

    public static void Install()
    {
        if (_s_instance != null && GodotObject.IsInstanceValid(_s_instance)) return;

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            GD.PushError($"{SpireInstrumentMod.LogTag} SceneTree unavailable; cannot install panel host.");
            return;
        }

        var host = new PanelHost { Name = "SpireInstrumentHost", Layer = LayerIndex };
        _s_instance = host;

        if (tree.Root != null)
        {
            tree.Root.CallDeferred(Node.MethodName.AddChild, host);
            return;
        }

        // 极端情况下根节点还没就绪：逐帧重试，直到挂上为止。
        void Retry()
        {
            if (tree.Root == null) return;
            tree.ProcessFrame -= Retry;
            tree.Root.CallDeferred(Node.MethodName.AddChild, host);
        }
        tree.ProcessFrame += Retry;
    }

    public override void _Ready()
    {
        _audio = new InstrumentAudio { Name = "InstrumentAudio" };
        AddChild(_audio);
        _audio.SetVolume(Core.ModSettings.Volume);   // 应用存盘里的音量

        _player = new Songs.SongPlayer(_audio) { Name = "SongPlayer" };
        AddChild(_player);

        _net = new Net.InstrumentNet();

        _panel = new InstrumentPanel(_audio, _player, _net);
        AddChild(_panel);

        GD.Print($"{SpireInstrumentMod.LogTag} panel host ready. {Core.ModSettings.Describe()}");
    }

    /// <summary>每帧把"改了但还没落盘"的设置刷下去（防抖在 ModSettings 里）。</summary>
    public override void _Process(double delta)
    {
        Core.ModSettings.FlushIfDirty();
    }

    public override void _Input(InputEvent @event)
    {
        if (_panel == null) return;

        var summon = Core.ModSettings.SummonKey;
        if (@event is InputEventKey key && key.Pressed && !key.Echo &&
            (key.Keycode == summon || key.PhysicalKeycode == summon))
        {
            _panel.Toggle();
            GetViewport()?.SetInputAsHandled();
            return;
        }

        if (_panel.Visible)
        {
            if (@event is InputEventKey k)
            {
                // 浮窗打开时独占键盘输入，避免与游戏内快捷键冲突
                if (_panel.HandleKey(k)) GetViewport()?.SetInputAsHandled();
            }
        }
    }

    public override void _ExitTree()
    {
        if (_s_instance == this) _s_instance = null;
    }
}
