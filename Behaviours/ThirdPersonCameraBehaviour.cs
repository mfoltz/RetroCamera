﻿using ProjectM;
using UnityEngine;
using static ModernCamera.Utilities.CameraStateUtilities;

namespace ModernCamera.Behaviours;
internal class ThirdPersonCameraBehaviour : CameraBehaviour
{
    float LastPitchPercent = float.PositiveInfinity;
    public ThirdPersonCameraBehaviour()
    {
        BehaviourType = BehaviourType.ThirdPerson;
    }
    public override void Activate(ref TopdownCameraState state)
    {
        base.Activate(ref state);

        if (CurrentBehaviourType == BehaviourType) TargetZoom = Settings.MaxZoom / 2;
        else TargetZoom = Settings.MinZoom;

        CurrentBehaviourType = BehaviourType;
        state.PitchPercent = LastPitchPercent == float.PositiveInfinity ? 0.5f : LastPitchPercent;
    }
    public override bool ShouldActivate(ref TopdownCameraState state)
    {
        return CurrentBehaviourType != BehaviourType && TargetZoom > 0;
    }
    public override void HandleInput(ref InputState inputState)
    {
        base.HandleInput(ref inputState);

        if (Settings.LockZoom) TargetZoom = Settings.LockZoomDistance;
    }
    public override void UpdateCameraInputs(ref TopdownCameraState state, ref TopdownCamera data)
    {
        DefaultMaxPitch = Settings.MaxPitch;
        DefaultMinPitch = Settings.MinPitch;

        base.UpdateCameraInputs(ref state, ref data);

        state.LastTarget.NormalizedLookAtOffset.y = IsMounted ? Settings.HeadHeightOffset + Settings.MountedOffset : Settings.HeadHeightOffset;

        if (Settings.OverTheShoulder && !IsShapeshifted && !IsMounted)
        {
            float lerpValue = Mathf.Max(0, state.Current.Zoom - state.ZoomSettings.MinZoom) / state.ZoomSettings.MaxZoom;

            state.LastTarget.NormalizedLookAtOffset.x = Mathf.Lerp(Settings.OverTheShoulderX, 0, lerpValue);
            state.LastTarget.NormalizedLookAtOffset.y = Mathf.Lerp(Settings.OverTheShoulderY, 0, lerpValue);
        }

        if (Settings.LockPitch && (!state.InBuildMode || !Settings.DefaultBuildMode))
        {
            state.ZoomSettings.MaxPitch = Settings.LockPitchAngle;
            state.ZoomSettings.MinPitch = Settings.LockPitchAngle;

            data.BuildModeZoomSettings.MaxPitch = Settings.LockPitchAngle;
            data.BuildModeZoomSettings.MinPitch = Settings.LockPitchAngle;
        }

        LastPitchPercent = state.PitchPercent;
    }
}
