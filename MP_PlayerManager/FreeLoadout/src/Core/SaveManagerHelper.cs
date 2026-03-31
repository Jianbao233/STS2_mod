using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;

namespace MP_PlayerManager
{
    /// <summary>
    /// 存档读写辅助工具：
    /// - 读取 `current_run_mp.save`（CRLF 明文 JSON）
    /// - 写入存档（`\n` → `\r\n`）
    /// - 获取存档扫描根目录（`OS.GetUserDataDir()`）
    /// - 备份文件复制
    /// </summary>
    internal static class SaveManagerHelper
    {
        private const string CurrentRunSaveFileName = "current_run_mp.save";

        /// <summary>
        /// 获取存档根目录。
        /// </summary>
        internal static string GetSaveRoot()
        {
            try
            {
                return OS.GetUserDataDir();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] GetSaveRoot failed: {ex.Message}");
                return "";
            }
        }

        /// <summary>
        /// 获取当前多人存档文件路径。
        /// </summary>
        internal static string GetCurrentRunSavePath()
        {
            string root = GetSaveRoot();
            if (string.IsNullOrEmpty(root)) return "";
            return Path.Combine(root, CurrentRunSaveFileName);
        }

        /// <summary>
        /// 读取当前多人存档（CRLF 明文 JSON）。
        /// </summary>
        internal static string? ReadCurrentRunSave()
        {
            string path = GetCurrentRunSavePath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                GD.Print($"[MP_PlayerManager] Save file not found: {path}");
                return null;
            }

            try
            {
                using var reader = new StreamReader(path, Encoding.UTF8);
                return reader.ReadToEnd();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] ReadCurrentRunSave failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 写入当前多人存档（自动处理 `\n` → `\r\n`）。
        /// </summary>
        internal static bool WriteCurrentRunSave(string jsonContent)
        {
            string path = GetCurrentRunSavePath();
            if (string.IsNullOrEmpty(path))
            {
                GD.PrintErr("[MP_PlayerManager] Cannot write: save path is empty.");
                return false;
            }

            try
            {
                string dir = Path.GetDirectoryName(path) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string crlfContent = jsonContent.Replace("\r\n", "\n").Replace("\n", "\r\n");
                using var writer = new StreamWriter(path, false, new UTF8Encoding(false));
                writer.Write(crlfContent);
                GD.Print($"[MP_PlayerManager] Wrote save file: {path} ({crlfContent.Length} chars)");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] WriteCurrentRunSave failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 解析当前存档 JSON。
        /// </summary>
        internal static JsonDocument? ParseCurrentSaveJson()
        {
            string content = ReadCurrentRunSave();
            if (string.IsNullOrEmpty(content)) return null;

            try
            {
                var opts = new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                return JsonDocument.Parse(content, opts);
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] ParseCurrentSaveJson failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 备份当前存档到同一目录（带时间戳）。
        /// </summary>
        internal static string? BackupCurrentRun(string destinationDir = null)
        {
            string sourcePath = GetCurrentRunSavePath();
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            {
                GD.PrintErr("[MP_PlayerManager] Backup failed: source file not found.");
                return null;
            }

            try
            {
                string dir = destinationDir ?? (Path.GetDirectoryName(sourcePath) ?? "");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupName = $"current_run_mp_backup_{timestamp}.save";
                string destPath = Path.Combine(dir, backupName);

                File.Copy(sourcePath, destPath, overwrite: true);
                GD.Print($"[MP_PlayerManager] Backup created: {destPath}");
                return destPath;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] BackupCurrentRun failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 扫描存档目录，返回所有 .save 文件列表（按修改时间降序）。
        /// </summary>
        internal static List<FileInfo> ScanSaveFiles(string searchRoot = null)
        {
            string root = searchRoot ?? GetSaveRoot();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return new List<FileInfo>();

            try
            {
                var files = new DirectoryInfo(root)
                    .GetFiles("*.save", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();
                GD.Print($"[MP_PlayerManager] Found {files.Count} save file(s) in: {root}");
                return files;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] ScanSaveFiles failed: {ex.Message}");
                return new List<FileInfo>();
            }
        }

        /// <summary>
        /// 恢复备份：从指定备份文件恢复到当前存档。
        /// </summary>
        internal static bool RestoreFromBackup(string backupPath)
        {
            if (string.IsNullOrEmpty(backupPath) || !File.Exists(backupPath))
            {
                GD.PrintErr($"[MP_PlayerManager] Restore failed: backup file not found: {backupPath}");
                return false;
            }

            try
            {
                string currentPath = GetCurrentRunSavePath();
                if (File.Exists(currentPath))
                {
                    BackupCurrentRun();
                }
                File.Copy(backupPath, currentPath, overwrite: true);
                GD.Print($"[MP_PlayerManager] Restored from backup: {backupPath} -> {currentPath}");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] RestoreFromBackup failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除指定存档文件（自动备份到 deleted/ 子目录）。
        /// </summary>
        internal static bool DeleteSaveFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                GD.PrintErr($"[MP_PlayerManager] DeleteSaveFile: file not found: {path}");
                return false;
            }

            try
            {
                string backupDir = Path.Combine(Path.GetDirectoryName(path) ?? "", "deleted");
                string backupName = Path.GetFileName(path) + $".deleted_{DateTime.Now:yyyyMMdd_HHmmss}";
                string backupDest = Path.Combine(backupDir, backupName);
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                File.Copy(path, backupDest);
                File.Delete(path);
                GD.Print($"[MP_PlayerManager] Deleted save file: {path} (backup: {backupDest})");
                return true;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] DeleteSaveFile failed: {ex.Message}");
                return false;
            }
        }
    }
}