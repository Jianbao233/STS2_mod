using System;
using Godot;

namespace MP_PlayerManager
{
    /// <summary>
    /// Steam 集成模块：
    /// 读取本地 Steam 配置（localconfig.vdf / loginusers.vdf），
    /// 获取当前用户的 Steam ID / 昵称。
    /// </summary>
    internal static class SteamIntegration
    {
        /// <summary>Steam 安装目录（自动探测）。</summary>
        internal static string SteamPath
        {
            get
            {
                if (_steamPath == null) _steamPath = DetectSteamPath();
                return _steamPath ?? "";
            }
        }

        /// <summary>当前登录用户的 Steam ID（64 位整数字符串）。</summary>
        internal static string CurrentSteamId => _currentSteamId ?? "";
        /// <summary>当前登录用户的 Steam 昵称。</summary>
        internal static string CurrentSteamName => _currentSteamName ?? "Unknown";

        private static string _steamPath = "";
        private static string _currentSteamId = "";
        private static string _currentSteamName = "Unknown";

        /// <summary>
        /// 初始化 Steam 集成（读取本地配置）。
        /// </summary>
        internal static void Initialize()
        {
            try
            {
                ReadLocalConfig();
                ReadLoginUsers();
                GD.Print($"[MP_PlayerManager] SteamIntegration initialized: id={CurrentSteamId}, name={CurrentSteamName}, path={SteamPath}");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] SteamIntegration.Initialize failed: {ex.Message}");
            }
        }

        // ── Steam 路径检测 ───────────────────────────────────────────────────

        private static string DetectSteamPath()
        {
            var candidates = new[]
            {
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFilesX86) + @"\Steam",
                @"C:\Program Files (x86)\Steam",
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles) + @"\Steam",
                @"C:\Program Files\Steam",
            };

            foreach (var p in candidates)
            {
                if (System.IO.Directory.Exists(p)) return p;
            }

            return "";
        }

        // ── localconfig.vdf（当前登录用户的 ID 和昵称）─────────────────────────

        private static void ReadLocalConfig()
        {
            string basePath = SteamPath;
            if (string.IsNullOrEmpty(basePath)) return;
            string path = System.IO.Path.Combine(basePath, "config", "localconfig.vdf");
            if (!System.IO.File.Exists(path)) return;

            try
            {
                string content = System.IO.File.ReadAllText(path);
                var nameMatch = System.Text.RegularExpressions.Regex.Match(content, "\"AccountName\"\\s+\"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (nameMatch.Success)
                    _currentSteamName = nameMatch.Groups[1].Value;

                var idMatch = System.Text.RegularExpressions.Regex.Match(content, "\"SteamID\"\\s+\"(\\d+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (idMatch.Success)
                    _currentSteamId = idMatch.Groups[1].Value;

                var personaMatch = System.Text.RegularExpressions.Regex.Match(content, "\"PersonaName\"\\s+\"([^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (personaMatch.Success && string.IsNullOrEmpty(_currentSteamName))
                    _currentSteamName = personaMatch.Groups[1].Value;
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] ReadLocalConfig failed: {ex.Message}");
            }
        }

        // ── loginusers.vdf（所有已登录用户的 Steam ID 列表）──────────────────

        private static void ReadLoginUsers()
        {
            string basePath = SteamPath;
            if (string.IsNullOrEmpty(basePath)) return;
            string path = System.IO.Path.Combine(basePath, "config", "loginusers.vdf");
            if (!System.IO.File.Exists(path)) return;

            try
            {
                string content = System.IO.File.ReadAllText(path);
                var pattern = "\"(\\d+)\"\\s*\\{[^}]*?\"PersonaName\"\\s+\"([^\"]+)\"";
                var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern, System.Text.RegularExpressions.RegexOptions.Singleline);
                foreach (System.Text.RegularExpressions.Match m in matches)
                {
                    string steamId = m.Groups[1].Value;
                    string name = m.Groups[2].Value;
                    GD.Print($"[MP_PlayerManager] Steam user: {name} ({steamId})");
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[MP_PlayerManager] ReadLoginUsers failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析 Steam ID（64 位）并转换为 Steam3（短）格式。
        /// </summary>
        internal static string ToSteam3(string steamId64)
        {
            if (string.IsNullOrEmpty(steamId64)) return "";
            try
            {
                if (ulong.TryParse(steamId64, out var id))
                {
                    ulong accountId = id - 76561197960265728UL;
                    return $"[U:1:{accountId}]";
                }
            }
            catch { }
            return steamId64;
        }

        /// <summary>
        /// 获取当前用户是否已登录 Steam。
        /// </summary>
        internal static bool IsSteamRunning()
        {
            try
            {
                if (string.IsNullOrEmpty(SteamPath)) return false;
                var steamExe = System.IO.Path.Combine(SteamPath, "steam.exe");
                return System.IO.File.Exists(steamExe);
            }
            catch { return false; }
        }
    }
}