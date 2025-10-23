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
        if (!_validGameplayInputState || !inputState.InputsPressed.IsCreated)
            return;

        if (Input.GetKeyDown(KeyCode.Escape)
            && EscapeMenuViewPatch._isEscapeMenuOpen)
        {
            IsMenuOpen = false;
            EscapeMenuViewPatch._isEscapeMenuOpen = false;
        }

        if (_isMouseLocked && !IsMenuOpen && !inputState.IsInputPressed(ButtonInputAction.RotateCamera))
        {
            inputState.InputsPressed.m_ListData->AddNoResize(ButtonInputAction.RotateCamera);
        }

        float zoomValue = inputState.GetAnalogValue(AnalogInputAction.ZoomCamera);
        if (zoomValue != 0 && !_inBuildMode)
        {
            var zoomAmount = Mathf.Lerp(.25f, 1.5f, Mathf.Max(0, _targetZoom - Settings.MinZoom) / Settings.MaxZoom);
            var zoomChange = inputState.GetAnalogValue(AnalogInputAction.ZoomCamera) > 0 ? zoomAmount : -zoomAmount;

            if ((_targetZoom > Settings.MinZoom && _targetZoom + zoomChange < Settings.MinZoom) || (_isFirstPerson && zoomChange > 0)) _targetZoom = Settings.MinZoom;
            else _targetZoom = Mathf.Clamp(_targetZoom + zoomChange, Settings.FirstPersonEnabled ? 0 : Settings.MinZoom, Settings.MaxZoom);

            inputState.SetAnalogValue(AnalogInputAction.ZoomCamera, 0);
        }

        if (_targetZoom > Settings.MaxZoom)
            _targetZoom = Settings.MaxZoom;

        if (SocialWheelActive)
        {
            Core.ActionWheelSystem.UpdateAndShowWheel(SocialWheel, inputState);
        }
    }
    public virtual void UpdateCameraInputs(ref TopdownCameraState state, ref TopdownCamera data)
    {
        _inBuildMode = state.InBuildMode;
        bool defaultBuildMode = Settings.DefaultBuildModeCamera;

        if (!_isBuildSettingsSet)
        {
            /*
            _buildModeZoomSettings = new ZoomSettings
            {
                MaxPitch = data.BuildModeZoomSettings.MaxPitch,
                MinPitch = data.BuildModeZoomSettings.MinPitch,
                MaxZoom = data.BuildModeZoomSettings.MaxZoom,
                MinZoom = data.BuildModeZoomSettings.MinZoom
            };
            */

            /*
            _buildModeZoomSettings = new ZoomSettings
            {
                MaxPitch = Settings.MaxPitch,
                MinPitch = Settings.MinPitch,
                MaxZoom = Settings.MaxZoom,
                MinZoom = Settings.MinZoom
            };
            */

            _buildModeZoomSettings = data.BuildModeZoomSettings;
            _isBuildSettingsSet = true;
        }

        if (state.InBuildMode)
        {
            data.BuildZoomEnabled = defaultBuildMode;

            if (!defaultBuildMode)
            {
                // Set camera pitch settings
                state.ZoomSettings.MaxPitch = DefaultMaxPitch;
                state.ZoomSettings.MinPitch = DefaultMinPitch;

                // Set camera zoom settings
                state.LastTarget.Zoom = _targetZoom;
                state.Target.Zoom = _targetZoom;
            }
            else
            {
                data.BuildModeZoomSettings = _buildModeZoomSettings;

                state.LastTarget.Zoom = data.BuildModeZoomDistance;
                state.Target.Zoom = data.BuildModeZoomDistance;
            }
        }
        else
        {
            // Set camera pitch settings
            state.ZoomSettings.MaxPitch = DefaultMaxPitch;
            state.ZoomSettings.MinPitch = DefaultMinPitch;

            // Set camera zoom settings
            state.LastTarget.Zoom = _targetZoom;
            state.Target.Zoom = _targetZoom;
        }

        /*
        // Always set zoom?
        // Manually set zoom if not in build mode
        if (!state.InBuildMode)
        {
            // data.BuildModeZoomSettings.MaxPitch = DefaultMaxPitch;
            // data.BuildModeZoomSettings.MinPitch = DefaultMinPitch;

            state.LastTarget.Zoom = _targetZoom;
            state.Target.Zoom = _targetZoom;
        }
        if (state.InBuildMode)
        {
            data.BuildZoomEnabled = true;

            // data.BuildModeZoomSettings.MaxZoom = Settings.MaxZoom;
            // data.BuildModeZoomSettings.MinZoom = Settings.MinZoom;

            // data.BuildModeZoomSettings.MaxPitch = DefaultMaxPitch;
            // data.BuildModeZoomSettings.MinPitch = DefaultMinPitch;

            state.LastTarget.Zoom = _targetZoom;
            state.Target.Zoom = _targetZoom;
        }
        */
    }
    public static void ResetState()
    {
        _isBuildSettingsSet = false;
    }
}
