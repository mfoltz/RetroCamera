using ProjectM;
using RetroCamera.Patches;
using UnityEngine;
using UnityEngine.InputSystem;
using static RetroCamera.Systems.RetroCamera;
using static RetroCamera.Utilities.CameraState;

namespace RetroCamera.Behaviours;
internal abstract class CameraBehaviour
{
    public BehaviourType BehaviourType;
    public float DefaultMaxPitch;
    public float DefaultMinPitch;
    public bool Active;

    const float YAW_SPEED = 2.0f;
    const float YAW_MULTIPLIER = 2.5f;
    const float PITCH_SPEED = 1.5f;
    const float DEAD_ZONE = 0.0004f;

    protected static float _targetZoom = Settings.MaxZoom / 2f;
    protected static ZoomSettings _buildModeZoomSettings;
    protected static bool _isBuildSettingsSet;
    public virtual void Activate(ref TopdownCameraState state)
    {
        Active = true;
    }
    public virtual void Deactivate()
    {
        _targetZoom = Settings.MaxZoom / 2f;
        Active = false;
    }
    public virtual bool ShouldActivate(ref TopdownCameraState state) => false;
    public static Vector2 RightStick() => Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
    static Vector2 _rsDelta = Vector2.zero;
    public virtual unsafe void HandleInput(ref InputState inputState)
    {
        if (!_validGameplayInputState || !inputState.InputsPressed.IsCreated) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (EscapeMenuViewPatch._isEscapeMenuOpen)
            {
                IsMenuOpen = false;
                EscapeMenuViewPatch._isEscapeMenuOpen = false;
            }
        }

        /*
        if (InputActionSystemPatch.IsGamepad)
        {
            Vector2 rightStick = RightStick();

            if (rightStick.sqrMagnitude > DEAD_ZONE)         
            {
                inputState.InputsPressed.m_ListData->AddNoResize(ButtonInputAction.RotateCamera);
                _rsDelta = rightStick;                    
            }
            else
            {
                _rsDelta = Vector2.zero;
            }

            return;
        }
        */

        if (_isMouseLocked && !IsMenuOpen && !inputState.IsInputPressed(ButtonInputAction.RotateCamera))
        {
            inputState.InputsPressed.m_ListData->AddNoResize(ButtonInputAction.RotateCamera);
        }

        float zoomValue = inputState.GetAnalogValue(AnalogInputAction.ZoomCamera);

        if (zoomValue != 0 && !_inBuildMode)
        {
            // Consume zoom input for the camera
            var zoomAmount = Mathf.Lerp(.25f, 1.5f, Mathf.Max(0, _targetZoom - Settings.MinZoom) / Settings.MaxZoom);
            var zoomChange = inputState.GetAnalogValue(AnalogInputAction.ZoomCamera) > 0 ? zoomAmount : -zoomAmount;

            if ((_targetZoom > Settings.MinZoom && _targetZoom + zoomChange < Settings.MinZoom) || (_isFirstPerson && zoomChange > 0)) _targetZoom = Settings.MinZoom;
            else _targetZoom = Mathf.Clamp(_targetZoom + zoomChange, Settings.FirstPersonEnabled ? 0 : Settings.MinZoom, Settings.MaxZoom);

            inputState.SetAnalogValue(AnalogInputAction.ZoomCamera, 0);
        }

        // Update zoom if MaxZoom is changed
        if (_targetZoom > Settings.MaxZoom) _targetZoom = Settings.MaxZoom;

        if (SocialWheelActive)
        {
            Core.ActionWheelSystem.UpdateAndShowWheel(SocialWheel, inputState);
            // Core.ActionWheelSystem._CurrentActiveWheel = SocialWheel;
            // Core.Log.LogWarning($"[RetroCamera] UsingActionWheel");
        }
    }
    public virtual void UpdateCameraInputs(ref TopdownCameraState state, ref TopdownCamera data)
    {
        if (!_validGameplayInputState) return;

        _inBuildMode = state.InBuildMode;

        if (!_isBuildSettingsSet)
        {
            _buildModeZoomSettings = data.BuildModeZoomSettings;
            _isBuildSettingsSet = true;
        }

        /*
        if (InputActionSystemPatch.IsGamepad)
        {
            float deltaTime = Time.deltaTime;
            float yawDelta = _rsDelta.x * YAW_SPEED * YAW_MULTIPLIER * deltaTime;

            // state.Yaw += yawDelta;
            state.ConsumeYawInput = yawDelta;

            float newPitch = state.Target.Pitch + _rsDelta.y * PITCH_SPEED * deltaTime;
            newPitch = Mathf.Clamp(
                newPitch,
                state.ZoomSettings.MinPitch,
                state.ZoomSettings.MaxPitch);

            state.Target.Pitch = newPitch;
            state.PitchPercent = Mathf.InverseLerp(
                state.ZoomSettings.MinPitch,
                state.ZoomSettings.MaxPitch,
                newPitch);

            state.IsRotatingCamera = _rsDelta.sqrMagnitude > DEAD_ZONE;
            return;
        }
        */

        // Set camera behaviour pitch settings
        state.ZoomSettings.MaxPitch = DefaultMaxPitch;
        state.ZoomSettings.MinPitch = DefaultMinPitch;

        // Manually set zoom if not in build mode
        if (!state.InBuildMode)
        {
            data.BuildModeZoomSettings.MaxPitch = DefaultMaxPitch;
            data.BuildModeZoomSettings.MinPitch = DefaultMinPitch;

            state.LastTarget.Zoom = _targetZoom;
            state.Target.Zoom = _targetZoom;
        }
        else if (state.InBuildMode)
        {
            data.BuildModeZoomSettings = _buildModeZoomSettings;

            state.LastTarget.Zoom = data.BuildModeZoomDistance;
            state.Target.Zoom = data.BuildModeZoomDistance;
        }
    }
}
