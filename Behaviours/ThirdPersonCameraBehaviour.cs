using ProjectM;
using UnityEngine;
using static RetroCamera.Utilities.CameraState;

namespace RetroCamera.Behaviours;
internal class ThirdPersonCameraBehaviour : CameraBehaviour
{
    float _lastPitchPercent = float.PositiveInfinity;
    public ThirdPersonCameraBehaviour()
    {
        BehaviourType = BehaviourType.ThirdPerson;
    }
    public override void Activate(ref TopdownCameraState state)
    {
        base.Activate(ref state);

        if (_currentBehaviourType == BehaviourType) _targetZoom = Settings.MaxZoom / 2;
        else _targetZoom = Settings.MinZoom;

        _currentBehaviourType = BehaviourType;
        state.PitchPercent = _lastPitchPercent == float.PositiveInfinity ? 0.5f : _lastPitchPercent;
    }
    public override bool ShouldActivate(ref TopdownCameraState state)
    {
        return _currentBehaviourType != BehaviourType && _targetZoom > 0;
    }
    public override void HandleInput(ref InputState inputState)
    {
        base.HandleInput(ref inputState);

        if (Settings.LockZoom) _targetZoom = Settings.LockZoomDistance;
    }
    public override void UpdateCameraInputs(ref TopdownCameraState state, ref TopdownCamera data)
    {
        DefaultMaxPitch = Settings.MaxPitch;
        DefaultMinPitch = Settings.MinPitch;

        base.UpdateCameraInputs(ref state, ref data);

        state.LastTarget.NormalizedLookAtOffset.y = _isMounted ? Settings.HEAD_HEIGHT_OFFSET + Settings.MOUNTED_OFFSET : Settings.HEAD_HEIGHT_OFFSET;

        if (Settings.OverTheShoulder && !_isShapeshifted && !_isMounted)
        {
            float lerpValue = Mathf.Max(0, state.Current.Zoom - state.ZoomSettings.MinZoom) / state.ZoomSettings.MaxZoom;

            state.LastTarget.NormalizedLookAtOffset.x = Mathf.Lerp(Settings.OverTheShoulderX, 0, lerpValue);
            state.LastTarget.NormalizedLookAtOffset.y = Mathf.Lerp(Settings.OverTheShoulderY, 0, lerpValue);
        }

        //if (Settings.LockPitch && (!state.InBuildMode || !Settings.ActiveDuringBuildMode))
        if (Settings.LockPitch && !state.InBuildMode)
        {
            state.ZoomSettings.MaxPitch = Settings.LockPitchAngle;
            state.ZoomSettings.MinPitch = Settings.LockPitchAngle;

            data.BuildModeZoomSettings.MaxPitch = Settings.LockPitchAngle;
            data.BuildModeZoomSettings.MinPitch = Settings.LockPitchAngle;
        }
        else if (state.InBuildMode)
        {
            data.BuildModeZoomSettings.MinZoom = Settings.MinZoom;
            data.BuildModeZoomSettings.MaxZoom = Settings.MaxZoom;

            data.BuildModeZoomSettings.MaxPitch = Settings.MaxPitch;
            data.BuildModeZoomSettings.MinPitch = Settings.MinPitch;
        }

        _lastPitchPercent = state.PitchPercent;
    }
}
