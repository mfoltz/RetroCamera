using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using RetroCamera.Patches;
using System.Reflection;
using UnityEngine;

namespace RetroCamera;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
internal class Plugin : BasePlugin
{
    Harmony _harmony;
    internal static Plugin Instance { get; private set; }
    public static ManualLogSource LogInstance => Instance.Log;
    public override void Load()
    {
        Instance = this;

        if (Application.productName == "VRisingServer")
        {
            Core.Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME}[{MyPluginInfo.PLUGIN_VERSION}] is a client mod! ({Application.productName})");
            return;
        }

        AddComponent<Systems.RetroCamera>();
        _harmony = Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        Core.Log.LogInfo($"{MyPluginInfo.PLUGIN_NAME}[{MyPluginInfo.PLUGIN_VERSION}] loaded on client!");
    }
    public override bool Unload()
    {
        TopdownCameraSystemHooks.Dispose();
        _harmony.UnpatchSelf();

        return true;
    }
}