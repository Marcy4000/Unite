using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public abstract class MatchTransitionNetworkTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<Component> clients = new List<Component>();
    private Component server;
    private Component coordinator;
    private GameObject coordinatorPrefab;
    private GameObject progressPrefab;
    private object previousLoadingScreen;
    private bool isolated;
    protected abstract bool Host { get; }

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethods(Private | BindingFlags.Public).First(candidate => candidate.Name == method &&
            candidate.GetParameters().Length == args.Length && candidate.GetParameters()
                .Select((parameter, index) => parameter.ParameterType.IsInstanceOfType(args[index])).All(matches => matches)).Invoke(target, args);

    private Component Manager(string name, ushort port)
    {
        var node = new GameObject(name);
        objects.Add(node);
        var manager = node.AddComponent(GameType("Unity.Netcode.NetworkManager"));
        var transport = node.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        Call(transport, "SetConnectionData", "127.0.0.1", port, "127.0.0.1");
        var config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
        manager.GetType().GetField("NetworkConfig").SetValue(manager, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        Call(manager, "AddNetworkPrefab", coordinatorPrefab);
        Call(manager, "AddNetworkPrefab", progressPrefab);
        return manager;
    }

    private IEnumerator Until(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "The transition acknowledgement did not reach the expected peer.");
    }

    private Component Remote(Component client)
    {
        var spawnManager = Get(client, "SpawnManager");
        var spawned = (IDictionary)spawnManager.GetType().GetField("SpawnedObjects").GetValue(spawnManager);
        object networkId = Get(coordinator, "NetworkObjectId");
        return spawned.Contains(networkId) ? ((Component)spawned[networkId]).GetComponent(GameType("LoadingManagerTracker")) : null;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (GameType("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("An external network session is active.");
#if UNITY_EDITOR
        coordinatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/ClothingTests/PlayMode/MatchTransitionCoordinator.prefab");
        progressPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/LoadingScreen/ProgressTracker.prefab");
#endif
        Assert.That(coordinatorPrefab, Is.Not.Null);
        previousLoadingScreen = GameType("LoadingScreen").GetProperty("Instance").GetValue(null);
        isolated = true;
        GameType("LoadingScreen").GetProperty("Instance").SetValue(null, null);
        server = Manager("Transition server", 0);
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        ushort port = (ushort)Get(Call(server.GetComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport")), "GetLocalEndpoint"), "Port");
        for (int index = 0; index < 2; index++)
        {
            var client = Manager("Transition client " + index, port);
            clients.Add(client);
            Assert.That(Call(client, "StartClient"), Is.True);
            yield return Until(() => (bool)Get(client, "IsConnectedClient"));
        }
        var instance = UnityEngine.Object.Instantiate(coordinatorPrefab);
        objects.Add(instance);
        coordinator = instance.GetComponent(GameType("LoadingManagerTracker"));
        Call(instance.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Spawn", true);
        foreach (var client in clients) yield return Until(() => Remote(client) != null);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (!isolated) yield break;
        foreach (var client in clients) if (client != null) Call(client, "Shutdown", false);
        yield return null;
        if (coordinator != null && (bool)Get(coordinator, "IsSpawned"))
            Call(coordinator.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Despawn", true);
        if (server != null) Call(server, "Shutdown", false);
        yield return null;
        foreach (var node in objects) if (node != null) UnityEngine.Object.Destroy(node);
        clients.Clear();
        objects.Clear();
        yield return null;
        GameType("LoadingScreen").GetProperty("Instance").SetValue(null, previousLoadingScreen);
        isolated = false;
    }

    [UnityTest]
    public IEnumerator ActualClientSendersGateBothPhasesAcrossConsecutiveMatches()
    {
        var rpcParams = Activator.CreateInstance(GameType("Unity.Netcode.RpcParams"));
        var ids = clients.Select(client => (ulong)Get(client, "LocalClientId")).ToList();
        if (Host) ids.Add(0);
        for (int generation = 1; generation <= 2; generation++)
        {
            var barrier = Activator.CreateInstance(GameType("Unite.Multiplayer.MatchTransitionBarrier"), generation, "CharacterSelect", ids);
            coordinator.GetType().GetField("selectionTransition", Private).SetValue(coordinator, barrier);
            Call(coordinator, "BeginSelectionTransitionRpc", generation, "CharacterSelect", 0d);
            foreach (var client in clients)
                yield return Until(() => (int)Remote(client).GetType().GetField("receivedSelectionTransition", Private).GetValue(Remote(client)) == generation);

            if (Host) Call(coordinator, "SelectionClosedRpc", generation, rpcParams);
            Call(Remote(clients[0]), "SelectionClosedRpc", generation, rpcParams);
            Call(Remote(clients[1]), "SelectionClosedRpc", generation - 1, rpcParams);
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(Get(barrier, "CanLoad"), Is.False);
            Call(Remote(clients[1]), "SelectionClosedRpc", generation, rpcParams);
            yield return Until(() => (bool)Get(barrier, "CanLoad"));
            Assert.That(Call(barrier, "StartLoading"), Is.True);

            if (Host) Call(coordinator, "SelectionScreenReadyRpc", generation, rpcParams);
            Call(Remote(clients[0]), "SelectionScreenReadyRpc", generation, rpcParams);
            Call(Remote(clients[1]), "SelectionScreenReadyRpc", generation - 1, rpcParams);
            Call(barrier, "MarkSceneLoaded", "CharacterSelect");
            yield return new WaitForSecondsRealtime(0.1f);
            Assert.That(Get(barrier, "CanOpen"), Is.False);
            Call(Remote(clients[1]), "SelectionScreenReadyRpc", generation, rpcParams);
            yield return Until(() => (bool)Get(barrier, "CanOpen"));
            Assert.That(Call(barrier, "ScheduleOpen"), Is.True);
        }
        Assert.That(Get(coordinator.GetComponent(GameType("Unity.Netcode.NetworkObject")), "DestroyWithScene"), Is.False);
    }
}

public class MatchTransitionHostTests : MatchTransitionNetworkTests { protected override bool Host => true; }
public class MatchTransitionDedicatedServerTests : MatchTransitionNetworkTests { protected override bool Host => false; }
