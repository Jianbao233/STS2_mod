using System;
using System.IO;
using Godot;

namespace MultiplayerTools.Platform
{
    /// <summary>
    /// Cross-platform detection for Android, iOS and PC.
    /// On startup, prints executable path, user data dir, platform name, and mod folder structure.
    /// </summary>
    internal static class PlatformInfo
    {
        /// <summary>True when running on Android or iOS.</summary>
        internal static bool IsMobile => OS.GetName() == "Android" || OS.GetName() == "iOS";
        /// <summary>True when running on Android.</summary>
        internal static bool IsAndroid => OS.GetName() == "Android";
        /// <summary>Current platform: "android", "ios", or "pc".</summary>
        internal static string GetPlatform() => OS.GetName().ToLower();

        /// <summary>Returns the user data directory path for mobile, falls back to OS.GetUserDataDir().</summary>
        internal static string GetMobileSaveRoot()
        {
            if (!IsMobile)
                return OS.GetUserDataDir();
            // OS.GetUserDataDir() should work on mobile; just return it
            return OS.GetUserDataDir();
        }

        /// <summary>
        /// Log platform info on startup:
        /// - executable path, user data dir, platform name
        /// - full directory scan of mod folder structure (prefixed [MultiplayerTools][Platform])
        /// </summary>
        internal static void LogStartupInfo()
        {
            try
            {
                GD.Print($"[MultiplayerTools][Platform] Executable: {OS.GetExecutablePath()}");
                GD.Print($"[MultiplayerTools][Platform] UserData: {OS.GetUserDataDir()}");
                GD.Print($"[MultiplayerTools][Platform] Config: {OS.GetConfigDir()}");
                GD.Print($"[MultiplayerTools][Platform] Platform: {GetPlatform()} (IsMobile={IsMobile})");

                // Scan mod folder structure
                string? exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
                if (!string.IsNullOrEmpty(exeDir))
                {
                    var modsDir = Path.Combine(exeDir, "mods");
                    if (Directory.Exists(modsDir))
                    {
                        GD.Print($"[MultiplayerTools][Platform] Mod directory: {modsDir}");
                        foreach (var modDir in Directory.GetDirectories(modsDir))
                        {
                            var modName = Path.GetFileName(modDir);
                            GD.Print($"[MultiplayerTools][Platform]   [{modName}]");
                            foreach (var sub in Directory.GetDirectories(modDir))
                                GD.Print($"[MultiplayerTools][Platform]     {Path.GetFileName(sub)}/");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools][Platform] LogStartupInfo failed: " + ex.Message);
            }
        }
    }
}
