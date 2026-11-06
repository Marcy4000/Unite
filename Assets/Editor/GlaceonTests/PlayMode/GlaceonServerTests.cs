using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GlaceonServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
    private Component server, actor, player, moves;
    private GameObject fixture;
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static object Get(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);
    private static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(instance, args);
    private static void Variable(object instance, string name, object value) { var field = instance.GetType().GetField(name, Private).GetValue(instance); field.GetType().GetProperty("Value").SetValue(field, value); }
    private static int Hp(Component target) => (int)Get(target, "CurrentHp");
    private object Combat => Get(moves, "GlaceCombat");
    private static bool Has(Component target, string status) => (bool)Call(target, "HasStatusEffect", E("StatusType", status));
    private object Status(string type, float duration = 1, ulong? source = null)
    {
        object value = Activator.CreateInstance(T("StatusEffect"), E("StatusType", type), duration, true, (ushort)123);
        return source.HasValue ? Call(value, "WithSource", source.Value) : value;
    }
    private GameObject Node(string name) { var node = new GameObject(name); objects.Add(node); return node; }
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("External network session active.");
        foreach (string name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        { var map = Node(name); map.tag = name; map.transform.position = Vector3.down * 30; map.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100); }
        var root = Node("Glaceon regression server"); server = root.AddComponent(T("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig")); server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport); config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab"); Call(server, "AddNetworkPrefab", fixture);
        foreach (string name in new[] { "GlaceMoveEffect", "GlaceProjectileEffect", "FreezeDryProjectile", "IcicleSpear", "GlaceonUnite" })
            Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Glaceon/" + name + ".prefab"));
#else
        Assert.Ignore("Editor fixtures required.");
#endif
#if UNITY_EDITOR
        Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Passives/Glaceon/IciclesHolder.prefab"));
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True); yield return null;
        actor = Pokemon(Vector3.zero, true, "Blue"); player = actor.GetComponent(T("PlayerManager")); moves = actor.GetComponent(T("MovesController"));
        var learned = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves);
        learned.SetValue(E("AvailableMoves", "GlaceIcyWind"), 0); learned.SetValue(E("AvailableMoves", "GlaceIceShard"), 1);
        Call(Combat, "Tick");
    }
    private Component Pokemon(Vector3 position, bool glaceon = false, string team = "Orange")
    {
        var root = UnityEngine.Object.Instantiate(fixture); root.transform.position = position; objects.Add(root);
        var pokemon = root.GetComponent(T("Pokemon")); var manager = root.GetComponent(T("PlayerManager")); var controller = root.GetComponent(T("MovesController"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase")); definitions.Add(definition);
        Set(definition, "pokemonName", glaceon ? "Glaceon" : "Test opponent");
        Set(definition, "maxHp", Enumerable.Repeat(20000, 15).ToArray()); Set(definition, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "spAttack", Enumerable.Repeat(100, 15).ToArray()); Set(definition, "defense", new int[15]); Set(definition, "spDefense", new int[15]);
        Set(definition, "critRate", new int[15]); Set(definition, "speed", Enumerable.Repeat(3650, 15).ToArray()); Set(definition, "atkSpeed", Enumerable.Repeat(10f, 15).ToArray());
        Set(definition, "passive", E("AvailablePassives", glaceon ? "GlaceonPassive" : "EmptyPassive")); Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0));
        Set(pokemon, "baseStats", definition); Set(controller, "pokemon", pokemon); Set(controller, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1ul, true);
        Variable(pokemon, "currentHp", 20000); Variable(pokemon, "currentLevel", 3); Call(pokemon, "UpdateTeamRPC", E("Team", team));
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
        return pokemon;
    }
    private Component Effect(string name, Vector3 direction, Component target = null, bool plus = false, bool empowered = false, int blasts = 0)
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Glaceon/" + name + ".prefab")); objects.Add(root);
        root.transform.position = actor.transform.position + Vector3.up * 0.8f;
        var effect = root.GetComponent(T("PrefabMoveEffect"));
        effect.GetType().GetProperty("MoveId").SetValue(effect, E("AvailableMoves", name == "IcicleSpear" ? "GlaceIcycleSpear" : name == "FreezeDryProjectile" ? "GlaceFreezeDry" : "GlaceonUnite"));
        if (effect.GetType().GetProperty("Empowered") != null)
        { effect.GetType().GetProperty("Empowered").SetValue(effect, empowered); effect.GetType().GetProperty("BlastCount").SetValue(effect, blasts); }
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true);
        effect.GetType().GetMethod("Initialize").Invoke(effect, new object[] { player, direction, target, plus }); return effect;
#else
        return null;
#endif
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (server != null)
        {
            foreach (var root in objects.ToArray()) if (root != null)
            { var network = root.GetComponent(T("Unity.Netcode.NetworkObject")); if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true); }
            Call(server, "Shutdown", false); yield return null;
        }
        foreach (var node in objects) if (node != null) UnityEngine.Object.Destroy(node);
        foreach (var definition in definitions) if (definition != null) UnityEngine.Object.Destroy(definition);
        objects.Clear(); definitions.Clear(); yield return null;
    }
    private void Hit(Component target, string shot, bool plus = false, object volley = null)
    {
        Combat.GetType().GetMethod("Hit").Invoke(Combat, new object[] { target, E("GlaceonShot", shot), plus, volley });
    }
    private IEnumerable<object> Stats(Component target)
    {
        var list = Get(target, "StatChanges");
        int count = (int)Get(list, "Count"); var item = list.GetType().GetProperty("Item");
        for (int i = 0; i < count; i++) yield return item.GetValue(list, new object[] { i });
    }
    private bool HasStat(Component target, string stat, int amount) => Stats(target).Any(s =>
        s.GetType().GetField("AffectedStat").GetValue(s).ToString() == stat && Convert.ToInt32(s.GetType().GetField("Amount").GetValue(s)) == amount);
    [UnityTest]
    public IEnumerator BoostedAndShardOnlyGrantCrystalsOnAcceptedHits()
    {
        var target = Pokemon(Vector3.forward * 2);
        Call(target, "AddStatusEffect", Status("Invincible")); Hit(target, "Boosted");
        Assert.That(Get(moves, "GlaceCrystalCount"), Is.Zero);
        Call(target, "ClearStatusEffectsRPC"); Hit(target, "Boosted");
        Assert.That(Get(moves, "GlaceCrystalCount"), Is.EqualTo(2));
        Hit(target, "Shard"); Assert.That(Get(moves, "GlaceCrystalCount"), Is.EqualTo(4));
        Assert.That(HasStat(target, "Speed", 30), Is.True);
        yield return null;
    }
    [UnityTest]
    public IEnumerator SwiftStarsLaunchWithTheBasicBeforeItHits()
    {
        var learned = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves);
        learned.SetValue(E("AvailableMoves", "GlaceSwift"), 0);
        Call(Combat, "Swift");
        var enemy = Pokemon(Vector3.forward * 6);
        Call(Combat, "Basic", enemy);
        float deadline = Time.realtimeSinceStartup + 3;
        Component[] shots;
        do
        {
            yield return null;
            shots = UnityEngine.Object.FindObjectsByType(T("GlaceProjectileEffect"), FindObjectsSortMode.None).Cast<Component>().ToArray();
        } while (!shots.Any(shot => Get(shot, "Shot").ToString() == "Basic") && Time.realtimeSinceStartup < deadline);
        Assert.That(shots.Select(shot => Get(shot, "Shot").ToString()), Does.Contain("Basic"));
        Assert.That(shots.Select(shot => Get(shot, "Shot").ToString()), Does.Contain("Star"));
        Assert.That(Hp(enemy), Is.EqualTo(20000), "Both projectiles must launch before the basic hits.");
        yield return new WaitForSeconds(0.5f);
    }
    [UnityTest]
    public IEnumerator TailWhipAppliesBothDefensesAndMovementDebuffs()
    {
        var target = Pokemon(Vector3.forward * 2); Hit(target, "Tail");
        Assert.That(HasStat(target, "Defense", 30), Is.True);
        Assert.That(HasStat(target, "SpDefense", 30), Is.True);
        Assert.That(HasStat(target, "Speed", 20), Is.True);
        Call(Combat, "TailReady"); Assert.That(Get(moves, "GlaceAttackCharge"), Is.EqualTo(2));
        yield return null;
    }
    [UnityTest]
    public IEnumerator WindSlowsAfterFourHitsAndUpgradeAddsRemainingHpDamage()
    {
        var normal = Pokemon(Vector3.forward * 2); var plus = Pokemon(Vector3.right * 2);
        var volley = Activator.CreateInstance(T("GlaceonWindVolley"));
        for (int i = 0; i < 3; i++) Hit(normal, "Wind", false, volley);
        Assert.That(HasStat(normal, "Speed", 32), Is.False);
        Hit(normal, "Wind", false, volley); Assert.That(HasStat(normal, "Speed", 32), Is.True);
        Hit(normal, "Wind", false, volley); Hit(normal, "Wind", false, volley); Hit(normal, "Wind", false, volley);
        Assert.That(HasStat(normal, "Speed", 48), Is.True);
        int before = Hp(plus); Hit(plus, "Wind", true, Activator.CreateInstance(T("GlaceonWindVolley")));
        Assert.That(before - Hp(plus), Is.GreaterThan(300));
        yield return null;
    }
    [UnityTest]
    public IEnumerator FreezeExplosionIgnoresAlliesDeduplicatesAndLeavesAFreezingZone()
    {
        var enemy = Pokemon(Vector3.forward * 2); var ally = Pokemon(Vector3.forward * 2 + Vector3.right * 0.2f, false, "Blue");
        var duplicate = Node("Second hurtbox"); duplicate.transform.SetParent(enemy.transform, false); duplicate.AddComponent<SphereCollider>().radius = 0.4f;
        Physics.SyncTransforms(); var effect = Effect("FreezeDryProjectile", Vector3.forward);
        yield return new WaitForSeconds(0.3f);
        Assert.That(Hp(enemy), Is.LessThan(20000)); Assert.That(Hp(ally), Is.EqualTo(20000));
        Assert.That(Get(moves, "GlaceCrystalCount"), Is.EqualTo(2));
        Assert.That(HasStat(actor, "SpAttack", 50), Is.True); Assert.That(HasStat(enemy, "Speed", 30), Is.True);
        int after = Hp(enemy); yield return new WaitForSeconds(2.5f);
        Assert.That(Hp(enemy), Is.EqualTo(after), "Zone must not repeat explosion damage.");
        Assert.That(Has(enemy, "Frozen"), Is.True);
        yield return new WaitForSeconds(0.5f); Assert.That(effect == null || !(bool)Get(effect, "IsSpawned"), Is.True);
    }
    [UnityTest]
    public IEnumerator SnowCloakNegatesFirstHindranceAndResetsOnRespawn()
    {
        var target = Pokemon(Vector3.forward * 2);
        Call(actor, "AddStatusEffect", Status("Frozen", 1, (ulong)Get(target, "NetworkObjectId")));
        Assert.That(Has(actor, "Frozen"), Is.False); Assert.That(Has(actor, "Invincible"), Is.True); Assert.That(Has(actor, "Invisible"), Is.True);
        Call(actor, "ClearStatusEffectsRPC"); Call(actor, "AddStatusEffect", Status("Frozen"));
        Assert.That(Has(actor, "Frozen"), Is.True);
        Variable(actor, "currentHp", 0); Call(actor, "ClearStatusEffectsRPC"); Variable(actor, "currentHp", 20000); Call(Combat, "Tick");
        Call(actor, "AddStatusEffect", Status("Frozen")); Assert.That(Has(actor, "Frozen"), Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator SpearReleaseAndDeathRemoveTheSlowAndAllActionLocks()
    {
        Call(Combat, "BeginSpear", Vector3.forward); var effect = Effect("IcicleSpear", Vector3.forward);
        yield return null; Assert.That(HasStat(actor, "Speed", 80), Is.True); Assert.That((int)Get(moves, "ServerActionLocks"), Is.GreaterThan(0));
        Call(Combat, "SetSpearInput", Vector3.forward, true); yield return null;
        Assert.That(HasStat(actor, "Speed", 80), Is.False); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
        Variable(actor, "currentHp", 0); yield return null;
        Assert.That(effect == null || !(bool)Get(effect, "IsSpawned"), Is.True); Assert.That(Get(moves, "GlaceCrystalCount"), Is.Zero);
        Variable(actor, "currentHp", 20000); Call(Combat, "Tick");
        Assert.That(HasStat(actor, "Speed", 80), Is.False); Assert.That(Get(moves, "GlaceAttackMode"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator UniteGrantsShieldAndCrystalsOnlyWhileInsideAndRemovesZoneSpeedOnExit()
    {
        var effect = Effect("GlaceonUnite", Vector3.forward); yield return new WaitForSeconds(0.3f);
        Assert.That((int)Get(actor, "ShieldHp"), Is.EqualTo(4000));
        // The authored stage collider is deliberately queried rather than assuming a radius.
        var volume = effect.GetComponent<Collider>(); actor.transform.position = volume.bounds.center; Physics.SyncTransforms();
        yield return new WaitForSeconds(0.9f); Assert.That(Get(moves, "GlaceCrystalCount"), Is.EqualTo(1));
        Assert.That(HasStat(actor, "Speed", 50), Is.True);
        actor.transform.position = Vector3.right * 30; Physics.SyncTransforms(); yield return null;
        Assert.That(HasStat(actor, "Speed", 50), Is.False);
        yield return new WaitForSeconds(1); Assert.That(Get(moves, "GlaceCrystalCount"), Is.EqualTo(1));
        Call(effect, "Despawn"); Assert.That(HasStat(actor, "Speed", 50), Is.False);
    }
    [UnityTest]
    public IEnumerator WindVolleyReturnsCapturedCrystalsWithoutOverwritingNewGains()
    {
        var enemy = Pokemon(Vector3.forward * 2); Call(Combat, "AddCrystals", 4); Call(Combat, "IcyWind");
        Assert.That(Get(moves, "GlaceAttackMode"), Is.EqualTo(2));
        Call(Combat, "Basic", enemy); yield return new WaitForSeconds(0.2f);
        Assert.That(Get(moves, "GlaceCrystalCount"), Is.Zero);
        Call(Combat, "AddCrystals", 3); yield return new WaitForSeconds(0.6f);
        Assert.That(Get(moves, "GlaceCrystalCount"), Is.EqualTo(7)); Assert.That(Hp(enemy), Is.LessThan(20000));
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator DeathInvalidatesPendingWindReturnsAndProjectiles()
    {
        var enemy = Pokemon(Vector3.forward * 7); Call(Combat, "AddCrystals", 8); Call(Combat, "IcyWind");
        Call(Combat, "Basic", enemy); yield return new WaitForSeconds(0.13f);
        Variable(actor, "currentHp", 0); Variable(actor, "currentHp", 20000); Call(Combat, "Tick");
        yield return new WaitForSeconds(0.8f);
        Assert.That(Get(moves, "GlaceCrystalCount"), Is.Zero); Assert.That(Get(moves, "GlaceAttackMode"), Is.Zero);
        Assert.That(Hp(enemy), Is.EqualTo(20000)); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator HolderCreatedAfterCrystalsReconstructsSnapshotAndClearsOnDeath()
    {
#if UNITY_EDITOR
        Call(Combat, "AddCrystals", 6);
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Passives/Glaceon/IciclesHolder.prefab")); objects.Add(root);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true);
        Call(root.GetComponent(T("IciclesHolder")), "Bind", (ulong)Get(actor, "NetworkObjectId"));
        yield return null;
        Assert.That(root.transform.childCount, Is.EqualTo(Host ? 6 : 0));
        Variable(actor, "currentHp", 0); yield return null; yield return null;
        Assert.That(root.transform.childCount, Is.Zero);
#else
        yield return null;
#endif
    }
}
public class GlaceonHostTests : GlaceonServerTests { protected override bool Host => true; }
