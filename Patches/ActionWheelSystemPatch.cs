using HarmonyLib;
using ProjectM.UI;
using RetroCamera.Configuration;
using RetroCamera.Utilities;
using Stunlock.Localization;
using System.Linq;
using static RetroCamera.Configuration.QuipManager;
using static RetroCamera.Utilities.Quips;
using static RetroCamera.Systems.RetroCamera;

namespace RetroCamera.Patches;

[HarmonyPatch]
internal static class ActionWheelSystemPatch
{
    public static bool _wheelVisible = false;

    [HarmonyPatch(typeof(ActionWheelSystem), nameof(ActionWheelSystem.OnUpdate))]
    [HarmonyPostfix]
    static void OnUpdatePostfix(ActionWheelSystem __instance)
    {
        if (_wheelVisible)
        {
            if (__instance._CurrentActiveWheel != null && !__instance._CurrentActiveWheel.IsVisible())
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
        else if (__instance._CurrentActiveWheel != null && __instance._CurrentActiveWheel.IsVisible())
        {
            _wheelVisible = true;
            CameraState.IsMenuOpen = true;
        }

        // Core.Log.LogWarning($"IsWheelActive {ActionWheelSystem.IsWheelActive}");
    }

    static DateTime _wheelOpened = DateTime.MinValue;
    static DateTime _lastQuipSendTime = DateTime.MinValue;
    const float QUIP_COOLDOWN_SECONDS = 0.5f;

    static bool _inCategoryMode = true;
    static string _activeCategory = string.Empty;
    static readonly LocalizationKey _backKey = LocalizationManager.GetLocalizationKey("Back");

    static void PopulateCategories()
    {
        var categories = CommandCategories.Keys.ToList();
        var dataList = ActionWheelSystem._SocialWheelDataList;
        var shortcuts = ActionWheelSystem._SocialWheelShortcutList;

        for (int i = 0; i < dataList.Count; i++)
        {
            ActionWheelData data = dataList[i];
            if (i < categories.Count)
            {
                data.Name = LocalizationManager.GetLocalizationKey(categories[i]);
                data.CategoryName = string.Empty;
                shortcuts[i]?.gameObject?.SetActive(true);
            }
            else
            {
                data.Name = string.Empty;
                data.CategoryName = string.Empty;
                shortcuts[i]?.gameObject?.SetActive(false);
            }

            dataList[i] = data;
        }
    }

    static void PopulateQuips(string category)
    {
        var quips = GetQuipsForCategory(category);
        var dataList = ActionWheelSystem._SocialWheelDataList;
        var shortcuts = ActionWheelSystem._SocialWheelShortcutList;

        ActionWheelData backData = dataList[0];
        backData.Name = _backKey;
        backData.CategoryName = string.Empty;
        dataList[0] = backData;
        shortcuts[0]?.gameObject?.SetActive(true);

        for (int i = 1; i < dataList.Count; i++)
        {
            ActionWheelData data = dataList[i];
            int quipIndex = i - 1;
            if (quipIndex < quips.Count)
            {
                data.Name = quips[quipIndex].NameKey;
                data.CategoryName = category;
                shortcuts[i]?.gameObject?.SetActive(true);
            }
            else
            {
                data.Name = string.Empty;
                data.CategoryName = string.Empty;
                shortcuts[i]?.gameObject?.SetActive(false);
            }

            dataList[i] = data;
        }
    }

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

        _lastQuipSendTime = now;

        if (SocialWheelActive)
        {
            if (_inCategoryMode)
            {
                var categories = CommandCategories.Keys.ToList();
                if (index < categories.Count)
                {
                    _activeCategory = categories[index];
                    _inCategoryMode = false;
                    PopulateQuips(_activeCategory);
                }

                return false;
            }

            if (index == 0)
            {
                _inCategoryMode = true;
                _activeCategory = string.Empty;
                PopulateCategories();
                return false;
            }

            var quips = GetQuipsForCategory(_activeCategory);
            int quipIndex = index - 1;
            if (quipIndex >= 0 && quipIndex < quips.Count)
            {
                SendCommandQuip(quips[quipIndex]);
            }

            return false;
        }

        if (CommandQuips.TryGetValue(index, out CommandQuip commandQuip))
        {
            SendCommandQuip(commandQuip);
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(ActionWheelSystem), nameof(ActionWheelSystem.HideCurrentWheel))]
    [HarmonyPrefix]
    static bool HideCurrentWheelPrefix(ActionWheelSystem __instance)
    {
        _inCategoryMode = true;
        _activeCategory = string.Empty;
        PopulateCategories();

        if (!_wheelOpened.Equals(DateTime.MinValue))
        {
            _wheelOpened = DateTime.MinValue;
        }

        return !SocialWheelActive;
    }

    [HarmonyPatch(typeof(ActionWheelSystem), nameof(ActionWheelSystem.UpdateAndShowWheel))]
    [HarmonyPrefix]
    static void UpdateAndShowWheelPrefix(ActionWheelSystem __instance)
    {
        // Core.Log.LogWarning($"[ActionWheelSystem.UpdateAndShowWheel]");
    }
}
