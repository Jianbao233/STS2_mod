using System;
using System.Text.Json;
using Godot;
// Godot 与 System.IO 都有 FileAccess，这里明确用 Godot 的
using FileAccess = Godot.FileAccess;

namespace SpireInstrument.Core;

/// <summary>
/// 设置存盘（Godot 侧 I/O）。
///
/// 路径用游戏既有的 mod 设置约定：<c>user://mods_settings/&lt;ModId&gt;.json</c>
/// （与 MerchantBlacklist 等 mod 同目录，便于玩家统一备份/排查）。
/// 读写失败一律降级为默认值，绝不让设置问题影响演奏。
/// </summary>
public static class SettingsStore
{
    private const string DirPath = "user://mods_settings";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        // 大小写不敏感 + 允许注释/尾逗号：手改过的文件也能读
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    public static string FilePath => $"{DirPath}/{SpireInstrumentMod.ModId}.json";

    public static SpireInstrumentSettings Load()
    {
        try
        {
            if (!FileAccess.FileExists(FilePath)) return new SpireInstrumentSettings().Normalize();

            using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Read);
            if (f == null)
            {
                GD.PushWarning($"{SpireInstrumentMod.LogTag} 设置文件打不开（{FileAccess.GetOpenError()}），使用默认值");
                return new SpireInstrumentSettings().Normalize();
            }

            string text = f.GetAsText();
            var data = JsonSerializer.Deserialize<SpireInstrumentSettings>(text, JsonOptions);
            if (data == null) return new SpireInstrumentSettings().Normalize();

            return data.Normalize();
        }
        catch (Exception e)
        {
            // 文件损坏/字段类型不对都不该让 mod 起不来
            GD.PushWarning($"{SpireInstrumentMod.LogTag} 设置读取失败，使用默认值：{e.Message}");
            return new SpireInstrumentSettings().Normalize();
        }
    }

    public static bool Save(SpireInstrumentSettings data)
    {
        try
        {
            if (!DirAccess.DirExistsAbsolute(DirPath))
            {
                var err = DirAccess.MakeDirRecursiveAbsolute(DirPath);
                if (err != Error.Ok) GD.PushWarning($"{SpireInstrumentMod.LogTag} 创建设置目录失败：{err}");
            }

            using var f = FileAccess.Open(FilePath, FileAccess.ModeFlags.Write);
            if (f == null)
            {
                GD.PushWarning($"{SpireInstrumentMod.LogTag} 设置写入失败（{FileAccess.GetOpenError()}）");
                return false;
            }

            f.StoreString(JsonSerializer.Serialize(data.Normalize(), JsonOptions));
            return true;
        }
        catch (Exception e)
        {
            GD.PushWarning($"{SpireInstrumentMod.LogTag} 设置写入异常：{e.Message}");
            return false;
        }
    }

    /// <summary>仅供自检：把设置文件的绝对路径报出来（便于人工核对）。</summary>
    public static string AbsolutePath()
    {
        try { return ProjectSettings.GlobalizePath(FilePath); }
        catch { return FilePath; }
    }
}
