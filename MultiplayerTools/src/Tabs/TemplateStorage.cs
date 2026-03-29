using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace MultiplayerTools.Tabs
{
    /// <summary>
    /// Template persistence. Stores templates in config.json's directory as mp_templates.json.
    /// </summary>
    internal static class TemplateStorage
    {
        private static readonly string StoragePath;

        static TemplateStorage()
        {
            string? dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string? exeDir = Path.GetDirectoryName(OS.GetExecutablePath());
            string fromDll = !string.IsNullOrEmpty(dir) ? Path.Combine(dir, "mp_templates.json") : "";
            string fromExe = !string.IsNullOrEmpty(exeDir) ? Path.Combine(exeDir, "mods", "MultiplayerTools", "mp_templates.json") : "";
            StoragePath = !string.IsNullOrEmpty(fromDll) ? fromDll : fromExe;
        }

        internal static List<TemplateData> LoadAll()
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    var json = File.ReadAllText(StoragePath);
                    var list = JsonSerializer.Deserialize<List<TemplateData>>(json);
                    if (list != null) return list;
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] LoadAll templates failed: " + ex.Message);
            }
            return new List<TemplateData>();
        }

        internal static void SaveAll(List<TemplateData> templates)
        {
            try
            {
                var dir = Path.GetDirectoryName(StoragePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var opts = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(templates, opts);
                File.WriteAllText(StoragePath, json);
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MultiplayerTools] SaveAll templates failed: " + ex.Message);
            }
        }
    }
}
