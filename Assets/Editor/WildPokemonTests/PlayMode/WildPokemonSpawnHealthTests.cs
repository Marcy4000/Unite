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

// Gameplay is in Assembly-CSharp, so use reflection just like the other network
// fixtures. Exercise real NGO replication/RPCs with an actual remote client.
public abstract class WildPokemonSpawnHealthTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private Component server;
    private Component client;
    private GameObject prefab;
    protected abstract bool Host { get; }

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static void SetValue(object pokemon, string field, object value)
    {
        object variable = Field(pokemon, field);
        variable.GetType().GetProperty("Value").SetValue(variable, value);
    }
    private GameObject Node(string name)
    {
        var node = new GameObject(name); objects.Add(node); return node;
    }

    private Component Manager(string name, ushort port)
    {
        var root = Node(name);
        var manager = root.AddComponent(GameType("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        Call(transport, "SetConnectionData", "127.0.0.1", port, "127.0.0.1");
        object config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
        manager.GetType().GetField("NetworkConfig").SetValue(manager, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        Call(manager, "AddNetworkPrefab", prefab);
        return manager;
    }

    private static IEnumerator Until(Func<bool> condition, string message)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, message);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (GameType("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("An external network session is active.");
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab");
        Assert.That(prefab, Is.Not.Null);
#else
        Assert.Ignore("The isolated fixture is Editor-only.");
#endif
        server = Manager("Spawn health server", 0);
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        var transport = server.GetComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        ushort port = (ushort)Get(Call(transport, "GetLocalEndpoint"), "Port");
        Assert.That(port, Is.Not.Zero);
        client = Manager("Spawn health remote client", port);
        Assert.That(Call(client, "StartClient"), Is.True);
        yield return Until(() => (bool)Get(client, "IsConnectedClient"), "Remote client did not connect.");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (client != null) Call(client, "Shutdown", false);
        yield return null;
        if (server != null)
        {
            foreach (var node in objects.ToArray())
            {
                if (node == null) continue;
                var network = node.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
            }
            Call(server, "Shutdown", false);
        }
        yield return null;
        // Destroy the remote manager before the singleton server.
        if (client != null) UnityEngine.Object.Destroy(client.gameObject);
        yield return null;
        foreach (var node in objects) if (node != null) UnityEngine.Object.Destroy(node);
        foreach (var asset in assets) if (asset != null) UnityEngine.Object.Destroy(asset);
        objects.Clear(); assets.Clear();
        yield return null;
    }

    private ScriptableObject Stats()
    {
        var stats = ScriptableObject.CreateInstance(GameType("PokemonBase")); assets.Add(stats);
        Set(stats, "maxHp", Enumerable.Range(0, 22).Select(i => 4000 + i * 1000).ToArray());
        Set(stats, "evolutions", Array.CreateInstance(GameType("PokemonEvolution"), 0));
        return stats;
    }

    private Component Spawn(string kind)
    {
        var node = UnityEngine.Object.Instantiate(prefab); objects.Add(node);
        var pokemon = node.GetComponent(GameType("Pokemon"));
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, Enum.Parse(GameType("PokemonType"), kind));
        Call(node.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Spawn", true);
        return pokemon;
    }

    private Component Remote(Component pokemon)
    {
        object spawnManager = Get(client, "SpawnManager");
        var spawned = (IDictionary)spawnManager.GetType().GetField("SpawnedObjects").GetValue(spawnManager);
        object id = Get(pokemon, "NetworkObjectId");
        return spawned.Contains(id) ? ((Component)spawned[id]).GetComponent(GameType("Pokemon")) : null;
    }

    private static void CatchUp(Component pokemon, int levels)
    {
        object stats = Get(pokemon, "BaseStats");
        int start = (int)Get(pokemon, "CurrentLevel");
        int exp = Enumerable.Range(start, levels).Sum(level => (int)Call(stats, "GetExpForNextLevel", level));
        Call(pokemon, "GainExperienceRPC", exp);
    }

    private IEnumerator StaleClientInitialization(string kind)
    {
        var pokemon = Spawn(kind);
        yield return Until(() => Remote(pokemon) != null, "Client did not receive the spawned Pokemon.");
        var remote = Remote(pokemon);
        Assert.That(Get(remote, "IsServer"), Is.False);
        // Delay the local asset callback, then complete server catch-up before its
        // new level can reach the client. This deterministically recreates the race
        // without depending on a particular ping or packet scheduling order.
        yield return new WaitForSeconds(0.2f);
        var stats = Stats();
        Call(pokemon, "SetNewPokemon", stats);
        CatchUp(pokemon, 16);
        Assert.That(Get(pokemon, "CurrentLevel"), Is.EqualTo(16));
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(20000));
        Assert.That(Get(remote, "CurrentLevel"), Is.Zero, "Client must still have stale spawn-level data.");
        Call(remote, "SetNewPokemon", stats);
        yield return Until(() => (int)Get(remote, "CurrentLevel") == 16, "Scaled level did not replicate.");
        yield return new WaitForSeconds(0.3f);
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(20000), "Client setup must not replace scaled HP with 4000 (20%).");
        Assert.That(Get(remote, "CurrentHp"), Is.EqualTo(20000));
    }

    [UnityTest]
    public IEnumerator LateWildSpawnStaysAtFullHealthWithAStaleClient() => StaleClientInitialization("Wild");

    [UnityTest]
    public IEnumerator LateObjectiveSpawnStaysAtFullHealthWithAStaleClient() => StaleClientInitialization("Objective");

    [UnityTest]
    public IEnumerator DelayedClientSetupPreservesReplicatedDamageExperienceShieldsAndScalingState()
    {
        var pokemon = Spawn("Objective");
        var stats = Stats();
        Call(pokemon, "SetNewPokemon", stats);
        CatchUp(pokemon, 16);
        Call(pokemon, "GainExperienceRPC", 37);
        SetValue(pokemon, "storedExp", 123);
        SetValue(pokemon, "currentHp", 10000);
        Call(pokemon, "FlipAtkStatsRPC", true);
        object shield = Activator.CreateInstance(GameType("ShieldInfo"), 1000, (ushort)123, (ushort)0, 0f, false, (ushort)0);
        Call(pokemon, "AddShieldRPC", shield);
        yield return Until(() => Remote(pokemon) != null && (int)Get(Remote(pokemon), "CurrentExp") == 37
            && (int)Get(Remote(pokemon), "StoredExp") == 123 && (int)Get(Remote(pokemon), "ShieldHp") == 1000
            && (bool)Get(Remote(pokemon), "SwitchAtkStat"), "Gameplay state did not replicate before client asset loading.");
        var remote = Remote(pokemon);
        Call(remote, "SetNewPokemon", stats);
        Assert.That(Get(remote, "LocalLevel"), Is.EqualTo(16));
        Assert.That(Get(remote, "LocalExp"), Is.EqualTo(37));
        Assert.That(Get(remote, "LocalStoredExp"), Is.EqualTo(123));
        yield return new WaitForSeconds(0.3f);
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(10000), "Loading on a client must not heal or reset existing damage.");
        Assert.That(Get(pokemon, "CurrentExp"), Is.EqualTo(37));
        Assert.That(Get(pokemon, "ShieldHp"), Is.EqualTo(1000));
        Assert.That(Get(pokemon, "SwitchAtkStat"), Is.True);
    }

    [UnityTest]
    public IEnumerator NormalLevelUpsPreserveMissingHealthAndRefreshHealthAndShieldBars()
    {
        var pokemon = Spawn("Wild");
        Call(pokemon, "SetNewPokemon", Stats());
        SetValue(pokemon, "currentHp", 2000);
        object shield = Activator.CreateInstance(GameType("ShieldInfo"), 1000, (ushort)123, (ushort)0, 0f, false, (ushort)0);
        Call(pokemon, "AddShieldRPC", shield);
        var barNode = Node("Spawn health UI"); barNode.SetActive(false);
        var bar = barNode.AddComponent(GameType("HPBar"));
        var imageNode = Node("Health fill"); imageNode.transform.SetParent(barNode.transform);
        var hp = imageNode.AddComponent(GameType("UnityEngine.UI.Image"));
        var shieldNode = Node("Shield fill"); shieldNode.transform.SetParent(barNode.transform);
        var shieldImage = shieldNode.AddComponent(GameType("UnityEngine.UI.Image"));
        Set(bar, "hpBar", hp); Set(bar, "shieldBar", shieldImage);
        Call(bar, "SetPokemon", pokemon, false);
        Assert.That(Get(hp, "fillAmount"), Is.EqualTo(0.5f).Within(0.001f));
        CatchUp(pokemon, 16);
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(10000));
        Assert.That(Get(hp, "fillAmount"), Is.EqualTo(0.5f).Within(0.001f), "A level change must refresh the new HP denominator.");
        Assert.That(Get(shieldImage, "fillAmount"), Is.EqualTo(0.05f).Within(0.001f));
        yield return null;
    }
}

public class WildPokemonSpawnHealthHostTests : WildPokemonSpawnHealthTests
{
    protected override bool Host => true;
}

public class WildPokemonSpawnHealthDedicatedServerTests : WildPokemonSpawnHealthTests
{
    protected override bool Host => false;
}
