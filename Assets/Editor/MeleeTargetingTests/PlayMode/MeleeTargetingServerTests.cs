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

public abstract class MeleeTargetingServerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private Component server, source, combat, aim;
    private GameObject prefab;
    private bool autoSync;
    protected virtual bool Host => false;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Value(string type, string value) => Enum.Parse(GameType(type), value);
    private static void Variable(object target, string name, object value)
    { var field = target.GetType().GetField(name, Private).GetValue(target); field.GetType().GetProperty("Value").SetValue(field, value); }
    private GameObject Node(string name) { var go = new GameObject(name); objects.Add(go); return go; }
    private static int Hp(Component pokemon) => (int)Get(pokemon, "CurrentHp");
    private static bool Contains(object set, Component pokemon) => ((IEnumerable)set).Cast<object>().Contains(pokemon);
    private static object Targets(string method, params object[] args) => GameType("CombatTargeting").GetMethod(method).Invoke(null, args);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        autoSync = (bool)typeof(Physics).GetProperty("autoSyncTransforms").GetValue(null);
        var manager = GameType("Unity.Netcode.NetworkManager");
        if (manager.GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("An external network session is active.");
        var root = Node("Melee regression server"); server = root.AddComponent(manager);
        var transport = root.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig")); manager.GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport); config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab");
        Assert.That(prefab, Is.Not.Null); Call(server, "AddNetworkPrefab", prefab);
#else
        Assert.Ignore("Requires the Editor fixture.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True); yield return null;
        source = Pokemon("Flareon", Vector3.zero, "Blue", "Player", true);
        Set(aim, "targetMask", (LayerMask)~0); aim.GetType().GetProperty("TeamToIgnore").SetValue(aim, Value("Team", "Blue"));
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        typeof(Physics).GetProperty("autoSyncTransforms").SetValue(null, autoSync);
        if (server != null)
        {
            foreach (GameObject go in objects.ToArray())
            {
                if (go == null) continue; var network = go.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
            }
            Call(server, "Shutdown", false); yield return null;
        }
        foreach (GameObject go in objects) if (go != null) UnityEngine.Object.Destroy(go);
        foreach (ScriptableObject asset in assets) if (asset != null) UnityEngine.Object.Destroy(asset);
        objects.Clear(); assets.Clear(); yield return null;
    }
    private Component Pokemon(string name, Vector3 position, string team = "Orange", string kind = "Player", bool caster = false)
    {
        var go = UnityEngine.Object.Instantiate(prefab); go.name = name; go.transform.position = position; objects.Add(go);
        var pokemon = go.GetComponent(GameType("Pokemon")); ((Behaviour)pokemon).enabled = false;
        var stats = ScriptableObject.CreateInstance(GameType("PokemonBase")); assets.Add(stats);
        Set(stats, "maxHp", Enumerable.Repeat(5000, 15).ToArray()); Set(stats, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(stats, "defense", new int[15]); Set(stats, "spDefense", new int[15]); Set(stats, "critRate", new int[15]);
        if (caster)
        {
            Set(stats, "passive", Value("AvailablePassives", "FlareonPassive"));
            combat = go.AddComponent(GameType("MovesController")); ((Behaviour)combat).enabled = false;
            aim = go.AddComponent(GameType("Aim")); ((Behaviour)aim).enabled = false;
            var view = Node("Inactive player view"); view.SetActive(false);
            var player = view.AddComponent(GameType("PlayerManager")); Set(player, "pokemon", pokemon);
            Set(combat, "pokemon", pokemon); Set(combat, "playerManager", player);
        }
        Set(pokemon, "baseStats", stats); pokemon.GetType().GetProperty("Type").SetValue(pokemon, Value("PokemonType", kind));
        go.tag = kind == "Player" ? "Player" : "WildPokemon";
        var body = go.AddComponent<SphereCollider>(); body.center = Vector3.up * 0.7f; body.radius = 0.5f;
        Call(go.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Spawn", true);
        Variable(pokemon, "currentLevel", 6); Variable(pokemon, "currentHp", 5000); Call(pokemon, "UpdateTeamRPC", Value("Team", team));
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); var changed = hp.GetType().GetField("OnValueChanged");
        var callback = Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private));
        changed.SetValue(hp, Delegate.Combine((Delegate)changed.GetValue(hp), callback));
        if (caster)
        {
            var death = pokemon.GetType().GetEvent("OnDeath");
            death.AddEventHandler(pokemon, Delegate.CreateDelegate(death.EventHandlerType, combat, combat.GetType().GetMethod("OnFlareonDeath", Private)));
        }
        return pokemon;
    }
    private void Vision(Component pokemon)
    {
        var child = new GameObject("Vision sensor"); child.transform.SetParent(pokemon.transform, false);
        ((Behaviour)child.AddComponent(GameType("VisionController"))).enabled = false;
    }
    private IEnumerator Contact(Component primary) => (IEnumerator)combat.GetType().GetMethod("ResolveFlareonBasic", Private)
        .Invoke(combat, new object[] { primary, Vector3.forward, false, 0 });

    [UnityTest]
    public IEnumerator SharedMeleeAimRejectsStalePlayerCollidersAndKeepsNearbyWildTargets()
    {
        typeof(Physics).GetProperty("autoSyncTransforms").SetValue(null, false);
        var enemy = Pokemon("Old player pose", Vector3.forward * 1.5f);
        var wild = Pokemon("Nearby wild", Vector3.forward * 1.5f, "Neutral", "Wild");
        Physics.SyncTransforms(); enemy.transform.position = Vector3.forward * 80;
        Assert.That(Physics.OverlapSphere(Vector3.forward * 1.377f, 1), Does.Contain(enemy.GetComponent<SphereCollider>()));
        Assert.That(Call(aim, "AimInCircle", 2.5f, Value("PokemonType", "Player")), Is.SameAs(wild.gameObject));
        foreach (float range in new[] { 2.25f, 2.5f, 3.25f, 3.5f })
        {
            var targets = (GameObject[])Call(aim, "AimInCircleAtPosition", Vector3.forward * (1.377f * range / 2.5f), range / 2.5f, Value("AimTarget", "NonAlly"), Value("Team", "Blue"), true);
            Assert.That(targets, Has.No.Member(enemy.gameObject)); Assert.That(targets, Has.Member(wild.gameObject));
        }
        yield return null;
    }
    [UnityTest]
    public IEnumerator SharedQueriesExcludeVisionSensorsButKeepTriggerHurtboxesAndDeduplicateThem()
    {
        var enemy = Pokemon("Remote sensor owner", Vector3.forward * 8); Vision(enemy);
        var near = Pokemon("Real hurtbox", Vector3.forward); near.GetComponent<SphereCollider>().isTrigger = true;
        near.gameObject.AddComponent<SphereCollider>().radius = 0.4f; Vision(near); Physics.SyncTransforms();
        var sphere = Targets("SphereTargets", Vector3.up * 0.7f + Vector3.forward, 1f);
        var box = Targets("BoxTargets", Vector3.up * 0.7f + Vector3.forward, Vector3.one, Quaternion.identity);
        Assert.That(Contains(sphere, enemy), Is.False); Assert.That(Contains(box, enemy), Is.False);
        Assert.That(Contains(sphere, near), Is.True); Assert.That(Contains(box, near), Is.True);
        Assert.That(((IEnumerable)sphere).Cast<object>().Count(item => ReferenceEquals(item, near)), Is.EqualTo(1));
        yield return null;
    }
    [UnityTest]
    public IEnumerator FlareonRechecksEveryVictimAtContactIncludingStaleSecondaryBodies()
    {
        typeof(Physics).GetProperty("autoSyncTransforms").SetValue(null, false);
        var primary = Pokemon("Primary", Vector3.forward * 1.5f, "Neutral", "Wild");
        var enemy = Pokemon("Old secondary", Vector3.forward * 1.8f); Physics.SyncTransforms();
        var contact = Contact(primary); Assert.That(contact.MoveNext(), Is.True);
        enemy.transform.position = Vector3.forward * 80;
        Assert.That(contact.MoveNext(), Is.False);
        Assert.That(Hp(primary), Is.EqualTo(4788)); Assert.That(Hp(enemy), Is.EqualTo(5000));
        yield return null;
    }
    [UnityTest]
    public IEnumerator FlareonRejectsAMovedBodyEvenIfItIsStillWithinMeleeRootRange()
    {
        typeof(Physics).GetProperty("autoSyncTransforms").SetValue(null, false);
        var primary = Pokemon("Primary", Vector3.forward * 1.5f, "Neutral", "Wild");
        var enemy = Pokemon("Moved behind the swing", Vector3.forward * 1.8f); Physics.SyncTransforms();
        var contact = Contact(primary); contact.MoveNext(); enemy.transform.position = Vector3.back * 2;
        contact.MoveNext(); Assert.That(Hp(primary), Is.EqualTo(4788)); Assert.That(Hp(enemy), Is.EqualTo(5000));
        yield return null;
    }
    [UnityTest]
    public IEnumerator FlareonStillCleaveHitsEachBodyOnce()
    {
        var primary = Pokemon("Primary", Vector3.forward * 1.5f, "Neutral", "Wild");
        var enemy = Pokemon("Nearby secondary", new Vector3(0.5f, 0, 1.5f)); Vision(enemy);
        enemy.gameObject.AddComponent<SphereCollider>().radius = 0.4f; Physics.SyncTransforms();
        var contact = Contact(primary); contact.MoveNext(); contact.MoveNext();
        Assert.That(Hp(primary), Is.EqualTo(4788)); Assert.That(Hp(enemy), Is.EqualTo(4788));
        yield return null;
    }
    [UnityTest]
    public IEnumerator DeathAndRespawnCannotReviveAPendingFlareonAttack()
    {
        var primary = Pokemon("Primary", Vector3.forward * 1.5f); Physics.SyncTransforms();
        ((MonoBehaviour)combat).StartCoroutine(Contact(primary));
        Variable(source, "currentHp", 0); Variable(source, "currentHp", 5000);
        yield return new WaitForSeconds(0.15f);
        Assert.That(Hp(primary), Is.EqualTo(5000));
    }
}
public class MeleeDedicatedServerTests : MeleeTargetingServerTests { }
public class MeleeHostTests : MeleeTargetingServerTests { protected override bool Host => true; }
