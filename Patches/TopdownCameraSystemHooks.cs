using BepInEx.Unity.IL2CPP.Hook;
using ProjectM;
using ProjectM.Presentation;
using ProjectM.UI;
using RetroCamera.Behaviours;
using RetroCamera.Utilities;
using System.Runtime.InteropServices;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using static RetroCamera.Utilities.CameraState;
using HarmonyLib;
using System.Reflection;

namespace RetroCamera.Patches;
#nullable enable
internal static class TopdownCameraSystemHooks
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    unsafe delegate void HandleInputHandler(
        IntPtr _this, 
        ref InputState inputState);

    static HandleInputHandler? _handleInputOriginal;
    static INativeDetour? _handleInputDetour;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        unsafe delegate void UpdateCameraHandler(
        IntPtr _this,
        ref CameraTarget cameraTarget,
        ref TopdownCamera topdownCamera,
        ref TopdownCameraState cameraState,
        ref Translation cameraTranslation,
        ref Rotation cameraRotation
    );

    static UpdateCameraHandler? _updateCameraOriginal;
    static INativeDetour? _updateCameraDetour;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        unsafe delegate void CursorPositionExecuteHandler(
        IntPtr _this,
        ref CollisionWorld collisionWorld,
        ref int heightLevel,
        ref FadeTargetsSingleton fadeTargets,
        ref CurrentFadingDataSingleton fadeData,
        ref CursorPosition cursorPosition,
        ref EntityManager entityManager
    );

    static CursorPositionExecuteHandler? _cursorPositionExecuteOriginal;
    static INativeDetour? _cursorPositionExecuteDetour;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    unsafe delegate void HandleGamepadHandler(IntPtr _this, ref InputState inputState);

    static HandleGamepadHandler? _handleGamepadOriginal;
    static INativeDetour? _handleGamepadDetour;

    static ZoomSettings _defaultZoomSettings;
    static ZoomSettings _defaultStandardZoomSettings;
    static ZoomSettings _defaultBuildModeZoomSettings;

    static bool _defaultZoomSettingsSaved;
    static bool _usingDefaultZoomSettings;
    static bool _initialized;
    public static unsafe void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        bool success = true;

        try
        {
            var handleInputMethod = typeof(TopdownCameraSystem).GetMethod("HandleInput", AccessTools.all);
            var handleInputAddress = MethodResolver.ResolveFromMethodInfo(handleInputMethod);
            Core.Log.LogInfo($"Resolved HandleInput address: {handleInputAddress}");
            _handleInputDetour = NativeDetour.Create(typeof(TopdownCameraSystem), "HandleInput", HandleInputPatch, out _handleInputOriginal);
        }
        catch (Exception e)
        {
            Core.Log.LogError($"Failed to create HandleInput detour: {e}");
            success = false;
        }

        try
        {
            var updateCameraType = typeof(TopdownCameraSystem)
                .GetNestedTypes()
                .First(t => t.Name.Contains("CameraUpdateJob") && t.GetMethod("UpdateCamera", AccessTools.all) != null);
            var updateCameraMethod = updateCameraType.GetMethod("UpdateCamera", AccessTools.all);
            var updateCameraAddress = MethodResolver.ResolveFromMethodInfo(updateCameraMethod);
            Core.Log.LogInfo($"Resolved UpdateCamera address: {updateCameraAddress}");
            _updateCameraDetour = NativeDetour.Create(
                typeof(TopdownCameraSystem),
                "CameraUpdateJob",
                "UpdateCamera",
                UpdateCameraPatch,
                out _updateCameraOriginal
            );
        }
        catch (Exception e)
        {
            Core.Log.LogError($"Failed to create UpdateCamera detour: {e}");
            success = false;
        }

        try
        {
            Type containerType = typeof(CursorPositionSystem);
            bool hasLambdaJob0 = containerType.GetNestedTypes().Any(t => t.Name.Contains("LambdaJob_0"));

            Func<Type, bool> nestedTypePredicate = t => hasLambdaJob0 ? t.Name.Contains("LambdaJob_0") : t.Name.Contains("LambdaJob");
            Func<MethodInfo, bool> methodPredicate = m =>
            {
                if (m.IsStatic)
                {
                    return false;
                }

                if (!(m.Name == "Execute" || m.Name.EndsWith("_Execute")))
                {
                    return false;
                }

                var targetParams = typeof(CursorPositionExecuteHandler)
                    .GetMethod("Invoke")!
                    .GetParameters()
                    .Skip(1)
                    .Select(p => p.ParameterType);

                return m.GetParameters()
                    .Select(p => p.ParameterType)
                    .SequenceEqual(targetParams);
            };

            var nestedType = containerType.GetNestedTypes().First(nestedTypePredicate);
            var cursorMethod = nestedType.GetMethods(AccessTools.all).First(methodPredicate);
            var cursorAddress = MethodResolver.ResolveFromMethodInfo(cursorMethod);
            Core.Log.LogInfo($"Resolved CursorPosition Execute address: {cursorAddress}");

            _cursorPositionExecuteDetour = NativeDetour.CreateBySignature<CursorPositionExecuteHandler>(
                containerType,
                nestedTypePredicate,
                methodPredicate,
                CursorPositionExecutePatch,
                out _cursorPositionExecuteOriginal
            );
        }
        catch (Exception e)
        {
            Core.Log.LogError($"Failed to create CursorPositionExecute detour: {e}");
            success = false;
        }

        try
        {
            var handleGamepadMethod = typeof(GamepadCursorSystem).GetMethod("HandleInput", AccessTools.all);
            var handleGamepadAddress = MethodResolver.ResolveFromMethodInfo(handleGamepadMethod);
            Core.Log.LogInfo($"Resolved Gamepad HandleInput address: {handleGamepadAddress}");
            _handleGamepadDetour = NativeDetour.Create(
                typeof(GamepadCursorSystem),
                "HandleInput",
                HandleGamepadPatch,
                out _handleGamepadOriginal
            );
        }
        catch (Exception ex)
        {
            Core.Log.LogError($"Failed to create HandleGamepadInput detour: {ex}");
            success = false;
        }

        if (success)
        {
            _initialized = true;
        }
    }
    static unsafe void HandleInputPatch(IntPtr _this, ref InputState inputState)
    {
        if (Settings.Enabled)
        {
            CurrentCameraBehaviour?.HandleInput(ref inputState);
        }

        _handleInputOriginal!(_this, ref inputState);
    }
    static unsafe void UpdateCameraPatch(
        IntPtr _this,
        ref CameraTarget cameraTarget,
        ref TopdownCamera topdownCamera,
        ref TopdownCameraState cameraState,
        ref Translation cameraTranslation,
        ref Rotation cameraRotation
    )
    {
        if (Settings.Enabled)
        {
            // Save default zoom settings once.
            if (!_defaultZoomSettingsSaved)
            {
                _defaultZoomSettings = cameraState.ZoomSettings;
                _defaultStandardZoomSettings = topdownCamera.StandardZoomSettings;
                _defaultBuildModeZoomSettings = topdownCamera.BuildModeZoomSettings;
                _defaultZoomSettingsSaved = true;
            }

            _usingDefaultZoomSettings = false;

            // Override the zoom if your mod wants to do so:
            cameraState.ZoomSettings.MaxZoom = Settings.MaxZoom;
            cameraState.ZoomSettings.MinZoom = 0f;

            // Check camera behaviors for activation
            foreach (CameraBehaviour cameraBehaviour in _cameraBehaviours.Values)
            {
                if (cameraBehaviour.ShouldActivate(ref cameraState))
                {
                    CurrentCameraBehaviour?.Deactivate();
                    cameraBehaviour.Activate(ref cameraState);
                    break;
                }
            }

            // Make sure the current behavior is active
            if (!CurrentCameraBehaviour!.Active)
            {
                CurrentCameraBehaviour!.Activate(ref cameraState);
            }

            // Let your behavior update the camera data
            CurrentCameraBehaviour!.UpdateCameraInputs(ref cameraState, ref topdownCamera);

            // If you need to copy the final ZoomSettings back to the TopdownCamera
            topdownCamera.StandardZoomSettings = cameraState.ZoomSettings;
        }
        else if (_defaultZoomSettingsSaved && !_usingDefaultZoomSettings)
        {
            // Revert to default settings if your mod is disabled
            cameraState.ZoomSettings = _defaultZoomSettings;
            topdownCamera.StandardZoomSettings = _defaultStandardZoomSettings;
            topdownCamera.BuildModeZoomSettings = _defaultBuildModeZoomSettings;
            _usingDefaultZoomSettings = true;
        }

        _updateCameraOriginal!(
            _this,
            ref cameraTarget,
            ref topdownCamera,
            ref cameraState,
            ref cameraTranslation,
            ref cameraRotation
        );
    }
    static unsafe void CursorPositionExecutePatch(
        IntPtr _this,
        ref CollisionWorld collisionWorld,
        ref int heightLevel,
        ref FadeTargetsSingleton fadeTargets,
        ref CurrentFadingDataSingleton fadeData,
        ref CursorPosition cursorPosition,
        ref EntityManager entityManager
    )
    {
        if (EscapeMenuViewPatch._isEscapeMenuOpen || EscapeMenuViewPatch._isServerPaused || IsMenuOpen || !_validGameplayInputState)
        {
            _cursorPositionExecuteOriginal!(
                _this,
                ref collisionWorld,
                ref heightLevel,
                ref fadeTargets,
                ref fadeData,
                ref cursorPosition,
                ref entityManager
            );

            return;
        }

        _usingMouseWheel = _gameplayInputState.IsInputPressed(ButtonInputAction.ToggleEmoteWheel) 
            || _gameplayInputState.IsInputPressed(ButtonInputAction.ToggleActionWheel);

        if (_usingMouseWheel)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        else if (_validGameplayInputState &&
           (_isMouseLocked || _gameplayInputState.IsInputPressed(ButtonInputAction.RotateCamera)) &&
           !IsMenuOpen)
        {
            if (_isActionMode || _isFirstPerson)
            {
                float2 screenPosition = new((Screen.width / 2) + Settings.AimOffsetX, (Screen.height / 2) + Settings.AimOffsetY);
                cursorPosition.ScreenPosition = screenPosition;

                Cursor.lockState = CursorLockMode.Locked;
            }
        }

        /*
        if (Settings._cursorPosition != Vector3.zero)
        {
            cursorPosition.ScreenPosition = new float2(
                Settings._cursorPosition.x,
                Settings._cursorPosition.y
            );

            Settings._cursorPosition = Vector3.zero;
        }
        */

        _cursorPositionExecuteOriginal!(
            _this,
            ref collisionWorld,
            ref heightLevel,
            ref fadeTargets,
            ref fadeData,
            ref cursorPosition,
            ref entityManager
        );
    }
    static unsafe void HandleGamepadPatch(IntPtr _this, ref InputState inputState)
    {
        Core.Log.LogWarning("[GamepadCursorSystem.HandleInput]");
        _handleGamepadOriginal!(_this, ref inputState);
    }
    public static void Dispose()
    {
        _handleInputDetour?.Dispose();
        _updateCameraDetour?.Dispose();
        _cursorPositionExecuteDetour?.Dispose();
        _handleGamepadDetour?.Dispose();
        _initialized = false;
    }
}
