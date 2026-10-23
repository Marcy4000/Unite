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

// Exercise the actual NGO RPC/damage pipeline, not a second copy of its formulas.
public abstract class RayquazaServerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private Component server;
    private GameObject prefab;
    protected virtual bool Host => false;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string method, params object[] args)
        => target.GetType().GetMethod(method, args.Select(a => a.GetType()).ToArray()).Invoke(target, args);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private GameObject Node(string name) { var node = new GameObject(name); objects.Add(node); return node; }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Type manager = GameType("Unity.Netcode.NetworkManager");
        if (manager.GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("An external network session is active.");
        var root = Node("Rayquaza isolated server");
        server = root.AddComponent(manager);
        var transport = root.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        object config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
        manager.GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        // Shared minimal fixture: just NetworkObject and Pokemon, no lobby or UI.
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab");
        Assert.That(prefab, Is.Not.Null);
        Call(server, "AddNetworkPrefab", prefab);
#else
        Assert.Ignore("The isolated fixture is Editor-only.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (server != null)
        {
            foreach (var go in objects.ToArray())
            {
                if (go == null) continue;
                var network = go.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
            }
            Call(server, "Shutdown", false);
            yield return null;
        }
        foreach (var go in objects) if (go != null) UnityEngine.Object.Destroy(go);
        foreach (var asset in assets) if (asset != null) UnityEngine.Object.Destroy(asset);
        objects.Clear(); assets.Clear();
        yield return null;
    }

    private Component Pokemon(string name, string team = "Blue", string kind = "Player")
    {
        var go = UnityEngine.Object.Instantiate(prefab); go.name = name; objects.Add(go);
        var pokemon = go.GetComponent(GameType("Pokemon"));
        var stats = ScriptableObject.CreateInstance(GameType("PokemonBase")); assets.Add(stats);
        Set(stats, "maxHp", Enumerable.Repeat(5000, 15).ToArray());
        Set(stats, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(stats, "spAttack", Enumerable.Repeat(100, 15).ToArray());
        Set(stats, "defense", new int[15]); Set(stats, "spDefense", new int[15]);
        Set(pokemon, "baseStats", stats);
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, Enum.Parse(GameType("PokemonType"), kind));
        Call(go.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Spawn", true);
        object hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon);
        hp.GetType().GetProperty("Value").SetValue(hp, 5000);
        // Subscribe the production death cleanup without loading an evolution model.
        var changed = hp.GetType().GetField("OnValueChanged");
        var callback = Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private));
        changed.SetValue(hp, Delegate.Combine((Delegate)changed.GetValue(hp), callback));
        Call(pokemon, "UpdateTeamRPC", Enum.Parse(GameType("Team"), team));
        return pokemon;
    }

    private object Shield(int amount, ushort id = 65000, ushort priority = ushort.MaxValue)
        => Activator.CreateInstance(GameType("ShieldInfo"), amount, id, priority, 0f, false, (ushort)0);

    private void Hit(Component source, Component target, short amount, string kind = "Physical")
    {
        object damage = Activator.CreateInstance(GameType("DamageInfo"), (ulong)Get(source, "NetworkObjectId"), 0f, (short)0, amount,
            Enum.Parse(GameType("DamageType"), kind), Enum.Parse(GameType("DamageProprieties"), "None"));
        Call(target, "TakeDamageRPC", damage);
    }

    [UnityTest]
    public IEnumerator RayquazaIsConsumedBeforeOtherShieldsAndBreakHitInterrupts()
    {
        var source = Pokemon("Enemy", "Orange"); var target = Pokemon("Shielded");
        // Even another shield using the maximum priority must come second.
        Call(target, "AddShieldRPC", Shield(200, 101, ushort.MaxValue));
        Call(target, "AddShieldRPC", Shield(100));
        Hit(source, target, 40);
        object ray = Call(target, "GetShieldWithID", (ushort)65000);
        Assert.That(ray.GetType().GetField("Amount").GetValue(ray), Is.EqualTo(60));
        Assert.That(Get(target, "LastDamageProtectedScoring"), Is.True);
        Hit(source, target, 80);
        Assert.That(Get(target, "HasRayquazaShield"), Is.False);
        Assert.That(Get(target, "ShieldHp"), Is.EqualTo(180));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(5000));
        Assert.That(Get(target, "LastDamageProtectedScoring"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator TrueDamageBypassesRayquazaAndInterruptsEvenWhenTheShieldSurvives()
    {
        var source = Pokemon("Enemy", "Orange"); var target = Pokemon("Shielded");
        Call(target, "AddShieldRPC", Shield(4500));
        Hit(source, target, 100, "True");
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4900));
        Assert.That(Get(target, "ShieldHp"), Is.EqualTo(4500));
        Assert.That(Get(target, "HasRayquazaShield"), Is.True);
        Assert.That(Get(target, "LastDamageProtectedScoring"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator LethalTrueDamageRemovesTheBuffAndHealingDoesNotRestoreIt()
    {
        var source = Pokemon("Enemy", "Orange"); var target = Pokemon("Shielded");
        Call(target, "AddShieldRPC", Shield(4500));
        Hit(source, target, 5000, "True");
        Assert.That(Get(target, "CurrentHp"), Is.Zero);
        Assert.That(Call(target, "HasShieldWithID", (ushort)65000), Is.False);
        Call(target, "HealDamageRPC", 5000);
        Assert.That(Get(target, "HasRayquazaShield"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ExcessDamageSpillsThroughRayquazaAndOtherShieldsIntoHp()
    {
        var source = Pokemon("Enemy", "Orange"); var target = Pokemon("Shielded");
        Call(target, "AddShieldRPC", Shield(50, 101, 1));
        Call(target, "AddShieldRPC", Shield(100));
        Hit(source, target, 200);
        Assert.That(Get(target, "ShieldHp"), Is.Zero);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4950));
        Assert.That(Get(target, "LastDamageProtectedScoring"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ScoringBonusIsDerivedFromTheActualShieldAndNotAnIndependentTimer()
    {
        var target = Pokemon("Shielded");
        // An inactive UI-free PlayerManager view exercises the real GetScoreBuffs
        // without starting its lobby, controls, or owner initialization.
        var view = Node("Scoring view"); view.SetActive(false);
        var player = view.AddComponent(GameType("PlayerManager"));
        Set(player, "pokemon", target);
        object boosts = Activator.CreateInstance(GameType("Unity.Netcode.NetworkList`1").MakeGenericType(GameType("ScoreBoost")));
        Set(player, "goalBuffs", boosts);
        try
        {
            Assert.That(((IList)Call(player, "GetScoreBuffs")).Count, Is.Zero);
            Call(target, "AddShieldRPC", Shield(4500));
            Assert.That(((IList)Call(player, "GetScoreBuffs")).Count, Is.EqualTo(1));
            yield return new WaitForSeconds(0.1f);
            Assert.That(Get(target, "HasRayquazaShield"), Is.True);
            Call(target, "RemoveShieldWithIDRPC", (ushort)65000);
            Assert.That(((IList)Call(player, "GetScoreBuffs")).Count, Is.Zero);
        }
        finally { ((IDisposable)boosts).Dispose(); }
    }

    [UnityTest]
    public IEnumerator ShieldVisualFollowsReplicatedStateAndVisionButIsNotCreatedOnDedicatedServers()
    {
        var target = Pokemon("Shielded");
        var vision = target.gameObject.AddComponent(GameType("Vision"));
        var marker = new GameObject("Visibility marker"); marker.transform.SetParent(target.transform);
        Call(vision, "AddObject", marker);
        Call(vision, "SetVisibility", true);
        var visual = target.gameObject.AddComponent(GameType("RayquazaShieldVisual"));
        Call(target, "AddShieldRPC", Shield(4500));
        yield return null; yield return null;
        var effectField = visual.GetType().GetField("effect", Private);
        var effect = (GameObject)effectField.GetValue(visual);
        if (!Host)
        {
            Assert.That(effect, Is.Null);
            yield break;
        }
        Assert.That(effect, Is.Not.Null);
        Assert.That(effect.activeSelf, Is.True);
        Call(vision, "SetVisibility", false);
        yield return null; yield return null;
        Assert.That(effect.activeSelf, Is.False);
        Call(vision, "SetVisibility", true);
        yield return null; yield return null;
        Assert.That(effect.activeSelf, Is.True);
        Call(target, "RemoveShieldWithIDRPC", (ushort)65000);
        yield return null; yield return null;
        Assert.That(effectField.GetValue(visual), Is.Null);
    }

    [UnityTest]
    public IEnumerator EveryDamageTypeGetsFortyPercentOnlyAgainstEnemyPlayersWhileShielded()
    {
        var source = Pokemon("Buffed");
        Call(source, "AddShieldRPC", Shield(4500));
        foreach (string kind in new[] { "Physical", "Special", "True" })
        {
            var enemy = Pokemon("Enemy " + kind, "Orange");
            Hit(source, enemy, 100, kind);
            Assert.That(Get(enemy, "CurrentHp"), Is.EqualTo(4860), kind);
        }
        var wild = Pokemon("Wild", "Neutral", "Wild"); Hit(source, wild, 100);
        Assert.That(Get(wild, "CurrentHp"), Is.EqualTo(4900));
        var objective = Pokemon("Objective", "Neutral", "Objective"); Hit(source, objective, 100);
        Assert.That(Get(objective, "CurrentHp"), Is.EqualTo(4900));
        var ally = Pokemon("Ally"); Hit(source, ally, 100);
        Assert.That(Get(ally, "CurrentHp"), Is.EqualTo(4900));
        Call(source, "RemoveShieldWithIDRPC", (ushort)65000);
        var unbuffedTarget = Pokemon("Enemy without buff", "Orange"); Hit(source, unbuffedTarget, 100);
        Assert.That(Get(unbuffedTarget, "CurrentHp"), Is.EqualTo(4900));
        yield return null;
    }
}

public class RayquazaDedicatedServerTests : RayquazaServerTests { }

public class RayquazaHostTests : RayquazaServerTests
{
    protected override bool Host => true;
}
