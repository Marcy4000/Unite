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

public class TrevenantServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
    private Component server, actor, player, moves;
    private GameObject fixture;
    private object Combat => Get(moves, "TrevenantCombat");
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static object Get(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);
    private static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(instance, args);
    private static void Variable(object instance, string name, object value) { var field = instance.GetType().GetField(name, Private).GetValue(instance); field.GetType().GetProperty("Value").SetValue(field, value); }
    private static int Hp(Component target) => (int)Get(target, "CurrentHp");
    private double Now => (double)Get(Get(server, "ServerTime"), "Time");
    private GameObject Node(string name) { var root = new GameObject(name); objects.Add(root); return root; }
    private object Damage(Component source, int amount = 1000, bool basic = false) => Activator.CreateInstance(T("DamageInfo"), Get(source, "NetworkObjectId"), 0f, (short)0, (short)amount,
        E("DamageType", "Physical"), E("DamageProprieties", basic ? "IsBasicAttack" : "None"));
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("External network session active.");
        foreach (string name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        { var map = Node(name); map.tag = name; map.transform.position = Vector3.down * (name == "WalkableWithWalls" ? 30 : 40); map.AddComponent<BoxCollider>().size = new Vector3(100, 0.1f, 100); }
        var root = Node("Trevenant regression server"); server = root.AddComponent(T("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig")); server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport); config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab"); Call(server, "AddNetworkPrefab", fixture);
        foreach (string name in new[] { "WillOWisp", "BranchPoke", "WoodHammer", "Curse", "HornLeech", "PainSplit", "PhantomForest" })
            Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Trevenant/Trevenant" + name + ".prefab"));
#else
        Assert.Ignore("Editor fixtures required.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True); yield return null;
        actor = Pokemon(Vector3.zero, true, "Blue"); player = actor.GetComponent(T("PlayerManager")); moves = actor.GetComponent(T("MovesController")); Call(Combat, "Tick");
    }
    private Component Pokemon(Vector3 position, bool trevenant = false, string team = "Orange")
    {
        var root = UnityEngine.Object.Instantiate(fixture); root.transform.position = position; objects.Add(root);
        var pokemon = root.GetComponent(T("Pokemon")); var manager = root.GetComponent(T("PlayerManager")); var controller = root.GetComponent(T("MovesController"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase")); definitions.Add(definition);
        Set(definition, "pokemonName", trevenant ? "Trevenant" : "Test opponent");
        Set(definition, "maxHp", Enumerable.Repeat(20000, 15).ToArray()); Set(definition, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "spAttack", Enumerable.Repeat(100, 15).ToArray()); Set(definition, "defense", new int[15]); Set(definition, "spDefense", new int[15]);
        Set(definition, "critRate", new int[15]); Set(definition, "speed", Enumerable.Repeat(3650, 15).ToArray()); Set(definition, "atkSpeed", Enumerable.Repeat(10f, 15).ToArray());
        Set(definition, "passive", E("AvailablePassives", trevenant ? "TrevenantPassive" : "EmptyPassive")); Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0));
        Set(pokemon, "baseStats", definition); Set(controller, "pokemon", pokemon); Set(controller, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1ul, true);
        Variable(pokemon, "currentHp", 20000); Variable(pokemon, "currentLevel", 4); Call(pokemon, "UpdateTeamRPC", E("Team", team));
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
        return pokemon;
    }
    private Component Effect(string name, Vector3 direction, Component target = null, bool plus = false, Component caster = null)
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Trevenant/Trevenant" + name + ".prefab")); objects.Add(root);
        var effect = root.GetComponent(T("PrefabMoveEffect")); effect.GetType().GetProperty("MoveId").SetValue(effect, E("AvailableMoves", "Trevenant" + name));
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true);
        effect.GetType().GetMethod("Initialize").Invoke(effect, new object[] { caster ?? player, direction, target, plus }); return effect;
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
    [UnityTest]
    public IEnumerator PainSplitRedirectsExactlyOnceAndBreaksOnDistance()
    {
        var enemy = Pokemon(Vector3.forward * 3); Variable(actor, "currentHp", 10000);
        var link = Effect("PainSplit", Vector3.forward, enemy); yield return new WaitForSeconds(0.45f);
        int own = Hp(actor), other = Hp(enemy); Call(actor, "ResolveDamage", Damage(enemy));
        Assert.That(Hp(actor), Is.EqualTo(own - 600)); Assert.That(Hp(enemy), Is.EqualTo(other - 400));
        enemy.transform.position = Vector3.forward * 8; yield return null; yield return null;
        Assert.That(link == null || !(bool)Get(link, "IsSpawned"), Is.True);
    }
    [UnityTest]
    public IEnumerator OpposingTethersCannotReflectRecursively()
    {
        var enemy = Pokemon(Vector3.forward * 3, true); Variable(actor, "currentHp", 10000);
        Effect("PainSplit", Vector3.forward, enemy); Effect("PainSplit", Vector3.back, actor, caster: enemy.GetComponent(T("PlayerManager")));
        yield return new WaitForSeconds(0.45f);
        int own = Hp(actor), other = Hp(enemy); Call(actor, "ResolveDamage", Damage(enemy));
        Assert.That(Hp(actor), Is.EqualTo(own - 600)); Assert.That(Hp(enemy), Is.EqualTo(other - 400));
    }
    [UnityTest]
    public IEnumerator SelfCostCountsOnceAndDoesNotRedirectOrTriggerNaturalCure()
    {
        var enemy = Pokemon(Vector3.forward * 6); Variable(actor, "currentHp", 6000);
        var curse = Effect("Curse", Vector3.forward); yield return new WaitForSeconds(0.6f);
        Effect("PainSplit", Vector3.forward, enemy); yield return new WaitForSeconds(0.45f);
        int own = Hp(actor), other = Hp(enemy); float gauge = (float)Get(curse, "Gauge");
        Call(Combat, "PayCost", 0.02f, true);
        Assert.That(Hp(actor), Is.EqualTo(own - 400)); Assert.That(Hp(enemy), Is.EqualTo(other));
        Assert.That((float)Get(curse, "Gauge"), Is.EqualTo(gauge + 0.05f).Within(0.0001));
        Assert.That(Combat.GetType().GetField("recovering", Private).GetValue(Combat), Is.False);
    }
    [UnityTest]
    public IEnumerator NaturalCureHasEightTicksAndCooldownSurvivesDeath()
    {
        var enemy = Pokemon(Vector3.forward * 3); Variable(actor, "currentHp", 7000);
        Call(actor, "ResolveDamage", Damage(enemy, 100));
        Assert.That(Combat.GetType().GetField("recovering", Private).GetValue(Combat), Is.True);
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Private).GetValue(moves); clocks[0] = clocks[1] = Now + 10;
        Set(Combat, "nextHeal", Now - 5); Set(Combat, "nextReduction", Now - 5); Call(Combat, "Tick");
        Assert.That(Hp(actor), Is.EqualTo(6900 + 8 * 544)); Assert.That(clocks[0], Is.EqualTo(Now + 6).Within(0.05));
        double ready = (double)Get(Combat, "PassiveReadyAt"); Assert.That(ready, Is.EqualTo(Now + 100).Within(0.05));
        Variable(actor, "currentHp", 0); Variable(actor, "currentHp", 20000); Call(Combat, "Tick");
        Assert.That(Get(Combat, "PassiveReadyAt"), Is.EqualTo(ready)); yield return null;
    }
    [UnityTest]
    public IEnumerator BranchPokeGrantsThreeHealingTicksPerDistinctEnemy()
    {
        Pokemon(Vector3.forward * 1.5f); Pokemon(Vector3.forward * 2.5f); Variable(actor, "currentHp", 10000); Physics.SyncTransforms();
        Effect("BranchPoke", Vector3.forward); yield return new WaitForSeconds(0.6f);
        var recoveries = (IList)Combat.GetType().GetField("recoveries", Private).GetValue(Combat);
        Assert.That(recoveries.Count, Is.EqualTo(2));
        foreach (object recovery in recoveries) recovery.GetType().GetField("Next").SetValue(recovery, Now - 3);
        Call(Combat, "Tick"); Assert.That(Hp(actor), Is.EqualTo(13600));
    }
    [UnityTest]
    public IEnumerator ForestAmplificationRequiresItsSpecificShieldAndMarksHavePerAttackerCooldown()
    {
        var enemy = Pokemon(Vector3.forward * 3); var ally = Pokemon(Vector3.right * 2, false, "Blue");
        Variable(actor, "currentHp", 10000); Physics.SyncTransforms();
        Effect("PhantomForest", Vector3.forward * 3); yield return new WaitForSeconds(0.85f);
        Call(actor, "HealDamageRPC", 100); Assert.That(Hp(actor), Is.EqualTo(10150));
        ushort shield = (ushort)Combat.GetType().GetField("shield", Private).GetValue(Combat);
        Call(actor, "RemoveShieldWithIDRPC", shield); Call(actor, "HealDamageRPC", 100); Assert.That(Hp(actor), Is.EqualTo(10250));
        int before = Hp(enemy); Call(enemy, "ResolveDamage", Damage(actor, 100, true)); Assert.That(Hp(enemy), Is.EqualTo(before - 322));
        before = Hp(enemy); Call(enemy, "ResolveDamage", Damage(actor, 100, true)); Assert.That(Hp(enemy), Is.EqualTo(before - 100));
        before = Hp(enemy); Call(enemy, "ResolveDamage", Damage(ally, 100, true)); Assert.That(Hp(enemy), Is.EqualTo(before - 322));
    }
    [UnityTest]
    public IEnumerator HammerFollowupChargesTwiceAndUpgradeReducesOutgoingDamage()
    {
        var enemy = Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var effect = Effect("WoodHammer", Vector3.forward, plus: true); yield return new WaitForSeconds(0.65f);
        Assert.That(Call(effect, "TryRecast", Vector3.forward), Is.True); yield return new WaitForSeconds(0.65f);
        Assert.That(Hp(actor), Is.EqualTo(18050));
        Assert.That(Call(actor, "CalculateDamage", Damage(enemy), enemy), Is.EqualTo(800));
    }
    [UnityTest]
    public IEnumerator ThreeWispsUseFullThenHalfDamageAndDeduplicateColliders()
    {
        var enemy = Pokemon(Vector3.forward * 3);
        var body = enemy.gameObject.AddComponent<BoxCollider>(); body.center = Vector3.up; body.size = new Vector3(4, 2, 1);
        Physics.SyncTransforms(); Effect("WillOWisp", Vector3.forward); yield return new WaitForSeconds(0.9f);
        Assert.That(Hp(enemy), Is.EqualTo(19424));
        Assert.That(Call(enemy, "HasStatusEffect", E("StatusType", "Burned")), Is.True);
    }
    [UnityTest]
    public IEnumerator WispsPierceEnemiesAndHitEachEnemyOnlyOncePerFlame()
    {
        var front = Pokemon(Vector3.forward * 2);
        var rear = Pokemon(Vector3.forward * 4);
        foreach (var enemy in new[] { front, rear })
        {
            var body = enemy.gameObject.AddComponent<BoxCollider>();
            body.center = Vector3.up; body.size = new Vector3(8, 2, 1);
        }
        Physics.SyncTransforms(); Effect("WillOWisp", Vector3.forward);
        yield return new WaitForSeconds(0.9f);
        foreach (var enemy in new[] { front, rear })
        {
            Assert.That(Hp(enemy), Is.EqualTo(19424), "All three flames pierce and deal full, half, then half damage once each.");
            Assert.That(Call(enemy, "HasStatusEffect", E("StatusType", "Burned")), Is.True);
        }
    }
    [UnityTest]
    public IEnumerator WispFanHitsThreeSeparateLanesAndLeavesTheGapsClear()
    {
        var enemies = new Component[3];
        for (int i = 0; i < 3; i++)
        {
            var point = (Vector3)CallRule("WispPosition", Vector3.zero, Vector3.forward, i, 4.5f); point.y = 0;
            enemies[i] = Pokemon(point);
        }
        var gap = Pokemon(new Vector3(1.05f, 0, 4.5f));
        Physics.SyncTransforms();
        var effect = Effect("WillOWisp", Vector3.forward);
        yield return new WaitForSeconds(0.4f);
        var flames = (Transform[])effect.GetType().GetField("flames", Private).GetValue(effect);
        Assert.That(flames[0].position.x, Is.LessThan(-0.6f));
        Assert.That(flames[1].position.x, Is.EqualTo(0).Within(0.01f));
        Assert.That(flames[2].position.x, Is.GreaterThan(0.6f));
        Assert.That(Vector3.Angle(flames[0].forward, flames[1].forward), Is.EqualTo(25).Within(0.1f));
        yield return new WaitForSeconds(0.5f);
        foreach (var enemy in enemies) Assert.That(Hp(enemy), Is.EqualTo(19712), "Each lane gets one full-damage flame.");
        Assert.That(Hp(gap), Is.EqualTo(20000));
    }
    private static object CallRule(string method, params object[] args) => T("TrevenantMoveRules").GetMethod(method).Invoke(null, args);

    [UnityTest]
    public IEnumerator HammerHitsHaveEqualWindupAndTotalActionLockDuration()
    {
        var enemy = Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        float began = Time.time;
        var effect = Effect("WoodHammer", Vector3.forward);
        while (Hp(enemy) == 20000 && Time.time - began < 2) yield return null;
        float firstWindup = Time.time - began;
        Assert.That(firstWindup, Is.EqualTo(0.35f).Within(0.08f));
        while ((float)Get(moves, "TrevenantHammerRemaining") <= 0 && Time.time - began < 2) yield return null;
        float firstDuration = Time.time - began;
        Assert.That(firstDuration, Is.EqualTo(0.55f).Within(0.1f));
        int hp = Hp(enemy); began = Time.time;
        Assert.That(Call(effect, "TryRecast", Vector3.forward), Is.True);
        Assert.That(Get(moves, "TrevenantHammerRemaining"), Is.EqualTo(0f));
        while (Hp(enemy) == hp && Time.time - began < 2) yield return null;
        float secondWindup = Time.time - began;
        Assert.That(secondWindup, Is.EqualTo(firstWindup).Within(0.06f));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Not.Zero);
        while (effect != null && (bool)Get(effect, "IsSpawned") && Time.time - began < 2) yield return null;
        Assert.That(Time.time - began, Is.EqualTo(firstDuration).Within(0.08f));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator DeathRemovesTetherCurseAndAllActionLocks()
    {
        var enemy = Pokemon(Vector3.forward * 3);
        var curse = Effect("Curse", Vector3.forward); yield return new WaitForSeconds(0.6f);
        var link = Effect("PainSplit", Vector3.forward, enemy); yield return new WaitForSeconds(0.45f);
        Variable(actor, "currentHp", 0); yield return null;
        Assert.That(curse == null || !(bool)Get(curse, "IsSpawned"), Is.True);
        Assert.That(link == null || !(bool)Get(link, "IsSpawned"), Is.True);
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator BranchCrossesPhysicalWallsButHornLeechStops()
    {
        var wall = Node("Dash wall"); wall.transform.position = new Vector3(0, 1, 2); wall.AddComponent<BoxCollider>().size = new Vector3(8, 3, 0.5f);
        Physics.SyncTransforms(); Effect("BranchPoke", Vector3.forward); yield return new WaitForSeconds(0.65f);
        Assert.That(actor.transform.position.z, Is.GreaterThan(3));
        actor.transform.position = Vector3.zero; Physics.SyncTransforms();
        Effect("HornLeech", Vector3.forward); yield return new WaitForSeconds(1.5f);
        Assert.That(actor.transform.position.z, Is.LessThan(2)); Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator HornLeechHealsPerEnemyAndReadiesThreeAttacks()
    {
        var first = Pokemon(Vector3.forward * 2); var second = Pokemon(Vector3.forward * 4);
        Variable(actor, "currentHp", 10000); Physics.SyncTransforms(); Effect("HornLeech", Vector3.forward, plus: true);
        yield return new WaitForSeconds(1.5f);
        Assert.That(Hp(first), Is.LessThan(20000)); Assert.That(Hp(second), Is.LessThan(20000));
        var recoveries = (IList)Combat.GetType().GetField("recoveries", Private).GetValue(Combat); Assert.That(recoveries.Count, Is.EqualTo(2));
        foreach (object recovery in recoveries) recovery.GetType().GetField("Next").SetValue(recovery, Now - 3);
        Call(Combat, "Tick"); Assert.That(Hp(actor), Is.EqualTo(17200)); Assert.That(Get(Combat, "HornAttacks"), Is.EqualTo(3));
        Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "Unstoppable")), Is.False);
    }
    [UnityTest]
    public IEnumerator BoostedHealingScalesPerVictimButHornChargeIsSpentOncePerAttack()
    {
        var first = Pokemon(new Vector3(-0.4f, 0, 1.5f)); var second = Pokemon(new Vector3(0.4f, 0, 1.5f));
        Variable(actor, "currentHp", 10000); Combat.GetType().GetProperty("Charge").SetValue(Combat, 2); Call(Combat, "EmpowerHorn");
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Private).GetValue(moves); clocks[1] = Now + 10;
        Physics.SyncTransforms(); Call(Get(moves, "ServerBasicAttack"), "PerformOnServer", first); yield return new WaitForSeconds(0.6f);
        Assert.That(Hp(actor), Is.EqualTo(12000)); Assert.That(Get(Combat, "HornAttacks"), Is.EqualTo(2));
        Assert.That(Hp(first), Is.EqualTo(19741)); Assert.That(Hp(second), Is.EqualTo(19741));
    }
    [UnityTest]
    public IEnumerator FullCurseDetonationStunsAndResetsOtherSlot()
    {
        var effect = Effect("Curse", Vector3.forward); yield return new WaitForSeconds(0.6f);
        for (int i = 0; i < 20; i++) Call(effect, "RecordDamageEvent");
        var enemy = Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Private).GetValue(moves); clocks[1] = Now + 10;
        Assert.That(Call(effect, "TryRecast", Vector3.forward), Is.True); yield return new WaitForSeconds(0.1f);
        Assert.That(clocks[1], Is.LessThanOrEqualTo(Now)); Assert.That(Call(enemy, "HasStatusEffect", E("StatusType", "Incapacitated")), Is.True);
        Assert.That(Hp(enemy), Is.EqualTo(19672));
    }
    [UnityTest]
    public IEnumerator ResetPainSplitAllowsReplacementWithoutOrphaningOldTether()
    {
        var first = Pokemon(Vector3.forward * 3); var second = Pokemon(Vector3.right * 3);
        var old = Effect("PainSplit", Vector3.forward, first); yield return new WaitForSeconds(0.45f);
        var active = (Array)moves.GetType().GetField("activePrefabMoves", Private).GetValue(moves); active.SetValue(old, 1);
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Private).GetValue(moves); clocks[1] = double.PositiveInfinity;
        Call(moves, "ResetTrevenantSecondMove"); Assert.That(double.IsInfinity(clocks[1]), Is.False); Assert.That(active.GetValue(1), Is.Null);
        Assert.That(Get(old, "LinkValid"), Is.True);
        var replacement = Effect("PainSplit", Vector3.right, second); yield return new WaitForSeconds(0.45f);
        Assert.That(old == null || !(bool)Get(old, "IsSpawned"), Is.True); Assert.That(Get(replacement, "LinkValid"), Is.True);
    }
    [UnityTest]
    public IEnumerator ActualDispatcherStartsCooldownOnlyAfterCurseFinishes()
    {
#if UNITY_EDITOR
        Call(actor.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", 0ul);
        var statuses = Array.CreateInstance(T("BattleActionStatus"), 2);
        for (int i = 0; i < 2; i++) statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), i);
        Set(moves, "moveStatuses", statuses);
        var localMoves = Array.CreateInstance(T("MoveBase"), 2);
        var localCurse = Activator.CreateInstance(T("TrevenantCurse"));
        T("MoveBase").GetField("playerManager").SetValue(localCurse, player);
        T("TrevenantMove").GetField("request", Private).SetValue(localCurse, 1);
        localMoves.SetValue(localCurse, 0); localMoves.SetValue(Activator.CreateInstance(T("LockedMove")), 1); Set(moves, "moves", localMoves);
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Trevenant.asset");
        var learnset = T("PokemonBase").GetField("learnableMoves", Private).GetValue(asset);
        Set(Get(actor, "BaseStats"), "learnableMoves", learnset); Variable(actor, "currentLevel", 14);
        var learned = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves); var id = E("AvailableMoves", "TrevenantCurse"); learned.SetValue(id, 0);
        Call(moves, "SpawnMoveEffectRPC", id, Vector3.forward, 0ul, 1);
        var active = (Array)moves.GetType().GetField("activePrefabMoves", Private).GetValue(moves);
        float deadline = Time.realtimeSinceStartup + 5;
        while (active.GetValue(0) == null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(active.GetValue(0), Is.Not.Null);
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Private).GetValue(moves); Assert.That(double.IsPositiveInfinity(clocks[0]), Is.True);
        yield return new WaitForSeconds(0.6f);
        Call(moves, "SpawnMoveEffectRPC", id, Vector3.forward, 0ul, 1); yield return new WaitForSeconds(0.4f);
        Assert.That(active.GetValue(0), Is.Null); Assert.That(clocks[0] - Now, Is.InRange(5.5, 6.1));
#else
        yield break;
#endif
    }
    [UnityTest]
    public IEnumerator CurseSecondaryRingCountsDownAndClearsOnDetonationAndDeath()
        => SecondaryRingCountsDownAndClearsOnRecastAndDeath("Curse", "TrevenantCurseRemaining");
    [UnityTest]
    public IEnumerator HammerSecondaryRingCountsDownAndClearsOnRecastAndDeath()
        => SecondaryRingCountsDownAndClearsOnRecastAndDeath("WoodHammer", "TrevenantHammerRemaining");
    private IEnumerator SecondaryRingCountsDownAndClearsOnRecastAndDeath(string move, string remaining)
    {
#if UNITY_EDITOR
        Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var effect = Effect(move, Vector3.forward); yield return new WaitForSeconds(0.6f);
        Assert.That((float)Get(moves, remaining), Is.InRange(4.7f, 6f));
        Call(actor.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", 0ul);
        var statuses = Array.CreateInstance(T("BattleActionStatus"), 2);
        for (int i = 0; i < 2; i++) statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), i);
        Set(moves, "moveStatuses", statuses);
        var uiRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MoveUI.prefab")); objects.Add(uiRoot);
        var localMoves = Array.CreateInstance(T("MoveBase"), 2);
        for (int i = 0; i < 2; i++) localMoves.SetValue(Activator.CreateInstance(T("LockedMove")), i);
        Set(moves, "moves", localMoves);
        var moveUI = uiRoot.GetComponent(T("MoveUI"));
        var secondary = (Image)moveUI.GetType().GetField("secondaryCd", Private).GetValue(moveUI);
        var holderRoot = Node("Test move holder"); holderRoot.SetActive(false); var holder = holderRoot.AddComponent(T("MovesHolderUI"));
        var panels = Array.CreateInstance(T("MoveUI"), 2); panels.SetValue(moveUI, 0); panels.SetValue(moveUI, 1); Set(holder, "moveUIs", panels);
        // The same owner action locks also refresh the Unite slot. Wire the
        // otherwise-unused slot so this isolated HUD matches the real HUD.
        var uniteRoot = Node("Test Unite slot"); uniteRoot.SetActive(false); var uniteUI = uniteRoot.AddComponent(T("UniteMoveUI"));
        Set(uniteUI, "lockIcon", Node("Unite lock")); Set(uniteUI, "disabledLock", Node("Unite disabled lock"));
        Set(uniteUI, "bg", Node("Unite background").AddComponent<Image>());
        Set(uniteUI, "cdText", moveUI.GetType().GetField("cdText", Private).GetValue(moveUI));
        Set(holder, "uniteMoveUI", uniteUI);
        var hudRoot = Node("Test duration HUD"); hudRoot.SetActive(false); var hud = hudRoot.AddComponent(T("BattleUIManager")); Set(hud, "currentUI", holder);
        var singleton = T("BattleUIManager").GetField("instance"); var original = singleton.GetValue(null); singleton.SetValue(null, hud);
        try
        {
            moves.GetType().GetMethod("UpdateTrevenantSecondaryDuration", Private).Invoke(moves, null);
            Assert.That(secondary.gameObject.activeSelf, Is.True); float initial = secondary.fillAmount;
            yield return new WaitForSeconds(0.15f); Assert.That(secondary.fillAmount, Is.LessThan(initial));
            Assert.That(Call(effect, "TryRecast", Vector3.forward), Is.True); yield return null;
            Assert.That(Get(moves, remaining), Is.EqualTo(0f)); Assert.That(secondary.gameObject.activeSelf, Is.False);
            yield return new WaitForSeconds(0.65f);
            Effect(move, Vector3.forward); yield return new WaitForSeconds(0.6f);
            Assert.That(secondary.gameObject.activeSelf, Is.True);
            Variable(actor, "currentHp", 0); yield return null;
            Assert.That(Get(moves, remaining), Is.EqualTo(0f)); Assert.That(secondary.gameObject.activeSelf, Is.False);
        }
        finally { singleton.SetValue(null, original); }
#else
        yield break;
#endif
    }
    [UnityTest]
    public IEnumerator HammerWindowClearsOnExpiryAndCleanupButNeverOpensOnMiss()
    {
        var window = T("TrevenantMoveRules").GetField("HammerRecastWindow"); var original = window.GetValue(null);
        try
        {
            window.SetValue(null, 0.25f);
            var missed = Effect("WoodHammer", Vector3.forward); yield return new WaitForSeconds(0.65f);
            Assert.That(Get(moves, "TrevenantHammerRemaining"), Is.EqualTo(0f));
            Assert.That(missed == null || !(bool)Get(missed, "IsSpawned"), Is.True);
            Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
            var effect = Effect("WoodHammer", Vector3.forward); yield return new WaitForSeconds(0.65f);
            Assert.That((float)Get(moves, "TrevenantHammerRemaining"), Is.GreaterThan(0));
            yield return new WaitForSeconds(0.25f);
            Assert.That(Get(moves, "TrevenantHammerRemaining"), Is.EqualTo(0f));
            Assert.That(effect == null || !(bool)Get(effect, "IsSpawned"), Is.True);
            effect = Effect("WoodHammer", Vector3.forward); yield return new WaitForSeconds(0.65f);
            Assert.That((float)Get(moves, "TrevenantHammerRemaining"), Is.GreaterThan(0));
            Call(effect, "Despawn");
            Assert.That(Get(moves, "TrevenantHammerRemaining"), Is.EqualTo(0f));
        }
        finally { window.SetValue(null, original); }
    }
}

public class TrevenantHostTests : TrevenantServerTests
{
    protected override bool Host => true;
}
