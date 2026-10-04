using System.Collections.Generic;
using Godot;
// Godot 与 System.IO 都有 FileAccess，这里明确用 Godot 的
using FileAccess = Godot.FileAccess;

namespace SpireInstrument.Songs;

/// <summary>
/// 导入曲目目录：玩家把 <c>.mid</c> 丢进 <c>user://songs/</c> 就会出现在曲目列表里。
///
/// 为什么不用文件对话框：游戏内弹出 Godot 自带对话框体验差且容易和游戏输入打架；
/// 固定目录 + 一个"打开文件夹"按钮更直接（也和 CustomBGM Player 等 mod 的做法一致）。
/// </summary>
public static class SongFolder
{
    public const string DirPath = "user://songs";

    public static string AbsolutePath
    {
        get
        {
            try { return ProjectSettings.GlobalizePath(DirPath); }
            catch { return DirPath; }
        }
    }

    public static void EnsureExists()
    {
        try
        {
            if (!DirAccess.DirExistsAbsolute(DirPath)) DirAccess.MakeDirRecursiveAbsolute(DirPath);
        }
        catch { /* 建不出来就让列表为空，不影响演奏 */ }
    }

    /// <summary>列出目录里的 .mid / .midi 文件（相对路径 + 显示名）。</summary>
    public static List<(string Path, string Name)> ListMidiFiles()
    {
        var result = new List<(string, string)>();
        try
        {
            EnsureExists();
            using var dir = DirAccess.Open(DirPath);
            if (dir == null) return result;

            foreach (string file in dir.GetFiles())
            {
                string lower = file.ToLowerInvariant();
                if (!lower.EndsWith(".mid") && !lower.EndsWith(".midi")) continue;
                result.Add(($"{DirPath}/{file}", System.IO.Path.GetFileNameWithoutExtension(file)));
            }
        }
        catch { /* 目录不可读时返回空列表 */ }
        return result;
    }

    /// <summary>读取并解析一个 MIDI 文件。</summary>
    public static MidiParseResult LoadFile(string path)
    {
        try
        {
            if (!FileAccess.FileExists(path))
                return new MidiParseResult { Error = "文件不存在" };

            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
            if (f == null)
                return new MidiParseResult { Error = $"打不开文件（{FileAccess.GetOpenError()}）" };

            byte[] bytes = f.GetBuffer((long)f.GetLength());
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            return MidiFileParser.Parse(bytes, name);
        }
        catch (System.Exception e)
        {
            return new MidiParseResult { Error = e.Message };
        }
    }
}
