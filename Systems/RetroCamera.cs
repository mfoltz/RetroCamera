using ProjectM;
using ProjectM.Sequencer;
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
using RetroCamera.Patches;
using UnityEngine.InputSystem;

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

        if (_crosshair != null) _crosshair.active = enabled && Settings.AlwaysShowCrosshair && !_inBuildMode;

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
        Settings.AddCompleteTutorialListener(CompleteJournalTask);
    }

    static GameObject _journalClaimButtonObject;
    static void CompleteJournalTask()
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
                foreach (var commandQuip in CommandQuips)
                {
                    if (string.IsNullOrEmpty(commandQuip.Value.Name)
                        || string.IsNullOrEmpty(commandQuip.Value.Command))
                        continue;

                    ChatQuip chatQuip = generalGameplayCollection.ChatQuips[commandQuip.Key];
                    chatQuip.Text = commandQuip.Value.NameKey;

                    // Core.Log.LogWarning($"[RetroCamera] QuipData - {commandQuip.Value.Name} | {commandQuip.Value.Command} | {chatQuip.Sequence} | {chatQuip.Sequence.ToPrefabGUID()}");

                    generalGameplayCollection.ChatQuips[commandQuip.Key] = chatQuip;
                }

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

                try
                {
                    var chatQuips = generalGameplayCollection.ChatQuips;
                    var socialWheelData = ActionWheelSystem._SocialWheelDataList;
                    var socialWheelShortcuts = ActionWheelSystem._SocialWheelShortcutList;

                    // Core.Log.LogWarning($"[RetroCamera] SocialWheelData count - {socialWheelData.Count} | {chatQuips.Length}");

                    foreach (var commandQuip in CommandQuips)
                    {
                        if (string.IsNullOrEmpty(commandQuip.Value.Name)
                            || string.IsNullOrEmpty(commandQuip.Value.Command))
                            continue;

                        ActionWheelData wheelData = socialWheelData[commandQuip.Key];

                        // Core.Log.LogWarning($"[RetroCamera] WheelData - {commandQuip.Value.Name} | {commandQuip.Value.Command} | {wheelData.Name}");
                        wheelData.Name = commandQuip.Value.NameKey;
                    }
                }
                catch (Exception ex)
                {
                    Core.Log.LogError(ex);
                }
            }

            _socialWheel = ActionWheelSystem?._SocialWheel;
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

        UpdateCrosshair();

        /*
        if (InputActionSystemPatch.IsGamepad)
        {
            // InputSchemeGamepadData, RemapAbilitySlotsForGamepadSystem
            float horizontal = Gamepad.current.rightStick.x.ReadValue();
            float vertical = Gamepad.current.rightStick.y.ReadValue();

            Core.Log.LogWarning($"[RetroCamera] RightStick - Horizontal: {horizontal}, Vertical: {vertical}");
        }
        */
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

            _crosshairPrefab = new("Crosshair")
            {
                active = false
            };

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

            _crosshairPrefab.active = false;
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
                _crosshair.active = true;
            }

            bool rotatingCamera = false;
            if (_validGameplayInputState) rotatingCamera = _gameplayInputState.IsInputPressed(ButtonInputAction.RotateCamera);

            bool shouldHandle = _validGameplayInputState && 
               (_isMouseLocked || rotatingCamera);

            if (_cachedVignette != null)
            {
                _cachedVignette.active = Settings.ShowVignette;
            }

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
                _crosshair.active = crosshairVisible || Settings.AlwaysShowCrosshair;

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
    }
}

