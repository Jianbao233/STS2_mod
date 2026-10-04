using System;
using System.Collections.Generic;
using Godot;
using SpireInstrument.Audio;
using SpireInstrument.Core;

namespace SpireInstrument.Ui;

/// <summary>
/// 演奏浮窗（UI 由代码构建，便于快速迭代；M2 再评估是否改回复用原生 .tscn 零件）。
///
/// 版式对齐已定稿的 HTML 原型：
///   标题栏（可拖动 + 尺寸档 + 收起/关闭）
///   ├ 左栏：音阶吸附开关 + 乐器音色列表
///   ├ 中栏：音符读数 + 琴键（五声 13 键 / 半音 25 键）
///   └ 右栏：音量 + 八度 + 状态
/// </summary>
public partial class InstrumentPanel : Control
{
    // ---------- 配色（对齐原型：暗石 + 鎏金） ----------
    private static readonly Color ColBg = new(0.106f, 0.122f, 0.149f, 0.97f);
    private static readonly Color ColBg2 = new(0.133f, 0.153f, 0.184f, 1f);
    private static readonly Color ColGold = new(0.788f, 0.663f, 0.380f);
    private static readonly Color ColGoldBright = new(0.957f, 0.875f, 0.659f);
    private static readonly Color ColInk = new(0.914f, 0.871f, 0.769f);
    private static readonly Color ColInkDim = new(0.631f, 0.576f, 0.478f);
    private static readonly Color ColKeyNormal = new(0.953f, 0.918f, 0.839f);
    private static readonly Color ColKeyHover = new(0.984f, 0.945f, 0.855f);
    private static readonly Color ColKeyDown = new(0.969f, 0.902f, 0.706f);
    private static readonly Color ColKeyBlack = new(0.204f, 0.227f, 0.267f);

    private readonly InstrumentAudio _audio;
    private readonly Songs.SongPlayer _player;
    private readonly Net.InstrumentNet _net;
    private readonly Dictionary<(ulong Sender, int Midi), VoiceHandle> _remoteVoices = new();

    // ---- 队友分轨（按 senderId）----
    private readonly Dictionary<ulong, int> _peerNotes = new();
    private readonly HashSet<ulong> _mutedPeers = new();
    private readonly Dictionary<ulong, Button> _peerButtons = new();
    private HBoxContainer _peerRow = null!;

    private PanelContainer _window = null!;
    private Control _root = null!;
    private PanelContainer _header = null!;
    private HBoxContainer _body = null!;
    private Control _keyStage = null!;
    private PanelContainer _keyWrap = null!;
    private Label _keyHint = null!;
    private HBoxContainer _miniRow = null!;
    private VBoxContainer _instrumentList = null!;
    private Label _readout = null!;
    private Label _heldLabel = null!;
    private Label _statusLabel = null!;
    private Label _voiceLabel = null!;
    private Label _ribbon = null!;
    private Label _octaveValue = null!;
    private Label _statusInfo = null!;
    private readonly List<string> _recent = new();
    private HSlider _volumeSlider = null!;
    private HSlider _octaveSlider = null!;
    private HSlider _audienceVolume = null!;
    private CheckButton _muteAudience = null!;
    private Label _netInfo = null!;
    private Button _dock = null!;

    // ---- 曲目 / 跟弹 ----
    private OptionButton _songPicker = null!;
    private Button _playButton = null!;
    private ProgressBar _progress = null!;
    private Label _songInfo = null!;
    private Label _scoreLabel = null!;
    private readonly List<Button> _modeButtons = new();
    private Control _lane = null!;
    private readonly Dictionary<int, ColorRect> _laneBlocks = new();
    private readonly List<(string Id, string Name, string? FilePath)> _songEntries = new();
    private Songs.Song? _loadedSong;

    private readonly Dictionary<int, Button> _keyButtons = new();
    private readonly Dictionary<string, Button> _instButtons = new();
    private readonly HashSet<int> _heldMidi = new();
    private readonly Dictionary<int, VoiceHandle> _heldVoices = new();
    private VoiceHandle? _sustainProbe;
    private int _sustainBaseline;

    /// <summary>
    /// 随视口推导的 UI 缩放（1080p 为基准 1.0，上限 2.0）。
    /// 面板与字号原本是绝对像素，在 2K/4K 屏上会显得又小又难读 —— 统一按这个系数放大。
    /// </summary>
    private static float _ui = 1f;

    /// <summary>把基准字号换算成当前缩放下的字号（静态：静态辅助方法也要用）。</summary>
    private static int Fs(int baseSize) => Mathf.RoundToInt(baseSize * _ui);

    /// <summary>供自检使用：视口尺寸与 UI 缩放系数（排查"面板在高分屏上偏小"用）。</summary>
    public string SelfTestUiScale()
    {
        var vp = GetViewportRect().Size;
        return "ui viewport=" + (int)vp.X + "x" + (int)vp.Y + " scale=" + _ui.ToString("F2") +
               " panel=" + (int)_window.Size.X + "x" + (int)_window.Size.Y +
               " fontBody=" + Fs(11) + " fontTitle=" + Fs(17) +
               " keys=" + _keyLayout.Count + " keyStage=" + (int)_keyStage.Size.X + "x" + (int)_keyStage.Size.Y +
               " lane=" + (int)_lane.Size.X + "x" + (int)_lane.Size.Y +
               " keyWrapVisible=" + _keyWrap.Visible + " body=" + (int)_body.Size.X + "x" + (int)_body.Size.Y;
    }

    private void ComputeUiScale()
    {
        var vp = GetViewportRect().Size;
        // 【已回退】整体系数缩放会让自动换行标签的最小宽度膨胀，把三栏挤到零宽后逐字换行，
        // 面板高度实测涨到 4459–5548px、键盘区宽度塌成 0（键盘不可见）。
        // 因此暂不启用整体缩放，保持既有布局；字号放大需改为"逐处显式调整 + 关掉状态栏自动换行"。
        // 缩放由**设置**决定（默认 130%），而不是按视口猜 ——
        // 实测在 2K 窗口下游戏逻辑视口仍是 ~1920，按视口推出来正好 1.0 等于没放大。
        _ui = Math.Clamp(Core.ModSettings.UiScale, 1f, 2f);
    }
    private bool _dragging;
    private Vector2 _dragOffset;
    private Songs.PlayMode _mode = Songs.PlayMode.Free;
    private Core.PanelSize _appliedSize = Core.PanelSize.Standard;
    private int _appliedScale;

    // ---- 录音 / 回放 ----
    private readonly List<Songs.SongNote> _recording = new();
    private bool _isRecording;
    private double _recStart;
    private Button _recButton = null!;
    private Button _playRecButton = null!;
    private Label _recLabel = null!;

    // ---- 节拍器 ----
    private CheckButton _metroToggle = null!;
    private HSlider _metroBpm = null!;
    private Label _metroLabel = null!;
    private bool _metroOn;
    private double _metroNext;
    private int _metroBeat;

    public InstrumentPanel(InstrumentAudio audio, Songs.SongPlayer player, Net.InstrumentNet net)
    {
        _audio = audio;
        _player = player;
        _net = net;
        _net.NoteReceived += OnRemoteNote;
        _net.SongReceived += OnRemoteSong;
    }

    /// <summary>远端音符：本机合成播放（只传音符编号，不传音频）。</summary>
    private void OnRemoteNote(Net.NoteMessage message, ulong senderId)
    {
        var def = InstrumentLibrary.Get(ModSettings.InstrumentId);
        int midi = message.KeyIndex;
        // 观众侧：按"队友演奏音量"缩放；静音时只做视觉提示，不出声
        // 分轨统计与静音：每位队友单独计数、可单独静音
        _peerNotes.TryGetValue(senderId, out int seen);
        _peerNotes[senderId] = seen + 1;
        RefreshPeerRow();

        float audience = ModSettings.AudienceVolume;
        if (_mutedPeers.Contains(senderId)) audience = 0f;
        // 观众侧礼让：我在战斗房间里时自动不出声（只影响"我听别人"，自己弹不受影响）
        if (ModSettings.MuteAudienceInCombat && IsInCombat()) audience = 0f;
        float vel = message.Velocity / 127f * audience;
        var key = (senderId, midi);

        if (message.Kind == Net.NoteMessage.KindOn)
        {
            if (_remoteVoices.TryGetValue(key, out var old) && def.IsSustain) _audio.Release(old);
            if (vel > 0.01f)
            {
                var handle = _audio.PlayPreview(midi, Math.Max(0.1f, vel), def, 1.2);
                if (handle != null && def.IsSustain) _remoteVoices[key] = handle;
            }

            int slot = MusicScale.MidiToSlot(midi, ModSettings.ScaleSnap, ModSettings.Octave);
            FlashRemote(slot);
            PushRecent(midi);
        }
        else if (_remoteVoices.TryGetValue(key, out var handle))
        {
            _audio.Release(handle);
            _remoteVoices.Remove(key);
        }
    }

    /// <summary>远端曲谱：按同一个起拍时刻本机播放（两端齐奏，而不是逐音符转发）。</summary>
    private void OnRemoteSong(Net.SongSyncMessage message, ulong senderId)
    {
        var song = Net.InstrumentNet.ToSong(message);
        Songs.SongBuilder.FoldToKeyboard(song, ModSettings.ScaleSnap, ModSettings.Octave);
        _loadedSong = song;
        // 起拍 = 发送方给的 lead + 本机同步补偿（补偿跨地域延迟；默认 0）
        double lead = message.LeadSeconds + ModSettings.SyncCompensationSeconds;
        _player.Start(song, Songs.PlayMode.Auto, ModSettings.InstrumentId, lead);
        UpdateStatus(Core.Strings.Pick($"队友开始演奏《{song.Name}》", $"Teammate is playing \u201c{song.Name}\u201d"));
    }

    /// <summary>
    /// 是否处于战斗房间（观众侧自动静音用）。
    /// 用 <c>RunState.CurrentRoom.RoomType</c> 判定 —— 房间类型与地图点类型是两回事
    /// （例如"?"事件里选"打一架"会把房间类型变成 Monster）。
    /// </summary>
    private static bool IsInCombat()
    {
        try
        {
            // RunManager.State 不是公开成员；公开可用的是 DebugOnlyGetState()（名字带 Debug 但就是取当前 RunState）。
            var state = MegaCrit.Sts2.Core.Runs.RunManager.Instance?.DebugOnlyGetState();
            var room = state?.CurrentRoom;
            return room != null && room.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Monster;
        }
        catch { return false; }
    }

    /// <summary>远端音符高亮（蓝色调制，和本机的金色区分开）。</summary>
    private void FlashRemote(int slot)
    {
        if (!_keyButtons.TryGetValue(slot, out var b) || !GodotObject.IsInstanceValid(b)) return;
        b.Modulate = new Color(0.62f, 0.80f, 1.0f);
        var timer = GetTree()?.CreateTimer(0.18);
        if (timer == null) return;
        timer.Timeout += () =>
        {
            if (GodotObject.IsInstanceValid(b) && !_heldMidi.Contains(MusicScale.SlotToMidi(slot, 0, ModSettings.ScaleSnap, ModSettings.Octave)))
                b.Modulate = Colors.White;
        };
    }

    public override void _Ready()
    {
        SetAnchorsPreset(LayoutPreset.FullRect);
        Size = GetViewportRect().Size;      // CanvasLayer 下不会自动获得父尺寸，显式给
        MouseFilter = MouseFilterEnum.Ignore;

        // 必须在建 UI 之前接好语言探测：状态行/联机行在 BuildWindow 里就会读语言，
        // 而 Strings.UseEnglish 有缓存 —— 顺序错了会把"英文"缓存下来（实测踩过）。
        Localizer.HookLocale();
        ComputeUiScale();
        BuildWindow();
        BuildDock();
        RebuildKeyboard();
        ApplySize(ModSettings.Size, reposition: true);
        SetPanelVisible(ModSettings.PanelVisible);
        UpdateReadout();

        // 恢复上次的演奏模式（默认 Free）
        if (System.Enum.TryParse<Songs.PlayMode>(Core.ModSettings.PlayMode, out var savedMode) &&
            savedMode != Songs.PlayMode.Free)
        {
            SetMode(savedMode);
        }

        // 英文环境：把整棵控件树的静态文案翻一遍（中文环境直接返回）
        Localizer.LocalizeTree(this);
        UpdateStatusInfo();
        UpdateNetInfo();
    }

    /// <summary>视口尺寸变化时（窗口缩放/分辨率切换）重排琴键与音符图标。</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationResized && IsInsideTree())
        {
            Size = GetViewportRect().Size;
            LayoutKeys();
            LayoutDock();
        }
    }

    // ==================================================================
    //  构建
    // ==================================================================

    private void BuildWindow()
    {
        _window = new PanelContainer
        {
            Name = "Window",
            MouseFilter = MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(980, 560),
        };
        _window.AddThemeStyleboxOverride("panel", MakePanelStyle(ColBg, ColGold, 2, 12, 10));
        _window.SetAnchorsPreset(LayoutPreset.TopLeft);
        AddChild(_window);

        _root = new VBoxContainer { Name = "Root" };
        _root.AddThemeConstantOverride("separation", 8);
        _window.AddChild(_root);

        BuildHeader();

        _body = new HBoxContainer { Name = "Body" };
        _body.AddThemeConstantOverride("separation", 10);
        _body.SizeFlagsVertical = SizeFlags.ExpandFill;
        _root.AddChild(_body);

        BuildLeftColumn();
        BuildCenterColumn();
        BuildRightColumn();

        BuildKeyboardSection();
        BuildKeyHint();
        BuildMiniRow();

        _statusLabel = new Label { Text = "就绪", };
        _statusLabel.AddThemeColorOverride("font_color", ColInkDim);
        _statusLabel.AddThemeFontSizeOverride("font_size", Fs(11));
        _root.AddChild(_statusLabel);
    }

    private void BuildHeader()
    {
        _header = new PanelContainer { Name = "Header", MouseFilter = MouseFilterEnum.Stop };
        _header.AddThemeStyleboxOverride("panel", MakePanelStyle(ColBg2, ColGold, 1, 8, 6));
        _root.AddChild(_header);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        _header.AddChild(row);

        var title = new Label { Text = "演奏" };
        title.AddThemeColorOverride("font_color", ColGoldBright);
        title.AddThemeFontSizeOverride("font_size", Fs(17));
        row.AddChild(title);

        var dep = new Label { Text = "依赖 RitsuLib" };
        dep.AddThemeColorOverride("font_color", ColInkDim);
        dep.AddThemeFontSizeOverride("font_size", Fs(10));
        row.AddChild(dep);

        row.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        row.AddChild(MakeSizeButton("迷你", PanelSize.Mini));
        row.AddChild(MakeSizeButton("标准", PanelSize.Standard));
        row.AddChild(MakeSizeButton("大", PanelSize.Large));

        var collapse = new Button { Text = "—", TooltipText = "收起为音符图标（P）", CustomMinimumSize = new Vector2(30, 24) };
        collapse.Pressed += () => SetPanelVisible(false);
        row.AddChild(collapse);

        var close = new Button { Text = "✕", TooltipText = "关闭（Esc）", CustomMinimumSize = new Vector2(30, 24) };
        close.Pressed += () => SetPanelVisible(false);
        row.AddChild(close);

        _header.GuiInput += OnHeaderGuiInput;
    }

    private Button MakeSizeButton(string text, PanelSize size)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(46, 24) };
        b.Pressed += () => ApplySize(size, reposition: true);
        return b;
    }

    private void BuildLeftColumn()
    {
        var left = new VBoxContainer { CustomMinimumSize = new Vector2(210, 0) };
        left.AddThemeConstantOverride("separation", 6);
        _body.AddChild(left);

        var snapRow = new HBoxContainer();
        var snapLabel = new Label { Text = "音阶吸附" };
        snapLabel.AddThemeColorOverride("font_color", ColInk);
        snapLabel.AddThemeFontSizeOverride("font_size", Fs(12));
        snapRow.AddChild(snapLabel);
        snapRow.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        var snap = new CheckButton { ButtonPressed = ModSettings.ScaleSnap, TooltipText = "五声音阶：怎么按都不会难听" };
        snap.Toggled += pressed =>
        {
            ModSettings.ScaleSnap = pressed;
            RebuildKeyboard();
            UpdateStatusInfo();
            UpdateStatus(pressed ? "音阶吸附已开：怎么按都不会难听" : "半音模式：完整 12 音，按错会难听");
        };
        snapRow.AddChild(snap);
        left.AddChild(snapRow);

        left.AddChild(MakeSectionTitle("乐器 / 音色"));

        _instrumentList = new VBoxContainer();
        _instrumentList.AddThemeConstantOverride("separation", 4);
        left.AddChild(_instrumentList);

        foreach (var inst in InstrumentLibrary.Items)
        {
            var b = new Button
            {
                Text = $"  {inst.Name}",
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 28),
            };
            b.Pressed += () => SelectInstrument(inst.Id);
            _instrumentList.AddChild(b);
            _instButtons[inst.Id] = b;
        }

        SelectInstrument(ModSettings.InstrumentId, silent: true);
    }

    /// <summary>曲目区：选曲 / 播放 / 模式 / 进度 / 跟弹成绩。放在中栏（左栏乐器列表已经很高）。</summary>
    private void BuildSongSection(VBoxContainer host)
    {
        host.AddChild(MakeSectionTitle("曲目"));

        _songPicker = new OptionButton { CustomMinimumSize = new Vector2(0, 28) };
        _songPicker.ItemSelected += _ => OnSongPicked();
        host.AddChild(_songPicker);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);
        _playButton = new Button
        {
            Text = "▶ 播放",
            CustomMinimumSize = new Vector2(0, 28),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _playButton.Pressed += TogglePlay;
        row.AddChild(_playButton);

        var folder = new Button
        {
            Text = "📂",
            TooltipText = "打开歌曲文件夹：把 .mid 放进去就能导入（不放也行，内置 6 首）",
            CustomMinimumSize = new Vector2(34, 28),
        };
        folder.Pressed += OpenSongFolder;
        row.AddChild(folder);
        host.AddChild(row);

        _progress = new ProgressBar
        {
            MinValue = 0, MaxValue = 1, Step = 0.001, ShowPercentage = false,
            CustomMinimumSize = new Vector2(0, 8),
        };
        host.AddChild(_progress);

        var modes = new HBoxContainer();
        modes.AddThemeConstantOverride("separation", 4);
        foreach (var (mode, label) in new[]
                 {
                     (Songs.PlayMode.Free, "自由弹"),
                     (Songs.PlayMode.Auto, "自动演奏"),
                     (Songs.PlayMode.Learn, "跟弹"),
                 })
        {
            var b = new Button
            {
                Text = label,
                CustomMinimumSize = new Vector2(0, 26),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ToggleMode = true,
                ButtonPressed = mode == Songs.PlayMode.Free,
            };
            var captured = mode;
            b.Pressed += () => SetMode(captured);
            modes.AddChild(b);
            _modeButtons.Add(b);
        }
        host.AddChild(modes);

        // 节拍器：给自由弹一个节奏底座（用鼓组的踩镲音色打点，首拍重音）
        var metroRow = new HBoxContainer();
        metroRow.AddThemeConstantOverride("separation", 6);
        _metroToggle = new CheckButton { Text = Core.Strings.Pick("节拍器", "Metronome") };
        _metroToggle.Toggled += on =>
        {
            _metroOn = on;
            _metroBeat = 0;
            _metroNext = 0;   // 立即从第一拍开始
            UpdateMetroLabel();
        };
        metroRow.AddChild(_metroToggle);

        _metroBpm = new HSlider
        {
            MinValue = 40, MaxValue = 200, Step = 5, Value = 90,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = Core.Strings.Pick("每分钟拍数", "Beats per minute"),
        };
        _metroBpm.ValueChanged += v => { UpdateMetroLabel(); };
        metroRow.AddChild(_metroBpm);

        _metroLabel = new Label { Text = "90" };
        _metroLabel.AddThemeColorOverride("font_color", ColInkDim);
        _metroLabel.AddThemeFontSizeOverride("font_size", Fs(11));
        metroRow.AddChild(_metroLabel);
        host.AddChild(metroRow);

        // 录音 / 回放：录下自己的演奏，回放时复用曲目播放器（录音 → 一首 Song）
        var recRow = new HBoxContainer();
        recRow.AddThemeConstantOverride("separation", 6);
        _recButton = new Button
        {
            Text = Core.Strings.Pick("● 录音", "● Rec"),
            ToggleMode = true,
            CustomMinimumSize = new Vector2(0, 26),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        _recButton.Toggled += on => ToggleRecording(on);
        recRow.AddChild(_recButton);

        _playRecButton = new Button
        {
            Text = Core.Strings.Pick("▶ 回放录音", "▶ Replay"),
            CustomMinimumSize = new Vector2(0, 26),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Disabled = true,
        };
        _playRecButton.Pressed += ReplayRecording;
        recRow.AddChild(_playRecButton);

        _recLabel = new Label { Text = "—" };
        _recLabel.AddThemeColorOverride("font_color", ColInkDim);
        _recLabel.AddThemeFontSizeOverride("font_size", Fs(10));
        recRow.AddChild(_recLabel);
        host.AddChild(recRow);

        _songInfo = new Label { Text = "未选择曲目", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _songInfo.AddThemeColorOverride("font_color", ColInkDim);
        _songInfo.AddThemeFontSizeOverride("font_size", Fs(10));
        host.AddChild(_songInfo);

        _scoreLabel = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _scoreLabel.AddThemeColorOverride("font_color", ColGold);
        _scoreLabel.AddThemeFontSizeOverride("font_size", Fs(11));
        host.AddChild(_scoreLabel);

        RefreshSongList();
    }

    private void RefreshSongList()
    {
        _songEntries.Clear();
        _songPicker.Clear();

        foreach (var def in Songs.SongLibrary.Items)
        {
            _songEntries.Add((def.Id, def.Name, null));
            _songPicker.AddItem(def.Name);
        }

        foreach (var (path, name) in Songs.SongFolder.ListMidiFiles())
        {
            _songEntries.Add(("file:" + path, name, path));
            _songPicker.AddItem("🎵 " + name);
        }

        // 恢复上次选中的曲目（找不到就回到第一首）
        int restore = 0;
        string last = Core.ModSettings.LastSongId;
        if (!string.IsNullOrEmpty(last))
        {
            for (int i = 0; i < _songEntries.Count; i++)
            {
                if (_songEntries[i].Id == last) { restore = i; break; }
            }
        }
        if (_songEntries.Count > 0)
        {
            _songPicker.Selected = restore;
            OnSongPicked();
        }
    }

    private void OnSongPicked()
    {
        int idx = _songPicker.Selected;
        if (idx < 0 || idx >= _songEntries.Count) return;

        _player.Stop();
        var entry = _songEntries[idx];
        Core.ModSettings.LastSongId = entry.Id;   // 记住选择，下次进游戏自动恢复
        Songs.Song? song = null;

        if (entry.FilePath != null)
        {
            var parsed = Songs.SongFolder.LoadFile(entry.FilePath);
            if (parsed.Success)
            {
                song = parsed.Song;
                if (ModSettings.Accompany) Songs.SongBuilder.AddAccompaniment(song!, 0.5);
            }
            else
            {
                _songInfo.Text = Core.Strings.Pick($"导入失败：{parsed.Error}", $"Import failed: {parsed.Error}");
                _loadedSong = null;
                return;
            }
        }
        else
        {
            var def = Songs.SongLibrary.Get(entry.Id);
            if (def != null) song = Songs.SongBuilder.FromDefinition(def, ModSettings.Accompany);
        }

        if (song == null) { _loadedSong = null; return; }

        Songs.SongBuilder.FoldToKeyboard(song, ModSettings.ScaleSnap, ModSettings.Octave);
        _loadedSong = song;

        var notes = new List<int>();
        foreach (var n in song.Notes) if (!n.Bass) notes.Add(n.Midi);
        notes.Sort();
        string range = notes.Count > 0
            ? $"{MusicScale.NoteName(notes[0])} ~ {MusicScale.NoteName(notes[notes.Count - 1])}"
            : "—";

        _songInfo.Text = Core.PanelText.SongInfo(song.MelodyCount, song.Duration, range, song.Snapped, song.Folded, song.Imported, Core.Strings.UseEnglish);
    }

    private void TogglePlay()
    {
        if (_player.IsPlaying)
        {
            _player.Stop();
            _playButton.Text = "▶ 播放";
            ClearLaneBlocks();
            return;
        }

        if (_loadedSong == null) OnSongPicked();
        if (_loadedSong == null) return;

        // 每次播放都按当前音阶/八度重折一次（玩家可能中途改过）
        Songs.SongBuilder.FoldToKeyboard(_loadedSong, ModSettings.ScaleSnap, ModSettings.Octave);
        const double lead = 0.45;
        _player.Start(_loadedSong, _mode, ModSettings.InstrumentId, lead);
        _playButton.Text = "■ 停止";

        // 自动演奏时把曲谱广播出去：队友按同一个起拍时刻本机播放 ⇒ 齐奏
        if (_mode == Songs.PlayMode.Auto) _net.SendSong(_loadedSong, (float)lead);
    }

    private void SetMode(Songs.PlayMode mode)
    {
        if (_player.IsPlaying)
        {
            _player.Stop();
            _playButton.Text = "▶ 播放";
        }
        ClearLaneBlocks();

        _mode = mode;
        Core.ModSettings.PlayMode = mode.ToString();   // 记住模式
        for (int i = 0; i < _modeButtons.Count; i++)
        {
            _modeButtons[i].ButtonPressed = i == (int)mode;
        }
        _lane.Visible = mode == Songs.PlayMode.Learn;
        _scoreLabel.Text = "";
        UpdateStatus(mode switch
        {
            Songs.PlayMode.Auto => "自动演奏：选好曲目按播放，mod 会把整曲弹出来",
            Songs.PlayMode.Learn => "跟弹引导：照着下落的方块按键，漏按会由辅助补音兜底",
            _ => "自由弹：随便按",
        });
    }

    private void OpenSongFolder()
    {
        Songs.SongFolder.EnsureExists();
        string path = Songs.SongFolder.AbsolutePath;
        try { OS.ShellOpen(path); } catch { /* 打不开就只提示路径 */ }
        UpdateStatus(Core.Strings.Pick($"歌曲文件夹：{path}", $"Songs folder: {path}"));
    }

    private void ClearLaneBlocks()
    {
        foreach (var kv in _laneBlocks)
        {
            if (GodotObject.IsInstanceValid(kv.Value)) kv.Value.QueueFree();
        }
        _laneBlocks.Clear();
    }

    /// <summary>跟弹下落轨道：把未来 3 秒内该按的音符画成方块。</summary>
    private void UpdateLane()
    {
        var song = _player.Current;
        bool active = _mode == Songs.PlayMode.Learn && _player.IsPlaying && song != null;
        _lane.Visible = active;
        if (!active)
        {
            if (_laneBlocks.Count > 0) ClearLaneBlocks();
            return;
        }

        double t = _player.Position;
        const double window = 3.0;
        float w = _lane.Size.X, h = _lane.Size.Y;
        var alive = new HashSet<int>();

        for (int i = 0; i < song!.Notes.Count; i++)
        {
            var n = song.Notes[i];
            if (n.Bass || n.State != Songs.NoteState.Pending) continue;
            double dt = n.Time - t;
            if (dt > window || dt < -0.3) continue;
            if (!_slotLayout.TryGetValue(n.Slot, out var layout)) continue;

            alive.Add(i);
            if (!_laneBlocks.TryGetValue(i, out var block) || !GodotObject.IsInstanceValid(block))
            {
                block = new ColorRect { Color = ColGold, MouseFilter = MouseFilterEnum.Ignore };
                _lane.AddChild(block);
                _laneBlocks[i] = block;
            }

            float y = (float)((1.0 - dt / window) * (h - 14));
            block.Position = new Vector2(layout.X * w, Math.Max(0, y));
            block.Size = new Vector2(Math.Max(6f, layout.W * w), 12);
        }

        if (_laneBlocks.Count > alive.Count)
        {
            var stale = new List<int>();
            foreach (var kv in _laneBlocks) if (!alive.Contains(kv.Key)) stale.Add(kv.Key);
            foreach (int key in stale)
            {
                if (GodotObject.IsInstanceValid(_laneBlocks[key])) _laneBlocks[key].QueueFree();
                _laneBlocks.Remove(key);
            }
        }
    }

    private void BuildCenterColumn()
    {
        var center = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        center.AddThemeConstantOverride("separation", 8);
        _body.AddChild(center);

        var readoutPanel = new PanelContainer();
        readoutPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(ColBg2, ColGold, 1, 8, 8));
        center.AddChild(readoutPanel);

        var readoutRow = new HBoxContainer();
        readoutRow.AddThemeConstantOverride("separation", 14);
        readoutPanel.AddChild(readoutRow);

        _readout = new Label { Text = "—", CustomMinimumSize = new Vector2(110, 0) };
        _readout.AddThemeColorOverride("font_color", ColGoldBright);
        _readout.AddThemeFontSizeOverride("font_size", Fs(24));
        readoutRow.AddChild(_readout);

        _heldLabel = new Label { Text = "按下琴键开始演奏", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _heldLabel.AddThemeColorOverride("font_color", ColInkDim);
        _heldLabel.AddThemeFontSizeOverride("font_size", Fs(13));
        readoutRow.AddChild(_heldLabel);

        _voiceLabel = new Label { Text = "声部 0/24" };
        _voiceLabel.AddThemeColorOverride("font_color", ColInkDim);
        _voiceLabel.AddThemeFontSizeOverride("font_size", Fs(11));
        readoutRow.AddChild(_voiceLabel);

        // 最近演奏：让中栏有内容，也让玩家看见自己刚弹了什么
        center.AddChild(MakeSectionTitle("最近演奏"));
        _ribbon = new Label
        {
            Text = "—",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsVertical = SizeFlags.ExpandFill,
        };
        _ribbon.AddThemeColorOverride("font_color", ColGold);
        _ribbon.AddThemeFontSizeOverride("font_size", Fs(13));
        center.AddChild(_ribbon);

        BuildSongSection(center);
    }

    /// <summary>
    /// 通栏琴键区：键盘占满窗口宽度（而不是挤在中栏里），
    /// 这样键宽接近真钢琴比例 —— 窄高的"管风琴管"观感就是宽度不够造成的。
    /// 上半部分留给跟弹用的下落轨道。
    /// </summary>
    private void BuildKeyboardSection()
    {
        var keyWrap = new PanelContainer { Name = "KeyboardSection" };
        keyWrap.AddThemeStyleboxOverride("panel", MakePanelStyle(ColBg2, new Color(0.18f, 0.20f, 0.24f), 1, 10, 10));
        _root.AddChild(keyWrap);
        _keyWrap = keyWrap;

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 6);
        keyWrap.AddChild(column);

        // 跟弹下落轨道（仅跟弹模式显示）
        _lane = new Control { Name = "Lane", CustomMinimumSize = new Vector2(0, Fs(84)), ClipContents = true };
        _lane.AddThemeConstantOverride("separation", 0);
        column.AddChild(_lane);

        _keyStage = new Control { Name = "KeyStage" };
        _keyStage.Resized += LayoutKeys;
        column.AddChild(_keyStage);
    }

    /// <summary>通栏键位提示（放在键盘下方）。</summary>
    private void BuildKeyHint()
    {
        var hint = new Label
        {
            Name = "KeyHint",
            Text = "键位：下排 Z S X D C V G B H N J M , ／ 上排 Q 2 W 3 E R 5 T 6 Y 7 U I　·　[ ] 移调　·　P 收起",
        };
        hint.AddThemeColorOverride("font_color", ColInkDim);
        hint.AddThemeFontSizeOverride("font_size", Fs(10));
        _root.AddChild(hint);
        _keyHint = hint;
    }

    private void BuildRightColumn()
    {
        var right = new VBoxContainer { CustomMinimumSize = new Vector2(200, 0) };
        right.AddThemeConstantOverride("separation", 6);
        _body.AddChild(right);

        right.AddChild(MakeSectionTitle("音量"));
        _volumeSlider = new HSlider { MinValue = 0, MaxValue = 100, Step = 1, Value = ModSettings.Volume * 100 };
        _volumeSlider.ValueChanged += v =>
        {
            ModSettings.Volume = (float)(v / 100.0);
            _audio.SetVolume(ModSettings.Volume);
        };
        right.AddChild(_volumeSlider);

        right.AddChild(MakeSectionTitle("八度"));
        _octaveValue = new Label { Text = "C" + ModSettings.Octave };
        _octaveValue.AddThemeColorOverride("font_color", ColGold);
        _octaveValue.AddThemeFontSizeOverride("font_size", Fs(11));
        right.AddChild(_octaveValue);
        _octaveSlider = new HSlider { MinValue = 2, MaxValue = 6, Step = 1, Value = ModSettings.Octave };
        _octaveSlider.ValueChanged += v =>
        {
            ModSettings.Octave = (int)v;
            if (_octaveValue != null) _octaveValue.Text = "C" + ModSettings.Octave;
            UpdateStatusInfo();
            RebuildKeyboard();
        };
        right.AddChild(_octaveSlider);

        right.AddChild(MakeSectionTitle("状态"));
        _statusInfo = new Label
        {
            Text = "—",
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _statusInfo.AddThemeColorOverride("font_color", ColInkDim);
        _statusInfo.AddThemeFontSizeOverride("font_size", Fs(11));
        right.AddChild(_statusInfo);
        UpdateStatusInfo();

        // ---- 观众侧：我听队友演奏的音量 / 静音（DESIGN 里这条决定 mod 口碑）----
        right.AddChild(MakeSectionTitle("队友演奏"));
        _audienceVolume = new HSlider
        {
            MinValue = 0, MaxValue = 100, Step = 5,
            Value = ModSettings.Data.AudienceVolumePercent,
            TooltipText = "别人弹给你听时的音量",
        };
        _audienceVolume.ValueChanged += v =>
        {
            ModSettings.AudienceVolume = (float)(v / 100.0);
            UpdateNetInfo();
        };
        right.AddChild(_audienceVolume);

        var muteRow = new HBoxContainer();
        var muteLabel = new Label { Text = "静音队友", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        muteLabel.AddThemeColorOverride("font_color", ColInk);
        muteLabel.AddThemeFontSizeOverride("font_size", Fs(12));
        muteRow.AddChild(muteLabel);
        _muteAudience = new CheckButton { ButtonPressed = ModSettings.MuteAudience };
        _muteAudience.Toggled += pressed =>
        {
            ModSettings.MuteAudience = pressed;
            UpdateNetInfo();
        };
        muteRow.AddChild(_muteAudience);
        right.AddChild(muteRow);

        // 队友分轨开关（收到第一个音符后自动出现）
        _peerRow = new HBoxContainer();
        _peerRow.AddThemeConstantOverride("separation", 4);
        right.AddChild(_peerRow);

        _netInfo = new Label { Text = "", AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _netInfo.AddThemeColorOverride("font_color", ColInkDim);
        _netInfo.AddThemeFontSizeOverride("font_size", Fs(10));
        right.AddChild(_netInfo);
        UpdateNetInfo();
    }

    /// <summary>开始/停止录音。停止后若录到了音符，回放按钮可用。</summary>
    private void ToggleRecording(bool on)
    {
        _isRecording = on;
        if (on)
        {
            _recording.Clear();
            _recStart = Time.GetTicksMsec() / 1000.0;
            _recButton.Text = Core.Strings.Pick("■ 停止录音", "■ Stop rec");
        }
        else
        {
            _recButton.Text = Core.Strings.Pick("● 录音", "● Rec");
        }
        _playRecButton.Disabled = _recording.Count == 0;
        UpdateRecLabel();
    }

    /// <summary>把录音转成一首 Song 交给曲目播放器回放（复用前视调度，不需要新播放器）。</summary>
    private void ReplayRecording()
    {
        if (_recording.Count == 0) return;

        var song = Songs.SongBuilder.FromRecording(_recording, Core.Strings.Pick("我的录音", "My recording"));

        Songs.SongBuilder.FoldToKeyboard(song, ModSettings.ScaleSnap, ModSettings.Octave);
        _loadedSong = song;
        _player.Start(song, Songs.PlayMode.Auto, ModSettings.InstrumentId, 0.3);
        _playButton.Text = Core.Strings.Pick("■ 停止", "■ Stop");
        UpdateStatus(Core.Strings.Pick($"回放录音：{song.MelodyCount} 个音", $"Replaying {song.MelodyCount} notes"));
    }

    private void UpdateRecLabel()
    {
        if (_recLabel == null) return;
        _recLabel.Text = _isRecording
            ? Core.Strings.Pick($"● {_recording.Count}", $"● {_recording.Count}")
            : (_recording.Count > 0 ? Core.Strings.Pick($"{_recording.Count} 音", $"{_recording.Count} notes") : "—");
    }

    private void UpdateMetroLabel()
    {
        if (_metroLabel != null) _metroLabel.Text = ((int)_metroBpm.Value) + " BPM";
    }

    /// <summary>为每位出现过的队友建一个静音开关（最多 6 个，按 senderId 分轨）。</summary>
    private void RefreshPeerRow()
    {
        if (_peerRow == null || !GodotObject.IsInstanceValid(_peerRow)) return;

        foreach (var kv in _peerNotes)
        {
            if (_peerButtons.ContainsKey(kv.Key) || _peerButtons.Count >= 6) continue;

            var b = new Button
            {
                Text = Core.Strings.Pick($"队友{_peerButtons.Count + 1}", $"P{_peerButtons.Count + 1}"),
                ToggleMode = true,
                CustomMinimumSize = new Vector2(0, 26),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                TooltipText = Core.Strings.Pick("静音这位队友的演奏", "Mute this teammate"),
            };
            ulong id = kv.Key;
            b.Toggled += on =>
            {
                if (on) _mutedPeers.Add(id); else _mutedPeers.Remove(id);
                UpdateNetInfo();
            };
            _peerRow.AddChild(b);
            _peerButtons[id] = b;
        }
    }

    /// <summary>联机与观众侧状态（只在文字变化时写，避免每帧刷 UI）。</summary>
    private void UpdateNetInfo()
    {
        if (_netInfo == null) return;
        string conn = _net.IsConnected
            ? Core.Strings.Pick("已连接", "connected")
            : Core.Strings.Pick("未连接", "not connected");
        string vol = ModSettings.MuteAudience
            ? Core.Strings.Pick("静音", "muted")
            : ModSettings.Data.AudienceVolumePercent + "%";
        string text = Core.Strings.Pick(
            $"联机：{conn}\n队友音量：{vol}\n队友 {_peerNotes.Count} 人 · 收到音符 {_net.ReceivedNotes} · 发出 {_net.SentNotes}" +
            (_mutedPeers.Count > 0 ? $" · 已静音 {_mutedPeers.Count}" : ""),
            $"Network: {conn}\nTeammate volume: {vol}\nTeammates {_peerNotes.Count} · notes in {_net.ReceivedNotes} · out {_net.SentNotes}" +
            (_mutedPeers.Count > 0 ? $" · muted {_mutedPeers.Count}" : ""));
        if (_netInfo.Text != text) _netInfo.Text = text;
    }

    /// <summary>右栏"状态"：给玩家看的有效信息（不是开发笔记）。</summary>
    private void UpdateStatusInfo()
    {
        if (_statusInfo == null) return;
        var def = InstrumentLibrary.Get(ModSettings.InstrumentId);
        string scale = ModSettings.ScaleSnap
            ? Core.Strings.Pick("五声吸附（怎么按都好听）", "Pentatonic snap (nothing sounds wrong)")
            : Core.Strings.Pick("半音（完整 12 音）", "Chromatic (all 12 notes)");
        _statusInfo.Text = Core.Strings.Pick(
            $"音阶：{scale}\n音色：{def.Name}\n复音：24 声部\n八度：C{ModSettings.Octave}",
            $"Scale: {scale}\nTimbre: {Core.Strings.Tr(def.Name)}\nVoices: 24\nOctave: C{ModSettings.Octave}");
    }

    private void BuildMiniRow()
    {
        _miniRow = new HBoxContainer { Visible = false };
        _miniRow.AddThemeConstantOverride("separation", 3);
        _root.AddChild(_miniRow);
    }

    private void BuildDock()
    {
        _dock = new Button
        {
            Text = "♪",
            TooltipText = "演奏面板（P）",
            CustomMinimumSize = new Vector2(Fs(52), Fs(52)),
            Size = new Vector2(52, 52),
            Visible = false,
        };
        _dock.AddThemeStyleboxOverride("normal", MakePanelStyle(new Color(0.18f, 0.16f, 0.11f), ColGold, 2, 26, 0));
        _dock.AddThemeStyleboxOverride("hover", MakePanelStyle(new Color(0.24f, 0.21f, 0.13f), ColGoldBright, 2, 26, 0));
        _dock.AddThemeStyleboxOverride("pressed", MakePanelStyle(new Color(0.30f, 0.26f, 0.15f), ColGoldBright, 2, 26, 0));
        _dock.AddThemeColorOverride("font_color", ColGoldBright);
        _dock.AddThemeFontSizeOverride("font_size", Fs(22));
        // 音符小图标（收起态）。注意：不要用 BottomRight 锚点 —— 根 Control 在
        // CanvasLayer 下拿不到可靠的父尺寸，锚点会把它算到屏幕外（实测 -78,-78）。
        // 改用每帧按视口尺寸绝对定位，行为可预测。
        _dock.SetAnchorsPreset(LayoutPreset.TopLeft);
        _dock.Pressed += () => SetPanelVisible(true);
        AddChild(_dock);
        LayoutDock();
    }

    /// <summary>把音符小图标放在右下角（26px 边距），绝对定位、不依赖父节点尺寸。</summary>
    private void LayoutDock()
    {
        var vp = GetViewportRect().Size;
        const float size = 52f, margin = 26f;
        _dock.Size = new Vector2(size, size);
        _dock.Position = new Vector2(Math.Max(margin, vp.X - size - margin), Math.Max(margin, vp.Y - size - margin));
    }

    private static Label MakeSectionTitle(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeColorOverride("font_color", ColGold);
        l.AddThemeFontSizeOverride("font_size", Fs(11));
        return l;
    }

    private static StyleBoxFlat MakePanelStyle(Color bg, Color border, int borderWidth, int radius, float contentMargin)
    {
        var sb = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        sb.SetBorderWidthAll(borderWidth);
        sb.SetCornerRadiusAll(radius);
        if (contentMargin > 0) sb.SetContentMarginAll(contentMargin);
        return sb;
    }

    private static StyleBoxFlat MakeKeyStyle(Color bg, Color border, int radius)
    {
        var sb = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        sb.SetBorderWidthAll(1);
        sb.SetCornerRadiusAll(radius);
        sb.CornerRadiusTopLeft = 0;
        sb.CornerRadiusTopRight = 0;
        return sb;
    }

    // ==================================================================
    //  琴键
    // ==================================================================

    /// <summary>一个琴键在键盘区里的归一化位置（0..1），随控件尺寸重新排版。</summary>
    private sealed class KeyLayout
    {
        public Button Btn = null!;
        public float X;
        public float W;
        public bool Black;
        public int Slot;
    }

    private readonly List<KeyLayout> _keyLayout = new();
    private readonly Dictionary<int, KeyLayout> _slotLayout = new();

    private int KeyHeightFor(PanelSize size) => Mathf.RoundToInt((size switch
    {
        PanelSize.Large => 190,
        PanelSize.Mini => 96,
        _ => 150,
    }) * _ui);

    private void RebuildKeyboard()
    {
        foreach (var child in _keyStage.GetChildren()) child.QueueFree();
        foreach (var child in _miniRow.GetChildren()) child.QueueFree();
        _keyButtons.Clear();
        _keyLayout.Clear();
        _slotLayout.Clear();

        bool snap = ModSettings.ScaleSnap;
        int count = MusicScale.SlotCount(snap);
        _keyStage.CustomMinimumSize = new Vector2(0, KeyHeightFor(ModSettings.Size));

        void Add(KeyLayout layout)
        {
            _keyLayout.Add(layout);
            _slotLayout[layout.Slot] = layout;
        }

        if (snap)
        {
            // 五声吸附：13 个等宽键平铺（没有黑键，每个都是大目标，点击/触屏都准）
            float w = 1f / count;
            for (int slot = 0; slot < count; slot++)
            {
                var b = MakeKeyButton(slot, snap, compact: false);
                _keyStage.AddChild(b);
                Add(new KeyLayout { Btn = b, X = slot * w, W = w * 0.94f, Black = false, Slot = slot });
                _keyButtons[slot] = b;
            }
        }
        else
        {
            // 半音模式：真钢琴几何 —— 白键满高，黑键 62% 高压在白键交界处
            int whites = 0;
            for (int s = 0; s < count; s++) if (!MusicScale.IsBlackSlot(s, false)) whites++;
            float ww = 1f / Math.Max(1, whites);
            int whiteIdx = 0;
            for (int slot = 0; slot < count; slot++)
            {
                bool black = MusicScale.IsBlackSlot(slot, false);
                var b = MakeKeyButton(slot, snap, compact: false);
                _keyStage.AddChild(b);
                if (!black)
                {
                    Add(new KeyLayout { Btn = b, X = whiteIdx * ww, W = ww * 0.94f, Black = false, Slot = slot });
                    whiteIdx++;
                }
                else
                {
                    float bw = ww * 0.60f;
                    Add(new KeyLayout { Btn = b, X = whiteIdx * ww - bw * 0.5f, W = bw, Black = true, Slot = slot });
                }
                _keyButtons[slot] = b;
            }
        }

        // 迷你条：只放 8 个键，等队友时随手弹两下
        int miniCount = Math.Min(8, count);
        for (int slot = 0; slot < miniCount; slot++)
        {
            var b = MakeKeyButton(slot, snap, compact: true);
            _miniRow.AddChild(b);
        }

        LayoutKeys();
    }

    /// <summary>按当前控件尺寸重新摆放琴键（窗口/档位变化时调用）。</summary>
    private void LayoutKeys()
    {
        float W = _keyStage.Size.X, H = _keyStage.Size.Y;
        if (W <= 1 || H <= 1) return;
        foreach (var k in _keyLayout)
        {
            float h = k.Black ? H * 0.62f : H;
            k.Btn.Position = new Vector2(k.X * W, 0);
            k.Btn.Size = new Vector2(Math.Max(8f, k.W * W), h);
            k.Btn.ZIndex = k.Black ? 2 : 1;   // 黑键压在白键之上
        }
    }

    private Button MakeKeyButton(int slot, bool snap, bool compact)
    {
        bool black = MusicScale.IsBlackSlot(slot, snap);
        string name = MusicScale.SlotNoteName(slot, snap, ModSettings.Octave);
        // 黑键窄，只放音名；白键放音名 + 键位提示
        string text = compact ? name : (black ? name : name + "\n" + MusicScale.SlotKeyHint(slot));

        var b = new Button
        {
            CustomMinimumSize = new Vector2(compact ? 44 : 18, compact ? 46 : 60),
            FocusMode = FocusModeEnum.None,
            Text = text,
            ClipText = true,
        };
        b.AddThemeFontSizeOverride("font_size", compact ? 11 : 10);
        b.AddThemeColorOverride("font_color", black ? ColInk : new Color(0.35f, 0.32f, 0.26f));
        b.AddThemeColorOverride("font_hover_color", black ? ColGoldBright : new Color(0.25f, 0.22f, 0.16f));

        var normal = black ? ColKeyBlack : ColKeyNormal;
        b.AddThemeStyleboxOverride("normal", MakeKeyStyle(normal, new Color(0.43f, 0.39f, 0.31f), compact ? 5 : 4));
        b.AddThemeStyleboxOverride("hover", MakeKeyStyle(black ? ColKeyBlack.Lightened(0.14f) : ColKeyHover, ColGold, compact ? 5 : 4));
        b.AddThemeStyleboxOverride("pressed", MakeKeyStyle(ColKeyDown, ColGold, compact ? 5 : 4));

        int midi = MusicScale.SlotToMidi(slot, 0, ModSettings.ScaleSnap, ModSettings.Octave);
        b.ButtonDown += () => NoteOn(midi, slot);
        b.ButtonUp += () => NoteOff(midi);
        return b;
    }

    // ==================================================================
    //  演奏
    // ==================================================================

    private void NoteOn(int midi, int slot)
    {
        if (!_heldMidi.Add(midi)) return;
        var def = InstrumentLibrary.Get(ModSettings.InstrumentId);
        var handle = _audio.Play(midi, 0.85f, def);
        if (handle != null) _heldVoices[midi] = handle;
        SetKeyPressed(slot, true);
        PushRecent(midi);
        UpdateReadout();
        _net.SendNote(true, midi, 0.85f);

        if (_isRecording)
        {
            _recording.Add(new Songs.SongNote
            {
                Time = Time.GetTicksMsec() / 1000.0 - _recStart,
                Midi = midi,
                Duration = 0.4,
                Velocity = 0.85f,
            });
            UpdateRecLabel();
        }

        // 跟弹模式：把这次按键交给判定
        if (_mode == Songs.PlayMode.Learn && _player.IsPlaying)
        {
            var grade = _player.Press(midi);
            if (grade == Songs.HitGrade.Perfect) _scoreLabel.Text = Core.Strings.Pick("完美！", "Perfect!");
            else if (grade == Songs.HitGrade.Good) _scoreLabel.Text = Core.Strings.Pick("不错", "Good");
        }
    }

    private void PushRecent(int midi)
    {
        _recent.Insert(0, MusicScale.NoteName(midi));
        if (_recent.Count > 16) _recent.RemoveAt(_recent.Count - 1);
        if (_ribbon != null) _ribbon.Text = string.Join("  ", _recent);
    }

    private void NoteOff(int midi)
    {
        _heldMidi.Remove(midi);

        // 延音类必须显式释放（循环采样不会自己停）；一次性音色自然衰减即可
        if (_heldVoices.TryGetValue(midi, out var handle))
        {
            _heldVoices.Remove(midi);
            if (InstrumentLibrary.Get(ModSettings.InstrumentId).IsSustain) _audio.Release(handle);
        }

        SetKeyPressed(MusicScale.MidiToSlot(midi, ModSettings.ScaleSnap, ModSettings.Octave), false);
        _net.SendNote(false, midi, 0f);
        UpdateReadout();
    }

    /// <summary>
    /// 琴键"正在发声"的高亮。
    /// 注意：不能靠 ButtonPressed —— 非 ToggleMode 的 Button 不会因此改变外观，
    /// 所以这里用 modulate 染色（对白键/黑键都成立，且不需要额外 stylebox）。
    /// </summary>
    private void SetKeyPressed(int slot, bool pressed)
    {
        if (!_keyButtons.TryGetValue(slot, out var b) || !GodotObject.IsInstanceValid(b)) return;
        b.Modulate = pressed ? new Color(1.00f, 0.90f, 0.62f) : Colors.White;
    }

    /// <summary>处理键盘事件；返回 true 表示已消费（宿主会 SetInputAsHandled 屏蔽游戏输入）。</summary>
    public bool HandleKey(InputEventKey k)
    {
        Key code = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;

        if (k.Pressed && !k.Echo)
        {
            if (code == Key.Escape) { SetPanelVisible(false); return true; }
            if (code == Key.Bracketleft) { ShiftOctave(-1); return true; }
            if (code == Key.Bracketright) { ShiftOctave(1); return true; }
        }

        for (int row = 0; row < 2; row++)
        {
            var keys = row == 0 ? MusicScale.RowLow : MusicScale.RowHigh;
            int rowOffset = row * MusicScale.RowOffsetSemitones;
            for (int slot = 0; slot < keys.Length; slot++)
            {
                if (keys[slot] != code) continue;
                int midi = MusicScale.SlotToMidi(slot, rowOffset, ModSettings.ScaleSnap, ModSettings.Octave);
                if (k.Pressed)
                {
                    if (!k.Echo) NoteOn(midi, slot);
                }
                else
                {
                    NoteOff(midi);
                }
                return true;
            }
        }
        return false;
    }

    private void ShiftOctave(int delta)
    {
        int next = Math.Clamp(ModSettings.Octave + delta, 2, 6);
        if (next == ModSettings.Octave) return;
        ModSettings.Octave = next;
        _octaveSlider.Value = next;
        if (_octaveValue != null) _octaveValue.Text = "C" + next;
        UpdateStatusInfo();
        RebuildKeyboard();
        UpdateStatus(Core.Strings.Pick($"八度：C{next}", $"Octave: C{next}"));
    }

    private void SelectInstrument(string id, bool silent = false)
    {
        ModSettings.InstrumentId = id;
        foreach (var (key, button) in _instButtons)
        {
            bool on = key == id;
            button.AddThemeColorOverride("font_color", on ? ColGoldBright : ColInk);
            button.AddThemeStyleboxOverride("normal",
                MakePanelStyle(on ? new Color(0.18f, 0.16f, 0.09f) : new Color(0.11f, 0.13f, 0.16f),
                               on ? ColGold : new Color(0.23f, 0.25f, 0.29f), 1, 6, 4));
        }

        if (!silent)
        {
            // 试听一个和弦，让玩家立刻听到音色差别
            var def = InstrumentLibrary.Get(id);
            int root = MusicScale.BaseMidi(ModSettings.Octave);
            foreach (int step in new[] { 0, 4, 7 })
            {
                int midi = ModSettings.ScaleSnap ? root + MusicScale.Pentatonic[Math.Min(step, MusicScale.Pentatonic.Length - 1)] : root + step;
                _audio.PlayPreview(midi, 0.7f, def, 0.9);   // 延音类会自动释放，避免一直响
            }
            UpdateStatus(Core.Strings.Pick($"音色：{def.Name}", $"Timbre: {Core.Strings.Tr(def.Name)}"));
        }
        UpdateStatusInfo();
    }

    // ==================================================================
    //  尺寸 / 显示
    // ==================================================================

    private void ApplySize(PanelSize size, bool reposition)
    {
        ModSettings.Size = size;
        bool mini = size == PanelSize.Mini;

        // 迷你档：只留标题栏 + 迷你条；通栏键盘、键位提示、状态栏、页脚都要收起来
        _body.Visible = !mini;
        _keyWrap.Visible = !mini;
        _keyHint.Visible = !mini;
        _miniRow.Visible = mini;
        _statusLabel.Visible = !mini;

        _appliedSize = size;
        _appliedScale = Core.ModSettings.UiScalePercent;
        var viewport = GetViewportRect().Size;
        // 键盘改为通栏后：宽度给足（键宽接近真钢琴比例），高度按内容自然高度收紧
        // 尺寸计算下沉到纯函数（Core.PanelLayout），可被离线自检覆盖
        var (tw, th) = Core.PanelLayout.TargetSize(size, _ui, viewport.X, viewport.Y, Fs(112));
        Vector2 target = new(tw, th);
        _appliedSize = size;
        _appliedScale = Core.ModSettings.UiScalePercent;
        _window.CustomMinimumSize = target;
        _window.Size = target;
        // 内容（缩放后的字号）可能比 target 更高：让面板按内容自适应，再夹到视口内 ——
        // 否则键盘与曲目区会被挤到屏幕外（实测在 130% 缩放下发生过）。
        _window.ResetSize();
        if (_window.Size.Y > viewport.Y - 40)
        {
            _window.Size = new Vector2(_window.Size.X, viewport.Y - 40);
        }
        if (reposition)
        {
            _window.Position = new Vector2(
                Math.Max(12, (viewport.X - target.X) * 0.5f),
                Math.Max(12, (viewport.Y - target.Y) * 0.42f));
        }

        RebuildKeyboard();
    }

    /// <summary>
    /// 设置页改了"尺寸/缩放"后**立刻重建 UI**（字号在构建时应用，不重建就不会变）。
    /// 只重建界面：设置、正在播放的曲子、声部池都不受影响。
    /// </summary>
    public void RebuildUi()
    {
        foreach (var child in GetChildren())
        {
            RemoveChild(child);   // 先摘除再释放，避免同帧内新旧控件并存
            child.QueueFree();
        }

        _keyButtons.Clear();
        _keyLayout.Clear();
        _slotLayout.Clear();
        _instButtons.Clear();
        _laneBlocks.Clear();
        _modeButtons.Clear();
        _songEntries.Clear();
        _recent.Clear();

        ComputeUiScale();
        BuildWindow();
        BuildDock();
        RebuildKeyboard();
        ApplySize(Core.ModSettings.Size, reposition: true);
        SetPanelVisible(Core.ModSettings.PanelVisible);
        UpdateReadout();
        Localizer.LocalizeTree(this);
        UpdateStatusInfo();
        UpdateNetInfo();
        GD.Print($"{SpireInstrumentMod.LogTag} UI rebuilt (size={Core.ModSettings.Size}, scale={Core.ModSettings.UiScalePercent}%)");
    }
    public void Toggle() => SetPanelVisible(!ModSettings.PanelVisible);

    public void SetPanelVisible(bool visible)
    {
        ModSettings.PanelVisible = visible;
        _window.Visible = visible;
        _dock.Visible = !visible;
        if (!visible)
        {
            _heldMidi.Clear();
            _heldVoices.Clear();
            _audio.ReleaseAll();
            UpdateReadout();
        }
    }

    private void OnHeaderGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
        {
            _dragging = mb.Pressed;
            if (_dragging) _dragOffset = mb.Position;
            AcceptEvent();
        }
        else if (@event is InputEventMouseMotion mm && _dragging)
        {
            _window.Position += mm.Relative;
            ClampWindowIntoView();
            AcceptEvent();
        }
    }

    private void ClampWindowIntoView()
    {
        var viewport = GetViewportRect().Size;
        var pos = _window.Position;
        pos.X = Math.Clamp(pos.X, -_window.Size.X + 120, viewport.X - 120);
        pos.Y = Math.Clamp(pos.Y, 0, viewport.Y - 40);
        _window.Position = pos;
    }

    // ==================================================================
    //  无头自检探针（由 SelfTest 调用；返回一行可解析结果）
    // ==================================================================

    public string SelfTestProbe()
    {
        int expectedKeys = MusicScale.SlotCount(ModSettings.ScaleSnap);
        int actualKeys = _keyButtons.Count;
        int insts = _instButtons.Count;
        bool windowOk = _window != null && GodotObject.IsInstanceValid(_window);

        int played = 0;
        bool ex = false;
        try
        {
            var def = InstrumentLibrary.Get(ModSettings.InstrumentId);
            int root = MusicScale.BaseMidi(ModSettings.Octave);
            foreach (int step in new[] { 0, 4, 7 })
            {
                int midi = ModSettings.ScaleSnap
                    ? root + MusicScale.Pentatonic[Math.Min(step, MusicScale.Pentatonic.Length - 1)]
                    : root + step;
                _audio.PlayPreview(midi, 0.8f, def, 0.8);   // 延音类自动释放，探针不会留下长音
                played++;
            }
        }
        catch (Exception e)
        {
            ex = true;
            GD.PushError($"{SpireInstrumentMod.LogTag} self-test play threw: {e}");
        }

        return $"panel={windowOk} keys={actualKeys}/{expectedKeys} insts={insts}/{InstrumentLibrary.Items.Count} " +
               $"voices={_audio.ActiveVoices} played={played} ex={ex.ToString().ToLowerInvariant()} " +
               $"snap={ModSettings.ScaleSnap} oct={ModSettings.Octave} " +
               $"dock={_dock.Visible}/{_dock.Position.X:F0},{_dock.Position.Y:F0}/{_dock.Size.X:F0}x{_dock.Size.Y:F0}";
    }

    /// <summary>供截图巡游使用：切尺寸/音阶模式/音色（静默，不试听）。</summary>
    public void SelfTestConfigure(PanelSize size, bool snap, string inst)
    {
        ModSettings.ScaleSnap = snap;
        ModSettings.InstrumentId = inst;
        SetPanelVisible(true);
        ApplySize(size, reposition: true);   // 内部会重建琴键
        SelectInstrument(inst, silent: true);
    }

    /// <summary>供截图巡游使用：按下几个键，让截图里能看到"按下态 + 最近演奏"。</summary>
    public void SelfTestPlayDemo()
    {
        foreach (int slot in new[] { 0, 2, 4 })
        {
            int midi = MusicScale.SlotToMidi(slot, 0, ModSettings.ScaleSnap, ModSettings.Octave);
            NoteOn(midi, slot);
        }
        // 最后一个键保持按住，截图里能看到高亮
        int hold = Math.Min(5, MusicScale.SlotCount(ModSettings.ScaleSnap) - 1);
        NoteOn(MusicScale.SlotToMidi(hold, 0, ModSettings.ScaleSnap, ModSettings.Octave), hold);
    }

    /// <summary>供自检使用：起一个延音音色（循环采样）。记录基线声部数供对比。</summary>
    public string SelfTestSustainStart()
    {
        var def = InstrumentLibrary.Get("melodica");
        _sustainBaseline = _audio.ActiveVoices;      // 试奏的一次性音还在衰减，必须用基线对比
        _sustainProbe = _audio.Play(MusicScale.BaseMidi(ModSettings.Octave), 0.8f, def);
        return _sustainProbe == null
            ? "sustain=null"
            : $"sustain=started voices={_audio.ActiveVoices} baseline={_sustainBaseline}";
    }

    /// <summary>供自检使用：释放延音音色。</summary>
    public string SelfTestSustainRelease()
    {
        if (_sustainProbe == null) return "sustain=null";
        _audio.Release(_sustainProbe);
        _sustainProbe = null;
        return $"sustain=released voices={_audio.ActiveVoices} baseline={_sustainBaseline}";
    }

    /// <summary>供自检使用：当前声部数（与基线对比判断延音是否停住）。</summary>
    public int SelfTestVoices() => _audio.ActiveVoices;

    public int SelfTestSustainBaseline() => _sustainBaseline;

    /// <summary>供自检使用：开始播放一首内置曲（自动演奏模式）。</summary>
    public string SelfTestSongStart()
    {
        var def = Songs.SongLibrary.Get("twinkle");
        if (def == null) return "song=no-library";

        var song = Songs.SongBuilder.FromDefinition(def, withAccompaniment: true);
        Songs.SongBuilder.FoldToKeyboard(song, ModSettings.ScaleSnap, ModSettings.Octave);
        _loadedSong = song;
        _player.Start(song, Songs.PlayMode.Auto, ModSettings.InstrumentId);
        return $"song=started name={song.Name} notes={song.MelodyCount} dur={song.Duration:F1}s";
    }

    /// <summary>供自检使用：停止播放并汇报调度数量与实测误差。</summary>
    public string SelfTestSongStop()
    {
        int scheduled = _player.ScheduledNotes;
        double avg = _player.AverageJitterMs;
        double worst = _player.WorstJitterMs;
        int fps = (int)Engine.GetFramesPerSecond();
        _player.Stop();
        // 误差与帧率一起报：调度精度受帧长限制（启动预载期帧长可达数百毫秒，误差会虚高）
        return $"song=stopped scheduled={scheduled} err={avg:F1}ms worst={worst:F1}ms fps={fps}";
    }

    /// <summary>供自检/截图使用：切到跟弹模式并开始播放（带下落轨道）。</summary>
    public string SelfTestLearnStart()
    {
        SetMode(Songs.PlayMode.Learn);

        var def = Songs.SongLibrary.Get("twinkle");
        if (def == null) return "learn=no-library";

        var song = Songs.SongBuilder.FromDefinition(def, withAccompaniment: true);
        Songs.SongBuilder.FoldToKeyboard(song, ModSettings.ScaleSnap, ModSettings.Octave);
        _loadedSong = song;
        _player.Start(song, Songs.PlayMode.Learn, ModSettings.InstrumentId, 0.2);
        _playButton.Text = "■ 停止";

        int minSlot = int.MaxValue, maxSlot = int.MinValue;
        foreach (var n in song.Notes)
        {
            if (n.Bass) continue;
            if (n.Slot < minSlot) minSlot = n.Slot;
            if (n.Slot > maxSlot) maxSlot = n.Slot;
        }
        return $"learn=started notes={song.MelodyCount} laneVisible={_lane.Visible} " +
               $"slots={minSlot}..{maxSlot} snapped={song.Snapped}";
    }

    /// <summary>
    /// 供自检使用：把语言强制成英文、重跑整树翻译，读出几个关键控件的文案。
    /// 放在自检链最后一步（截图之后），所以不会影响中文截图。
    /// </summary>
    public string SelfTestLocalizeProbe()
    {
        Core.Strings.ForcedLanguage = "en_US";
        Localizer.LocalizeTree(this);
        UpdateStatusInfo();
        UpdateNetInfo();

        string play = _playButton?.Text ?? "";
        string auto = _modeButtons.Count > 1 ? _modeButtons[1].Text : "";
        string status = _statusInfo?.Text.Split('\n')[0] ?? "";
        string voices = _voiceLabel?.Text ?? "";
        string song = (_songInfo?.Text ?? "").Replace("\n", " | ");

        return $"localize play='{play}' auto='{auto}' status='{status}' voices='{voices}' song='{song}'";
    }

    /// <summary>
    /// 供自检使用：压力边界验收 —— 密集发声是否会爆声部池 / 爆采样缓存。
    /// 覆盖：跨音色跨音高大量发声（触发缓存淘汰）、超过 24 路（触发声部抢占）。
    /// </summary>
    public string SelfTestStress()
    {
        int peakVoices = 0;
        string error = "";

        try
        {
            // 1) 缓存淘汰：9 音色 × 12 音高 = 108 条（接近 128 上限）
            foreach (var def in InstrumentLibrary.Items)
            {
                for (int i = 0; i < 12; i++)
                {
                    _audio.Play(60 + i * 2, 0.8f, def);
                    if (_audio.ActiveVoices > peakVoices) peakVoices = _audio.ActiveVoices;
                }
            }

            // 2) 声部抢占：连续 40 次发声（远超 24 路）
            var harp = InstrumentLibrary.Get("harp");
            for (int i = 0; i < 40; i++)
            {
                _audio.Play(48 + (i % 24), 0.8f, harp);
                if (_audio.ActiveVoices > peakVoices) peakVoices = _audio.ActiveVoices;
            }
        }
        catch (System.Exception e)
        {
            error = e.GetType().Name + ": " + e.Message;
        }

        int voices = _audio.ActiveVoices;
        int cache = _audio.SampleCacheCount;
        int cacheLimit = _audio.SampleCacheLimitPublic;

        // 收尾：释放所有声部，避免影响后续步骤
        _audio.ReleaseAll();

        return $"stress peakVoices={peakVoices} voices={voices} cache={cache}/{cacheLimit} err='{error}'";
    }

    /// <summary>
    /// 供自检使用：播一首**很短的曲子**并等它自然播完 ——
    /// 覆盖 24 轮里从未跑过的"整曲收尾"路径（`song finished` + 判定收尾）。
    /// </summary>
    public string SelfTestShortSong()
    {
        var song = new Songs.Song { Name = "自检短曲", Duration = 1.2 };
        song.Notes.Add(new Songs.SongNote { Time = 0.0, Midi = 60, Duration = 0.2, Velocity = 0.8f, Slot = 0, KeyMidi = 60 });
        song.Notes.Add(new Songs.SongNote { Time = 0.3, Midi = 64, Duration = 0.2, Velocity = 0.8f, Slot = 2, KeyMidi = 64 });
        song.Notes.Add(new Songs.SongNote { Time = 0.6, Midi = 67, Duration = 0.2, Velocity = 0.8f, Slot = 4, KeyMidi = 67 });

        _loadedSong = song;
        _player.Start(song, Songs.PlayMode.Learn, ModSettings.InstrumentId, 0.1);
        return $"shortsong=started notes={song.Notes.Count} dur={song.Duration:F1}s finished={_player.FinishedCount}";
    }

    /// <summary>供自检使用：短曲是否已自然播完（收尾路径是否跑到）。</summary>
    public string SelfTestShortSongState()
        => $"shortsong=state playing={_player.IsPlaying} finished={_player.FinishedCount} " +
           $"judge={(_player.Judge != null ? _player.Judge.Describe() : "none")}";

    // ==================================================================
    //  状态刷新
    // ==================================================================

    public override void _Process(double delta)
    {
        _voiceLabel.Text = Core.Strings.Pick($"声部 {_audio.ActiveVoices}/24", $"Voices {_audio.ActiveVoices}/24");
        _net.Update();      // 跟踪网络服务实例（进房/断线重连/退出对局都会换实例）

        // 设置页改了尺寸/缩放 → 立刻重建（否则要重开游戏才生效）
        if (Core.ModSettings.Size != _appliedSize || Core.ModSettings.UiScalePercent != _appliedScale)
        {
            RebuildUi();
            return;
        }
        UpdateNetInfo();

        // 节拍器打点（首拍重音：低音=重音，高音=弱拍）
        if (_metroOn)
        {
            double nowSec = Time.GetTicksMsec() / 1000.0;
            if (_metroNext <= 0 || nowSec >= _metroNext)
            {
                if (_metroNext <= 0) _metroNext = nowSec;
                bool accent = _metroBeat % 4 == 0;
                var drumDef = InstrumentLibrary.Get("drum");
                _audio.PlayPreview(accent ? 48 : 60, accent ? 0.85f : 0.5f, drumDef, 0.3);
                _metroBeat++;
                _metroNext += 60.0 / Math.Max(40, (int)_metroBpm.Value);
            }
        }

        // 曲目播放：进度条 + 跟弹轨道 + 成绩
        if (_player.IsPlaying)
        {
            _progress.Value = _player.Progress;
            UpdateLane();
            if (_mode == Songs.PlayMode.Learn && _player.Judge != null)
            {
                var j = _player.Judge;
                _scoreLabel.Text = Core.Strings.Pick(
                    $"命中 {j.Hits} · 失误 {j.Misses} · 连击 {j.Combo} · 准确 {j.Accuracy:P0}",
                    $"Hit {j.Hits} · Miss {j.Misses} · Combo {j.Combo} · Acc {j.Accuracy:P0}");
            }
        }
        else if (_playButton != null && _playButton.Text != "▶ 播放")
        {
            _playButton.Text = "▶ 播放";
            _progress.Value = 0;
            ClearLaneBlocks();
        }
    }

    private void UpdateReadout()
    {
        if (_heldMidi.Count == 0)
        {
            _readout.Text = "—";
            _heldLabel.Text = "按下琴键开始演奏";
            return;
        }

        int top = 0;
        foreach (int m in _heldMidi) if (m > top) top = m;
        _readout.Text = MusicScale.NoteName(top);

        var names = new List<string>();
        foreach (int m in _heldMidi) names.Add(MusicScale.NoteName(m));
        names.Sort();
        _heldLabel.Text = string.Join(" · ", names);
    }

    private void UpdateStatus(string text)
    {
        // 精确命中文案表的消息自动翻译；拼接消息由调用点用 Strings.Pick 处理
        if (_statusLabel != null) _statusLabel.Text = Core.Strings.Tr(text);
    }
}
