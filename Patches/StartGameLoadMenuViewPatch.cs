using HarmonyLib;
using ProjectM.UI;

namespace RetroCamera.Patches;

[HarmonyPatch]
internal static class StartGameLoadMenuViewPatch
{
    [HarmonyPatch(typeof(StartGameLoadMenuView), nameof(StartGameLoadMenuView.Update))]
    [HarmonyPrefix]
    static void UpdatePrefix(StartGameLoadMenuView __instance)
    {
        if (!Settings.SkipIntro) return;
        else if (__instance.VideoPlayer.isPlaying)
        {
            __instance.VideoPlayer.Stop();
        }
    }
}
