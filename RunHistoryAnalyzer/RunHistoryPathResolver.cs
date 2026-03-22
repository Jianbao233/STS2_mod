using System;
using System.IO;
using System.Reflection;
using Godot;
using HarmonyLib;

namespace RunHistoryAnalyzer;

/// <summary>
/// 将游戏内的历史记录路径解析为 OS 可读的绝对路径。
/// RunHistorySaveManager.GetHistoryPath 仅返回 profile/saves/history 相对片段，需接 user:// 并由 GlobalizePath 转换。
/// </summary>
internal static class RunHistoryPathResolver
{
    /// <summary>
    /// 尝试得到真实磁盘上存在的 .run 文件路径。
    /// </summary>
    public static string? TryResolveExistingFile(string fileName, int profileId)
    {
        if (string.IsNullOrEmpty(fileName))
            return null;

        fileName = fileName.Trim();
        // 已是绝对路径且存在
        try
        {
            if (File.Exists(fileName))
                return Path.GetFullPath(fileName);
        }
        catch { }

        // 仅文件名或相对路径：拼 user:// 再 GlobalizePath
        var baseName = Path.GetFileName(fileName);
        if (string.IsNullOrEmpty(baseName) || !baseName.EndsWith(".run", StringComparison.OrdinalIgnoreCase))
            baseName = fileName;

        var godotUserPath = TryBuildUserHistoryFileGodotPath(profileId, baseName);
        if (string.IsNullOrEmpty(godotUserPath))
            return null;

        try
        {
            var osPath = ProjectSettings.GlobalizePath(godotUserPath);
            if (File.Exists(osPath))
            {
                GD.Print($"[RunHistoryAnalyzer] Resolved history file: {osPath}");
                return osPath;
            }

            GD.PrintErr($"[RunHistoryAnalyzer] File missing after GlobalizePath: {osPath} (godot={godotUserPath})");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[RunHistoryAnalyzer] GlobalizePath failed: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// 构建 user://.../saves/history/xxx.run
    /// </summary>
    static string? TryBuildUserHistoryFileGodotPath(int profileId, string fileName)
    {
        try
        {
            var udp = AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.UserDataPathProvider");
            if (udp == null)
                return null;

            var platformType = udp.Assembly.GetType("MegaCrit.Sts2.Core.Platform.PlatformType");
            if (platformType == null)
                return null;

            var nullablePlatform = typeof(Nullable<>).MakeGenericType(platformType);
            var mi = udp.GetMethod(
                "GetProfileScopedPath",
                BindingFlags.Public | BindingFlags.Static,
                null,
                new[] { typeof(int), typeof(string), nullablePlatform, typeof(ulong?) },
                null);
            if (mi == null)
                return null;

            object?[] args = { profileId, "saves", null, null };
            var scopedSaves = mi.Invoke(null, args) as string;
            if (string.IsNullOrEmpty(scopedSaves))
                return null;

            var dir = scopedSaves.TrimEnd('/', '\\').Replace('\\', '/');
            return $"{dir}/history/{fileName}";
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[RunHistoryAnalyzer] TryBuildUserHistoryFileGodotPath: {ex.Message}");
            return null;
        }
    }
}
