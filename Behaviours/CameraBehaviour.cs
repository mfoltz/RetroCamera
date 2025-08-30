using ProjectM;
using RetroCamera.Patches;
using UnityEngine;
using static RetroCamera.Utilities.CameraState;
using static RetroCamera.Systems.RetroCamera;

namespace RetroCamera.Behaviours;
internal abstract class CameraBehaviour
{
    public BehaviourType BehaviourType;
    public float DefaultMaxPitch;
    public float DefaultMinPitch;
    public bool Active;

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

        if (_isMouseLocked && !IsMenuOpen && !inputState.IsInputPressed(ButtonInputAction.RotateCamera))
        {
            inputState.InputsPressed.m_ListData->AddNoResize(ButtonInputAction.RotateCamera);
        }

        // Manually manage camera zoom
        float zoomValue = inputState.GetAnalogValue(AnalogInputAction.ZoomCamera);

        // if (zoomValue != 0 && (!_inBuildMode || !Settings.ActiveDuringBuildMode))
        if (zoomValue != 0 && !_inBuildMode)
        {
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
        _inBuildMode = state.InBuildMode;

        if (!_isBuildSettingsSet)
        {
            _buildModeZoomSettings = data.BuildModeZoomSettings;
            _isBuildSettingsSet = true;
        }

        // Set camera behaviour pitch settings
        state.ZoomSettings.MaxPitch = DefaultMaxPitch;
        state.ZoomSettings.MinPitch = DefaultMinPitch;

        // Manually set zoom if not in build mode
        // if (!state.InBuildMode || !Settings.ActiveDuringBuildMode)
        if (!state.InBuildMode)
        {
            data.BuildModeZoomSettings.MaxPitch = DefaultMaxPitch;
            data.BuildModeZoomSettings.MinPitch = DefaultMinPitch;

            state.LastTarget.Zoom = _targetZoom;
            state.Target.Zoom = _targetZoom;
        }
        // else if (state.InBuildMode && Settings.ActiveDuringBuildMode)
        else if (state.InBuildMode)
        {
            data.BuildModeZoomSettings = _buildModeZoomSettings;

            state.LastTarget.Zoom = data.BuildModeZoomDistance;
            state.Target.Zoom = data.BuildModeZoomDistance;
        }
    }
}
