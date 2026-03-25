using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace MP_PlayerManager
{
    /// <summary>
    /// 模板的持久化存储（JSON 文件）。
    /// </summary>
    internal static class TemplateStorage
    {
        private static readonly string StoragePath = Path.Combine(
            OS.GetUserDataDir(), "MP_PlayerManager_templates.json");

        public static List<TemplateData> LoadAll()
        {
            try
            {
                if (File.Exists(StoragePath))
                {
                    string json = File.ReadAllText(StoragePath);
                    var templates = JsonSerializer.Deserialize<List<TemplateData>>(json);
                    return templates ?? new List<TemplateData>();
                }
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] Failed to load templates: " + ex.Message);
            }
            return new List<TemplateData>();
        }

        public static void SaveAll(List<TemplateData> templates)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(templates, options);
                File.WriteAllText(StoragePath, json);
            }
            catch (Exception ex)
            {
                GD.PrintErr("[MP_PlayerManager] Failed to save templates: " + ex.Message);
            }
        }
    }
}
