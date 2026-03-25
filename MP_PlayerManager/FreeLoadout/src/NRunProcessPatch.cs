using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace MP_PlayerManager
{
    /// <summary>
    /// 在游戏 _Process 中触发初始化和预设应用。
    /// </summary>
    [HarmonyPatch]
    internal static class NRunProcessPatch
    {
        [HarmonyPostfix]
        [HarmonyPatch(typeof(NRun), "_Process")]
        private static void Postfix(NRun __instance)
        {
            TrainerState.Apply(__instance);
            PowerPresets.CheckAndApply();
        }
    }
}
