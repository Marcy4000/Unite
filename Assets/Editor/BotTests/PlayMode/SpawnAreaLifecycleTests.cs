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
using Object = UnityEngine.Object;

public class SpawnAreaLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new();
    private Component server, game, area, player, pokemon;
    private ScriptableObject stats;
    private object previousGame;
    private SphereCollider volume;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Call(object target, string method, params object[] arguments) => target.GetType()
        .GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, arguments.Select(argument => argument.GetType()).ToArray(), null).Invoke(target, arguments);
    private static object Value(string type, string name) => Enum.Parse(GameType(type), name);
    private static void NetworkValue(object target, string field, object value)
    {
        object variable = target.GetType().GetField(field, Private).GetValue(target);
        variable.GetType().GetProperty("Value").SetValue(variable, value);
    }
    private static void Flag(Component target, string property, bool value) => GameType("Unity.Netcode.NetworkBehaviour")
        .GetProperty(property).SetValue(target, value);
    private GameObject Node(string name, bool active = true)
    {
        var node = new GameObject(name);
        node.SetActive(active);
        objects.Add(node);
        return node;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (GameType("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("An external network session is active.");
        previousGame = GameType("GameManager").GetField("Instance").GetValue(null);
        var hostRoot = Node("Spawn area dedicated server");
        server = hostRoot.AddComponent(GameType("Unity.Netcode.NetworkManager"));
        var transport = hostRoot.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        Call(transport, "SetConnectionData", "127.0.0.1", (ushort)0, "127.0.0.1");
        object config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
        server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab");
        Assert.That(prefab, Is.Not.Null);
        Call(server, "AddNetworkPrefab", prefab);
#else
        GameObject prefab = null;
        Assert.Ignore("Requires the Editor-authored Pokemon fixture.");
#endif
        Assert.That(Call(server, "StartServer"), Is.True);
        yield return null;

        game = Node("Spawn area match", false).AddComponent(GameType("GameManager"));
        GameType("GameManager").GetField("Instance").SetValue(null, game);
        NetworkValue(game, "gameState", Value("GameState", "Playing"));
        var pokemonRoot = Object.Instantiate(prefab);
        objects.Add(pokemonRoot);
        pokemon = pokemonRoot.GetComponent(GameType("Pokemon"));
        stats = ScriptableObject.CreateInstance(GameType("PokemonBase"));
        Set(stats, "maxHp", Enumerable.Repeat(1000, 15).ToArray());
        Set(pokemon, "baseStats", stats);
        Call(pokemonRoot.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Spawn", true);
        NetworkValue(pokemon, "currentHp", 100);
        Call(pokemon, "UpdateTeamRPC", Value("Team", "Blue"));

        var playerRoot = Node("Registered bot participant", false);
        playerRoot.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        player = playerRoot.AddComponent(GameType("PlayerManager"));
        Set(player, "pokemon", pokemon);
        Flag(player, "IsSpawned", true);
        NetworkValue(player, "botControlled", true);
        ((IList)Get(game, "Players")).Add(player);

        var areaRoot = Node("Spawn healing volume");
        volume = areaRoot.AddComponent<SphereCollider>();
        volume.isTrigger = true;
        volume.radius = 5;
        areaRoot.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        area = areaRoot.AddComponent(GameType("SpawnArea"));
        ((Behaviour)area).enabled = false;
        Set(area, "team", Value("Team", "Blue"));
        Flag(area, "IsServer", true);
        Call(area, "OnNetworkSpawn");
        Physics.SyncTransforms();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (player != null) Flag(player, "IsSpawned", false);
        foreach (var node in objects.ToArray())
        {
            if (node == null) continue;
            var network = node.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
        }
        if (server != null) Call(server, "Shutdown", false);
        yield return null;
        foreach (var node in objects) if (node != null) Object.Destroy(node);
        if (stats != null) Object.Destroy(stats);
        objects.Clear();
        GameType("GameManager").GetField("Instance").SetValue(null, previousGame);
        yield return null;
    }

    private void Tick()
    {
        Physics.SyncTransforms();
        Set(area, "healTimer", 0f);
        Call(area, "Update");
    }

    [TestCase(true)]
    [TestCase(false)]
    public void RegisteredParticipantHealsWithoutAnyTriggerEnter(bool bot)
    {
        NetworkValue(player, "botControlled", bot);
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(250));
        Assert.That(area.GetType().GetField("healTimer", Private).GetValue(area), Is.EqualTo(0.5f));
        Set(area, "healTimer", 10f);
        Call(area, "Update");
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(250));
    }

    [Test]
    public void LateTeamInitializationStartsHealingWithoutReentry()
    {
        Call(pokemon, "UpdateTeamRPC", Value("Team", "Neutral"));
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(100));
        Call(pokemon, "UpdateTeamRPC", Value("Team", "Blue"));
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(250));
    }

    [Test]
    public void TeleportOutStopsHealingAndRecallBackRestartsIt()
    {
        Tick();
        player.transform.position = Vector3.right * 20;
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(250));
        player.transform.position = Vector3.zero;
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(400));
    }

    [TestCase("Waiting")]
    [TestCase("Starting")]
    [TestCase("Ended")]
    public void DoesNotHealOutsidePlayingState(string state)
    {
        NetworkValue(game, "gameState", Value("GameState", state));
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(100));
    }

    [Test]
    public void EnemiesDeadUninitializedAndDespawnedPlayersAreNotHealed()
    {
        Call(pokemon, "UpdateTeamRPC", Value("Team", "Orange"));
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(100));
        Call(pokemon, "UpdateTeamRPC", Value("Team", "Blue"));
        NetworkValue(player, "playerState", Value("PlayerState", "Dead"));
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(100));
        NetworkValue(player, "playerState", Value("PlayerState", "Alive"));
        Set(pokemon, "baseStats", null);
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(100));
        Set(pokemon, "baseStats", stats);
        Flag(player, "IsSpawned", false);
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(100));
        Flag(player, "IsSpawned", true);
        NetworkValue(pokemon, "currentHp", 0);
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.Zero);
    }

    [Test]
    public void HealingClampsAtMaxHpAndUsesActualVolumeRatherThanBoundingBox()
    {
        player.transform.position = new Vector3(4, 0, 4);
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(100));
        player.transform.position = Vector3.zero;
        NetworkValue(pokemon, "currentHp", 950);
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(1000));
        Tick();
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(1000));
    }

    [Test]
    public void DisabledVolumeClientAndMissingGameManagerDoNotHeal()
    {
        volume.enabled = false;
        Tick();
        volume.enabled = true;
        Flag(area, "IsServer", false);
        Tick();
        Flag(area, "IsServer", true);
        GameType("GameManager").GetField("Instance").SetValue(null, null);
        Assert.DoesNotThrow(Tick);
        Assert.That(Get(pokemon, "CurrentHp"), Is.EqualTo(100));
    }

    [TestCase(true, true, "Blue", true)]
    [TestCase(true, true, "Orange", false)]
    [TestCase(true, false, "Blue", false)]
    [TestCase(false, true, "Blue", false)]
    public void BarrierTraversalRequiresServerControlledAlly(bool authoritative, bool owner, string team, bool expected)
    {
        var controllerRoot = Node("Controlled participant collision");
        var controller = controllerRoot.AddComponent<CharacterController>();
        var movement = controllerRoot.AddComponent(GameType("PlayerMovement"));
        ((Behaviour)movement).enabled = false;
        Set(movement, "characterController", controller);
        Set(player, "playerMovement", movement);
        Flag(player, "IsOwner", owner);
        Flag(area, "IsServer", authoritative);
        Call(pokemon, "UpdateTeamRPC", Value("Team", team));
        var wall = Node("Spawn barrier").AddComponent<BoxCollider>();
        wall.enabled = false;
        Set(area, "wallCollider", wall);
        Call(area, "OnNetworkSpawn");
        Assert.That(wall.enabled, Is.True);
        Call(area, "AllowControlledPlayerTraversal", player);
        Assert.That(Physics.GetIgnoreCollision(controller, wall), Is.EqualTo(expected));
        Set(movement, "characterController", null);
        Assert.DoesNotThrow(() => Call(area, "AllowControlledPlayerTraversal", player));
    }
}
