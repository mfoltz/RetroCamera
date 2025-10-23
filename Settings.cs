using RetroCamera.Configuration;
using RetroCamera.Patches;
using RetroCamera.Systems;
using UnityEngine;
using static RetroCamera.Configuration.Keybinding;
using static RetroCamera.Configuration.KeybindsManager;
using static RetroCamera.Configuration.OptionsManager;
using static RetroCamera.Utilities.CameraState;
using static RetroCamera.Utilities.Persistence;
using static RetroCamera.Configuration.QuipManager;
using static RetroCamera.Systems.RetroCamera;
using BoolChanged = RetroCamera.Configuration.MenuOption<bool>.OptionChangedHandler<bool>;
using FloatChanged = RetroCamera.Configuration.MenuOption<float>.OptionChangedHandler<float>;
using UnityEngine.InputSystem;

namespace RetroCamera;
internal static class Settings
{
    public static bool Enabled { get => _enabledOption.Value; set => _enabledOption.SetValue(value); }
    public static bool FirstPersonEnabled { get => _firstPersonEnabledOption.Value; set => _firstPersonEnabledOption.SetValue(value); }
    public static bool CommandWheelEnabled { get => _commandWheelEnabled.Value; set => _commandWheelEnabled.SetValue(value); }
    public static bool AlwaysShowCrosshair { get => _alwaysShowCrosshairOption.Value; set => _alwaysShowCrosshairOption.SetValue(value); }
    public static bool ActionModeCrosshair { get => _actionModeCrosshairOption.Value; set => _actionModeCrosshairOption.SetValue(value); }
    public static bool HoldActionModeKey { get => _holdActionModeKeyOption.Value; set => _holdActionModeKeyOption.SetValue(value); }
    public static float FieldOfView { get => _fieldOfViewOption.Value; set => _fieldOfViewOption.SetValue(value); }
    public static bool ShowVignette { get => _showVignette.Value; set => _showVignette.SetValue(value); }
    public static bool SkipIntro { get => _skipIntro.Value; set => _skipIntro.SetValue(value); }
    public static float CrosshairSize { get => _crosshairSize.Value; set => _crosshairSize.SetValue(value); }
    public static bool HideCharacterInfoPanel { get => _hideCharacterInfoPanel.Value; set => _hideCharacterInfoPanel.SetValue(value); }
    public static bool DefaultBuildModeCamera { get => _defaultBuildModeCamera.Value; set => _defaultBuildModeCamera.SetValue(value); }
    public static int AimOffsetX { get => (int)(Screen.width * (_aimOffsetXOption.Value / 100)); set => _aimOffsetXOption.SetValue(Mathf.Clamp(value / Screen.width, -25, 25)); }
    public static int AimOffsetY { get => (int)(Screen.height * (_aimOffsetYOption.Value / 100)); set => _aimOffsetYOption.SetValue(Mathf.Clamp(value / Screen.width, -25, 25)); }
    public static bool LockZoom { get => _lockCameraZoomOption.Value; set => _lockCameraZoomOption.SetValue(value); }
    public static float LockZoomDistance { get => _lockCameraZoomDistanceOption.Value; set => _lockCameraZoomDistanceOption.SetValue(value); }
    public static float MinZoom { get => _minZoomOption.Value; set => _minZoomOption.SetValue(value); }
    public static float MaxZoom { get => _maxZoomOption.Value; set => _maxZoomOption.SetValue(value); }
    public static bool LockPitch { get => _lockCamerPitchOption.Value; set => _lockCamerPitchOption.SetValue(value); }
    public static float LockPitchAngle { get => _lockCameraPitchAngleOption.Value * Mathf.Deg2Rad; set => _lockCameraPitchAngleOption.SetValue(Mathf.Clamp(value * Mathf.Rad2Deg, 0, 90)); }
    public static float MinPitch { get => _minPitchOption.Value * Mathf.Deg2Rad; set => _minPitchOption.SetValue(Mathf.Clamp(value * Mathf.Rad2Deg, 0, 90)); }
    public static float MaxPitch { get => _maxPitchOption.Value * Mathf.Deg2Rad; set => _maxPitchOption.SetValue(Mathf.Clamp(value * Mathf.Rad2Deg, 0, 90)); }
    public static bool OverTheShoulder { get => _overTheShoulderOption.Value; set => _overTheShoulderOption.SetValue(value); }
    public static float OverTheShoulderX { get => _overTheShoulderXOption.Value; set => _overTheShoulderXOption.SetValue(value); }
    public static float OverTheShoulderY { get => _overTheShoulderYOption.Value; set => _overTheShoulderYOption.SetValue(value); }

    public const float FIRST_PERSON_FORWARD_OFFSET = 1.65f;
    public const float MOUNTED_OFFSET = 1.6f;
    public const float HEAD_HEIGHT_OFFSET = 1.05f;
    public const float SHOULDER_RIGHT_OFFSET = 0.8f;

    const float ZOOM_OFFSET = 2f;

    public static Vector3 _cursorPosition = Vector3.zero;

    static Toggle _enabledOption;
    static Toggle _firstPersonEnabledOption;
    static Toggle _commandWheelEnabled;
    static Slider _fieldOfViewOption;
    static Toggle _showVignette;
    static Toggle _skipIntro;
    static Slider _crosshairSize;
    static Toggle _alwaysShowCrosshairOption;
    static Toggle _actionModeCrosshairOption;
    static Toggle _holdActionModeKeyOption;
    static Toggle _hideCharacterInfoPanel;
    static Toggle _defaultBuildModeCamera;

    static Slider _aimOffsetXOption;
    static Slider _aimOffsetYOption;

    static Toggle _lockCameraZoomOption;
    static Slider _lockCameraZoomDistanceOption;
    static Slider _minZoomOption;
    static Slider _maxZoomOption;

    static Toggle _lockCamerPitchOption;
    static Slider _lockCameraPitchAngleOption;
    static Slider _minPitchOption;
    static Slider _maxPitchOption;

    static Toggle _overTheShoulderOption;
    static Slider _overTheShoulderXOption;
    static Slider _overTheShoulderYOption;

    static Keybinding _enabledKeybind;
    static Keybinding _actionModeKeybind;
    static Keybinding _toggleHUDKeybind;
    static Keybinding _toggleFogKeybind;
    static Keybinding _completeTutorialKeybind;
    static Keybinding _toggleSocialWheel;
    public static void Initialize()
    {
        try
        {
            RegisterOptions();
            RegisterKeybinds();

            TryLoadOptions();
            TryLoadKeybinds();
            TryLoadCommands();

            SaveOptions();
            SaveKeybinds();
        }
        catch (Exception ex)
        {
            Core.Log.LogError(ex);
        }
    }
    public static void AddEnabledListener(BoolChanged handler) =>
        _enabledOption.AddListener(handler);
    public static void AddFieldOfViewListener(FloatChanged handler) =>
        _fieldOfViewOption.AddListener(handler);
    public static void AddHideHUDListener(KeyHandler action) =>
        _toggleHUDKeybind.AddKeyDownListener(action);
    public static void AddHideFogListener(KeyHandler action) =>
        _toggleFogKeybind.AddKeyDownListener(action);
    public static void AddCompleteTutorialListener(KeyHandler action) =>
        _completeTutorialKeybind.AddKeyDownListener(action);
    public static void AddSocialWheelPressedListener(KeyHandler action) =>
        _toggleSocialWheel.AddKeyPressedListener(action);
    public static void AddSocialWheelUpListener(KeyHandler action) =>
        _toggleSocialWheel.AddKeyUpListener(action);

    public static bool _wasDisabled = false;
    static void HandleActionModeKeyDown()
    {
        if (HoldActionModeKey)
        {
            TryEnterActionMode();
            return;
        }

        if (_isActionMode)
        {
            TryExitActionMode();
        }
        else
        {
            TryEnterActionMode();
        }
    }
    static void HandleActionModeKeyUp()
    {
        if (!HoldActionModeKey) return;

        TryExitActionMode();
    }
    static void TryEnterActionMode()
    {
        if (_isFirstPerson) return;
        if (EscapeMenuViewPatch._isServerPaused) return;

        bool enabledTemporarily = false;

        if (!Enabled)
        {
            if (IsMenuOpen) return;

            enabledTemporarily = true;
            _enabledOption.SetValue(true);
        }

        _wasDisabled = enabledTemporarily;

        UpdateActionModeState(true);
    }
    static void TryExitActionMode()
    {
        if (_isFirstPerson) return;
        if (!_isActionMode && !_wasDisabled) return;

        UpdateActionModeState(false);

        if (_wasDisabled)
        {
            _wasDisabled = false;
            _enabledOption.SetValue(false);
        }
    }
    static void UpdateActionModeState(bool enabled)
    {
        ActionMode(enabled);

        if (IsMenuOpen) IsMenuOpen = false;
        if (ActionWheelSystemPatch._wheelVisible) ActionWheelSystemPatch._wheelVisible = false;

        if (_isActionMode && _isMouseLocked && !Cursor.lockState.Equals(CursorLockMode.Locked))
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
        else if (!enabled && Cursor.lockState.Equals(CursorLockMode.Locked) && (!_isActionMode || !_isMouseLocked))
        {
            Cursor.lockState = CursorLockMode.None;
        }
    }
    static void RegisterOptions()
    {
        // Core.Log.LogWarning("Registering options...");

        _enabledOption = AddToggle("Enabled", "Enable or disable RetroCamera", true);
        _firstPersonEnabledOption = AddToggle("First Person", "Enable zooming in far enough for first-person view", true);
        _commandWheelEnabled = AddToggle("Command Wheel", "Enable command wheel", false);
        _alwaysShowCrosshairOption = AddToggle("Always Show Crosshair", "Keep crosshair visible always", false);
        _actionModeCrosshairOption = AddToggle("Action Mode Crosshair", "Show crosshair during action mode", false);
        _holdActionModeKeyOption = AddToggle("Hold Action Mode Key", "Hold the action mode key instead of toggling", false);
        _hideCharacterInfoPanel = AddToggle("Hide Character Info Panel", "Removes character info panel from the top of the screen during action mode", false);
        _defaultBuildModeCamera = AddToggle("Default Build Menu Camera", "Use the default camera when entering build mode.", false);
        _fieldOfViewOption = AddSlider("FOV", "Camera field of view", 50, 90, 60);
        _showVignette = AddToggle("Show Vignette", "Shows or hides the vignette", true);
        _skipIntro = AddToggle("Skip Intro", "Skip the intro when launching game", true);
        _crosshairSize = AddSlider("Crosshair Size", "Crosshair size scaling", 1f, 5f, 1f);

        AddDivider("Third Person Zoom");
        _minZoomOption = AddSlider("Min Zoom", "Minimum zoom", 1f, 15f, 1f);
        _maxZoomOption = AddSlider("Max Zoom", "Maximum zoom", 5f, 30f, 15f);
        _lockCameraZoomOption = AddToggle("Lock Zoom", "Lock zoom distance", false);
        _lockCameraZoomDistanceOption = AddSlider("Locked Zoom Distance", "Fixed zoom distance when locked", 1f, 20f, 15f);

        AddDivider("Third Person Pitch");
        _minPitchOption = AddSlider("Min Pitch", "Minimum camera pitch", 0f, 90f, 10f);
        _maxPitchOption = AddSlider("Max Pitch", "Maximum camera pitch", 0f, 90f, 90f);
        _lockCamerPitchOption = AddToggle("Lock Pitch", "Lock camera pitch", false);
        _lockCameraPitchAngleOption = AddSlider("Locked Pitch Angle", "Fixed pitch angle when locked", 0f, 90f, 60f);

        AddDivider("Third Person Aiming");
        // _cameraAimModeOption = AddDropdown("Aiming Mode", "Ability aiming style", (int)CameraAimMode.Default, Enum.GetNames(typeof(CameraAimMode)));
        _aimOffsetXOption = AddSlider("Aiming Horizontal Offset", "Aim horizontal offset", -25f, 25f, 0f);
        _aimOffsetYOption = AddSlider("Aiming Vertical Offset", "Aim vertical offset", -25f, 25f, 0f);

        AddDivider("Over Shoulder");
        _overTheShoulderOption = AddToggle("Enable Shoulder Offset", "Enable over-the-shoulder camera", false);
        _overTheShoulderXOption = AddSlider("Shoulder Horizontal Offset", "Shoulder view horizontal offset", -10f, 10f, 0f);
        _overTheShoulderYOption = AddSlider("Shoulder Vertical Offset", "Shoulder view vertical offset", -10f, 10f, 0f);

        _minZoomOption.AddListener(value =>
        {
            if (value + ZOOM_OFFSET > MaxZoom && value + ZOOM_OFFSET < _maxZoomOption.MaxValue)
                _maxZoomOption.SetValue(value + ZOOM_OFFSET);
            else if (value + ZOOM_OFFSET > _maxZoomOption.MaxValue)
                _minZoomOption.SetValue(_maxZoomOption.MaxValue - ZOOM_OFFSET);
        });

        _maxZoomOption.AddListener(value =>
        {
            if (value - ZOOM_OFFSET < MinZoom && value - ZOOM_OFFSET > _minZoomOption.MinValue)
                _minZoomOption.SetValue(value - ZOOM_OFFSET);
            else if (value - ZOOM_OFFSET < _minZoomOption.MinValue)
                _maxZoomOption.SetValue(_minZoomOption.MinValue + ZOOM_OFFSET);
        });

        _minPitchOption.AddListener(value =>
        {
            if (value > _maxPitchOption.Value && value < _maxPitchOption.MaxValue)
                _maxPitchOption.SetValue(value);
            else if (value > _maxPitchOption.MaxValue)
                _minPitchOption.SetValue(_maxPitchOption.MaxValue);
        });

        _maxPitchOption.AddListener(value =>
        {
            if (value < _minPitchOption.Value && value > _minPitchOption.MinValue)
                _minPitchOption.SetValue(value);
            else if (value < _minPitchOption.MinValue)
                _maxPitchOption.SetValue(_minPitchOption.MinValue);
        });
    }
    static void RegisterKeybinds()
    {
        // Core.Log.LogWarning("Registering keybinds...");

        _enabledKeybind = AddKeybind("Toggle RetroCamera", "Enable or disable RetroCamera functions", KeyCode.LeftBracket);
        _enabledKeybind.AddKeyDownListener(() =>
        {
            if (!EscapeMenuViewPatch._isServerPaused) _enabledOption.SetValue(!Enabled);

            if (Enabled && (_isFirstPerson || _isActionMode))
            {
                Cursor.lockState = CursorLockMode.Locked;
            }
            else if (Cursor.lockState.Equals(CursorLockMode.Locked) && !Enabled)
            {
                Cursor.lockState = CursorLockMode.None;
            }
        });

        _actionModeKeybind = AddKeybind("Toggle Action Mode", "Toggle action mode", KeyCode.RightBracket);
        _actionModeKeybind.AddKeyDownListener(HandleActionModeKeyDown);
        _actionModeKeybind.AddKeyUpListener(HandleActionModeKeyUp);

        _toggleHUDKeybind = AddKeybind("Toggle HUD", "Toggle HUD visibility", KeyCode.Backslash);

        _toggleFogKeybind = AddKeybind("Toggle Fog/Clouds", "Toggle visibility of fog and clouds (cloud ground shadows also affected)", KeyCode.Equals);

        _completeTutorialKeybind = AddKeybind("Complete Tutorial", "Pushes button for completed tutorials if applicable", KeyCode.Minus);

        _toggleSocialWheel = AddKeybind("Use Social Wheel", "Toggle social wheel visibility", KeyCode.RightAlt);
    }
    public static bool TryLoadOptions()
    {
        var loaded = LoadOptions();

        if (loaded == null)
        {
            Core.Log.LogWarning("No options saved!");
            return false;
        }


        foreach (var (key, loadedOption) in loaded)
        {
            if (Options.TryGetValue(key, out var registeredOption))
                registeredOption.ApplySaved(loadedOption);
        }

        return true;
    }
    public static bool TryLoadKeybinds()
    {
        var loaded = LoadKeybinds();

        if (loaded == null)
        {
            Core.Log.LogWarning("No keybinds saved!");
            return false;
        }

        foreach (var (key, loadedBind) in loaded)
        {
            if (Keybinds.TryGetValue(key, out var registeredBind))
                registeredBind.ApplySaved(loadedBind);
        }

        return true;
    }
}