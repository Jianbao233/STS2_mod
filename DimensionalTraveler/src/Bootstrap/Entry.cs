using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using DimensionalTraveler.Alchemy;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Resources;

namespace DimensionalTraveler.Bootstrap;

[ModInitializer("Init")]
public static class Entry
{
    public const string ModId = "DimensionalTraveler";

    public static readonly Logger Logger = RitsuLibFramework.CreateLogger(ModId, (LogType)0);

    public static void Init()
    {
        var assembly = Assembly.GetExecutingAssembly();
        RitsuLibFramework.EnsureGodotScriptsRegistered(assembly, Logger);
        ModTypeDiscoveryHub.RegisterModAssembly(ModId, assembly);
        AlchemyTargetTypes.Register();
        AlchemyBackpack.Register();
        AlchemyPrinciples.Register();
        PotionCatalog.ValidateCompleteness();
        SystemCardProtection.Install(assembly);
        Logger.Info("[DimensionalTraveler] 内容程序集、目标类型、药剂背包、炼金原理与系统牌保护已注册。");
    }
}