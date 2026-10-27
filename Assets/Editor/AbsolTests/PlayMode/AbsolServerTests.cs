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

// Actual NGO server, damage, shields, interception and effect coroutines. No
// lobby/input dependencies; the actor is owned by an absent remote client.
public class AbsolServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> stats = new List<ScriptableObject>();
    private Component server, actor, player, moves;
    private GameObject fixture;
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(target, args);
    private static void Variable(object target, string name, object value) => target.GetType().GetField(name, Private).GetValue(target).GetType().GetProperty("Value").SetValue(target.GetType().GetField(name, Private).GetValue(target), value);
    private GameObject Node(string name) { var root = new GameObject(name); objects.Add(root); return root; }
    private static int Hp(Component target) => (int)Get(target, "CurrentHp");
    private static float Rule(string name) => Convert.ToSingle(T("AbsolMoveRules").GetField(name).GetRawConstantValue());
    private static object PrivateCall(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static float NightFirstReady => Rule("NightFirstWindup") + Rule("DashDuration") + Rule("NightFirstRecovery") + Rule("NightRecastDelay") + 0.12f;
    private static float NightSecondComplete => Rule("NightSecondWindup") + Rule("DashDuration") + Rule("NightSecondRecovery") + 0.15f;
    private static float BoostedComplete => Rule("BoostedWindup") + Rule("BoostedHitDelay") + Rule("BoostedRecovery") + 0.15f;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("External network session active.");
        foreach (var name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        { var map = Node(name); map.tag = name; map.transform.position = new Vector3(0, name == "WalkableWithWalls" ? -30.5f : -40.5f, 0); map.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100); }
        var root = Node("Absol regression server"); server = root.AddComponent(T("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string)).Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig")); server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport); config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab");
        Call(server, "AddNetworkPrefab", fixture);
        foreach (var name in new[] { "AbsolFeint", "AbsolNightSlash", "AbsolPsychoCut", "AbsolSuckerPunch", "AbsolMidnightSlash" }) Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Absol/" + name + ".prefab"));
#else
        Assert.Ignore("Editor fixtures required.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True); yield return null;
        actor = Pokemon(Vector3.zero, "Blue"); player = actor.GetComponent(T("PlayerManager")); moves = actor.GetComponent(T("MovesController"));
    }

    private Component Pokemon(Vector3 position, string team = "Orange")
    {
        var root = UnityEngine.Object.Instantiate(fixture); root.transform.position = position; objects.Add(root);
        var pokemon = root.GetComponent(T("Pokemon")); var combat = root.GetComponent(T("MovesController")); var manager = root.GetComponent(T("PlayerManager"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase")); stats.Add(definition);
        Set(definition, "pokemonName", "Absol"); Set(definition, "maxHp", Enumerable.Repeat(5000, 15).ToArray()); Set(definition, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "defense", new int[15]); Set(definition, "spDefense", new int[15]); Set(definition, "critRate", new int[15]); Set(definition, "speed", Enumerable.Repeat(3650, 15).ToArray());
        Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0)); Set(pokemon, "baseStats", definition);
        Set(combat, "pokemon", pokemon); Set(combat, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1ul, true);
        Variable(pokemon, "currentHp", 5000); Call(pokemon, "UpdateTeamRPC", E("Team", team));
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); var onChanged = hp.GetType().GetField("OnValueChanged");
        onChanged.SetValue(hp, Delegate.CreateDelegate(onChanged.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
        return pokemon;
    }

    private Component Effect(string name, Component target = null, bool upgraded = false)
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Absol/" + name + ".prefab")); objects.Add(root);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true);
        var effect = root.GetComponent(T("PrefabMoveEffect"));
        effect.GetType().GetMethod("Initialize").Invoke(effect, new object[] { player, Vector3.forward, target, upgraded }); return effect;
#else
        return null;
#endif
    }
    private static object Damage(Component source, int amount)
    {
        var damage = Activator.CreateInstance(T("DamageInfo"), Get(source, "NetworkObjectId"), 0f, (short)0, (short)amount, E("DamageType", "Physical"), E("DamageProprieties", "None")); return damage;
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
        foreach (var root in objects) if (root != null) UnityEngine.Object.Destroy(root);
        foreach (var asset in stats) if (asset != null) UnityEngine.Object.Destroy(asset);
        objects.Clear(); stats.Clear(); yield return null;
    }

    [UnityTest]
    public IEnumerator SynchronousDamageIncludesShieldAbsorptionAndFeintBypassesIt()
    {
        var target = Pokemon(Vector3.forward);
        var shield = Activator.CreateInstance(T("ShieldInfo"), 500, (ushort)77, (ushort)0, 10f, true, (ushort)0); Call(target, "AddShieldRPC", shield);
        Assert.That(Call(target, "ResolveDamage", Damage(actor, 100)), Is.EqualTo(100)); Assert.That(Hp(target), Is.EqualTo(5000)); Assert.That(Get(target, "ShieldHp"), Is.EqualTo(400));
        var feint = T("AbsolMoveRules").GetMethod("DashDamage").Invoke(null, new object[] { Get(actor, "NetworkObjectId"), true, false, 0f });
        Assert.That((int)Call(target, "ResolveDamage", feint), Is.GreaterThan(0)); Assert.That(Hp(target), Is.LessThan(5000)); Assert.That(Get(target, "ShieldHp"), Is.EqualTo(400));
        yield return null;
    }

    [UnityTest]
    public IEnumerator NetworkBasicBlendsInAndOutWithoutDelayingDamageOrRecovery()
    {
#if UNITY_EDITOR
        var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Absol/Absol.prefab"), actor.transform);
        var animator = model.GetComponentInChildren<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var animations = actor.GetComponent(T("AnimationManager"));
        Call(animations, "AssignAnimator", animator); ((Behaviour)animations).enabled = true;
        string idle = (string)T("AbsolMoveRules").GetField("Idle").GetRawConstantValue();
        animator.Play(idle, 0, 0); animator.Update(0);
        var target = Pokemon(Vector3.forward * 1.5f); Physics.SyncTransforms();
        Call(Get(moves, "ServerBasicAttack"), "PerformOnServer", target); animator.Update(0.001f);
        Assert.That(animator.IsInTransition(0), Is.True);
        Assert.That(animator.GetNextAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash("ani_atk1_bat_0359_XYZFixed")));
        yield return new WaitForSeconds(Rule("BasicWindup") * 0.5f);
        Assert.That(Hp(target), Is.EqualTo(5000));
        yield return new WaitForSeconds(Rule("BasicWindup") * 0.5f + 0.04f);
        Assert.That(Hp(target), Is.EqualTo(4900));
        float until = Time.time + 1;
        while ((bool)Get(moves, "IsServerBasicAttackAnimating") && Time.time < until) yield return null;
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
        Assert.That(animator.speed, Is.EqualTo(1));
        animator.Update(0.001f);
        Assert.That(animator.IsInTransition(0), Is.True);
        Assert.That(animator.GetNextAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash(idle)));
#else
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator PsychoCutMarksIgnoreItemsConsumeOnceAndCleanseRemovesSpeed()
    {
        var target = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); var effect = Effect("AbsolPsychoCut", null, true);
        yield return new WaitForSeconds(0.65f);
        Assert.That(Call(actor, "HasStatChangeWithID", (ushort)35904), Is.True);
        var dictionary = (IDictionary)effect.GetType().GetField("marks", Private).GetValue(effect);
        Assert.That(dictionary.Count, Is.EqualTo(1)); object mark = dictionary[target];
        Assert.That(mark.GetType().GetField("remaining").GetValue(mark), Is.EqualTo(3));
        var item = Damage(actor, 10); item.GetType().GetField("proprieties").SetValue(item, E("DamageProprieties", "IsMuscleBand")); Call(target, "ResolveDamage", item);
        Assert.That(mark.GetType().GetField("remaining").GetValue(mark), Is.EqualTo(3));
        Call(target, "ResolveDamage", Damage(actor, 10));
        Assert.That(mark.GetType().GetField("remaining").GetValue(mark), Is.EqualTo(2));
        target.transform.position = Vector3.forward * 30; yield return null;
        Assert.That(Call(actor, "HasStatChangeWithID", (ushort)35904), Is.True, "Marked targets grant speed regardless of distance.");
        Call(target, "RemoveAllStatusEffectsRPC"); yield return null; yield return null;
        Assert.That(Call(actor, "HasStatChangeWithID", (ushort)35904), Is.False);
    }

    [UnityTest]
    public IEnumerator SuckerPunchBlocksFirstHitThenCleansUpEveryInterceptor()
    {
        var target = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); var effect = Effect("AbsolSuckerPunch", target);
        Assert.That(Call(actor, "ResolveDamage", Damage(target, 200)), Is.EqualTo(0)); Assert.That(Hp(actor), Is.EqualTo(5000));
        Assert.That(Call(actor, "ResolveDamage", Damage(target, 100)), Is.EqualTo(100));
        yield return new WaitForSeconds(0.5f);
        Assert.That(Hp(target), Is.LessThan(5000));
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
        foreach (var name in new[] { "BeforeDamage", "BeforeStatChange", "BeforeStatusEffect" }) Assert.That(actor.GetType().GetField(name, Private).GetValue(actor), Is.Null, name);
    }

    [UnityTest]
    public IEnumerator NightSlashRequiresHitAndOffersOnlyOneRecast()
    {
        var target = Pokemon(new Vector3(-1.4f, 0, 2)); Physics.SyncTransforms(); var effect = Effect("AbsolNightSlash");
        yield return new WaitForSeconds(NightFirstReady);
        Assert.That(Hp(target), Is.LessThan(5000)); Assert.That(Call(effect, "TryRecast", Vector3.back), Is.True);
        Assert.That(Call(effect, "TryRecast", Vector3.forward), Is.False);
        yield return new WaitForSeconds(NightSecondComplete); Assert.That((bool)Get(effect, "IsSpawned"), Is.False);
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator MidnightSlashKeepsGameplayRootFixedAndCleansUpAfterDeath()
    {
        var target = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); var effect = Effect("AbsolMidnightSlash");
        Vector3 origin = actor.transform.position;
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.EqualTo(1));
        yield return new WaitForSeconds(0.8f);
        Assert.That(actor.transform.position, Is.EqualTo(origin)); Assert.That(Hp(target), Is.LessThan(5000));
        Variable(actor, "currentHp", 0); yield return null;
        Assert.That((bool)Get(effect, "IsSpawned"), Is.False); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
        Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "Untargetable")), Is.False);
        Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "Invincible")), Is.False);
    }

    [UnityTest]
    public IEnumerator PursuitReplacesBoostedAttackCleavesAndResetsCharge()
    {
        var first = Pokemon(Vector3.forward * 3); var second = Pokemon(new Vector3(0.4f, 0, 3)); Physics.SyncTransforms();
        var attack = Get(moves, "ServerBasicAttack"); Set(attack, "charge", 2);
        Call(attack, "EmpowerPursuit", true); Assert.That(Call(actor, "HasStatChangeWithID", (ushort)35903), Is.True);
        Call(attack, "PerformOnServer", first); yield return new WaitForSeconds(0.5f);
        Assert.That(Hp(first), Is.LessThan(5000)); Assert.That(Hp(second), Is.LessThan(5000));
        Assert.That(Get(attack, "Charge"), Is.EqualTo(0)); Assert.That(Get(attack, "PursuitReady"), Is.False);
        Assert.That(Call(actor, "HasStatChangeWithID", (ushort)35903), Is.False);
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator BoostedAttackDealsIsolationBonusOnlyForOneVictimAndLowersDefense()
    {
        var first = Pokemon(Vector3.forward * 1.5f); Physics.SyncTransforms(); var attack = Get(moves, "ServerBasicAttack"); Set(attack, "charge", 2);
        Call(attack, "PerformOnServer", first); yield return new WaitForSeconds(BoostedComplete); int isolated = 5000 - Hp(first);
        Assert.That(isolated, Is.EqualTo(208)); Assert.That(Call(first, "HasStatChangeWithID", (ushort)35901), Is.True);
        Variable(first, "currentHp", 5000); var second = Pokemon(new Vector3(0.4f, 0, 1.5f)); Physics.SyncTransforms(); Set(attack, "charge", 2);
        Call(attack, "PerformOnServer", first); yield return new WaitForSeconds(BoostedComplete);
        Assert.That(5000 - Hp(first), Is.EqualTo(151)); Assert.That(5000 - Hp(second), Is.EqualTo(151));
    }

    [UnityTest]
    public IEnumerator NightSlashUpgradeHealsFromShieldDamage()
    {
        Pokemon(new Vector3(-1.4f, 0, 2)); var shielded = Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var shield = Activator.CreateInstance(T("ShieldInfo"), 1000, (ushort)78, (ushort)0, 10f, true, (ushort)0); Call(shielded, "AddShieldRPC", shield);
        Variable(actor, "currentHp", 3000); var effect = Effect("AbsolNightSlash", null, true);
        yield return new WaitForSeconds(NightFirstReady); Assert.That(Call(effect, "TryRecast", Vector3.back), Is.True);
        yield return new WaitForSeconds(NightSecondComplete);
        Assert.That(Hp(shielded), Is.EqualTo(5000)); Assert.That((int)Get(shielded, "ShieldHp"), Is.LessThan(1000)); Assert.That(Hp(actor), Is.GreaterThan(3000));
    }

    [UnityTest]
    public IEnumerator SuckerPunchReactsToOffensiveActionBeforeImpactAndCancelsOnTargetKo()
    {
        var target = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); var effect = Effect("AbsolSuckerPunch", target);
        yield return new WaitForSeconds(0.1f); Assert.That(Hp(target), Is.EqualTo(5000));
        Call(target, "NotifyOffensiveAction"); yield return new WaitForSeconds(0.4f);
        Assert.That(Hp(target), Is.LessThan(5000)); Assert.That(effect.GetType().GetField("countered", Private).GetValue(effect), Is.True);
        actor.transform.position = Vector3.zero; target.transform.position = Vector3.forward * 2; Physics.SyncTransforms(); effect = Effect("AbsolSuckerPunch", target);
        Variable(target, "currentHp", 0); yield return null; yield return null;
        Assert.That((bool)Get(effect, "IsSpawned"), Is.False); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator PermanentPassiveModifierIsIdempotentAndSurvivesCleanse()
    {
        var luck = Activator.CreateInstance(T("StatChange"), (short)15, E("Stat", "CritRate"), 0f, false, true, false, (ushort)35900, false);
        Call(actor, "SetPersistentStatRPC", luck, true); Call(actor, "SetPersistentStatRPC", luck, true);
        Assert.That(Call(actor, "GetCritRate"), Is.EqualTo(15)); Call(actor, "RemoveAllStatChangesRPC"); Assert.That(Call(actor, "GetCritRate"), Is.EqualTo(15));
        Call(actor, "SetPersistentStatRPC", luck, false); Assert.That(Call(actor, "GetCritRate"), Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator FeintCrossesPhysicalWallsButCannotLeaveTheMap()
    {
        var wall = Node("Internal wall"); wall.transform.position = new Vector3(0, 1, 2); wall.AddComponent<BoxCollider>().size = new Vector3(5, 2, 0.4f); Physics.SyncTransforms();
        Effect("AbsolFeint"); yield return new WaitForSeconds(0.5f);
        Assert.That(actor.transform.position.z, Is.EqualTo(4).Within(0.15f)); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
        actor.transform.position = Vector3.forward * 49; Physics.SyncTransforms(); Effect("AbsolFeint"); yield return new WaitForSeconds(0.5f);
        Assert.That(actor.transform.position.z, Is.InRange(49, 49.6f));
    }

    [UnityTest]
    public IEnumerator DeathTeleportAndImmediateRespawnCannotBeOverwrittenByOldMovement()
    {
        Vector3 death = new Vector3(0, -50, -50);
        Action teleport = () => actor.transform.position = death;
        var deathEvent = actor.GetType().GetEvent("OnDeath");
        var parameter = System.Linq.Expressions.Expression.Parameter(T("DamageInfo"), "damage");
        var callback = System.Linq.Expressions.Expression.Lambda(deathEvent.EventHandlerType,
            System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(teleport)), parameter).Compile();
        // Subscribe before the cast, reproducing PlayerManager's earlier death
        // listener which may teleport before the effect's cleanup is invoked.
        deathEvent.AddEventHandler(actor, callback);
        try
        {
            var effect = Effect("AbsolFeint"); yield return new WaitForSeconds(0.1f);
            Variable(actor, "currentHp", 0); Assert.That(actor.transform.position, Is.EqualTo(death));
            Variable(actor, "currentHp", 5000); Vector3 respawn = new Vector3(-20, 0, 0); actor.transform.position = respawn;
            yield return new WaitForSeconds(0.5f);
            Assert.That(actor.transform.position, Is.EqualTo(respawn)); Assert.That((bool)Get(effect, "IsSpawned"), Is.False);
            Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
        }
        finally { deathEvent.RemoveEventHandler(actor, callback); }
    }

    private Delegate ObserveDamage(Action<object, object, int> record)
    {
        var changed = actor.GetType().GetEvent("OnDamageApplied");
        var parameters = changed.EventHandlerType.GetMethod("Invoke").GetParameters().Select(p => System.Linq.Expressions.Expression.Parameter(p.ParameterType, p.Name)).ToArray();
        var body = System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(record),
            System.Linq.Expressions.Expression.Convert(parameters[0], typeof(object)), System.Linq.Expressions.Expression.Convert(parameters[1], typeof(object)), parameters[2]);
        var callback = System.Linq.Expressions.Expression.Lambda(changed.EventHandlerType, body, parameters).Compile();
        changed.AddEventHandler(actor, callback); return callback;
    }

    [UnityTest]
    public IEnumerator BoostedIsolationBonusIsASecondDelayedTickAndRechecksContact()
    {
        var target = Pokemon(Vector3.forward * 1.5f); Physics.SyncTransforms();
        var attack = Get(moves, "ServerBasicAttack"); Set(attack, "charge", 2);
        var times = new List<float>(); var amounts = new List<int>();
        var callback = ObserveDamage((victim, damage, amount) => { if (ReferenceEquals(victim, target)) { times.Add(Time.time); amounts.Add(amount); } });
        try
        {
            Call(attack, "PerformOnServer", target); yield return new WaitForSeconds(BoostedComplete);
            Assert.That(amounts, Is.EqualTo(new[] { 151, 57 }));
            Assert.That(times[1] - times[0], Is.GreaterThanOrEqualTo(Rule("BoostedHitDelay") - 0.01f));
            Assert.That(Get(attack, "Charge"), Is.EqualTo(0));
        }
        finally { actor.GetType().GetEvent("OnDamageApplied").RemoveEventHandler(actor, callback); }

        Variable(target, "currentHp", 5000); Set(attack, "charge", 2);
        callback = ObserveDamage((victim, damage, amount) => { if (ReferenceEquals(victim, target)) target.transform.position = Vector3.forward * 40; });
        try
        {
            Call(attack, "PerformOnServer", target); yield return new WaitForSeconds(BoostedComplete);
            Assert.That(Hp(target), Is.EqualTo(4849), "The delayed bonus must not hit a target that left melee range.");
        }
        finally { actor.GetType().GetEvent("OnDamageApplied").RemoveEventHandler(actor, callback); }
    }

    [UnityTest]
    public IEnumerator PsychoCutReleasesItsCastLockBeforeMarksExpire()
    {
        Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); var effect = Effect("AbsolPsychoCut", null, true);
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.EqualTo(1));
        yield return new WaitForSeconds(Rule("PsychoWindup") * 0.5f);
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.EqualTo(1));
        yield return new WaitForSeconds(Rule("PsychoAnimationDuration") + Rule("PsychoRecovery") + 0.15f);
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
        Assert.That((bool)Get(effect, "IsSpawned"), Is.True);
        Assert.That(((IDictionary)effect.GetType().GetField("marks", Private).GetValue(effect)).Count, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator NightSlashSecondDashStaysStillDuringItsWindup()
    {
        Pokemon(new Vector3(-1.4f, 0, 2)); Physics.SyncTransforms(); var effect = Effect("AbsolNightSlash");
        yield return new WaitForSeconds(NightFirstReady);
        Vector3 origin = actor.transform.position;
        Assert.That(Call(effect, "TryRecast", Vector3.back), Is.True);
        yield return new WaitForSeconds(Rule("NightSecondWindup") * 0.5f);
        Assert.That(actor.transform.position, Is.EqualTo(origin)); Assert.That((int)Get(moves, "ServerActionLocks"), Is.EqualTo(1));
        yield return new WaitForSeconds(NightSecondComplete);
        Assert.That(actor.transform.position.z, Is.LessThan(origin.z - 3.5f));
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }

    private void PrepareBufferedMoves()
    {
        var entries = (Array)moves.GetType().GetField("moves", Private).GetValue(moves);
        var statuses = (Array)moves.GetType().GetField("moveStatuses", Private).GetValue(moves);
        for (int i = 0; i < 2; i++) { entries.SetValue(Activator.CreateInstance(T("LockedMove")), i); statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), i); }
        Set(moves, "uniteMove", Activator.CreateInstance(T("LockedMove")));
        Set(moves, "basicAttack", Activator.CreateInstance(T("AbsolBasicAtk")));
    }

    [UnityTest]
    public IEnumerator BufferedTapWaitsForBasicRecoveryWithoutGreyingMoveSlots()
    {
        PrepareBufferedMoves(); var target = Pokemon(Vector3.forward * 1.5f); Physics.SyncTransforms();
        var attack = Get(moves, "ServerBasicAttack"); Call(attack, "PerformOnServer", target);
        yield return new WaitForSeconds(Rule("BasicWindup") + Rule("BasicRecovery") - Rule("BasicInputBuffer") * 0.6f);
        Call(moves, "TryUsingMove", 0); Call(moves, "TryFinishingMove", 0);
        var buffer = moves.GetType().GetField("serverAttackInput", Private).GetValue(moves);
        Assert.That(Get(buffer, "Pending"), Is.True); Assert.That(Get(buffer, "Released"), Is.True);
        var statuses = (Array)moves.GetType().GetField("moveStatuses", Private).GetValue(moves);
        foreach (var status in statuses) Assert.That(Call(status, "HasStatus", E("ActionStatusType", "None")), Is.True);
        PrivateCall(moves, "ProcessServerAttackBufferedInput"); Assert.That(Get(buffer, "Pending"), Is.True);
        float until = Time.time + 1;
        while ((bool)Get(moves, "IsServerBasicAttackAnimating") && Time.time < until) yield return null;
        LogAssert.Expect(LogType.Log, "This move is not learned yet.");
        PrivateCall(moves, "ProcessServerAttackBufferedInput");
        Assert.That(Get(buffer, "Pending"), Is.False); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator BasicInputBufferCannotSurviveDeathOrCancellation()
    {
        PrepareBufferedMoves(); var target = Pokemon(Vector3.forward * 1.5f); Physics.SyncTransforms();
        var attack = Get(moves, "ServerBasicAttack"); Call(attack, "PerformOnServer", target);
        Call(moves, "TryUsingMove", 0); var buffer = moves.GetType().GetField("serverAttackInput", Private).GetValue(moves);
        Assert.That(Get(buffer, "Pending"), Is.True);
        Call(moves, "CancelAllMoves"); Assert.That(Get(buffer, "Pending"), Is.False);
        Call(moves, "TryUsingMove", 1); Assert.That(Get(buffer, "Pending"), Is.True);
        Variable(actor, "currentHp", 0); Variable(actor, "currentHp", 5000); yield return null;
        Assert.That(Get(buffer, "Pending"), Is.False); Assert.That(Get(moves, "IsServerBasicAttackAnimating"), Is.False);
    }

    [UnityTest]
    public IEnumerator UniteFlurrySplitsFiveTicksThenFinaleHitsByDistanceWithoutMovingCaster()
    {
        var first = Pokemon(new Vector3(-0.3f, 0, 1.2f)); var second = Pokemon(new Vector3(0.3f, 0, 1.2f)); var far = Pokemon(Vector3.forward * 7.5f); Physics.SyncTransforms();
        var records = new Dictionary<object, List<int>>();
        Action<object, object, int> record = (target, damage, amount) => { if (!records.ContainsKey(target)) records[target] = new List<int>(); records[target].Add(amount); };
        var changed = actor.GetType().GetEvent("OnDamageApplied"); var signature = changed.EventHandlerType.GetMethod("Invoke").GetParameters();
        var parameters = signature.Select(p => System.Linq.Expressions.Expression.Parameter(p.ParameterType, p.Name)).ToArray();
        var body = System.Linq.Expressions.Expression.Invoke(System.Linq.Expressions.Expression.Constant(record), System.Linq.Expressions.Expression.Convert(parameters[0], typeof(object)), System.Linq.Expressions.Expression.Convert(parameters[1], typeof(object)), parameters[2]);
        var callback = System.Linq.Expressions.Expression.Lambda(changed.EventHandlerType, body, parameters).Compile(); changed.AddEventHandler(actor, callback);
        try
        {
            Vector3 origin = actor.transform.position; Effect("AbsolMidnightSlash"); yield return new WaitForSeconds(3.1f);
            Assert.That(actor.transform.position, Is.EqualTo(origin)); Assert.That(records[first].Count, Is.EqualTo(9)); Assert.That(records[second].Count, Is.EqualTo(9)); Assert.That(records[far].Count, Is.EqualTo(1));
            Assert.That(records[first].Take(5), Is.All.EqualTo(133)); Assert.That(records[first][5], Is.EqualTo(267)); Assert.That(records[first].Skip(6), Is.All.EqualTo(80));
            Assert.That((int)Get(actor, "ShieldHp"), Is.EqualTo(1000)); Assert.That(Call(actor, "HasStatChangeWithID", (ushort)35910), Is.True);
            Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
        }
        finally { changed.RemoveEventHandler(actor, callback); }
    }
}

public class AbsolHostTests : AbsolServerTests { protected override bool Host => true; }
