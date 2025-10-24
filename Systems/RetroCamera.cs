using ProjectM;
using ProjectM.Sequencer;
using System.Collections.Generic;
using ProjectM.UI;
using RetroCamera.Behaviours;
using RetroCamera.Configuration;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using static RetroCamera.Configuration.QuipManager;
using static RetroCamera.Utilities.CameraState;
using static RetroCamera.Patches.MoodManagerComponentPatch;
using RetroCamera.Utilities;
using Stunlock.Localization;

namespace RetroCamera.Systems;
public class RetroCamera : MonoBehaviour
{
    static ZoomModifierSystem ZoomModifierSystem => Core.ZoomModifierSystem;
    static ActionWheelSystem ActionWheelSystem => Core.ActionWheelSystem;

    static GameObject _crosshairPrefab;
    static GameObject _crosshair;
    static CanvasScaler _canvasScaler;
    public static Camera GameCamera => _gameCamera;
    static Camera _gameCamera;

    static GeneralGameplayCollection? _generalGameplayCollection;
    static readonly LocalizationKey EmptyLocalizationKey = LocalizationManager.GetLocalizationKey(string.Empty);

    static bool _gameFocused = true;
    static bool _listening = false;
    static bool HideCharacterInfoPanel => Settings.HideCharacterInfoPanel;
    static GameObject _characterInfoPanel;
    public static void Enabled(bool enabled)
    {
        Settings.Enabled = enabled;
        UpdateEnabled(enabled);
    }
    public static void ActionMode(bool enabled)
    {
        _isMouseLocked = enabled;
        _isActionMode = enabled;
    }
    static void UpdateEnabled(bool enabled)
    {
        if (ZoomModifierSystem != null) ZoomModifierSystem.Enabled = !enabled;

        if (_crosshair != null) _crosshair.SetActive(enabled && Settings.AlwaysShowCrosshair && !_inBuildMode);

        if (!enabled)
        {
            Cursor.visible = true;
            ActionMode(false);
        }
    }
    static void UpdateFieldOfView(float fov)
    {
        if (_gameCamera != null) _gameCamera.fieldOfView = fov;
    }
    static void ToggleHUD()
    {
        if (!Settings.Enabled) return;

        _isUIHidden = !_isUIHidden;
        DisableUISettings.SetHideHUD(_isUIHidden, Core._client);
    }
    static void ToggleFog()
    {
        if (!Settings.Enabled) return;

        Utilities.ClearSkies.ToggleFog();
    }
    void Awake()
    {
        Settings.Initialize();
        RegisterBehaviours();
    }
    static void RegisterBehaviours()
    {
        RegisterCameraBehaviour(new FirstPersonCameraBehaviour());
        RegisterCameraBehaviour(new ThirdPersonCameraBehaviour());
    }
    static void AddListeners()
    {
        Settings.AddEnabledListener(UpdateEnabled);
        Settings.AddFieldOfViewListener(UpdateFieldOfView);
        Settings.AddHideHUDListener(ToggleHUD);
        Settings.AddHideFogListener(ToggleFog);
        Settings.AddSocialWheelPressedListener(SocialWheelKeyPressed);
        Settings.AddSocialWheelUpListener(SocialWheelKeyUp);
        Settings.AddCompleteTutorialListener(CompleteTutorial);
    }

    static GameObject _journalClaimButtonObject;
    static void CompleteTutorial()
    {
        if (!Settings.Enabled) return;
        if (_journalClaimButtonObject == null) _journalClaimButtonObject = GameObject.Find("HUDCanvas(Clone)/JournalCanvas/JournalParent(Clone)/Content/Layout/JournalEntry_Multi/ButtonParent/ClaimButton");

        if (_journalClaimButtonObject != null)
        {
            SimpleStunButton claimButton = _journalClaimButtonObject.GetComponent<SimpleStunButton>();
            claimButton?.Press();
        }
        else
        {
            Core.Log.LogWarning($"[RetroCamera] Journal claim button not found!");
        }
    }
    public static bool SocialWheelActive => _socialWheelActive;
    static bool _socialWheelActive = false;
    public static ActionWheel SocialWheel => _socialWheel;
    static ActionWheel _socialWheel;
    public static bool _shouldActivateWheel = false;

    static Entity _rootPrefabCollection;
    static bool _socialWheelInitialized = false;
    static void SocialWheelKeyPressed()
    {
        if (!Settings.CommandWheelEnabled) return;

        if (!_rootPrefabCollection.Exists() || _socialWheel == null)
        {
            Core.Log.LogWarning($"[RetroCamera] Initializing SocialWheel...");
            ActionWheelSystem?._RootPrefabCollectionAccessor.TryGetSingletonEntity(out _rootPrefabCollection);

            if (!_socialWheelInitialized && _rootPrefabCollection.TryGetComponent(out RootPrefabCollection rootPrefabCollection)
                && rootPrefabCollection.GeneralGameplayCollectionPrefab.TryGetComponent(out GeneralGameplayCollection generalGameplayCollection))
            {
                _generalGameplayCollection = generalGameplayCollection;

                UpdateSocialWheelQuips(generalGameplayCollection);

                ActionWheelSystem.InitializeSocialWheel(true, generalGameplayCollection);
                _socialWheelInitialized = true;

                try
                {
                    LocalizationManager.LocalizeText();
                }
                catch (Exception ex)
                {
                    Core.Log.LogError($"[RetroCamera.Update] Failed to localize keys - {ex.Message}");
                }
            }

            _socialWheel = ActionWheelSystem?._SocialWheel;
            TryEnsureGeneralGameplayCollection();
            var shortcuts = _socialWheel.ActionWheelShortcuts;

            foreach (var shortcut in shortcuts)
            {
                shortcut?.gameObject?.SetActive(false);
            }

            _socialWheel.gameObject.SetActive(true);
        }

        if (!_socialWheelActive)
        {
            _shouldActivateWheel = true;
            _socialWheelActive = true;
            ActionWheelSystem._CurrentActiveWheel = SocialWheel;
            // Core.Log.LogWarning($"[RetroCamera] Activating wheel");
        }
    }
    static void UpdateSocialWheelQuips(GeneralGameplayCollection generalGameplayCollection)
    {
        try
        {
            _generalGameplayCollection = generalGameplayCollection;
            ClearActiveCategory();

            var categories = GetCategories();
            var processedSlots = new HashSet<byte>();

            foreach (var categoryPair in categories)
            {
                byte categorySlot = categoryPair.Key;
                var category = categoryPair.Value;

                UpdateSocialWheelSlot(generalGameplayCollection, categorySlot, category.NameKey, true);
                processedSlots.Add(categorySlot);

                foreach (var entry in category.Entries)
                {
                    byte quipSlot = entry.Key;
                    var commandQuip = entry.Value;

                    if (commandQuip.IsEmpty)
                        continue;

                    UpdateSocialWheelSlot(generalGameplayCollection, quipSlot, commandQuip.NameKey, false);
                    processedSlots.Add(quipSlot);
                }
            }

            foreach (var commandPair in CommandQuips)
            {
                byte slot = commandPair.Key;
                var commandQuip = commandPair.Value;

                if (!processedSlots.Add(slot) || commandQuip.IsEmpty)
                    continue;

                UpdateSocialWheelSlot(generalGameplayCollection, slot, commandQuip.NameKey, false);
            }
        }
        catch (Exception ex)
        {
            Core.Log.LogError(ex);
        }
    }

    internal static bool ShowCategoryQuips(byte categorySlot)
    {
        if (!TryEnsureGeneralGameplayCollection())
            return false;

        var actionWheelSystem = ActionWheelSystem;
        var generalGameplayCollection = _generalGameplayCollection;

        if (actionWheelSystem == null || !generalGameplayCollection.HasValue)
            return false;

        var socialWheelData = ActionWheelSystem._SocialWheelDataList;
        if (socialWheelData == null || socialWheelData.Count == 0)
            return false;

        if (!TryGetCategory(categorySlot, out var category) || !category.HasEntries)
            return false;

        var generalGameplayCollectionValue = generalGameplayCollection.Value;

        int slotLimit = Math.Min(generalGameplayCollectionValue.ChatQuips.Length, socialWheelData.Count);

        if (slotLimit == 0)
            return false;

        UpdateSocialWheelSlot(generalGameplayCollectionValue, 0, BackToCategoriesLabelKey, true);

        var usedSlots = new HashSet<byte>
        {
            0
        };

        int displaySlot = 1;

        foreach (var entry in category.Entries)
        {
            if (displaySlot >= slotLimit)
                break;

            UpdateSocialWheelSlot(generalGameplayCollectionValue, (byte)displaySlot, entry.Value.NameKey, false);
            usedSlots.Add((byte)displaySlot);
            displaySlot++;
        }

        if (usedSlots.Count <= 1)
            return false;

        ClearUnusedSocialWheelSlots(usedSlots);
        RefreshSocialWheelDisplay();
        return true;
    }

    static void ClearUnusedSocialWheelSlots(ISet<byte> usedSlots)
    {
        var generalGameplayCollection = _generalGameplayCollection;

        if (!generalGameplayCollection.HasValue)
            return;

        var actionWheelSystem = ActionWheelSystem;
        if (actionWheelSystem == null)
            return;

        var socialWheelData = ActionWheelSystem._SocialWheelDataList;

        if (socialWheelData == null)
            return;

        var generalGameplayCollectionValue = generalGameplayCollection.Value;

        int slotLimit = Math.Min(generalGameplayCollectionValue.ChatQuips.Length, socialWheelData.Count);

        for (int slotIndex = 0; slotIndex < slotLimit; slotIndex++)
        {
            byte slot = (byte)slotIndex;

            if (usedSlots != null && usedSlots.Contains(slot))
                continue;

            bool restored = false;

            if (_originalChatQuips.TryGetValue(slot, out var originalQuip))
            {
                generalGameplayCollectionValue.ChatQuips[slot] = originalQuip;
                restored = true;
            }

            if (_originalActionWheelData.TryGetValue(slot, out var originalWheelData))
            {
                socialWheelData[slot] = originalWheelData;
                restored = true;
            }

            if (restored)
                continue;

            UpdateSocialWheelSlot(generalGameplayCollectionValue, slot, EmptyLocalizationKey, true);
        }
    }

    static void UpdateSocialWheelSlot(GeneralGameplayCollection generalGameplayCollection, byte slot, LocalizationKey nameKey, bool isCategory)
    {
        if (slot < generalGameplayCollection.ChatQuips.Length)
        {
            if (!_originalChatQuips.ContainsKey(slot))
                _originalChatQuips[slot] = generalGameplayCollection.ChatQuips[slot];

            ChatQuip chatQuip = generalGameplayCollection.ChatQuips[slot];
            chatQuip.Text = nameKey;

            if (isCategory)
            {
                chatQuip.Sequence = default;
            }

            generalGameplayCollection.ChatQuips[slot] = chatQuip;
        }

        var socialWheelData = ActionWheelSystem._SocialWheelDataList;

        if (slot < socialWheelData.Count)
        {
            if (!_originalActionWheelData.ContainsKey(slot))
                _originalActionWheelData[slot] = socialWheelData[slot];

            ActionWheelData wheelData = socialWheelData[slot];
            wheelData.Name = nameKey;
            socialWheelData[slot] = wheelData;
        }
    }
    }

    static void UpdateSocialWheelSlot(GeneralGameplayCollection generalGameplayCollection, byte slot, Stunlock.Localization.LocalizationKey nameKey, bool isCategory)
    {
        if (slot < generalGameplayCollection.ChatQuips.Length)
        {
            ChatQuip chatQuip = generalGameplayCollection.ChatQuips[slot];
            chatQuip.Text = nameKey;

            if (isCategory)
            {
                chatQuip.Sequence = default;
            }

            generalGameplayCollection.ChatQuips[slot] = chatQuip;
        }

        var socialWheelData = ActionWheelSystem._SocialWheelDataList;

        if (slot < socialWheelData.Count)
        {
            ActionWheelData wheelData = socialWheelData[slot];
            wheelData.Name = nameKey;
            socialWheelData[slot] = wheelData;
        }
    }


    static void SocialWheelKeyUp()
    {
        if (!Settings.CommandWheelEnabled) return;

        if (_socialWheelActive)
        {
            _socialWheelActive = false;
            ActionWheelSystem.HideCurrentWheel();
            _socialWheel.gameObject.SetActive(false);
            ActionWheelSystem._CurrentActiveWheel = null;
            // Core.Log.LogWarning($"[RetroCamera] SocialWheelKeyUp");
        }
    }
    void Update()
    {
        if (!Core._initialized) return;
        else if (!_gameFocused || !Settings.Enabled) return;
        // else if (!Settings.Enabled) return;

        if (!_listening)
        {
            _listening = true;
            AddListeners();
        }

        if (_crosshairPrefab == null) BuildCrosshair();
        if (_gameCamera == null) _gameCamera = CameraManager.GetCamera();

        if (_characterInfoPanel == null)
        {
            GameObject characterInfoPanelCanvas = GameObject.Find("HUDCanvas(Clone)/TargetInfoPanelCanvas");
            _characterInfoPanel = characterInfoPanelCanvas?.transform.GetChild(0).gameObject;

            if (_characterInfoPanel == null)
            {
                Core.Log.LogWarning($"[RetroCamera] CharacterInfoPanel (0) not found!");
            }
            else
            {
                Core.Log.LogWarning($"[RetroCamera] CharacterInfoPanel (0) found!");
            }
        }

        if (Core.LocalCharacter.TryGetComponent(out Mounter mounter))
        {
            CameraState._isMounted = mounter.MountEntity.Exists();
        }

        UpdateCrosshair();
    }
    void OnApplicationFocus(bool hasFocus)
    {
        _gameFocused = hasFocus;
        if (hasFocus) IsMenuOpen = false;
    }
    static void BuildCrosshair()
    {
        try
        {
            CursorData cursorData = CursorController._CursorDatas.First(x => x.CursorType == CursorType.Game_Normal);
            if (cursorData == null) return;

            _crosshairPrefab = new("Crosshair");
            _crosshairPrefab.SetActive(false);

            _crosshairPrefab.AddComponent<CanvasRenderer>();
            RectTransform rectTransform = _crosshairPrefab.AddComponent<RectTransform>();

            rectTransform.transform.SetSiblingIndex(1);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.sizeDelta = new Vector2(32, 32);
            rectTransform.localScale = Vector3.one;
            rectTransform.localPosition = new Vector3(0, 0, 0);

            Image image = _crosshairPrefab.AddComponent<Image>();
            image.sprite = Sprite.Create(cursorData.Texture, new Rect(0, 0, cursorData.Texture.width, cursorData.Texture.height), new Vector2(0.5f, 0.5f), 100f);

            _crosshairPrefab.SetActive(false);
        }
        catch (Exception ex)
        {
            Core.Log.LogError(ex);
        }
    }
    static void UpdateCrosshair()
    {
        try
        {
            bool cursorVisible = true;
            bool crosshairVisible = false;

            if (_crosshair == null && _crosshairPrefab != null)
            {
                GameObject uiCanvas = GameObject.Find("HUDCanvas(Clone)/Canvas");

                if (uiCanvas == null) return;

                _canvasScaler = uiCanvas.GetComponent<CanvasScaler>();
                _crosshair = Instantiate(_crosshairPrefab, uiCanvas.transform);
                _crosshair.SetActive(true);
            }

            bool rotatingCamera = false;
            if (_validGameplayInputState) rotatingCamera = _gameplayInputState.IsInputPressed(ButtonInputAction.RotateCamera);

            bool shouldHandle = _validGameplayInputState &&
               (_isMouseLocked || rotatingCamera);

            if (_cachedVignette != null) _cachedVignette.active = Settings.ShowVignette;

            if (shouldHandle && !IsMenuOpen)
            {
                if (_isActionMode && HideCharacterInfoPanel)
                {
                    _characterInfoPanel.SetActive(false);
                }
                else
                {
                    _characterInfoPanel.SetActive(true);
                }

                crosshairVisible = _isFirstPerson || (_isActionMode && Settings.ActionModeCrosshair);
                cursorVisible = _usingMouseWheel;
            }
            else if (shouldHandle && _inBuildMode)
            {
                crosshairVisible = Settings.AlwaysShowCrosshair;
                cursorVisible = false;
            }

            if (_crosshair != null)
            {
                _crosshair.SetActive(crosshairVisible || Settings.AlwaysShowCrosshair);

                float scale = Settings.CrosshairSize;
                _crosshair.transform.localScale = new(scale, scale, scale);

                if (_isFirstPerson)
                {
                    _crosshair.transform.localPosition = Vector3.zero;
                }
                else
                {
                    if (_canvasScaler != null)
                    {
                        _crosshair.transform.localPosition = new Vector3(
                            Settings.AimOffsetX * (_canvasScaler.referenceResolution.x / Screen.width),
                            Settings.AimOffsetY * (_canvasScaler.referenceResolution.y / Screen.height),
                            0
                        );
                    }
                }
            }

            if (_inBuildMode && !rotatingCamera && !cursorVisible) cursorVisible = true;
            else if (IsMenuOpen && rotatingCamera) cursorVisible = false;

            Cursor.visible = cursorVisible;
        }
        catch (Exception ex)
        {
            Core.Log.LogError(ex);
        }
    }
    public static void ResetState()
    {
        _socialWheel = null;
        _socialWheelActive = false;
        _socialWheelInitialized = false;
        _shouldActivateWheel = false;
        _rootPrefabCollection = Entity.Null;
        _generalGameplayCollection = null;
    }
}

