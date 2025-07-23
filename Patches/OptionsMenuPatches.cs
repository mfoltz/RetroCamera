using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using ProjectM;
using ProjectM.UI;
using RetroCamera.Configuration;
using RetroCamera.Utilities;
using Stunlock.Localization;
using StunShared.UI;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using static RetroCamera.Configuration.LocalizationManager;
using static RetroCamera.Configuration.OptionsManager;

namespace RetroCamera.Patches;

[HarmonyPatch]
internal static class OptionsMenuPatches
{
    static readonly bool _shouldLocalize = true;

    [HarmonyPatch(typeof(OptionsMenu_Base), nameof(OptionsMenu_Base.Start))]
    [HarmonyPostfix]
    static void StartPostfix() => CameraState.IsMenuOpen = true;

    [HarmonyPatch(typeof(OptionsMenu_Base), nameof(OptionsMenu_Base.OnDestroy))]
    [HarmonyPostfix]
    static void OnDestroyPostfix()
    {
        Persistence.SaveOptions();
        CameraState.IsMenuOpen = false;
    }

    [HarmonyPatch(typeof(OptionsPanel_Interface), nameof(OptionsPanel_Interface.Start))]
    [HarmonyPostfix]
    static void StartPostfix(OptionsPanel_Interface __instance)
    {
        /*
        if (!Localization.Initialized)
        {
            Core.Log.LogWarning("[OptionsPanel_Interface.Start()] Localization may not be ready!");
            try
            {
                Localization.LoadDefaultLanguage();
            }
            catch (Exception ex)
            {
                Core.Log.LogError($"[OptionsPanel_Interface.Start()] Failed to load localization: {ex.Message}");
                return;
            }
        }
        */
        
        if (_shouldLocalize)
        {
            Core.Log.LogWarning("[OptionsPanel_Interface.Start()] Attempting to localize keys...");

            try
            {
                LocalizeText();
                Core.Log.LogWarning("[OptionsPanel_Interface.Start()] Keys localized successfully!");
            }
            catch (Exception ex)
            {
                Core.Log.LogError($"[OptionsPanel_Interface.Start] Failed to localize keys - {ex.Message}");
            }

            // _shouldLocalize = false;
        }

        __instance.AddHeader(_sectionHeader);

        foreach (var entry in OrderedEntries)
        {
            try
            {
                switch (entry.Type)
                {
                    case OptionItemType.Toggle:
                        if (!TryGetOption(entry, out var toggleOption)) continue;

                        Toggle toggle = toggleOption as Toggle;
                        var toggleEntry = UIHelper.InstantiatePrefabUnderAnchor(__instance.CheckboxPrefab, __instance.ContentNode);

                        toggleEntry.Initialize(
                            toggle.NameKey,
                            new Il2CppSystem.Nullable_Unboxed<LocalizationKey>(toggle.DescKey),
                            toggle.DefaultValue,
                            toggle.Value,
                            OnChange(toggle)
                        );

                        SettingsEntryBase toggleBase = toggleEntry;
                        __instance.EntriesSelectionGroup.AddEntry(ref toggleBase, true);
                        break;
                    case OptionItemType.Slider:
                        if (!TryGetOption(entry, out var sliderOption)) continue;

                        Slider slider = sliderOption as Slider;
                        var sliderEntry = UIHelper.InstantiatePrefabUnderAnchor(__instance.SliderPrefab, __instance.ContentNode);
                        
                        sliderEntry.Initialize(
                            slider.NameKey,
                            new Il2CppSystem.Nullable_Unboxed<LocalizationKey>(slider.DescKey),
                            slider.MinValue,
                            slider.MaxValue,
                            slider.DefaultValue,
                            slider.Value,
                            slider.Decimals,
                            slider.Decimals == 0,
                            OnChange(slider),
                            fixedStepValue: slider.StepValue
                        );

                        SettingsEntryBase sliderBase = sliderEntry;
                        __instance.EntriesSelectionGroup.AddEntry(ref sliderBase, true);
                        break;
                    case OptionItemType.Dropdown:
                        if (!TryGetOption(entry, out var dropdownOption)) continue;

                        Dropdown dropdown = dropdownOption as Dropdown;
                        var dropdownEntry = UIHelper.InstantiatePrefabUnderAnchor(__instance.DropdownPrefab, __instance.ContentNode);
                        var dropdownOptions = new Il2CppSystem.Collections.Generic.List<string>(dropdown.Values.Count);
                        
                        foreach (var value in dropdown.Values)
                            dropdownOptions.Add(value);

                        dropdownEntry.Initialize(
                            dropdown.NameKey,
                            new Il2CppSystem.Nullable_Unboxed<LocalizationKey>(dropdown.DescKey),
                            new Il2CppReferenceArray<LocalizedKeyValue>([]),
                            dropdownOptions,
                            dropdown.DefaultValue,
                            dropdown.Value,
                            OnChange(dropdown)
                        );

                        SettingsEntryBase dropdownBase = dropdownEntry;
                        __instance.EntriesSelectionGroup.AddEntry(ref dropdownBase);
                        break;
                    case OptionItemType.Divider:
                        CreateDivider(__instance.ContentNode, entry.Key);
                        break;
                }
            }
            catch (Exception ex)
            {
                Core.Log.LogError($"Failed to create option {entry.Key} - {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(RebindingMenu), nameof(RebindingMenu.Start))]
    [HarmonyPostfix]
    static void StartPostfix(RebindingMenu __instance)
    {
        if (__instance._BindingTypeToDisplay != ControllerType.KeyboardAndMouse)
        {
            return;
        }

        __instance.AddHeader(_sectionHeader);

        foreach (Keybinding keybind in KeybindsManager.Keybinds.Values)
        {
            SettingsEntry_Binding settingsEntryBinding = UIHelper.InstantiatePrefabUnderAnchor(__instance.ControlsInputEntryPrefab, __instance.ContentNode);

            settingsEntryBinding.Initialize(
                ControllerType.KeyboardAndMouse,
                keybind.InputFlag,
                AnalogInputAction.None,
                true,
                false,
                true,
                onClick: (Il2CppSystem.Action<SettingsEntry_Binding, bool, ButtonInputAction, AnalogInputAction, bool>)__instance.OnEntryButtonClicked,
                onClear: (Il2CppSystem.Action<SettingsEntry_Binding, ButtonInputAction>)__instance.OnEntryCleared,
                true
            );

            settingsEntryBinding.SetInputInfo(keybind.NameKey, keybind.DescriptionKey);
            settingsEntryBinding.SetPrimary(keybind.PrimaryName);

            settingsEntryBinding.PrimaryButton.onClick.AddListener((UnityAction)(() => RefreshKeybind(__instance, settingsEntryBinding, keybind).Start()));
            settingsEntryBinding.SecondaryButton.gameObject.SetActive(false);

            SettingsEntryBase settingsEntryBase = settingsEntryBinding;
            __instance.EntriesSelectionGroup.AddEntry(ref settingsEntryBase);
        }
    }
    static IEnumerator RefreshKeybind(RebindingMenu __instance, SettingsEntry_Binding binding, Keybinding keybind)
    {
        KeyCode newKey = KeyCode.None;
        __instance.OnEntryButtonClicked(binding, true, ButtonInputAction.None, AnalogInputAction.None, true);

        while (true)
        {
            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(key))
                {
                    if (key == KeyCode.Escape)
                    {
                        Core.Log.LogWarning($"[{binding.name}] Rebind cancelled");

                        __instance.OnRebindingCancel(false, false);

                        yield break;
                    }

                    if (key == KeyCode.Backspace)
                    {
                        Core.Log.LogWarning($"[{binding.name}] Rebind cancelled");

                        __instance.OnEntryCleared(binding, ButtonInputAction.None);
                        KeybindsManager.Rebind(keybind, KeyCode.None);
                        binding.SetPrimary(keybind.PrimaryName);

                        yield break;
                    }

                    newKey = key;
                    break;
                }
            }

            if (newKey != KeyCode.None)
                break;

            yield return null;
        }

        KeybindsManager.Rebind(keybind, newKey);
        __instance.OnRebindingComplete(false);
        binding.SetPrimary(keybind.PrimaryName);

        Core.Log.LogWarning($"[{binding.name}] rebound to {newKey}");
    }
    static GameObject CreateDivider(Transform parent, string dividerText)
    {
        GameObject dividerGameObject = new("Divider");

        RectTransform dividerTransform = dividerGameObject.AddComponent<RectTransform>();
        dividerTransform.SetParent(parent);
        dividerTransform.localScale = Vector3.one;
        dividerTransform.sizeDelta = new(0f, 28f);

        UnityEngine.UI.Image dividerImage = dividerGameObject.AddComponent<UnityEngine.UI.Image>();
        dividerImage.color = new(0.12f, 0.152f, 0.2f, 0.15f);

        UnityEngine.UI.LayoutElement dividerLayout = dividerGameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        dividerLayout.preferredHeight = 28f;

        GameObject dividerTextGameObject = new("Text");
        RectTransform dividerTextTransform = dividerTextGameObject.AddComponent<RectTransform>();
        dividerTextTransform.SetParent(dividerGameObject.transform);
        dividerTextTransform.localScale = Vector3.one;

        TextMeshProUGUI textMeshDivider = dividerTextGameObject.AddComponent<TextMeshProUGUI>();
        Il2CppArrayBase<TextMeshProUGUI> textMeshArray = parent.GetComponentsInChildren<TextMeshProUGUI>();
        TMP_FontAsset fontAsset = textMeshArray.First().font;

        textMeshDivider.alignment = TextAlignmentOptions.Center;
        textMeshDivider.fontStyle = FontStyles.SmallCaps;
        textMeshDivider.font = fontAsset;
        textMeshDivider.fontSize = 20f;
        textMeshDivider.SetText(dividerText);

        dividerGameObject.SetActive(true);
        return dividerGameObject;
    }
    static Il2CppSystem.Action<T> OnChange<T>(MenuOption<T> option)
    {
        return (Il2CppSystem.Action<T>)(value =>
        {
            option.SetValue(value);
            Persistence.SaveOptions();
        });
    }
}
