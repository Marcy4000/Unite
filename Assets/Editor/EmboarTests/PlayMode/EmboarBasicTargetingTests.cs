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

// Exercise the real basic RPC, physics query and HP pipeline on both NGO server modes.
// Reuse the minimal Pokemon fixture: no lobby, player input or match UI is started.
public abstract class EmboarBasicTargetingTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private Component server, source, combat;
    private GameObject prefab;
    private bool autoSyncTransforms;
    protected virtual bool Host => false;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method).Invoke(target, args);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static void Variable(object target, string field, object value)
    {
        object variable = target.GetType().GetField(field, Private).GetValue(target);
        variable.GetType().GetProperty("Value").SetValue(variable, value);
    }
    private static int Hp(Component pokemon) => (int)Get(pokemon, "CurrentHp");
    private GameObject Node(string name) { var node = new GameObject(name); objects.Add(node); return node; }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        autoSyncTransforms = (bool)typeof(Physics).GetProperty("autoSyncTransforms").GetValue(null);
        Type managerType = GameType("Unity.Netcode.NetworkManager");
        if (managerType.GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("An external network session is active.");
        var root = Node("Emboar isolated targeting server"); server = root.AddComponent(managerType);
        var transport = root.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        object config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
        managerType.GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab");
        Assert.That(prefab, Is.Not.Null); Call(server, "AddNetworkPrefab", prefab);
#else
        Assert.Ignore("The isolated fixture is Editor-only.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        yield return null;
        source = Pokemon("Emboar", Vector3.zero, "Blue", "Player", true);
        // Bind the production death/reset handlers without enabling the match-driven Update.
        combat.GetType().GetMethod("UpdateEmboarRuntime", Private).Invoke(combat, null);
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        typeof(Physics).GetProperty("autoSyncTransforms").SetValue(null, autoSyncTransforms);
        if (server != null)
        {
            foreach (GameObject go in objects.ToArray())
            {
                if (go == null) continue;
                var network = go.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
            }
            Call(server, "Shutdown", false); yield return null;
        }
        foreach (GameObject go in objects) if (go != null) UnityEngine.Object.Destroy(go);
        foreach (ScriptableObject asset in assets) if (asset != null) UnityEngine.Object.Destroy(asset);
        objects.Clear(); assets.Clear(); yield return null;
    }
    private Component Pokemon(string name, Vector3 position, string team = "Orange", string kind = "Player", bool emboar = false)
    {
        var go = UnityEngine.Object.Instantiate(prefab); go.name = name; go.transform.position = position; objects.Add(go);
        var pokemon = go.GetComponent(GameType("Pokemon")); ((Behaviour)pokemon).enabled = false;
        var stats = ScriptableObject.CreateInstance(GameType("PokemonBase")); assets.Add(stats);
        Set(stats, "maxHp", Enumerable.Repeat(5000, 15).ToArray()); Set(stats, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(stats, "defense", new int[15]); Set(stats, "spDefense", new int[15]); Set(stats, "critRate", new int[15]);
        if (emboar)
        {
            Set(stats, "passive", Enum.Parse(GameType("AvailablePassives"), "EmboarPassive"));
            combat = go.AddComponent(GameType("MovesController")); ((Behaviour)combat).enabled = false;
            var view = Node("Inactive player view"); view.SetActive(false);
            var player = view.AddComponent(GameType("PlayerManager")); Set(player, "pokemon", pokemon);
            Set(combat, "pokemon", pokemon); Set(combat, "playerManager", player);
        }
        Set(pokemon, "baseStats", stats);
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, Enum.Parse(GameType("PokemonType"), kind));
        var body = go.AddComponent<SphereCollider>(); body.center = Vector3.up * 0.7f; body.radius = 0.5f;
        Call(go.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Spawn", true);
        Variable(pokemon, "currentLevel", 6); Variable(pokemon, "currentHp", 5000);
        Call(pokemon, "UpdateTeamRPC", Enum.Parse(GameType("Team"), team));
        object hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon);
        var changed = hp.GetType().GetField("OnValueChanged");
        var callback = Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private));
        changed.SetValue(hp, Delegate.Combine((Delegate)changed.GetValue(hp), callback));
        return pokemon;
    }
    private SphereCollider Vision(Component pokemon)
    {
        var node = new GameObject("VisionController"); node.transform.SetParent(pokemon.transform, false);
        ((Behaviour)node.AddComponent(GameType("VisionController"))).enabled = false;
        return node.GetComponent<SphereCollider>();
    }
    private void Attack(Component primary)
    {
        Set(combat, "emboarNextBasic", 0d);
        Call(combat, "EmboarBasicRPC", (ulong)Get(primary, "NetworkObjectId"), Vector3.forward);
    }

    [UnityTest]
    public IEnumerator LargeVisionTriggerDoesNotMakeADistantPlayerASecondaryBasicTarget()
    {
        var wild = Pokemon("Nearby wild", Vector3.forward * 2, "Neutral", "Wild");
        var enemy = Pokemon("Distant player", Vector3.forward * 8);
        SphereCollider vision = Vision(enemy); Physics.SyncTransforms();
        Assert.That(Physics.OverlapSphere(Vector3.forward * 1.82f + Vector3.up * 0.6f, 1.75f), Does.Contain(vision), "The real vision trigger overlaps the swing, but the enemy body does not.");
        Attack(wild);
        Assert.That(Hp(wild), Is.EqualTo(4810));
        Assert.That(Hp(enemy), Is.EqualTo(5000), "A vision sensor must never receive splash damage for its owner.");
        yield return null;
    }
    [UnityTest]
    public IEnumerator DeathRespawnAndAStaleBodyColliderCannotDamageTheOldOpponent()
    {
        typeof(Physics).GetProperty("autoSyncTransforms").SetValue(null, false);
        var enemy = Pokemon("Initially nearby opponent", Vector3.forward * 2);
        var wild = Pokemon("Wild after respawn", Vector3.forward * 1.5f, "Neutral", "Wild");
        Physics.SyncTransforms(); Attack(enemy); Assert.That(Hp(enemy), Is.LessThan(5000));
        Variable(source, "currentHp", 0); source.transform.position = new Vector3(0, -50, -50);
        Assert.That(combat.GetType().GetField("emboarBound", Private).GetValue(combat), Is.True);
        source.transform.position = Vector3.zero; Variable(source, "currentHp", 5000);
        enemy.transform.position = Vector3.forward * 80;
        // No SyncTransforms/frame boundary: deliberately retain the old broad-phase collider pose.
        Assert.That(Physics.OverlapSphere(Vector3.forward * 1.82f + Vector3.up * 0.6f, 1.75f), Does.Contain(enemy.GetComponent<SphereCollider>()));
        int hp = Hp(enemy);
        Attack(wild); Attack(wild);
        Assert.That(Hp(wild), Is.LessThan(5000));
        Assert.That(Hp(enemy), Is.EqualTo(hp), "Every secondary victim must be range-checked at its current transform, not its stale physics pose.");
        yield return null;
    }
    [UnityTest]
    public IEnumerator VisionCannotMakeABodyBehindTheSwingASecondaryTarget()
    {
        var wild = Pokemon("Nearby wild", Vector3.forward * 2, "Neutral", "Wild");
        var enemy = Pokemon("Player behind Emboar", Vector3.back * 2); Vision(enemy);
        Physics.SyncTransforms(); Attack(wild);
        Assert.That(Hp(wild), Is.EqualTo(4810));
        Assert.That(Hp(enemy), Is.EqualTo(5000), "Being in melee range is not enough: only a body in the forward swing may be cleaved.");
        yield return null;
    }
    [UnityTest]
    public IEnumerator NearbyBodiesStillCleaveExactlyOnceAndPledgeConsumesOneChargePerSwing()
    {
        var wild = Pokemon("Nearby wild", Vector3.forward * 2, "Neutral", "Wild");
        var enemy = Pokemon("Nearby opponent", new Vector3(0.5f, 0, 2)); Vision(enemy);
        enemy.GetComponent<SphereCollider>().isTrigger = true; // Real trigger hurtboxes must still work; only vision sensors are excluded.
        Physics.SyncTransforms(); Call(combat, "ArmEmboarPledge"); Attack(wild);
        Assert.That(Hp(wild), Is.EqualTo(4738)); Assert.That(Hp(enemy), Is.EqualTo(4738));
        Assert.That(Get(combat, "EmboarPledgeHits"), Is.EqualTo(2), "One confirmed cleave is one Pledge swing, not one charge per target/collider.");
        yield return null;
    }
    [UnityTest]
    public IEnumerator StalePrimaryIdOutsideMeleeRangeIsRejected()
    {
        var enemy = Pokemon("Out-of-range old primary", Vector3.forward * 80);
        Attack(enemy);
        Assert.That(Hp(enemy), Is.EqualTo(5000));
        yield return null;
    }
}

public class EmboarBasicDedicatedServerTests : EmboarBasicTargetingTests { }
public class EmboarBasicHostTests : EmboarBasicTargetingTests { protected override bool Host => true; }
