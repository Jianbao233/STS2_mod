using Godot;
using System;
using System.Reflection;

namespace ControlPanel;

/// <summary>
/// 从游戏资源加载图标：卡牌、遗物、药水、能力等。
/// 优先使用 AtlasManager，fallback 使用 ResourceLoader。
/// </summary>
public static class IconLoader
{
    private const string CardAtlas = "card_atlas";
    private const string RelicAtlas = "relic_atlas";
    private const string PotionAtlas = "potion_atlas";
    private const string PowerAtlas = "power_atlas";

    /// <summary>卡牌图标路径：res://images/packed/card_portraits/{id}.png</summary>
    private const string CardFallbackPrefix = "res://images/packed/card_portraits/";
    /// <summary>遗物图标路径：res://images/relics/{id}.png</summary>
    private const string RelicFallbackPrefix = "res://images/relics/";
    /// <summary>药水图标路径：res://images/potions/{id}.png</summary>
    private const string PotionFallbackPrefix = "res://images/potions/";
    /// <summary>能力图标路径：res://images/powers/{id}.png</summary>
    private const string PowerFallbackPrefix = "res://images/powers/";

    /// <summary>获取卡牌图标，spriteName 通常为 CardClassName 或 Id（如 BODY_SLAM）</summary>
    public static Texture2D GetCardIcon(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        var atlas = TryAtlasGetSprite(CardAtlas, spriteName);
        if (atlas is Texture2D tex) return tex;
        return ResourceLoader.Load<Texture2D>($"{CardFallbackPrefix}{spriteName}.png", null, ResourceLoader.CacheMode.Reuse);
    }

    /// <summary>获取遗物图标（游戏 atlas 使用小写 ID）</summary>
    public static Texture2D GetRelicIcon(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        var lower = spriteName.ToLowerInvariant();
        var atlas = TryAtlasGetSprite(RelicAtlas, lower);
        if (atlas is Texture2D tex) return tex;
        return ResourceLoader.Load<Texture2D>($"{RelicFallbackPrefix}{lower}.png", null, ResourceLoader.CacheMode.Reuse);
    }

    /// <summary>获取药水图标（游戏 atlas 使用小写 ID）</summary>
    public static Texture2D GetPotionIcon(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        var lower = spriteName.ToLowerInvariant();
        var atlas = TryAtlasGetSprite(PotionAtlas, lower);
        if (atlas is Texture2D tex) return tex;
        return ResourceLoader.Load<Texture2D>($"{PotionFallbackPrefix}{lower}.png", null, ResourceLoader.CacheMode.Reuse);
    }

    /// <summary>获取能力图标（游戏 atlas 使用小写 ID，如 plated_armor_power）</summary>
    public static Texture2D GetPowerIcon(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return null;
        var lower = spriteName.ToLowerInvariant();
        var atlas = TryAtlasGetSprite(PowerAtlas, lower);
        if (atlas is Texture2D tex) return tex;
        return ResourceLoader.Load<Texture2D>($"{PowerFallbackPrefix}{lower}.png", null, ResourceLoader.CacheMode.Reuse);
    }

    /// <summary>尝试通过 AtlasManager.GetSprite 获取贴图（返回 AtlasTexture，继承自 Texture2D）</summary>
    private static object TryAtlasGetSprite(string atlasName, string spriteName)
    {
        try
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var atlasType = asm.GetType("MegaCrit.Sts2.Core.Assets.AtlasManager")
                    ?? asm.GetType("Sts2.Core.Assets.AtlasManager");
                if (atlasType == null) continue;
                var method = atlasType.GetMethod("GetSprite", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string), typeof(string) }, null);
                if (method == null) continue;
                return method.Invoke(null, new object[] { atlasName, spriteName });
            }
        }
        catch { /* 忽略 */ }
        return null;
    }
}
