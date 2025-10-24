using HarmonyLib;
using ProjectM.UI;
using RetroCamera.Utilities;
using static RetroCamera.Configuration.QuipManager;
using static RetroCamera.Systems.RetroCamera;
using static RetroCamera.Utilities.Quips;

namespace RetroCamera.Patches;

[HarmonyPatch]
internal static class ActionWheelSystemPatch
{
    public static bool _wheelVisible;

    [HarmonyPatch(typeof(ActionWheelSystem), nameof(ActionWheelSystem.OnUpdate))]
    [HarmonyPostfix]
    static void OnUpdatePostfix(ActionWheelSystem __instance)
    {
        if (_wheelVisible)
        {
            if (__instance._CurrentActiveWheel?.IsVisible() == false)
            {
                CameraState.IsMenuOpen = false;
                _wheelVisible = false;
            }
            else if (__instance._CurrentActiveWheel == null)
            {
                CameraState.IsMenuOpen = false;
                _wheelVisible = false;
            }
        }
        else if (__instance._CurrentActiveWheel?.IsVisible() == true)
        {
            _wheelVisible = true;
            CameraState.IsMenuOpen = true;
        }
    }

    static DateTime _wheelOpened = DateTime.MinValue;
    static DateTime _lastQuipSendTime = DateTime.MinValue;
    const float QUIP_COOLDOWN_SECONDS = 0.5f;

    [HarmonyPatch(typeof(ActionWheelSystem), nameof(ActionWheelSystem.SendQuipChatMessage))]
    [HarmonyPrefix]
    static bool SendQuipChatMessagePrefix(byte index)
    {
        DateTime now = DateTime.UtcNow;

        if (_wheelOpened.Equals(DateTime.MinValue))
        {
            _wheelOpened = now;
        }

        if ((now - _wheelOpened).TotalSeconds < 0.1f)
            return false;

        if ((now - _lastQuipSendTime).TotalSeconds < QUIP_COOLDOWN_SECONDS)
            return false;

        if (ActiveCategory.HasValue)
        {
            if (index == 0)
            {
                ClearActiveCategory();
                ShowCategoryMenu();
                return false;
            }

            if (index > 0)
            {
                byte quipIndex = (byte)(index - 1);

                if (TryGetQuip(quipIndex, out CommandQuip commandQuip))
                {
                    _lastQuipSendTime = now;
                    SendCommandQuip(commandQuip);
                    return false;
                }

                ClearActiveCategory();
                ShowCategoryMenu();
                return false;
            }
        }
        else if (TryGetCategory(index, out CommandCategory category) && category.HasEntries)
        {
            if (SetActiveCategory(index))
            {
                if (!ShowCategoryQuips(index))
                {
                    ClearActiveCategory();
                    ShowCategoryMenu();
                }

                return false;
            }
        }

        if (TryGetQuip(index, out CommandQuip fallbackQuip))
        {
            _lastQuipSendTime = now;
            SendCommandQuip(fallbackQuip);
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(ActionWheelSystem), nameof(ActionWheelSystem.HideCurrentWheel))]
    [HarmonyPrefix]
    static bool HideCurrentWheelPrefix(ActionWheelSystem __instance)
    {
        bool closingSocialWheel = SocialWheel != null && __instance?._CurrentActiveWheel == SocialWheel;
        bool shouldResetWheel = SocialWheelActive || closingSocialWheel || ActiveCategory.HasValue;

        if (shouldResetWheel)
        {
            ClearActiveCategory();
            ShowCategoryMenu();
        }

        if (!_wheelOpened.Equals(DateTime.MinValue))
        {
            _wheelOpened = DateTime.MinValue;
        }

        if (SocialWheelActive)
        {
            return false;
        }

        return true;
    }

    /*
    [HarmonyPatch(typeof(ActionWheelSystem), nameof(ActionWheelSystem.UpdateAndShowWheel))]
    [HarmonyPrefix]
    static void UpdateAndShowWheelPrefix(ActionWheelSystem __instance)
    {
    }
    */
}
