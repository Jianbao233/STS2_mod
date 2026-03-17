using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace ControlPanel;

/// <summary>
/// 从 VC_STS2_FULL_IDS.json 加载完整卡牌/遗物/事件数据。
/// JSON 需放在 mod 目录或游戏 mods/ControlPanel 下。
/// </summary>
public static class FullDataLoader
{
    private static (string[] ids, string[] zhs) _cards;
    private static (string id, string zh, string rarity)[] _relics;
    private static (string id, string zh)[] _powers;

    public static (string[] ids, string[] zhs) GetFullCards()
    {
        if (_cards.ids != null) return _cards;
        try
        {
            var path = GetJsonPath();
            if (string.IsNullOrEmpty(path)) return (PotionAndCardData.CardIds, PotionAndCardData.CardZhs);
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Cards", out var cardsEl)) return (PotionAndCardData.CardIds, PotionAndCardData.CardZhs);
            var ids = new List<string>();
            var zhs = new List<string>();
            foreach (var c in cardsEl.EnumerateArray())
            {
                var id = c.TryGetProperty("Id", out var idEl) ? idEl.GetString() : "";
                var zh = c.TryGetProperty("Zhs", out var zhEl) ? zhEl.GetString() : "";
                if (!string.IsNullOrEmpty(id)) { ids.Add(id); zhs.Add(zh ?? ""); }
            }
            if (ids.Count > 0) _cards = (ids.ToArray(), zhs.ToArray());
        }
        catch { }
        return _cards.ids != null ? _cards : (PotionAndCardData.CardIds, PotionAndCardData.CardZhs);
    }

    public static (string id, string zh, string rarity)[] GetFullRelics()
    {
        if (_relics != null) return _relics;
        try
        {
            var path = GetJsonPath();
            if (string.IsNullOrEmpty(path)) return PotionAndCardData.RelicData;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Relics", out var relEl)) return PotionAndCardData.RelicData;
            var list = new List<(string, string, string)>();
            foreach (var r in relEl.EnumerateArray())
            {
                var id = r.TryGetProperty("Id", out var idEl) ? idEl.GetString() : "";
                var zh = r.TryGetProperty("Zhs", out var zhEl) ? zhEl.GetString() : "";
                var rarity = r.TryGetProperty("Rarity", out var rEl) ? rEl.GetString() ?? "Common" : "Common";
                if (!string.IsNullOrEmpty(id)) list.Add((id, zh ?? "", rarity ?? "Common"));
            }
            if (list.Count > 0) _relics = list.ToArray();
        }
        catch { }
        return _relics ?? PotionAndCardData.RelicData;
    }

    /// <summary>从 JSON Powers 加载完整能力列表（用于能力/Buff）</summary>
    public static (string id, string zh)[] GetFullPowers()
    {
        if (_powers != null) return _powers;
        try
        {
            var path = GetJsonPath();
            if (string.IsNullOrEmpty(path)) return PotionAndCardData.PowerData;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("Powers", out var pwEl)) return PotionAndCardData.PowerData;
            var list = new List<(string, string)>();
            foreach (var p in pwEl.EnumerateArray())
            {
                var id = p.TryGetProperty("Id", out var idEl) ? idEl.GetString() : "";
                var zh = p.TryGetProperty("Zhs", out var zhEl) ? zhEl.GetString() : "";
                if (!string.IsNullOrEmpty(id)) list.Add((id, zh ?? ""));
            }
            if (list.Count > 0) _powers = list.ToArray();
        }
        catch { }
        return _powers ?? PotionAndCardData.PowerData;
    }

    private static string GetJsonPath()
    {
        try
        {
            var exePath = OS.GetExecutablePath();
            var gameDir = Path.GetDirectoryName(exePath);
            var candidates = new[]
            {
                Path.Combine(gameDir ?? "", "mods", "ControlPanel", "VC_STS2_FULL_IDS.json"),
                Path.Combine(gameDir ?? "", "VC_STS2_FULL_IDS.json"),
                Path.Combine(AppContext.BaseDirectory, "VC_STS2_FULL_IDS.json"),
            };
            foreach (var p in candidates)
            {
                if (!string.IsNullOrEmpty(p) && File.Exists(p)) return p;
            }
        }
        catch { }
        return "";
    }
}
