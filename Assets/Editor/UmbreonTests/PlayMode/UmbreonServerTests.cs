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

public class UmbreonServerTests
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
    private object Combat => Get(moves, "UmbreonCombat");
    private static bool Has(Component target, string name) => (bool)Call(target, "HasStatusEffect", E("StatusType", name));
    private static object Status(string type, ushort id = 123) => Activator.CreateInstance(T("StatusEffect"), E("StatusType", type), 2f, true, id);
    private GameObject Node(string name) { var root = new GameObject(name); objects.Add(root); return root; }
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("External network session active.");
        foreach (var name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        { var root = Node(name); root.tag = name; root.transform.position = Vector3.down * (name == "WalkableWithWalls" ? 30 : 40); root.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100); }
        var network = Node("Umbreon test server"); server = network.AddComponent(T("Unity.Netcode.NetworkManager"));
        var transport = network.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig")); server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport); config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab"); Call(server, "AddNetworkPrefab", fixture);
        foreach (var name in new[] { "UmbreonSwift", "UmbreonFakeTears", "UmbreonMeanLook", "UmbreonFoulPlay", "UmbreonWish", "UmbreonSnarl", "UmbreonMoonlightPrance" })
            Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Umbreon/" + name + ".prefab"));
#else
        Assert.Ignore("Editor fixtures required.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True); yield return null;
        actor = Pokemon(Vector3.zero, true, "Blue"); player = actor.GetComponent(T("PlayerManager")); moves = actor.GetComponent(T("MovesController")); Call(Combat, "Tick");
    }
    private Component Pokemon(Vector3 position, bool umbreon = false, string team = "Orange")
    {
        var root = UnityEngine.Object.Instantiate(fixture); root.transform.position = position; objects.Add(root);
        var p = root.GetComponent(T("Pokemon")); var manager = root.GetComponent(T("PlayerManager")); var controller = root.GetComponent(T("MovesController"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase")); definitions.Add(definition);
        Set(definition, "pokemonName", umbreon ? "Umbreon" : "Opponent");
        foreach (string stat in new[] { "maxHp", "attack", "spAttack", "defense", "spDefense", "critRate", "speed" })
            Set(definition, stat, Enumerable.Repeat(stat == "maxHp" ? 20000 : stat == "attack" || stat == "spAttack" ? 100 : stat == "speed" ? 3650 : 0, 15).ToArray());
        Set(definition, "atkSpeed", Enumerable.Repeat(10f, 15).ToArray());
        Set(definition, "passive", E("AvailablePassives", umbreon ? "UmbreonPassive" : "EmptyPassive")); Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0));
        Set(p, "baseStats", definition); Set(controller, "pokemon", p); Set(controller, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1ul, true);
        Variable(p, "currentHp", 20000); Variable(p, "currentLevel", 3); Call(p, "UpdateTeamRPC", E("Team", team));
        var hp = p.GetType().GetField("currentHp", Private).GetValue(p); var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, p, p.GetType().GetMethod("CurrentHpChanged", Private)));
        return p;
    }
    private Component Effect(string name, Component target = null, bool plus = false)
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Umbreon/" + name + ".prefab")); objects.Add(root);
        root.transform.position = actor.transform.position; var effect = root.GetComponent(T("PrefabMoveEffect")); effect.GetType().GetProperty("MoveId").SetValue(effect, E("AvailableMoves", name));
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true);
        effect.GetType().GetMethod("Initialize").Invoke(effect, new object[] { player, Vector3.forward, target, plus }); return effect;
#else
        return null;
#endif
    }
    private static object Shield(int amount, ushort id, float duration = 10) => Activator.CreateInstance(T("ShieldInfo"), amount, id, (ushort)0, duration, true, (ushort)0);
    private static object Damage(Component source, int amount) => Activator.CreateInstance(T("DamageInfo"), Get(source, "NetworkObjectId"), 0f, (short)0, (short)amount, E("DamageType", "True"), E("DamageProprieties", "None"));
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (server != null)
        {
            foreach (var root in objects.ToArray()) if (root != null)
            { var net = root.GetComponent(T("Unity.Netcode.NetworkObject")); if (net != null && (bool)Get(net, "IsSpawned")) Call(net, "Despawn", true); }
            Call(server, "Shutdown", false); yield return null;
        }
        foreach (var root in objects) if (root != null) UnityEngine.Object.Destroy(root);
        foreach (var definition in definitions) if (definition != null) UnityEngine.Object.Destroy(definition);
        objects.Clear(); definitions.Clear(); yield return null;
    }
    [UnityTest]
    public IEnumerator InnerFocusBlocksSoftConditionsAndThrowsButNeverBound()
    {
        Call(actor, "AddStatusEffect", Status("Burned")); Assert.That(Has(actor, "Burned"), Is.False); Assert.That(Has(actor, "Unstoppable"), Is.True);
        Assert.That(Call(actor, "RejectDisplacement"), Is.True);
        Call(actor, "AddStatusEffect", Status("Bound", 124)); Assert.That(Has(actor, "Bound"), Is.True);
        Call(actor, "ClearStatusEffectsRPC"); Set(Combat, "immuneUntil", 0d);
        Call(actor, "AddStatusEffect", Status("Poisoned")); Assert.That(Has(actor, "Poisoned"), Is.True, "Passive is on cooldown.");
        yield return null;
    }
    [UnityTest]
    public IEnumerator SwiftMarkReadiesRepeatedBoostsWithoutMovingTheBasicAttackRoot()
    {
        var enemy = Pokemon(Vector3.forward * 1.5f); Physics.SyncTransforms(); Effect("UmbreonSwift", enemy);
        yield return new WaitForSeconds(0.8f);
        Assert.That(Has(enemy, "UmbreonSwiftMarked"), Is.True);
        Call(enemy, "ResolveDamage", Damage(actor, 10)); Assert.That(Get(Combat, "Charge"), Is.EqualTo(2));
        Variable(actor, "currentHp", 10000); Vector3 origin = actor.transform.position;
        var attack = Activator.CreateInstance(T("UmbreonBasicAtk")); Call(attack, "InitializeServer", player); Call(attack, "PerformOnServer", enemy);
        yield return new WaitForSeconds(0.5f);
        Assert.That(actor.transform.position, Is.EqualTo(origin)); Assert.That(Hp(actor), Is.EqualTo(11000)); Assert.That(Get(Combat, "Charge"), Is.EqualTo(2));
        Call(attack, "Dispose");
    }
    [UnityTest]
    public IEnumerator MeanLookCanBePreventedButNotCleansedAfterFormation()
    {
        var enemy = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); Effect("UmbreonMeanLook", enemy);
        yield return new WaitForSeconds(0.2f); Call(enemy, "CleanseConditions"); yield return new WaitForSeconds(0.4f);
        Assert.That(Get(Combat, "InMeanZone"), Is.False);
        var zone = Effect("UmbreonMeanLook", enemy); yield return new WaitForSeconds(0.6f);
        Assert.That(Get(zone, "Active"), Is.True); Call(enemy, "CleanseConditions");
        Vector3 outside = Vector3.forward * 10;
        Vector3 constrained = (Vector3)Call(enemy, "ConstrainMovement", outside);
        Assert.That(Vector3.Distance(constrained, Vector3.forward * 2), Is.EqualTo(2).Within(0.01));
        Call(zone, "Despawn"); Assert.That(Call(enemy, "ConstrainMovement", outside), Is.EqualTo(outside));
    }
    [UnityTest]
    public IEnumerator FoulPlayOffersOnlyAHitConfirmedSingleFollowup()
    {
        Assert.That(Get(moves, "UmbreonFoulRemaining"), Is.EqualTo(0));
        var enemy = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); var foul = Effect("UmbreonFoulPlay", plus: true);
        yield return new WaitForSeconds(0.4f);
        Assert.That(Hp(enemy), Is.LessThan(20000)); Assert.That(Get(Combat, "Charge"), Is.EqualTo(2));
        float remaining = (float)Get(moves, "UmbreonFoulRemaining");
        Assert.That(remaining, Is.GreaterThan(5).And.LessThanOrEqualTo(6));
        yield return new WaitForSeconds(0.1f);
        Assert.That((float)Get(moves, "UmbreonFoulRemaining"), Is.LessThan(remaining));
        Assert.That(Call(foul, "TryRecast", Vector3.back), Is.True); Assert.That(Call(foul, "TryRecast", Vector3.back), Is.False);
        Assert.That(Get(moves, "UmbreonFoulRemaining"), Is.EqualTo(0));
        yield return new WaitForSeconds(0.4f); Assert.That(foul == null, Is.True);
        enemy.transform.position = Vector3.right * 30; Physics.SyncTransforms(); var miss = Effect("UmbreonFoulPlay");
        yield return new WaitForSeconds(0.4f); Assert.That(miss == null, Is.True, "A miss has no recast window.");
        Assert.That(Get(moves, "UmbreonFoulRemaining"), Is.EqualTo(0));
    }
    [UnityTest]
    public IEnumerator FoulPlaySecondaryTimerClearsOnExpiryAndInterruption()
    {
        var window = T("UmbreonMoveRules").GetField("RecastWindow"); float original = (float)window.GetValue(null);
        try
        {
            window.SetValue(null, 0.5f);
            var enemy = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); var foul = Effect("UmbreonFoulPlay");
            yield return new WaitForSeconds(0.4f);
            Assert.That((float)Get(moves, "UmbreonFoulRemaining"), Is.GreaterThan(0));
            yield return new WaitForSeconds(0.5f);
            Assert.That(foul == null, Is.True); Assert.That(Get(moves, "UmbreonFoulRemaining"), Is.EqualTo(0));
            enemy.transform.position = actor.transform.position + Vector3.forward * 2; Physics.SyncTransforms();
            foul = Effect("UmbreonFoulPlay"); yield return new WaitForSeconds(0.4f);
            Assert.That((float)Get(moves, "UmbreonFoulRemaining"), Is.GreaterThan(0));
            Call(foul, "Despawn"); Assert.That(Get(moves, "UmbreonFoulRemaining"), Is.EqualTo(0));
        }
        finally { window.SetValue(null, original); }
    }
    [UnityTest]
    public IEnumerator WishTransfersToLowestPercentageAllyAndKeepsItsDeadline()
    {
        var dying = Pokemon(Vector3.forward, team: "Blue"); var low = Pokemon(Vector3.forward * 2, team: "Blue"); var high = Pokemon(Vector3.right, team: "Blue");
        Variable(low, "currentHp", 5000); Variable(high, "currentHp", 12000);
        Effect("UmbreonWish", dying); yield return new WaitForSeconds(0.8f); Call(dying, "ResolveDamage", Damage(actor, 30000));
        yield return new WaitForSeconds(1.3f);
        Assert.That(Hp(low), Is.EqualTo(5954)); Assert.That(Hp(high), Is.EqualTo(12000));
    }
    [UnityTest]
    public IEnumerator CommittedSelfWishSurvivesCasterDeathAndTransfers()
    {
        var ally = Pokemon(Vector3.forward * 2, team: "Blue"); Variable(ally, "currentHp", 5000);
        var enemy = Pokemon(Vector3.right * 3); var wish = Effect("UmbreonWish", actor);
        yield return new WaitForSeconds(0.4f); Call(actor, "ResolveDamage", Damage(enemy, 30000));
        Assert.That(Hp(actor), Is.Zero); Assert.That(wish != null, Is.True);
        yield return new WaitForSeconds(1.7f); Assert.That(Hp(ally), Is.EqualTo(5954));
    }
    [UnityTest]
    public IEnumerator LearningUniteEnablesOutOfCombatHealingAndRespawnResetsPassive()
    {
        var ids = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves);
        ids.SetValue(E("AvailableMoves", "UmbreonMoonlightPrance"), 2);
        Variable(actor, "currentHp", 10000); yield return new WaitForSeconds(1.1f); Call(Combat, "Tick"); Assert.That(Hp(actor), Is.EqualTo(10200));
        Call(actor, "AddStatusEffect", Status("Burned")); Assert.That(Has(actor, "Burned"), Is.False);
        var enemy = Pokemon(Vector3.right * 3); Call(actor, "ResolveDamage", Damage(enemy, 30000));
        Variable(actor, "currentHp", 20000); Call(Combat, "Tick"); Call(actor, "AddStatusEffect", Status("Poisoned"));
        Assert.That(Has(actor, "Poisoned"), Is.False); Assert.That(Has(actor, "Unstoppable"), Is.True);
    }
    [UnityTest]
    public IEnumerator SnarlDealsEightPulsesAndFinalShoutAndUnlocksActions()
    {
        var enemy = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); Effect("UmbreonSnarl");
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.GreaterThan(0));
        yield return new WaitForSeconds(1.8f);
        Assert.That(Hp(enemy), Is.EqualTo(20000 - 8 * 58 - 116)); Assert.That(Has(enemy, "Incapacitated"), Is.True);
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator ShieldTheftNeverDamagesHpAndExcludesSpecialShields()
    {
        var enemy = Pokemon(Vector3.forward * 2); ushort ray = (ushort)T("RayquazaBuff").GetField("ShieldId").GetRawConstantValue();
        ushort vest = (ushort)T("HeldItemRules").GetField("AssaultVestId").GetRawConstantValue();
        Call(enemy, "AddShieldRPC", Shield(1000, 500)); Call(enemy, "AddShieldRPC", Shield(2000, ray)); Call(enemy, "AddShieldRPC", Shield(3000, vest));
        Assert.That(Call(enemy, "ExtractEligibleShields", 0.5f), Is.EqualTo(500)); Assert.That(Hp(enemy), Is.EqualTo(20000)); Assert.That(Get(enemy, "ShieldHp"), Is.EqualTo(5500));
        double now = (double)Get(Get(server, "ServerTime"), "Time"); Call(actor, "AddStolenShield", 200, (ushort)700, now + 8);
        Call(actor, "AddStolenShield", 300, (ushort)700, now + 8);
        var timers = (List<float>)actor.GetType().GetField("shieldTimers", Private).GetValue(actor);
        Assert.That(timers.Last(), Is.EqualTo(8).Within(0.05)); Assert.That(Get(actor, "ShieldHp"), Is.EqualTo(500)); yield return null;
    }
    [UnityTest]
    public IEnumerator UniteInterceptsNewShieldGrantsOnlyDuringItsAura()
    {
        var enemy = Pokemon(Vector3.forward * 3); Physics.SyncTransforms();
        var unite = Effect("UmbreonMoonlightPrance"); yield return new WaitForSeconds(1.1f);
        Call(enemy, "AddShieldRPC", Shield(1000, 500)); Assert.That(Get(enemy, "ShieldHp"), Is.EqualTo(500));
        Assert.That((int)Get(actor, "ShieldHp"), Is.GreaterThanOrEqualTo(2500));
        Call(unite, "Despawn"); Call(enemy, "AddShieldRPC", Shield(1000, 501)); Assert.That(Get(enemy, "ShieldHp"), Is.EqualTo(1500));
    }
}

public class UmbreonHostTests : UmbreonServerTests { protected override bool Host => true; }
