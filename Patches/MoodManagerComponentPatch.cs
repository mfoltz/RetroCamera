using HarmonyLib;
using ProjectM.Presentation;
using UnityEngine;
using UnityEngine.Rendering;

namespace RetroCamera.Patches;

[HarmonyPatch]
internal static class MoodManagerComponentPatch
{
    public static VolumeComponent _cachedVignette;
    public static MoodManagerComponent _moodManager;

    [HarmonyPatch(typeof(MoodManagerComponent), nameof(MoodManagerComponent.OnEnable))]
    [HarmonyPostfix]
    static void OnEnable(MoodManagerComponent __instance)
    {
        Transform scenePostProcess = __instance.transform.Find("Scene PostProcess");
        Volume volume = scenePostProcess.GetComponent<Volume>();

        foreach (VolumeComponent volumeComponent in volume.profile.components)
        {
            if (volumeComponent.name.StartsWith("Vignette"))
                _cachedVignette = volumeComponent;
                _moodManager = __instance;
                break;
        }
    }
}
