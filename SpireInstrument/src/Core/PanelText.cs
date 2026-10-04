using System;
using System.Globalization;

namespace SpireInstrument.Core;

/// <summary>
/// 面板文案格式化（**纯逻辑，不引用 Godot**，可离线自检）。
///
/// 抽出来的原因：这些行是玩家最常看到的文字（曲目信息、跟弹成绩），
/// 而且**中英双语都要对** —— 放在面板里就只能靠"游戏内强制英文读控件"验证，
/// 无法断言具体内容。下沉成纯函数后，两种语言的输出都能逐条断言。
/// </summary>
public static class PanelText
{
    /// <summary>曲目信息行：音符数 / 时长 / 音域 + 吸附与折叠提示 + 导入标记。</summary>
    public static string SongInfo(int noteCount, double duration, string range,
                                  int snapped, int folded, bool imported, bool en)
    {
        string head = en
            ? $"Notes {noteCount} · {duration.ToString("F1", CultureInfo.InvariantCulture)}s · Range {range}"
            : $"音符 {noteCount} · 时长 {duration.ToString("F1", CultureInfo.InvariantCulture)}s · 音域 {range}";

        if (snapped > 0) head += en ? $"\nSnapped {snapped}" : $"\n五声吸附 {snapped} 处";
        if (folded > 0) head += en ? $"\nOctave-folded {folded}" : $"\n八度折叠 {folded} 处";
        if (imported) head += en ? "\n(imported MIDI)" : "\n（导入的 MIDI）";
        return head;
    }

    /// <summary>跟弹成绩行：命中 / 失误 / 连击 / 准确率。</summary>
    public static string Score(int hits, int misses, int combo, double accuracy, bool en)
    {
        string acc = accuracy.ToString("P0", CultureInfo.InvariantCulture);
        return en
            ? $"Hit {hits} · Miss {misses} · Combo {combo} · Acc {acc}"
            : $"命中 {hits} · 失误 {misses} · 连击 {combo} · 准确 {acc}";
    }

    /// <summary>联机状态行：连接 / 队友音量 / 队友数与收发计数（可选静音数）。</summary>
    public static string NetInfo(bool connected, string volumeText, int peerCount,
                                 int received, int sent, int muted, bool en)
    {
        string conn = connected ? (en ? "connected" : "已连接") : (en ? "not connected" : "未连接");
        string head = en
            ? $"Network: {conn}\nTeammate volume: {volumeText}\nTeammates {peerCount} · notes in {received} · out {sent}"
            : $"联机：{conn}\n队友音量：{volumeText}\n队友 {peerCount} 人 · 收到音符 {received} · 发出 {sent}";
        if (muted > 0) head += en ? $" · muted {muted}" : $" · 已静音 {muted}";
        return head;
    }
}
