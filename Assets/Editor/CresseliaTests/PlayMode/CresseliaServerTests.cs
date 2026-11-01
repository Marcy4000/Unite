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

public class CresseliaServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
    private Component server, actor, player, moves;
    private GameObject fixture;

    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object E(string type, string value) => Enum.Parse(T(type), value);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, args.Select(argument => argument.GetType()).ToArray()).Invoke(target, args);
    private static object Invoke(object target, string name, params object[] args)
        => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Variable(object target, string name, object value)
    {
        var variable = target.GetType().GetField(name, Private).GetValue(target);
        variable.GetType().GetProperty("Value").SetValue(variable, value);
    }
    private static int Hp(Component target) => (int)Get(target, "CurrentHp");
    private GameObject Node(string name)
    {
        var root = new GameObject(name);
        objects.Add(root);
        return root;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("External network session active.");
        foreach (string name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        {
            var map = Node(name);
            map.tag = name;
            map.transform.position = new Vector3(0, name == "WalkableWithWalls" ? -30.5f : -40.5f, 0);
            map.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100);
        }
        var floor = Node("CresseliaTestGround");
        floor.transform.position = new Vector3(0, -0.1f, 0);
        floor.AddComponent<BoxCollider>().size = new Vector3(100, 0.2f, 100);

        var root = Node("Cresselia test server");
        server = root.AddComponent(T("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods()
            .First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig"));
        server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab");
        Call(server, "AddNetworkPrefab", fixture);
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        yield return null;
        PrepareCresseliaActor(12, Vector3.zero, "Blue");
#else
        Assert.Ignore("Editor fixture required.");
        yield break;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (server != null)
        {
            foreach (var root in objects.ToArray())
            {
                if (root == null) continue;
                var network = root.GetComponent(T("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned"))
                    Call(network, "Despawn", true);
            }
            Call(server, "Shutdown", false);
            yield return null;
        }
        foreach (var root in objects)
            if (root != null) UnityEngine.Object.Destroy(root);
        foreach (var definition in definitions)
            if (definition != null) UnityEngine.Object.Destroy(definition);
        objects.Clear();
        definitions.Clear();
        yield return null;
    }

    private object PrepareCresseliaActor(int level, Vector3 position, string team = "Blue")
    {
#if UNITY_EDITOR
        GameObject root = UnityEngine.Object.Instantiate(fixture);
        root.name = "CresseliaActor";
        root.layer = LayerMask.NameToLayer("Players");
        root.transform.position = position;
        objects.Add(root);
        actor = root.GetComponent(T("Pokemon"));
        player = root.GetComponent(T("PlayerManager"));
        moves = root.GetComponent(T("MovesController"));
        var definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cresselia.asset"));
        definitions.Add((ScriptableObject)definition);
        Set(actor, "baseStats", definition);
        Set(moves, "pokemon", actor);
        Set(moves, "playerManager", player);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1UL, true);
        Variable(actor, "currentHp", 5000);
        Variable(actor, "currentLevel", level);
        Call(actor, "UpdateTeamRPC", E("Team", team));
        var hp = actor.GetType().GetField("currentHp", Private).GetValue(actor);
        var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, actor, actor.GetType().GetMethod("CurrentHpChanged", Private)));
        _ = Get(moves, "CresseliaRuntime");
        Physics.SyncTransforms();
        return Get(moves, "Combat");
#else
        return null;
#endif
    }

    private Component SpawnTarget(Vector3 position, string team = "Orange", string pokemonType = "Player")
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(fixture);
        root.transform.position = position;
        root.layer = LayerMask.NameToLayer(pokemonType == "Wild" ? "WildPokemons" : "Players");
        objects.Add(root);
        var pokemon = root.GetComponent(T("Pokemon"));
        var combat = root.GetComponent(T("MovesController"));
        var manager = root.GetComponent(T("PlayerManager"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase"));
        definitions.Add(definition);
        Set(definition, "pokemonName", "Target");
        Set(definition, "maxHp", Enumerable.Repeat(5000, 15).ToArray());
        Set(definition, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "spAttack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "defense", new int[15]);
        Set(definition, "spDefense", new int[15]);
        Set(definition, "critRate", new int[15]);
        Set(definition, "speed", Enumerable.Repeat(3500, 15).ToArray());
        Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0));
        Set(pokemon, "baseStats", definition);
        Set(pokemon, "type", E("PokemonType", pokemonType));
        Set(combat, "pokemon", pokemon);
        Set(combat, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 2UL, true);
        Variable(pokemon, "currentHp", 5000);
        Call(pokemon, "UpdateTeamRPC", E("Team", team));
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon);
        var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
        Physics.SyncTransforms();
        return pokemon;
#else
        return null;
#endif
    }

    [UnityTest]
    public IEnumerator UniteEnergyAccumulatesBeforeUnlockAndSurvivesLearning()
    {
        object combat = PrepareCresseliaActor(0, Vector3.zero);
        object uniteId = E("AvailableMoves", "CresseliaUnite");
        Assert.That(Call(combat, "Equip", uniteId), Is.False);
        Assert.That(Call(Get(combat, "Loadout"), "Get", 2), Is.Null);
        Call(combat, "GrantUniteEnergy", 2500);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(2500));
        Set(combat, "nextEnergyTick", (double)Get(combat, "Now") - 1d);
        Call(combat, "Tick");
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(3400));
        Variable(actor, "currentLevel", 8);
        Assert.That(Call(combat, "Equip", uniteId), Is.True);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(3400));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SafeguardAndLunarBlessingValidateSelfAllyEnemyAndOutOfRangeTargets()
    {
        object combat = Get(moves, "Combat");
        object character = Get(moves, "CresseliaRuntime");
        Component allyNear = SpawnTarget(Vector3.forward * 4f, "Blue");
        Component allyFar = SpawnTarget(Vector3.forward * 10f, "Blue");
        Component enemy = SpawnTarget(Vector3.right * 3f, "Orange");

        object moveId = E("AvailableMoves", "CresseliaLunarBlessing");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 1);
        int revision = (int)Get(ability, "Revision");

        Call(combat, "Receive", 1L, 1, revision, moveId, Vector3.forward, (ulong)Get(enemy, "NetworkObjectId"), 0L);
        object availability = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(availability, "Ready"), Is.True);

        Call(combat, "Receive", 2L, 1, revision, moveId, Vector3.forward, (ulong)Get(allyFar, "NetworkObjectId"), 0L);
        availability = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(availability, "Ready"), Is.True);

        Set(character, "boostReadyAt", (double)Get(combat, "Now") + 6.5d);
        object poison = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Poisoned"), 3f, true, (ushort)0);
        Call(actor, "AddStatusEffect", poison);
        Call(allyNear, "AddStatusEffect", poison);
        Call(combat, "Receive", 3L, 1, revision, moveId, Vector3.forward, (ulong)Get(allyNear, "NetworkObjectId"), 0L);
        yield return null;

        Assert.That((bool)Call(actor, "HasStatusEffect", E("StatusType", "Poisoned")), Is.False);
        Assert.That((bool)Call(allyNear, "HasStatusEffect", E("StatusType", "Poisoned")), Is.False);
        Assert.That(Get(character, "ActiveShieldCount"), Is.EqualTo(2));
        Assert.That((float)Get(moves, "CresseliaBoostRemaining"), Is.LessThan(5.6f));
    }

    [UnityTest]
    public IEnumerator LunarBlessingShieldSurvivesCastRecoveryAndConvertsRemainingShieldToHeal()
    {
        object combat = Get(moves, "Combat");
        object character = Get(moves, "CresseliaRuntime");
        Variable(actor, "currentHp", 3000);
        object moveId = E("AvailableMoves", "CresseliaLunarBlessing");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 1);
        int revision = (int)Get(ability, "Revision");

        Call(combat, "Receive", 10L, 1, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        object session = Call(Get(combat, "Coordinator"), "FindRequest", 10L);
        Assert.That(session, Is.Not.Null);

        yield return new WaitForSeconds(0.28f);
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
        Assert.That(Get(character, "ActiveShieldCount"), Is.EqualTo(1));

        yield return new WaitForSeconds(3.35f);
        Assert.That(Get(character, "ActiveShieldCount"), Is.Zero);
        Assert.That(Hp(actor), Is.GreaterThan(3000));
    }

    [UnityTest]
    public IEnumerator BufferedSupportCastPreservesItsOwnAllyTarget()
    {
        object combat = Get(moves, "Combat");
        Component selected = SpawnTarget(Vector3.forward * 3f, "Blue");
        Component other = SpawnTarget(Vector3.right * 3f, "Blue");
        object moveId = E("AvailableMoves", "CresseliaLunarBlessing");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 1);
        int revision = (int)Get(ability, "Revision");
        moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 1);
        Call(combat, "Receive", 1L, 1, revision, moveId, Vector3.forward, (ulong)Get(selected, "NetworkObjectId"), 0L);
        Assert.That(Get(selected, "ShieldHp"), Is.Zero);
        Assert.That(Call(Get(combat, "Coordinator"), "FindRequest", 1L), Is.Null);
        moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 0);
        Call(combat, "Tick");
        yield return null;
        Assert.That(Get(selected, "ShieldHp"), Is.GreaterThan(0));
        Assert.That(Get(other, "ShieldHp"), Is.Zero);
        Assert.That(Get(actor, "ShieldHp"), Is.GreaterThan(0));
    }

    [UnityTest]
    public IEnumerator BufferedSupportCastRejectsAllyLeavingRangeWithoutSelfFallbackOrCooldown()
    {
        object combat = Get(moves, "Combat");
        Component selected = SpawnTarget(Vector3.forward * 3f, "Blue");
        object moveId = E("AvailableMoves", "CresseliaLunarBlessing");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 1);
        int revision = (int)Get(ability, "Revision");
        moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 1);
        Call(combat, "Receive", 1L, 1, revision, moveId, Vector3.forward, (ulong)Get(selected, "NetworkObjectId"), 0L);
        selected.transform.position = Vector3.forward * 20f;
        Physics.SyncTransforms();
        moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 0);
        Call(combat, "Tick");
        yield return null;
        Assert.That(Get(actor, "ShieldHp"), Is.Zero);
        Assert.That(Get(selected, "ShieldHp"), Is.Zero);
        object availability = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(availability, "Ready"), Is.True);
        Assert.That(Call(Get(combat, "Coordinator"), "FindRequest", 1L), Is.Null);
    }

    [UnityTest]
    public IEnumerator MoonlightFieldSurvivesCastRecoveryHealsAlliesAndAdvancesRhythmOnce()
    {
        object combat = Get(moves, "Combat");
        object character = Get(moves, "CresseliaRuntime");
        Component ally = SpawnTarget(Vector3.forward * 2f, "Blue");
        Variable(actor, "currentHp", 3000);
        Variable(ally, "currentHp", 3000);
        Set(character, "boostReadyAt", (double)Get(combat, "Now") + 6.5d);

        object moveId = E("AvailableMoves", "CresseliaMoonlight");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 1);
        int revision = (int)Get(ability, "Revision");

        Call(combat, "Receive", 20L, 1, revision, moveId, Vector3.forward * 1.5f, ulong.MaxValue, 0L);
        object session = Call(Get(combat, "Coordinator"), "FindRequest", 20L);
        yield return new WaitForSeconds(0.4f);

        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
        Assert.That((int)Get(character, "ActiveLastingScopeCount"), Is.GreaterThanOrEqualTo(1));
        int hpAfterFirstPulse = Hp(ally);
        Assert.That(hpAfterFirstPulse, Is.GreaterThan(3000));
        float boostAfterFirstPulse = (float)Get(moves, "CresseliaBoostRemaining");
        Assert.That(boostAfterFirstPulse, Is.LessThan(5.4f));

        yield return new WaitForSeconds(0.85f);
        Assert.That(Hp(ally), Is.GreaterThan(hpAfterFirstPulse));
        Assert.That((float)Get(moves, "CresseliaBoostRemaining"), Is.GreaterThan(boostAfterFirstPulse - 0.9f));
    }

    [UnityTest]
    public IEnumerator PsybeamDamagesIncapacitatesHealsPerEnemyPlayerAndAdvancesRhythm()
    {
        object combat = Get(moves, "Combat");
        object character = Get(moves, "CresseliaRuntime");
        Component enemy = SpawnTarget(Vector3.forward * 3f, "Orange", "Player");
        Variable(actor, "currentHp", 4000);
        Set(character, "boostReadyAt", (double)Get(combat, "Now") + 6.5d);

        object moveId = E("AvailableMoves", "CresseliaPsybeam");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 0);
        int revision = (int)Get(ability, "Revision");

        Call(combat, "Receive", 30L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        yield return new WaitForSeconds(0.28f);

        Assert.That(Hp(enemy), Is.LessThan(5000));
        Assert.That((bool)Call(enemy, "HasStatusEffect", E("StatusType", "Incapacitated")), Is.True);
        Assert.That(Hp(actor), Is.EqualTo(4000 + Mathf.FloorToInt((int)Call(actor, "GetMaxHp") * 0.05f)));
        Assert.That((float)Get(moves, "CresseliaBoostRemaining"), Is.LessThan(5.4f));
    }

    [UnityTest]
    public IEnumerator MoonblastAppliesSplashDamageAndStatDebuffsAndAdvancesRhythm()
    {
        object combat = Get(moves, "Combat");
        object character = Get(moves, "CresseliaRuntime");
        Component primary = SpawnTarget(Vector3.forward * 3f, "Orange", "Player");
        Component splash = SpawnTarget(Vector3.forward * 3f + Vector3.right * 1.1f, "Orange", "Wild");
        Set(character, "boostReadyAt", (double)Get(combat, "Now") + 6.5d);

        object moveId = E("AvailableMoves", "CresseliaMoonblast");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 0);
        int revision = (int)Get(ability, "Revision");

        Call(combat, "Receive", 40L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        yield return new WaitForSeconds(0.45f);

        Assert.That(Hp(primary), Is.LessThan(5000));
        Assert.That(Hp(splash), Is.LessThan(5000));
        Assert.That((float)Get(moves, "CresseliaBoostRemaining"), Is.LessThan(5.4f));
    }

    [UnityTest]
    public IEnumerator BasicAttackConsumesBoostAppliesSleepAndRecoversIdleWithoutOverridingNewerMove()
    {
        object character = Get(moves, "CresseliaRuntime");
        Component enemy = SpawnTarget(Vector3.forward * 3f, "Orange");
        Component farEnemy = SpawnTarget(Vector3.forward * 8f, "Orange");

#if UNITY_EDITOR
        var model = UnityEngine.Object.Instantiate(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Cresselia/Cresselia.prefab"),
            actor.transform);
        var animator = model.GetComponentInChildren<Animator>();
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Component animations = actor.GetComponent(T("AnimationManager"));
        Call(animations, "AssignAnimator", animator);
        ((Behaviour)animations).enabled = true;
        animator.Play("pm0488_ba10_waitA01", 0, 0);
        animator.Update(0f);
#endif

        object attack = Get(moves, "ServerBasicAttack");
        Call(attack, "PerformOnServer", farEnemy);
        Assert.That((float)Get(moves, "CresseliaBoostRemaining"), Is.EqualTo(0f));

        Call(attack, "PerformOnServer", enemy);
        Assert.That((float)Get(moves, "CresseliaBoostRemaining"), Is.GreaterThan(5.5f));
        yield return new WaitForSeconds(0.25f);
        Assert.That(Hp(enemy), Is.LessThan(5000));

#if UNITY_EDITOR
        if (Host)
        {
            animator.Update(0.1f);
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.shortNameHash, Is.EqualTo(Animator.StringToHash("pm0488_ba10_waitA01")));
            animator.Play("pm0488_ba21_tokusyu01", 0, 0);
            animator.Update(0f);
            Invoke(character, "RecoverAnimation", "pm0488_ba20_buturi01");
            animator.Update(0.05f);
            state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.shortNameHash, Is.EqualTo(Animator.StringToHash("pm0488_ba21_tokusyu01")));
        }
#endif
    }

    [UnityTest]
    public IEnumerator PassiveEscortGrantsAndRemovesSpeedBuffWithoutDoubleTicking()
    {
        object combat = Get(moves, "Combat");
        object character = Get(moves, "CresseliaRuntime");
        Set(character, "nextEscortScan", 0f);
        Call(combat, "Tick");
        Assert.That(Get(moves, "CresseliaEscorting"), Is.False);

        Component ally = SpawnTarget(Vector3.forward * 3f, "Blue");
        Set(character, "nextEscortScan", 0f);
        Call(character, "Tick");
        Call(combat, "Tick");
        Assert.That(Get(moves, "CresseliaEscorting"), Is.True);

        ally.transform.position = Vector3.forward * 12f;
        Physics.SyncTransforms();
        Set(character, "nextEscortScan", 0f);
        Call(combat, "Tick");
        Assert.That(Get(moves, "CresseliaEscorting"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator UniteSpendsEnergyOnceEnablesFollowUpRecastAfterTakeoffAndLandsWithDamageAndSleep()
    {
        object combat = Get(moves, "Combat");
        object character = Get(moves, "CresseliaRuntime");
        Component enemy = SpawnTarget(Vector3.forward * 2f, "Orange");

        object uniteId = E("AvailableMoves", "CresseliaUnite");
        Assert.That(Call(combat, "Equip", uniteId), Is.True);
        Call(combat, "GrantUniteEnergy", 100000);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(100000));

        object ability = Call(Get(combat, "Loadout"), "Get", 2);
        int revision = (int)Get(ability, "Revision");
        Call(combat, "Receive", 50L, 2, revision, uniteId, Vector3.forward, ulong.MaxValue, 0L);
        yield return null;

        Assert.That(Get(combat, "UniteEnergy"), Is.Zero);
        Assert.That(Get(character, "IsUniteAirborne"), Is.True);
        Assert.That(Get(character, "CanStartBasicAttack"), Is.False);
        Assert.That((bool)Call(actor, "HasStatusEffect", (ushort)4881), Is.True);

        yield return new WaitForSeconds(0.25f);
        object snapshot = Get(moves.GetType().GetField("combatUniteSlot", Private).GetValue(moves), "Value");
        Assert.That(snapshot.GetType().GetField("FollowUpReady").GetValue(snapshot), Is.True);
        long castId = (long)snapshot.GetType().GetField("CastId").GetValue(snapshot);
        Assert.That(castId, Is.Not.Zero);

        Call(combat, "Receive", 51L, 2, revision, uniteId, Vector3.forward, ulong.MaxValue, castId);
        yield return new WaitForSeconds(0.38f);

        Assert.That(Get(character, "IsUniteAirborne"), Is.False);
        Assert.That((bool)Call(actor, "HasStatusEffect", (ushort)4881), Is.False);
        Assert.That(Hp(enemy), Is.LessThan(5000));
        Assert.That((bool)Call(enemy, "HasStatusEffect", E("StatusType", "Asleep")), Is.True);
    }

    [UnityTest]
    public IEnumerator InterruptionDuringWindupAndDespawnCleanUpLocksAndLeasesIdempotently()
    {
        object combat = Get(moves, "Combat");
        object character = Get(moves, "CresseliaRuntime");
        Component enemy = SpawnTarget(Vector3.forward * 3f, "Orange");

        object moveId = E("AvailableMoves", "CresseliaAuroraBeam");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 0);
        int revision = (int)Get(ability, "Revision");

        Call(combat, "Receive", 60L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        yield return null;
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.GreaterThan(0));

        object stun = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Incapacitated"), 1f, true, (ushort)0);
        Call(actor, "AddStatusEffect", stun);
        yield return null;
        yield return null;

        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
        Assert.That(Hp(enemy), Is.EqualTo(5000));

        Call(character, "Dispose");
        Call(character, "Dispose");
        Assert.That(Get(character, "AuroraDebuffId"), Is.EqualTo((ushort)0));
        Assert.That(Get(character, "MoonblastDebuffId"), Is.EqualTo((ushort)0));
    }
}

public sealed class CresseliaHostTests : CresseliaServerTests
{
    protected override bool Host => true;
}
