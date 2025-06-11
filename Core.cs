using BepInEx.Logging;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using ProjectM;
using ProjectM.Physics;
using ProjectM.Scripting;
using ProjectM.Sequencer;
using ProjectM.UI;
using RetroCamera.Patches;
using RetroCamera.Utilities;
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace RetroCamera;
internal class Core
{
    public static World _client;

    static Entity _localCharacter = Entity.Null;
    static Entity _localUser = Entity.Null;
    public static Entity LocalCharacter =>
        _localCharacter.Exists()
        ? _localCharacter
        : (ConsoleShared.TryGetLocalCharacterInCurrentWorld(out _localCharacter, _client) && _localCharacter.Exists()
            ? _localCharacter
            : Entity.Null);
    public static Entity LocalUser =>
        _localUser.Exists()
        ? _localUser
        : (ConsoleShared.TryGetLocalUserInCurrentWorld(out _localUser, _client) && _localUser.Exists()
            ? _localUser
            : Entity.Null);
    public static EntityManager EntityManager => _client.EntityManager;
    public static ClientScriptMapper ClientScriptMapper { get; set; }
    public static ClientGameManager ClientGameManager { get; set; }
    public static GameDataSystem GameDataSystem { get; set; }
    public static ZoomModifierSystem ZoomModifierSystem { get; set; }
    public static TopdownCameraSystem TopdownCameraSystem { get; set; }
    public static TargetInfoParentSystem TargetInfoParentSystem { get; set; }
    public static PrefabCollectionSystem PrefabCollectionSystem { get; set; }
    public static ActionWheelSystem ActionWheelSystem { get; set; }
    public static UIDataSystem UIDataSystem { get; set; }
    public static CursorPositionSystem CursorPositionSystem { get; set; }
    public static InputActionSystem InputActionSystem { get; set; }
    public static LocalUserSystem LocalUserSystem { get; set; }
    public static ManualLogSource Log => Plugin.LogInstance;

    static MonoBehaviour _monoBehaviour;

    public static bool _initialized = false;
    public static void Initialize(GameDataManager __instance)
    {
        if (_initialized) return;

        _client = __instance.World;

        ZoomModifierSystem = _client.GetExistingSystemManaged<ZoomModifierSystem>();
        ZoomModifierSystem.Enabled = false; // necessary?

        TopdownCameraSystem = _client.GetExistingSystemManaged<TopdownCameraSystem>();
        TargetInfoParentSystem = _client.GetExistingSystemManaged<TargetInfoParentSystem>();
        PrefabCollectionSystem = _client.GetExistingSystemManaged<PrefabCollectionSystem>();
        ActionWheelSystem = _client.GetExistingSystemManaged<ActionWheelSystem>();
        UIDataSystem = _client.GetExistingSystemManaged<UIDataSystem>();
        CursorPositionSystem = _client.GetExistingSystemManaged<CursorPositionSystem>();

        ClientScriptMapper = _client.GetExistingSystemManaged<ClientScriptMapper>();
        ClientGameManager = ClientScriptMapper._ClientGameManager;
        GameDataSystem = _client.GetExistingSystemManaged<GameDataSystem>();

        InputActionSystem = _client.GetExistingSystemManaged<InputActionSystem>();
        LocalUserSystem = _client.GetExistingSystemManaged<LocalUserSystem>();
        TopdownCameraSystemHooks.Initialize();

        _initialized = true;
    }
    public static Coroutine StartCoroutine(IEnumerator routine)
    {
        if (_monoBehaviour == null)
        {
            var go = new GameObject(MyPluginInfo.PLUGIN_NAME);
            _monoBehaviour = go.AddComponent<IgnorePhysicsDebugSystem>();
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        return _monoBehaviour.StartCoroutine(routine.WrapToIl2Cpp());
    }
    public static void ResetStates()
    {
        ClearSkies.Reset();
        TopdownCameraSystemHooks.Dispose();
        Systems.RetroCamera.ResetState();

        _initialized = false;
        _localCharacter = Entity.Null;
        _localUser = Entity.Null;
    }
    public static void LogEntity(World world, Entity entity)
    {
        Il2CppSystem.Text.StringBuilder sb = new();

        try
        {
            EntityDebuggingUtility.DumpEntity(world, entity, true, sb);
            Log.LogInfo($"Entity Dump:\n{sb.ToString()}");
        }
        catch (Exception e)
        {
            Log.LogWarning($"Error dumping entity: {e.Message}");
        }
    }
    public static EntityQuery BuildEntityQuery(
    EntityManager entityManager,
    ComponentType[] all,
    EntityQueryOptions options)
    {
        var builder = new EntityQueryBuilder(Allocator.Temp);

        foreach (var componentType in all)
            builder.AddAll(componentType);

        builder.WithOptions(options);

        return entityManager.CreateEntityQuery(ref builder);
    }
}