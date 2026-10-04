using System;
using System.IO;
using Godot;

namespace SpireInstrument;

/// <summary>
/// 游戏内无头自检：**默认完全惰性**，只有存在标记文件时才动作。
///
/// 为什么用标记文件而不是环境变量：本工作区已有先例记录 ——
/// 在 PowerShell 里设的 $env: 变量在游戏进程里读不到（MerchantBlacklist 踩过）。
///
/// 为什么用 SceneTreeTimer 链而不是 _Process / SceneTree.ProcessFrame：
/// 这两个在游戏启动期会被暂停，会导致自检静默卡死（LoadOrderManager、MerchantBlacklist 都踩过）。
///
/// 用法：见 verify-in-game.ps1（建标记 → 无头启动游戏 → 解析日志 → 清标记）。
/// </summary>
internal static class SelfTest
{
    private const string MarkerName = "spireinstrument-selftest";

    private static bool _active;
    private static int _failures;

    public static string MarkerPath =>
        Path.Combine(Path.GetTempPath(), MarkerName);

    public static bool IsActive => _active;

    public static void Install()
    {
        if (!File.Exists(MarkerPath)) return;

        _active = true;
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] marker found: {MarkerPath}");

        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
        {
            GD.PushError($"{SpireInstrumentMod.LogTag} [SELFTEST] no SceneTree; aborting");
            return;
        }

        // 启动期等 3 秒让游戏本体与 mod 全部就位，随后逐步探测
        Schedule(tree, 3.0, () => Step(tree, 1));
    }

    private static void Schedule(SceneTree tree, double seconds, Action next)
    {
        var timer = tree.CreateTimer(seconds);
        if (timer == null)
        {
            GD.PushError($"{SpireInstrumentMod.LogTag} [SELFTEST] CreateTimer failed");
            next();
            return;
        }
        // 任何一步抛异常都不能让链条静默断掉（否则表现为"游戏不退出"→超时）。
        // 这里记一次失败并直接进入收尾，让结果落成 FAIL 而不是挂死。
        timer.Timeout += () =>
        {
            try
            {
                next();
            }
            catch (Exception e)
            {
                _failures++;
                GD.PushError($"{SpireInstrumentMod.LogTag} [SELFTEST] step threw: {e}");
                Finish(tree);
            }
        };
    }

    private static void Step(SceneTree tree, int step)
    {
        switch (step)
        {
            case 1: ProbeHost(tree); break;
            case 2: ProbePanel(tree); break;
            case 3: SustainStart(tree); break;
            case 4: SustainHold(tree); break;
            case 5: SustainEnd(tree); break;
            case 6: ShotStandard(tree); break;
            case 7: ShotLarge(tree); break;
            case 8: ShotMini(tree); break;
            case 9: ShotChromatic(tree); break;
            case 10: ShotChip(tree); break;
            case 11: ShotDock(tree); break;
            case 12: Finish(tree); break;
            case 13: SongStart(tree); break;
            case 14: SongEnd(tree); break;
            case 15: ShotLearn(tree); break;
            case 16: LoopbackStart(tree); break;
            case 17: LoopbackEnd(tree); break;
            case 18: LocalizeProbe(tree); break;
            case 19: ShotEnglish(tree); break;
            case 20: StressProbe(tree); break;
            case 21: ShortSongStart(tree); break;
            case 22: ShortSongEnd(tree); break;
        }
    }

    /// <summary>整曲收尾验收：播一首 1.2 秒的短曲，等它自然播完。</summary>
    private static void ShortSongStart(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        string s = host != null ? host.SelfTestShortSong() : "shortsong=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {s}");
        // 1.2s 曲长 + 0.8s 尾音 + 起拍 0.1s ≈ 2.1s，留到 2.6s
        Schedule(tree, 2.6, () => Step(tree, 22));
    }

    /// <summary>整曲收尾路径验收：短曲自然播完后应停止、且收尾计数递增。</summary>
    private static void ShortSongEnd(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        string result = host?.SelfTestShortSongState() ?? "shortsong=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {result}");

        Check(result.Contains("playing=False"), $"短曲未自然收尾（{result}）");
        var m = System.Text.RegularExpressions.Regex.Match(result, @"finished=(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out int fin))
        {
            Check(fin >= 1, $"整曲收尾路径未跑到（finished={fin}）");
        }
        else
        {
            Check(false, "短曲状态未返回 finished");
        }

        Schedule(tree, 0.2, () => Step(tree, 12));
    }
    /// <summary>压力边界验收：密集发声不爆声部池 / 不爆采样缓存。</summary>
    private static void StressProbe(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        string result = host?.SelfTestStress() ?? "stress=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {result}");

        Check(!result.Contains("err=''") == false || result.Contains("err=''"), "压力测试抛异常");
        Check(result.Contains("err=''"), $"压力测试抛异常（{result}）");

        var m = System.Text.RegularExpressions.Regex.Match(result, @"peakVoices=(\d+)");
        if (m.Success && int.TryParse(m.Groups[1].Value, out int peak))
        {
            Check(peak <= 24, $"并发声部超过池上限 24：{peak}");
            Check(peak >= 10, $"压力测试未真正打到多声部（峰值仅 {peak}），用例可能失效");
        }
        else
        {
            Check(false, "压力测试未返回 peakVoices");
        }

        var c = System.Text.RegularExpressions.Regex.Match(result, @"cache=(\d+)/(\d+)");
        if (c.Success)
        {
            int used = int.Parse(c.Groups[1].Value);
            int limit = int.Parse(c.Groups[2].Value);
            Check(used <= limit, $"采样缓存超上限：{used}/{limit}");
        }

        Schedule(tree, 0.3, () => Step(tree, 21));
    }

    /// <summary>英文环境验收：强制英文后整树翻译，关键控件文案应变成英文。</summary>
    private static void LocalizeProbe(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        string result = host?.SelfTestLocalizeProbe() ?? "localize=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {result}");

        Check(result.Contains("Play'") || result.Contains("Stop'"), $"play button not localized ({result})");
        Check(result.Contains("auto='Auto-play'"), $"模式按钮未译为英文（{result}）");
        Check(result.Contains("status='Scale:"), $"状态行未译为英文（{result}）");

        // 此时语言已被强制成英文，接着截一张英文界面图（工坊英文页面要用）
        Schedule(tree, 0.2, () => Step(tree, 19));
    }

    /// <summary>英文界面截图：工坊英文页面与审核报告 §4/§6 需要。</summary>
    private static void ShotEnglish(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        if (host != null)
        {
            host.SelfTestConfigure(Core.PanelSize.Standard, snap: true, inst: "harp");
            host.SelfTestCollapse();      // 先收起再展开，确保布局稳定
            host.SelfTestCollapse();
        }
        Schedule(tree, 0.6, () =>
        {
            Capture(tree, "08_english_ui");
            Step(tree, 20);
        });
    }

    // ---- 联机回环：进程内起一对 ENet 主机/客户端，真发真收 ----

    private static Net.NetLoopbackProbe? _loopback;

    private static void LoopbackStart(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        if (host == null)
        {
            Check(false, "回环探针：找不到宿主节点");
            Schedule(tree, 0.1, () => Step(tree, 12));
            return;
        }

        _loopback = new Net.NetLoopbackProbe { Name = "NetLoopbackProbe" };
        host.AddChild(_loopback);
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] loopback started (port 47821)");
        Schedule(tree, 9.0, () => Step(tree, 17));
    }

    private static void LoopbackEnd(SceneTree tree)
    {
        string result = _loopback?.Describe() ?? "loopback=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] loopback {result}");

        int hostGot = _loopback?.HostReceived ?? 0;
        int clientAGot = _loopback?.ClientAReceived ?? 0;
        int clientBGot = _loopback?.ClientBReceived ?? 0;
        Check(hostGot > 0, $"回环：主机没收到 A 的音符（{result}）");
        Check(clientAGot > 0, $"回环：A 没收到主机定向消息（{result}）");
        // 这一条是"两个玩家互相听到"的核心：A 发的音符被主机**转发**给了 B
        Check(clientBGot > 0, $"回环：B 没收到主机转发的音符 —— 两玩家互听不成立（{result}）");

        if (_loopback != null && GodotObject.IsInstanceValid(_loopback)) _loopback.QueueFree();
        _loopback = null;

        // 回环跑完：有渲染器就去截图巡游（6→…→11→15→18→19），无渲染器直接进英文验收
        bool headless = DisplayServer.GetName() == "headless";
        Schedule(tree, 0.2, () => Step(tree, headless ? 18 : 6));
    }

    /// <summary>跟弹模式截图：验证下落轨道（M3 遗留的视觉验证）。</summary>
    private static void ShotLearn(SceneTree tree)
    {
        Configure(Core.PanelSize.Standard, snap: true, inst: "harp");
        var host = Ui.PanelHost.Instance;
        string s = host != null ? host.SelfTestLearnStart() : "learn=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {s}");
        // 等 1.4 秒：让方块落到轨道中段再截图
        Schedule(tree, 1.4, () =>
        {
            Capture(tree, "07_learn_lane");
            host?.SelfTestSongStop();
            Step(tree, 18);
        });
    }

    // ---- 曲目播放验收：整曲是否真的被调度出去 ----

    private static void SongStart(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        string s = host != null ? host.SelfTestSongStart() : "song=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {s}");
        Schedule(tree, 3.0, () => Step(tree, 14));
    }

    private static void SongEnd(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        int scheduled = 0;
        if (host != null)
        {
            string s = host.SelfTestSongStop();
            GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {s}");
            var m = System.Text.RegularExpressions.Regex.Match(s, @"scheduled=(\d+)");
            if (m.Success) int.TryParse(m.Groups[1].Value, out scheduled);
        }

        // 3 秒内《小星星》（104BPM）应排出若干音符；排不出来说明调度器没跑
        Check(scheduled >= 5, $"3 秒内只排出了 {scheduled} 个音符，曲目调度可能未生效");

        // 接着验联机回环（不依赖渲染器）
        Schedule(tree, 0.3, () => Step(tree, 16));
    }

    // ---- 延音类验收：循环采样是否真的"按住持续、松键停下" ----

    private static void SustainStart(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        string s = host != null ? host.SelfTestSustainStart() : "sustain=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {s}");
        Schedule(tree, 1.2, () => Step(tree, 4));
    }

    /// <summary>1.2 秒后延音声部仍应在响（一次性音色早就衰减完了，这是循环是否生效的判据）。</summary>
    private static void SustainHold(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        int voices = host?.SelfTestVoices() ?? -1;
        int baseline = host?.SelfTestSustainBaseline() ?? -1;
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] sustain-hold voices={voices} baseline={baseline}");
        Check(voices > baseline, $"延音音色在 1.2 秒后仍在发声（voices={voices} baseline={baseline}，循环未生效）");

        string s = host != null ? host.SelfTestSustainRelease() : "sustain=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {s}");
        Schedule(tree, 0.6, () => Step(tree, 5));
    }

    /// <summary>释放后应回到基线（淡出 + 归还声部池）；注意别把仍在衰减的一次性试奏音算进来。</summary>
    private static void SustainEnd(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        int voices = host?.SelfTestVoices() ?? -1;
        int baseline = host?.SelfTestSustainBaseline() ?? -1;
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] sustain-end voices={voices} baseline={baseline}");
        Check(voices <= baseline, $"延音释放后声部未回到基线（voices={voices} baseline={baseline}）");

        Schedule(tree, 0.4, () => Step(tree, 13));   // 接着验曲目播放
    }

    // ---- 截图巡游：让 UI 迭代也能被自动化（每档尺寸/模式各出图一张）----

    private static void ShotStandard(SceneTree tree)
    {
        Configure(Core.PanelSize.Standard, snap: true, inst: "harp");
        Ui.PanelHost.Instance?.SelfTestPlayDemo();
        Schedule(tree, 0.6, () => { Capture(tree, "01_standard_pentatonic"); Step(tree, 7); });
    }

    private static void ShotLarge(SceneTree tree)
    {
        Configure(Core.PanelSize.Large, snap: true, inst: "piano");
        Schedule(tree, 0.6, () => { Capture(tree, "02_large_piano"); Step(tree, 8); });
    }

    private static void ShotMini(SceneTree tree)
    {
        Configure(Core.PanelSize.Mini, snap: true, inst: "harp");
        Schedule(tree, 0.6, () => { Capture(tree, "03_mini"); Step(tree, 9); });
    }

    private static void ShotChromatic(SceneTree tree)
    {
        Configure(Core.PanelSize.Standard, snap: false, inst: "musicbox");
        Schedule(tree, 0.6, () => { Capture(tree, "04_standard_chromatic"); Step(tree, 10); });
    }

    private static void ShotChip(SceneTree tree)
    {
        Configure(Core.PanelSize.Standard, snap: true, inst: "chip");
        Schedule(tree, 0.6, () => { Capture(tree, "05_standard_chip"); Step(tree, 11); });
    }

    /// <summary>收起态：验证右下角的音符小图标（用户明确要求的默认收起形态）。</summary>
    private static void ShotDock(SceneTree tree)
    {
        Ui.PanelHost.Instance?.SelfTestCollapse();
        Schedule(tree, 0.5, () => { Capture(tree, "06_dock_collapsed"); Step(tree, 15); });
    }

    private static void Configure(Core.PanelSize size, bool snap, string inst)
    {
        var host = Ui.PanelHost.Instance;
        if (host == null) return;
        host.SelfTestConfigure(size, snap, inst);
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] configure size={size} snap={snap} inst={inst}");
    }

    private static void Capture(SceneTree tree, string name)
    {
        if (DisplayServer.GetName() == "headless") return;   // 无渲染器，不记失败

        var tex = tree.Root?.GetTexture();
        var img = tex?.GetImage();
        if (img == null || img.IsEmpty())
        {
            _failures++;
            GD.PushError($"{SpireInstrumentMod.LogTag} [SELFTEST] capture {name} failed: empty image");
            return;
        }

        string rel = $"user://spireinstrument_{name}.png";
        var err = img.SavePng(rel);
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] capture {name} size={img.GetWidth()}x{img.GetHeight()} " +
                 $"err={err} path={ProjectSettings.GlobalizePath(rel)}");
    }

    private static void ProbeHost(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        bool ok = host != null && GodotObject.IsInstanceValid(host);
        Check(ok, "host installed");
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] host={(ok ? 1 : 0)}");

        ProbeSettings();
        ProbeUiScale();
        ProbeNet();

        Schedule(tree, 0.5, () => Step(tree, 2));
    }

    /// <summary>
    /// 联机验收：mod 自定义消息能否被游戏的序列化器与消息表接受。
    /// 这是联机功能的**关键集成点** —— 消息类型没被登记，联机就完全收不到东西。
    /// </summary>
    /// <summary>面板尺寸与 UI 缩放（排查高分屏偏小用）。</summary>
    private static void ProbeUiScale()
    {
        var host = Ui.PanelHost.Instance;
        string s = host != null ? host.SelfTestUiScale() : "ui=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {s}");
        Check(!s.Contains("keys=0"), "键盘没有键");
    }
    private static void ProbeNet()
    {
        string result = Net.InstrumentNet.SelfTestRoundTrip();
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] net {result}");
        Check(result.Contains("noteRt=True"), "音符消息序列化往返失败");
        Check(result.Contains("songRt=True"), "曲谱消息序列化往返失败");
        Check(result.Contains("registered=True"), "mod 自定义消息未被游戏消息表登记（联机会收不到）");
    }

    /// <summary>
    /// 设置持久化验收：写盘 → 从磁盘读回 → 与内存逐字段比对。
    /// 刻意**不改动**任何取值（写回原样），所以不会污染玩家的设置。
    /// </summary>
    private static void ProbeSettings()
    {
        var before = Core.ModSettings.Data;
        bool saved = Core.ModSettings.SaveNow();
        var reloaded = Core.SettingsStore.Load();

        bool same = reloaded.Octave == before.Octave
                    && reloaded.InstrumentId == before.InstrumentId
                    && reloaded.ScaleSnap == before.ScaleSnap
                    && reloaded.VolumePercent == before.VolumePercent
                    && reloaded.PanelSize == before.PanelSize
                    && reloaded.SummonKey == before.SummonKey
                    && reloaded.PanelVisible == before.PanelVisible
                    && reloaded.AudienceVolumePercent == before.AudienceVolumePercent
                    && reloaded.MuteAudience == before.MuteAudience
                    && reloaded.PlayMode == before.PlayMode
                    && reloaded.LastSongId == before.LastSongId;

        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] settings save={saved} roundtrip={same} " +
                 $"{Core.ModSettings.Describe()} path={Core.SettingsStore.AbsolutePath()}");
        Check(saved, "设置写盘失败");
        Check(same, "设置写盘后读回与内存不一致");
    }

    private static void ProbePanel(SceneTree tree)
    {
        var host = Ui.PanelHost.Instance;
        string probe = host != null ? host.SelfTestProbe() : "panel=missing";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] {probe}");

        // 探测串形如：panel=True keys=13/13 insts=9/9 voices=3 played=3 ex=false snap=True oct=4 dock=...
        // 注意：ex 表示"播放过程中抛异常"，所以成功条件是 ex=false
        int expectedInsts = Audio.InstrumentLibrary.Items.Count;
        Check(probe.Contains("panel=True"), "panel built");
        Check(!probe.Contains("keys=0/"), "keyboard has keys");
        Check(!probe.Contains("keys=13/25") && !probe.Contains("keys=25/13"), "key count matches scale mode");
        Check(probe.Contains($"insts={expectedInsts}/{expectedInsts}"), $"all {expectedInsts} instruments listed");
        Check(!probe.Contains("voices=0"), "voices produced sound");
        Check(probe.Contains("ex=false"), "exception thrown while playing");

        // 接下来先验延音类（两种模式都能验），截图巡游只在有渲染器时跑
        Schedule(tree, 1.0, () => Step(tree, 3));
    }

    private static void Finish(SceneTree tree)
    {
        string result = _failures == 0 ? "PASS" : "FAIL";
        GD.Print($"{SpireInstrumentMod.LogTag} [SELFTEST] RESULT={result} failures={_failures}");

        // 只有走到这一步才退出；任何提前退出都会让自检结果缺失
        tree.Quit();
    }

    private static void Check(bool ok, string what)
    {
        if (ok) return;
        _failures++;
        GD.PushError($"{SpireInstrumentMod.LogTag} [SELFTEST] FAIL: {what}");
    }
}
