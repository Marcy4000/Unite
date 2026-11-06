using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GlaceonReplicationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> roots = new List<GameObject>();
    private NetworkManager server, client;
    private GameObject actorPrefab, holderPrefab, spearPrefab, unitePrefab;
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Get(object value, string name) => value.GetType().GetProperty(name).GetValue(value);
    private static void Set(object value, string name, object data) => value.GetType().GetField(name, Private).SetValue(value, data);
    private static object Call(object value, string method, params object[] args) => value.GetType().GetMethod(method, args.Select(a => a.GetType()).ToArray()).Invoke(value, args);
    private static void Variable(object value, string name, object data)
    {
        var variable = value.GetType().GetField(name, Private).GetValue(value); variable.GetType().GetProperty("Value").SetValue(variable, data);
    }
    private static IEnumerator Until(Func<bool> ready)
    {
        float end = Time.realtimeSinceStartup + 8;
        while (!ready() && Time.realtimeSinceStartup < end) yield return null;
        Assert.That(ready(), Is.True, "Glaceon's expected network state did not arrive.");
    }
    private static int PresentedUniteCharge(Component moves)
    {
        // The network fixture disables the normal controller Update.
        moves.GetType().GetMethod("UpdateGlaceonPresentation", Private).Invoke(moves, null);
        return (int)Get(moves, "UniteMoveCharge");
    }
    private NetworkManager Manager(string name, ushort port)
    {
        var root = new GameObject(name); roots.Add(root);
        var manager = root.AddComponent<NetworkManager>(); var transport = root.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, EnableSceneManagement = false };
        foreach (var prefab in new[] { actorPrefab, holderPrefab, spearPrefab, unitePrefab }) manager.AddNetworkPrefab(prefab);
        return manager;
    }
    [UnityTest] public IEnumerator DedicatedServerReplicatesLateSnapshotsAndAcceptsRemoteSpearInput() { yield return Replicate(false); }
    [UnityTest] public IEnumerator HostReplicatesLateSnapshotsAndAcceptsRemoteSpearInput() { yield return Replicate(true); }
    private IEnumerator Replicate(bool host)
    {
        if (NetworkManager.Singleton != null) Assert.Ignore("An external network session is active.");
#if UNITY_EDITOR
        actorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab");
        holderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Passives/Glaceon/IciclesHolder.prefab");
        spearPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Glaceon/IcicleSpear.prefab");
        unitePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Glaceon/GlaceonUnite.prefab");
        var definition = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Glaceon.asset");
#else
        Assert.Ignore("Requires Editor-authored network fixtures."); yield break;
#endif
        foreach (string tag in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        {
            var map = new GameObject(tag); roots.Add(map); map.tag = tag;
            map.transform.position = Vector3.down * 30; map.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100);
        }
        server = Manager("Glaceon replication server", 0);
        Assert.That(host ? server.StartHost() : server.StartServer(), Is.True); yield return null;
        var actor = UnityEngine.Object.Instantiate(actorPrefab); roots.Add(actor);
        var pokemon = actor.GetComponent(T("Pokemon")); var moves = actor.GetComponent(T("MovesController")); var player = actor.GetComponent(T("PlayerManager"));
        Set(pokemon, "baseStats", definition); Set(moves, "pokemon", pokemon); Set(moves, "playerManager", player);
        var actorNet = actor.GetComponent<NetworkObject>(); actorNet.SpawnWithOwnership(99, true);
        Variable(pokemon, "currentLevel", 3); Variable(pokemon, "currentHp", 2000);
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); var callback = hp.GetType().GetField("OnValueChanged");
        callback.SetValue(hp, Delegate.CreateDelegate(callback.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
        var learned = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves);
        learned.SetValue(Enum.Parse(T("AvailableMoves"), "GlaceIcycleSpear"), 0);
        var combat = Get(moves, "GlaceCombat"); Call(combat, "Tick"); Call(combat, "AddCrystals", 6);
        var holder = UnityEngine.Object.Instantiate(holderPrefab); roots.Add(holder); var holderNet = holder.GetComponent<NetworkObject>(); holderNet.Spawn(true);
        Call(holder.GetComponent(T("IciclesHolder")), "Bind", actorNet.NetworkObjectId);
        // The observer connects after both resource gains and stock creation.
        client = Manager("Glaceon late observer", server.GetComponent<UnityTransport>().GetLocalEndpoint().Port);
        Assert.That(client.StartClient(), Is.True);
        yield return Until(() => client.IsConnectedClient && client.SpawnManager.SpawnedObjects.ContainsKey(actorNet.NetworkObjectId)
            && client.SpawnManager.SpawnedObjects.ContainsKey(holderNet.NetworkObjectId));
        var remoteActor = client.SpawnManager.SpawnedObjects[actorNet.NetworkObjectId]; var remoteMoves = remoteActor.GetComponent(T("MovesController"));
        var remoteHolder = client.SpawnManager.SpawnedObjects[holderNet.NetworkObjectId];
        yield return Until(() => (int)Get(remoteMoves, "GlaceCrystalCount") == 6 && remoteHolder.transform.childCount == 6);
        Assert.That((float)Get(remoteMoves, "GlaceCrystalRemaining"), Is.GreaterThan(0));
        Set(remoteActor.GetComponent(T("Pokemon")), "baseStats", definition);
        Set(remoteMoves, "pokemon", remoteActor.GetComponent(T("Pokemon"))); Set(remoteMoves, "playerManager", remoteActor.GetComponent(T("PlayerManager")));
        var statuses = Array.CreateInstance(T("BattleActionStatus"), 2);
        for (int i = 0; i < 2; i++) statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), i);
        Set(remoteMoves, "moveStatuses", statuses);
        actorNet.ChangeOwnership(client.LocalClientId); yield return Until(() => remoteActor.IsOwner);
        var remotePlayer = remoteActor.GetComponent(T("PlayerManager"));
        Variable(player, "botControlled", true);
        yield return Until(() => (bool)Get(remotePlayer, "IsBot"));
        var aimRoot = new GameObject("Glaceon input regression aim"); roots.Add(aimRoot);
        var aim = aimRoot.AddComponent(T("Aim")); ((Behaviour)aim).enabled = false;
        Set(aim, "actor", remotePlayer); Set(aim, "botAimPoint", Vector3.forward * 6);
        foreach (string field in new[] { "skillShotLine", "circleIndicator", "glaceonUniteIndicator" })
        {
            var visual = new GameObject(field); visual.transform.SetParent(aimRoot.transform); visual.SetActive(false); Set(aim, field, visual);
        }
        Set(remotePlayer, "aim", aim);
        Set(remoteMoves, "glaceRequestSerial", 99);
        var runtime = Activator.CreateInstance(T("GlaceIcicleSpear"));
        var runtimeMoves = (Array)remoteMoves.GetType().GetField("moves", Private).GetValue(remoteMoves);
        runtimeMoves.SetValue(runtime, 0);
        // Exercise press, release and recast through the actual owner input path.
        Call(remoteMoves, "TryUsingMove", 0);
        yield return Until(() => server.SpawnManager.SpawnedObjects.Values.Any(n => n.GetComponent(T("IcicleSpearHitbox")) != null));
        var spearNet = server.SpawnManager.SpawnedObjects.Values.First(n => n.GetComponent(T("IcicleSpearHitbox")) != null);
        var spear = spearNet.gameObject;
        yield return Until(() => client.SpawnManager.SpawnedObjects.ContainsKey(spearNet.NetworkObjectId));
        Call(remoteMoves, "GlaceSpearInputRPC", Vector3.right, false, 100);
        yield return Until(() => ((Vector3)Get(combat, "SpearDirection")).x > 0.9f
            && Vector3.Dot(spear.transform.forward, Vector3.right) > 0.9f
            && Vector3.Dot(client.SpawnManager.SpawnedObjects[spearNet.NetworkObjectId].transform.forward, Vector3.right) > 0.9f);
        Call(remoteMoves, "GlaceSpearInputRPC", Vector3.back, true, -1);
        yield return new WaitForSeconds(0.1f);
        Assert.That((bool)Get(combat, "SpearHeld"), Is.True, "Stale cast input must not release a newer channel.");
        Call(remoteMoves, "TryFinishingMove", 0);
        yield return Until(() => !(bool)Get(combat, "SpearHeld") && (int)Get(moves, "ServerActionLocks") == 0);
        Assert.That(Call(statuses.GetValue(0), "HasStatus", Enum.Parse(T("ActionStatusType"), "Busy")), Is.False);
        yield return Until(() => (bool)Get(remoteMoves, "GlaceSpearActive") && (float)Get(remoteMoves, "GlaceSpearTickRemaining") > 0);
        Call(remoteMoves, "SpawnMoveEffectRPC", Enum.Parse(T("AvailableMoves"), "GlaceIcycleSpear"), Vector3.back, 0ul, 99);
        yield return new WaitForSeconds(0.1f);
        Assert.That((bool)Get(combat, "SpearHeld"), Is.False, "An older cast request must not resume the current cast.");
        Set(aim, "botAimPoint", Vector3.back * 6);
        Call(remoteMoves, "TryUsingMove", 0);
        Assert.That(Get(runtime, "ExecutionRequest"), Is.EqualTo(100), "Resuming must retain the original cast's acknowledgment identity.");
        yield return Until(() => (bool)Get(combat, "SpearHeld") && (int)Get(moves, "ServerActionLocks") > 0
            && !(bool)Call(statuses.GetValue(0), "HasStatus", Enum.Parse(T("ActionStatusType"), "Busy")));
        Assert.That(server.SpawnManager.SpawnedObjects.Values.Count(n => n.GetComponent(T("IcicleSpearHitbox")) != null), Is.EqualTo(1));
        Call(remoteMoves, "TryFinishingMove", 0);
        yield return Until(() => !(bool)Get(combat, "SpearHeld") && (int)Get(moves, "ServerActionLocks") == 0);
        int stockBeforeTick = (int)Get(remoteMoves, "GlaceCrystalCount");
        var tick = remoteMoves.GetType().GetField("glaceSpearNextTick", Private).GetValue(remoteMoves);
        double previousTick = (double)tick.GetType().GetProperty("Value").GetValue(tick);
        yield return Until(() => (double)tick.GetType().GetProperty("Value").GetValue(tick) > previousTick
            && (int)Get(remoteMoves, "GlaceCrystalCount") < stockBeforeTick
            && (float)Get(remoteMoves, "GlaceSpearTickRemaining") > 0);
        Variable(pokemon, "currentHp", 0);
        yield return Until(() => (int)Get(remoteMoves, "GlaceCrystalCount") == 0 && remoteHolder.transform.childCount == 0
            && !client.SpawnManager.SpawnedObjects.ContainsKey(spearNet.NetworkObjectId));
        yield return Until(() => !(bool)Get(remoteMoves, "GlaceSpearActive"));
        // Ready display, validation and spending must agree for a remote Unite.
        Variable(pokemon, "currentHp", 2000); Variable(pokemon, "currentLevel", 7);
        learned.SetValue(Enum.Parse(T("AvailableMoves"), "GlaceonUnite"), 2);
        var unite = Activator.CreateInstance(T("GlaceUnite"));
        T("MoveBase").GetField("playerManager").SetValue(unite, remoteActor.GetComponent(T("PlayerManager")));
        var over = T("MoveBase").GetEvent("onMoveOver");
        over.AddEventHandler(unite, Delegate.CreateDelegate(over.EventHandlerType, remoteMoves, remoteMoves.GetType().GetMethod("OnMoveOver")));
        Set(remoteMoves, "uniteMove", unite); Set(remoteMoves, "uniteMoveMaxCharge", 100000);
        Call(moves, "GrantLegacyItemEnergy", 100000);
        yield return Until(() => PresentedUniteCharge(remoteMoves) == 100000);
        Call(remoteMoves, "TryUsingUniteMove");
        Assert.That(((GameObject)aim.GetType().GetField("glaceonUniteIndicator", Private).GetValue(aim)).activeSelf, Is.True);
        Assert.That(((GameObject)aim.GetType().GetField("skillShotLine", Private).GetValue(aim)).activeSelf, Is.False);
        Call(remoteMoves, "TryFinishingUniteMove");
        Assert.That(((GameObject)aim.GetType().GetField("glaceonUniteIndicator", Private).GetValue(aim)).activeSelf, Is.False);
        yield return Until(() => server.SpawnManager.SpawnedObjects.Values.Any(n => n.GetComponent(T("GlaceUniteArea")) != null)
            && PresentedUniteCharge(remoteMoves) < 100000);
        var uniteStatus = remoteMoves.GetType().GetField("uniteMoveStatus", Private).GetValue(remoteMoves);
        Assert.That(Call(uniteStatus, "HasStatus", Enum.Parse(T("ActionStatusType"), "Cooldown")), Is.True);
        // KO/scoring energy can refill before 112 seconds; no second timer may reject it.
        Call(moves, "GrantLegacyItemEnergy", 100000);
        yield return Until(() => PresentedUniteCharge(remoteMoves) == 100000);
        Call(remoteMoves, "TryUsingUniteMove");
        Call(remoteMoves, "TryFinishingUniteMove");
        yield return Until(() => server.SpawnManager.SpawnedObjects.Values.Count(n => n.GetComponent(T("GlaceUniteArea")) != null) == 2
            && PresentedUniteCharge(remoteMoves) < 100000);

    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (server != null && server.IsListening)
            foreach (var network in server.SpawnManager.SpawnedObjects.Values.ToArray()) if (network != null && network.IsSpawned) network.Despawn(true);
        if (client != null) client.Shutdown(); if (server != null) server.Shutdown(); yield return null;
        foreach (var root in roots) if (root != null) UnityEngine.Object.Destroy(root);
        roots.Clear(); yield return null;
    }
}
