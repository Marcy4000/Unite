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

// Real NGO RPCs with remote clients catch broadcast -> server-request fan-out.
// Reflection keeps the test assembly independent of Assembly-CSharp.
public abstract class DefeatExperienceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<Component> clients = new List<Component>();
    private Component server;
    private GameObject prefab;
    private ScriptableObject stats;
    private ushort port;
    protected abstract bool Host { get; }

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);

    private Component Manager(string name, ushort connectionPort)
    {
        var node = new GameObject(name); objects.Add(node);
        var manager = node.AddComponent(GameType("Unity.Netcode.NetworkManager"));
        var transport = node.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        Call(transport, "SetConnectionData", "127.0.0.1", connectionPort, "127.0.0.1");
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

    private IEnumerator ConnectClient()
    {
        var client = Manager("XP remote client " + clients.Count, port);
        clients.Add(client);
        Assert.That(Call(client, "StartClient"), Is.True);
        yield return Until(() => (bool)Get(client, "IsConnectedClient"), "Remote client did not connect.");
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
        stats = ScriptableObject.CreateInstance(GameType("PokemonBase"));
        Set(stats, "maxHp", Enumerable.Repeat(5000, 22).ToArray());
        Set(stats, "evolutions", Array.CreateInstance(GameType("PokemonEvolution"), 0));
        server = Manager("XP server", 0);
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        var transport = server.GetComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        port = (ushort)Get(Call(transport, "GetLocalEndpoint"), "Port");
        Assert.That(port, Is.Not.Zero);
        yield return ConnectClient();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var client in clients) if (client != null) Call(client, "Shutdown", false);
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
        foreach (var client in clients) if (client != null) UnityEngine.Object.Destroy(client.gameObject);
        yield return null;
        foreach (var node in objects) if (node != null) UnityEngine.Object.Destroy(node);
        if (stats != null) UnityEngine.Object.Destroy(stats);
        objects.Clear(); clients.Clear();
        yield return null;
    }

    private Component Spawn(string kind, ulong owner)
    {
        var node = UnityEngine.Object.Instantiate(prefab); objects.Add(node);
        var pokemon = node.GetComponent(GameType("Pokemon"));
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, Enum.Parse(GameType("PokemonType"), kind));
        Call(node.GetComponent(GameType("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", owner, true);
        Call(pokemon, "SetNewPokemon", stats);
        return pokemon;
    }

    private static Component Remote(Component pokemon, Component client)
    {
        var spawned = (IDictionary)Get(client, "SpawnManager").GetType().GetField("SpawnedObjects").GetValue(Get(client, "SpawnManager"));
        object id = Get(pokemon, "NetworkObjectId");
        return spawned.Contains(id) ? ((Component)spawned[id]).GetComponent(GameType("Pokemon")) : null;
    }

    private static void PlayerView(Component pokemon)
    {
        // A view on the same object lets production proximity lookup find the
        // player, without starting PlayerManager's lobby/input/UI lifecycle.
        pokemon.gameObject.SetActive(false);
        var player = pokemon.gameObject.AddComponent(GameType("PlayerManager"));
        Set(player, "pokemon", pokemon);
    }

    private IEnumerator PreparePlayers(params Component[] players)
    {
        foreach (var player in players)
        {
            Call(player, "UpdateTeamRPC", Enum.Parse(GameType("Team"), "Blue"));
            foreach (var client in clients)
            {
                yield return Until(() => Remote(player, client) != null, "Player did not replicate.");
                Call(Remote(player, client), "SetNewPokemon", stats);
                PlayerView(Remote(player, client));
            }
            PlayerView(player);
        }
    }

    private IEnumerator AssertExperience(Component pokemon, int expected)
    {
        yield return Until(() => (int)Get(pokemon, "CurrentExp") >= expected, "XP award did not reach the server.");
        // Allow any erroneous client echo requests to return before asserting.
        yield return new WaitForSeconds(0.5f);
        Assert.That(Get(pokemon, "CurrentLevel"), Is.Zero, "A duplicate award unexpectedly leveled the Pokemon.");
        Assert.That(Get(pokemon, "CurrentExp"), Is.EqualTo(expected), "A defeat must award XP exactly once.");
        foreach (var client in clients)
        {
            yield return Until(() => (int)Get(Remote(pokemon, client), "CurrentExp") == expected, "Authoritative XP did not replicate.");
            Assert.That(Get(Remote(pokemon, client), "CurrentLevel"), Is.Zero);
        }
    }

    private IEnumerator ServerReward(bool clientOwnedAttacker, bool extraClient)
    {
        if (extraClient) yield return ConnectClient();
        ulong owner = clientOwnedAttacker ? (ulong)Get(clients[0], "LocalClientId") : (ulong)Get(server, "LocalClientId");
        var attacker = Spawn("Player", owner);
        var ally = Spawn("Player", (ulong)Get(server, "LocalClientId"));
        var defeated = Spawn("Wild", (ulong)Get(server, "LocalClientId"));
        yield return PreparePlayers(attacker, ally);
        // Same entry point used by the server-only WildPokemon.Die handler.
        Call(defeated, "GiveExpRpc", (ulong)Get(attacker, "NetworkObjectId"), Vector3.zero, 40f);
        yield return AssertExperience(attacker, 26);
        yield return AssertExperience(ally, 26);
    }

    [UnityTest]
    public IEnumerator RemotePlayerWildKillSplitsXpEquallyOnce() => ServerReward(true, false);

    [UnityTest]
    public IEnumerator ServerPlayerWildKillSplitsXpEquallyOnce() => ServerReward(false, false);

    [UnityTest]
    public IEnumerator ExtraConnectedClientDoesNotMultiplyDefeatXp() => ServerReward(true, true);

    private IEnumerator GroupReward(int playerCount, float baseExp, int expected, bool rangedAttacker = false)
    {
        var players = new Component[playerCount];
        for (int index = 0; index < playerCount; index++)
        {
            players[index] = Spawn("Player", (ulong)Get(server, "LocalClientId"));
        }
        var excludedAlly = Spawn("Player", (ulong)Get(server, "LocalClientId"));
        var opponent = Spawn("Player", (ulong)Get(server, "LocalClientId"));
        var defeated = Spawn("Wild", (ulong)Get(server, "LocalClientId"));
        yield return PreparePlayers(players.Concat(new[] { excludedAlly, opponent }).ToArray());
        excludedAlly.transform.position = new Vector3(20f, 0f, 0f);
        Call(opponent, "UpdateTeamRPC", Enum.Parse(GameType("Team"), "Orange"));
        if (rangedAttacker) players[0].transform.position = new Vector3(20f, 0f, 0f);

        Call(defeated, "GiveExpRpc", (ulong)Get(players[0], "NetworkObjectId"), Vector3.zero, baseExp);

        foreach (var player in players) yield return AssertExperience(player, expected);
        yield return AssertExperience(excludedAlly, 0);
        yield return AssertExperience(opponent, 0);
    }

    [UnityTest]
    public IEnumerator SoloKillAwardsFullXp() => GroupReward(1, 40f, 40);

    [UnityTest]
    public IEnumerator ThreePlayersSplitOneHundredThirtyPercent() => GroupReward(3, 60f, 26);

    [UnityTest]
    public IEnumerator FourPlayersSplitOneHundredThirtyPercent() => GroupReward(4, 40f, 13);

    [UnityTest]
    public IEnumerator FivePlayersSplitOneHundredThirtyPercent() => GroupReward(5, 100f, 26);

    [UnityTest]
    public IEnumerator FractionalSharesRoundDownEqually() => GroupReward(3, 40f, 17);

    [UnityTest]
    public IEnumerator RangedSoloAttackerStillReceivesFullXp() => GroupReward(1, 40f, 40, true);

    [UnityTest]
    public IEnumerator RangedAttackerCountsTowardSharedXp() => GroupReward(2, 40f, 26, true);

    [UnityTest]
    public IEnumerator OwnerRequestedPlayerKoRewardStillReachesTheServerOnce()
    {
        var attacker = Spawn("Player", (ulong)Get(server, "LocalClientId"));
        var defeated = Spawn("Player", (ulong)Get(clients[0], "LocalClientId"));
        yield return PreparePlayers(attacker);
        yield return Until(() => Remote(defeated, clients[0]) != null, "Defeated player did not replicate.");
        // PlayerManager.OnPokemonDeath currently requests the reward on its owner.
        Call(Remote(defeated, clients[0]), "GiveExpRpc", (ulong)Get(attacker, "NetworkObjectId"), Vector3.zero, 40f);
        yield return AssertExperience(attacker, 40);
    }

    [UnityTest]
    public IEnumerator OwnerRequestedPlayerKoSplitsXpEqually()
    {
        var attacker = Spawn("Player", (ulong)Get(server, "LocalClientId"));
        var ally = Spawn("Player", (ulong)Get(server, "LocalClientId"));
        var defeated = Spawn("Player", (ulong)Get(clients[0], "LocalClientId"));
        yield return PreparePlayers(attacker, ally);
        yield return Until(() => Remote(defeated, clients[0]) != null, "Defeated player did not replicate.");
        Call(Remote(defeated, clients[0]), "GiveExpRpc", (ulong)Get(attacker, "NetworkObjectId"), Vector3.zero, 40f);
        yield return AssertExperience(attacker, 26);
        yield return AssertExperience(ally, 26);
    }
}

public class DefeatExperienceHostTests : DefeatExperienceTests
{
    protected override bool Host => true;
}

public class DefeatExperienceDedicatedServerTests : DefeatExperienceTests
{
    protected override bool Host => false;
}
