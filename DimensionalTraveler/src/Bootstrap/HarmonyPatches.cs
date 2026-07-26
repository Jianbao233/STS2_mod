using System.Reflection;
using HarmonyLib;

namespace DimensionalTraveler.Bootstrap;

public static class HarmonyPatches
{
    private const string HarmonyId = "DimensionalTraveler";
    private static bool _installed;

    public static void Install(Assembly assembly)
    {
        if (_installed)
            return;

        new Harmony(HarmonyId).PatchAll(assembly);
        _installed = true;
    }
}