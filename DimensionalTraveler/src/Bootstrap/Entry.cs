using System.Reflection;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;
using STS2RitsuLib.Interop;
using DimensionalTraveler.Alchemy;
using DimensionalTraveler.Alchemy.Backpack;
using DimensionalTraveler.Alchemy.Extraction;
using DimensionalTraveler.Content.Cards.Potions;
using DimensionalTraveler.Resources;
using DimensionalTraveler.Progression;

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
        NarrativeTimelineRegistration.Register(ModId);
        NarrativeProgression.RegisterRunUnlockRules(ModId);
        HarmonyPatches.Install(assembly);
        AlchemyTargetTypes.Register();
        AlchemyBackpack.Register();
        AlchemyPrinciples.Register();
        ExtractionFlow.Register();
        PotionCatalog.ValidateCompleteness();
        Logger.Info("[DimensionalTraveler] 内容程序集、叙事时间线、进度规则、炼金原理与运行时补丁已注册。");
    }
}