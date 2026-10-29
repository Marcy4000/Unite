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

public class PsyduckRacingMoveTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private Component host;
    private Component client;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, args.Select(arg => arg.GetType()).ToArray(), null).Invoke(target, args);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (GameType("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("An external network session is active.");
        var root = new GameObject("Racing move test server"); objects.Add(root);
        host = root.AddComponent(GameType("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        Call(transport, "SetConnectionData", "127.0.0.1", (ushort)0, "127.0.0.1");
        object config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
        host.GetType().GetField("NetworkConfig").SetValue(host, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        foreach (string name in new[] { "PsyduckBarrier", "PsyduckRaceShield" })
            Call(host, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Psyduck/" + name + ".prefab"));
        Assert.That(Call(host, "StartHost"), Is.True);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (client != null) Call(client, "Shutdown", false);
        yield return null;
        if (host != null)
        {
            foreach (var root in objects.ToArray())
            {
                if (root == null) continue;
                var network = root.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
            }
            Call(host, "Shutdown", false);
        }
        yield return null;
        foreach (var root in objects) if (root != null) UnityEngine.Object.Destroy(root);
        objects.Clear(); client = null; host = null;
        yield return null;
    }

    private Component Racer(Vector3 position)
    {
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/PsyduckRacingTests/Fixtures/Racer.prefab"));
        objects.Add(root); root.transform.position = position;
        var network = root.GetComponent(GameType("Unity.Netcode.NetworkObject"));
        network.GetType().GetField("SpawnWithObservers").SetValue(network, false);
        var pokemon = root.GetComponent(GameType("Pokemon")); ((Behaviour)pokemon).enabled = false;
        root.AddComponent<CharacterController>();
        var player = root.AddComponent(GameType("PlayerManager")); ((Behaviour)player).enabled = false;
        Call(network, "SpawnWithOwnership", 123UL, false);
        return player;
    }

    private Component Effect(string prefabName, string type)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Psyduck/" + prefabName + ".prefab");
        var root = UnityEngine.Object.Instantiate(prefab, Vector3.up * 100, Quaternion.identity); objects.Add(root);
        Call(root.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Spawn", true);
        return root.GetComponent(GameType(type));
    }

    private static bool HasStatus(Component player, string status) => (bool)Call(Get(player, "Pokemon"), "HasStatusEffect", Enum.Parse(GameType("StatusType"), status));
    private static int StatusCount(Component player) => (int)Get(Get(Get(player, "Pokemon"), "StatusEffects"), "Count");

    [UnityTest]
    public IEnumerator BarrierUsesRealTriggerPhysicsAndIgnoresItsCaster()
    {
        var caster = Racer(Vector3.up * 100);
        var target = Racer(Vector3.up * 100 + Vector3.forward * 5);
        var wall = Effect("PsyduckBarrier", "PsyduckRaceBarrierWall");
        Call(wall, "InitializeRPC", caster.transform.position, Vector3.zero, (ulong)Get(caster, "NetworkObjectId"));
        var collider = wall.GetComponent<BoxCollider>();
        Assert.That(collider.enabled && collider.isTrigger, Is.True);
        Assert.That(wall.GetComponent<Rigidbody>().isKinematic, Is.True);
        Call(wall, "OnTriggerEnter", (Collider)caster.GetComponent<CharacterController>());
        Assert.That(Get(wall, "IsSpawned"), Is.True);
        Assert.That(StatusCount(caster), Is.Zero);
        target.transform.position = collider.bounds.center;
        Physics.SyncTransforms();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(HasStatus(target, "Incapacitated"), Is.True);
        Assert.That(wall == null || !(bool)Get(wall, "IsSpawned"), Is.True);
    }

    [UnityTest]
    public IEnumerator ShieldBlocksExactlyOneHitWithoutPublishingFreezeOrRemovingBuffs()
    {
        var caster = Racer(Vector3.up * 100 + Vector3.forward * 10);
        var target = Racer(Vector3.up * 100);
        var shield = Effect("PsyduckRaceShield", "PsyduckRacingShieldObject");
        Call(shield, "Initialize", (ulong)Get(target, "NetworkObjectId"));
        var projectile = Effect("PsyduckRaceFreezeRay", "PsyduckRaceHoamingProjectile");
        Call(projectile, "SetTargetRPC", (ulong)Get(target, "NetworkObjectId"), (ulong)Get(caster, "NetworkObjectId"), caster.transform.position);
        Call(projectile, "OnTriggerEnter", (Collider)target.GetComponent<CharacterController>());
        Assert.That(StatusCount(target), Is.Zero);
        Assert.That(Get(shield, "IsSpawned"), Is.False);
        Assert.That(Get(projectile, "IsSpawned"), Is.False);
        object unrelated = Activator.CreateInstance(GameType("StatusEffect"), Enum.Parse(GameType("StatusType"), "Scriptable"), 3f, true, (ushort)99);
        Call(Get(target, "Pokemon"), "AddStatusEffect", unrelated);
        var second = Effect("PsyduckRaceConfuseRay", "PsyduckRaceHoamingProjectile");
        Call(second, "SetTargetRPC", (ulong)Get(target, "NetworkObjectId"), (ulong)Get(caster, "NetworkObjectId"), caster.transform.position);
        Call(second, "OnTriggerEnter", (Collider)target.GetComponent<CharacterController>());
        Assert.That(HasStatus(target, "Incapacitated"), Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShieldPreservesSpeedBuffsAndRejectsDebuffsBeforePublication()
    {
        var target = Racer(Vector3.up * 100);
        var shield = Effect("PsyduckRaceShield", "PsyduckRacingShieldObject");
        Call(shield, "Initialize", (ulong)Get(target, "NetworkObjectId"));
        object speed = Enum.Parse(GameType("Stat"), "Speed");
        object buff = Activator.CreateInstance(GameType("StatChange"), (short)20, speed, 3f, true, true, true, (ushort)81, true);
        object slow = Activator.CreateInstance(GameType("StatChange"), (short)20, speed, 3f, true, false, true, (ushort)82, true);
        var pokemon = Get(target, "Pokemon");
        Call(pokemon, "AddStatChange", buff);
        Assert.That(Get(shield, "IsSpawned"), Is.True);
        Call(pokemon, "AddStatChange", slow);
        Assert.That(Get(Get(pokemon, "StatChanges"), "Count"), Is.EqualTo(1));
        Assert.That(Get(shield, "IsSpawned"), Is.False);
        Assert.That(StatusCount(target), Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ProjectilesExpireAfterLosingTheirTargetOrWithoutInitialization()
    {
        var caster = Racer(Vector3.up * 100);
        var target = Racer(Vector3.up * 100 + Vector3.forward * 20);
        var ray = Effect("PsyduckRaceConfuseRay", "PsyduckRaceHoamingProjectile");
        Call(ray, "SetTargetRPC", (ulong)Get(target, "NetworkObjectId"), (ulong)Get(caster, "NetworkObjectId"), caster.transform.position);
        Call(target.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Despawn", false);
        yield return null;
        yield return null;
        Assert.That(ray == null || !(bool)Get(ray, "IsSpawned"), Is.True);
        var bubble = Effect("PsyduckRaceBubble", "PsyduckRaceBubbleObject");
        Set(bubble, "timer", -1f);
        Call(bubble, "Update");
        Assert.That(Get(bubble, "IsSpawned"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator BubbleIgnoresCasterAndKillsItsTravelTweenOnDespawn()
    {
        var caster = Racer(Vector3.up * 100);
        var target = Racer(Vector3.up * 100 + Vector3.forward * 10);
        var bubble = Effect("PsyduckRaceBubble", "PsyduckRaceBubbleObject");
        Call(bubble, "InitializeRPC", (ulong)Get(caster, "NetworkObjectId"), caster.transform.position, caster.transform.position + Vector3.forward * 6.5f);
        Call(bubble, "OnTriggerEnter", (Collider)caster.GetComponent<CharacterController>());
        Assert.That(Get(bubble, "IsSpawned"), Is.True);
        var travel = bubble.GetType().GetField("travel", Private).GetValue(bubble);
        Call(bubble, "OnTriggerEnter", (Collider)target.GetComponent<CharacterController>());
        Assert.That(HasStatus(target, "Incapacitated"), Is.True);
        Assert.That(Get(bubble, "IsSpawned"), Is.False);
        Assert.That(Get(travel, "active"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShieldRejectsDirectFreezeBeforeItEntersTheStatusList()
    {
        var target = Racer(Vector3.up * 100);
        var shield = Effect("PsyduckRaceShield", "PsyduckRacingShieldObject");
        Call(shield, "Initialize", (ulong)Get(target, "NetworkObjectId"));
        object freeze = Activator.CreateInstance(GameType("StatusEffect"), Enum.Parse(GameType("StatusType"), "Scriptable"), 1f, true, (ushort)20);
        Call(Get(target, "Pokemon"), "AddStatusEffect", freeze);
        Assert.That(StatusCount(target), Is.Zero);
        Assert.That(Get(shield, "IsSpawned"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShieldReplacementAndExpiryRemoveOnlyTheirOwnInterceptors()
    {
        var target = Racer(Vector3.up * 100);
        var first = Effect("PsyduckRaceShield", "PsyduckRacingShieldObject");
        Call(first, "Initialize", (ulong)Get(target, "NetworkObjectId"));
        var second = Effect("PsyduckRaceShield", "PsyduckRacingShieldObject");
        Call(second, "Initialize", (ulong)Get(target, "NetworkObjectId"));
        Assert.That(Get(first, "IsSpawned"), Is.False);
        yield return null;
        Assert.That(Get(second, "IsSpawned"), Is.True);
        Set(second, "timer", -1f); Call(second, "Update");
        var pokemon = Get(target, "Pokemon");
        Assert.That(pokemon.GetType().GetField("BeforeStatusEffect", Private).GetValue(pokemon), Is.Null);
        Assert.That(pokemon.GetType().GetField("BeforeStatChange", Private).GetValue(pokemon), Is.Null);
        Assert.That(StatusCount(target), Is.Zero);
    }

    [UnityTest]
    public IEnumerator ServerOwnedBarrierAndShieldReplicateTheirPlacementToRemoteClient()
    {
        var root = new GameObject("Racing item observer"); objects.Add(root);
        client = root.AddComponent(GameType("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        var hostTransport = host.GetComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        ushort port = (ushort)Get(Call(hostTransport, "GetLocalEndpoint"), "Port");
        Call(transport, "SetConnectionData", "127.0.0.1", port, "127.0.0.1");
        object config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
        client.GetType().GetField("NetworkConfig").SetValue(client, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        foreach (string name in new[] { "PsyduckBarrier", "PsyduckRaceShield" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Psyduck/" + name + ".prefab");
            Call(client, "AddNetworkPrefab", prefab);
        }
        Assert.That(Call(client, "StartClient"), Is.True);
        float deadline = Time.realtimeSinceStartup + 5;
        while (!(bool)Get(client, "IsConnectedClient") && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Get(client, "IsConnectedClient"), Is.True);

        var target = Racer(new Vector3(9, 100, 7));
        var wall = Effect("PsyduckBarrier", "PsyduckRaceBarrierWall");
        Call(wall, "InitializeRPC", target.transform.position, new Vector3(0, 45, 0), (ulong)Get(target, "NetworkObjectId"));
        var shield = Effect("PsyduckRaceShield", "PsyduckRacingShieldObject");
        Call(shield, "Initialize", (ulong)Get(target, "NetworkObjectId"));
        object spawnManager = Get(client, "SpawnManager");
        var spawned = (IDictionary)spawnManager.GetType().GetField("SpawnedObjects").GetValue(spawnManager);
        ulong wallID = (ulong)Get(wall, "NetworkObjectId"), shieldID = (ulong)Get(shield, "NetworkObjectId");
        deadline = Time.realtimeSinceStartup + 5;
        while ((!spawned.Contains(wallID) || !spawned.Contains(shieldID)) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(spawned.Contains(wallID) && spawned.Contains(shieldID), Is.True);
        var remoteWall = (Component)spawned[wallID]; var remoteShield = (Component)spawned[shieldID];
        deadline = Time.realtimeSinceStartup + 5;
        while ((Vector3.Distance(remoteWall.transform.position, target.transform.position) >= 0.1f
            || Vector3.Distance(remoteShield.transform.position, target.transform.position) >= 0.1f)
            && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Get(wall, "OwnerClientId"), Is.EqualTo(0UL));
        Assert.That(Get(shield, "OwnerClientId"), Is.EqualTo(0UL));
        Assert.That(Vector3.Distance(remoteWall.transform.position, target.transform.position), Is.LessThan(0.1f));
        Assert.That(Vector3.Distance(remoteShield.transform.position, target.transform.position), Is.LessThan(0.1f));
        Assert.That(remoteWall.GetComponent<BoxCollider>().enabled, Is.True);
    }
}
#endif
