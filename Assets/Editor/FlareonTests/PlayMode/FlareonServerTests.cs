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

public class FlareonServerTests
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
        var floor = Node("FlareonTestGround");
        floor.transform.position = new Vector3(0, -0.1f, 0);
        floor.AddComponent<BoxCollider>().size = new Vector3(100, 0.2f, 100);

        var root = Node("Flareon test server");
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
        foreach (string kind in new[] { "Swift", "Tackle", "HeatWave", "Takedown", "Slam", "Flamethrower", "FlareBlitz", "Unite", "State" })
            Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Flareon/Flareon" + kind + "Object.prefab"));
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        yield return null;
        PrepareFlareonActor(12, Vector3.zero, "Blue");
        Call(Combat, "Tick");
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

    private object PrepareFlareonActor(int level, Vector3 position, string team = "Blue")
    {
#if UNITY_EDITOR
        GameObject root = UnityEngine.Object.Instantiate(fixture);
        root.name = "FlareonActor";
        root.layer = LayerMask.NameToLayer("Players");
        root.transform.position = position;
        objects.Add(root);
        actor = root.GetComponent(T("Pokemon"));
        player = root.GetComponent(T("PlayerManager"));
        moves = root.GetComponent(T("MovesController"));
        var definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Flareon.asset"));
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
        Assert.That(Get(Get(moves, "Combat"), "Character").GetType().Name, Is.EqualTo("FlareonCharacterRuntime"));
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

    private object Combat => Get(moves, "Combat");
    private object Character => Get(Combat, "Character");
    private ulong Id(Component pokemon) => (ulong)Get(pokemon, "NetworkObjectId");
    private object Availability(object ability) => Call(Get(ability, "Resources"), "Read", (double)Get(Combat, "Now"));

    private object Equip(string name, int slot)
    {
        Assert.That(Call(Combat, "Equip", E("AvailableMoves", name)), Is.True, name);
        return Call(Get(Combat, "Loadout"), "Get", slot);
    }

    private object Cast(object ability, string name, int slot, long request, Vector3 direction, ulong target = ulong.MaxValue, long followUp = 0)
    {
        Call(Combat, "Receive", request, slot, (int)Get(ability, "Revision"), E("AvailableMoves", name), direction, target, followUp);
        return Call(Get(Combat, "Coordinator"), "FindRequest", request);
    }

    private IEnumerator AwaitAcceptance(object session)
    {
        Assert.That(session, Is.Not.Null);
        float deadline = Time.time + 8;
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(session, "Accepted"), Is.True, Get(session, "Outcome").ToString());
    }

    private IEnumerator AwaitCompletion(object session)
    {
        float deadline = Time.time + 6;
        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }

    private IEnumerator VerifyDamagingMove(string name, int slot, bool targeted)
    {
        Component enemy = SpawnTarget(Vector3.forward * 2);
        object ability = Equip(name, slot);
        if (slot == 2) Call(Combat, "GrantUniteEnergy", int.MaxValue);
        object session = Cast(ability, name, slot, 1L, Vector3.forward, targeted ? Id(enemy) : ulong.MaxValue);
        yield return AwaitAcceptance(session);
        yield return AwaitCompletion(session);
        yield return new WaitForSeconds(0.6f);
        Assert.That(Hp(enemy), Is.LessThan(5000), name);
        if (slot == 2) Assert.That(Get(Combat, "UniteEnergy"), Is.Zero, name);
        else Assert.That(Get(Availability(ability), "Ready"), Is.False, name);
        Variable(actor, "currentHp", 0);
        yield return null;
        int afterDeath = Hp(enemy);
        yield return new WaitForSeconds(0.8f);
        Assert.That(Hp(enemy), Is.EqualTo(afterDeath), "Death must stop lasting fields and their ticks.");
    }

    [UnityTest] public IEnumerator SwiftHitsAfterCastRecovery() => VerifyDamagingMove("FlareonSwift", 0, false);
    [UnityTest] public IEnumerator TackleSweepsItsTraversal() => VerifyDamagingMove("FlareonTackle", 1, false);
    [UnityTest] public IEnumerator HeatWaveHitsAndCleansUpOnDeath() => VerifyDamagingMove("FlareonHeatWave", 0, false);
    [UnityTest] public IEnumerator FlamethrowerHitsAndReleasesCastLocks() => VerifyDamagingMove("FlareonFlameThrower", 1, false);
    [UnityTest] public IEnumerator FlareBlitzHitsAndRefundsPartOfItsCooldown() => VerifyDamagingMove("FlareonFlareBlitz", 1, false);
    [UnityTest] public IEnumerator UniteLandsAndDamagesBeforeCompletion() => VerifyDamagingMove("FlareonUnite", 2, true);

    [UnityTest]
    public IEnumerator TakedownAllowsOneTargetCorrelatedSlamWithoutAnotherCooldownSpend()
    {
        Component enemy = SpawnTarget(Vector3.forward * 2);
        Component other = SpawnTarget(Vector3.right * 2);
        object ability = Equip("FlareonTakedown", 0);
        object session = Cast(ability, "FlareonTakedown", 0, 1L, Vector3.forward, Id(enemy));
        yield return AwaitAcceptance(session);
        float deadline = Time.time + 3;
        while (!(bool)Get(Character, "RecastAvailable") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(Character, "RecastAvailable"), Is.True);
        while ((int)Get(moves, "ServerActionLocks") > 0 && Time.time < deadline) yield return null;
        long castId = (long)Get(session, "CastId");
        object replicated = Get(moves.GetType().GetField("combatSlotA", Private).GetValue(moves), "Value");
        Assert.That(replicated.GetType().GetField("FollowUpTargetId").GetValue(replicated), Is.EqualTo(Id(enemy)));
        Assert.That(replicated.GetType().GetField("CastId").GetValue(replicated), Is.EqualTo(castId));
        int firstDamage = Hp(enemy);
        Assert.That(firstDamage, Is.LessThan(5000));
        object coordinator = Get(Combat, "Coordinator");
        Cast(ability, "FlareonTakedown", 0, 2L, Vector3.right, Id(other), castId);
        Assert.That(Call(coordinator, "FindRequest", 2L), Is.Null);
        Assert.That(Get(Character, "RecastAvailable"), Is.True);
        Cast(ability, "FlareonTakedown", 0, 3L, Vector3.forward, Id(enemy), castId);
        yield return AwaitCompletion(session);
        Assert.That(Hp(enemy), Is.LessThan(firstDamage));
        Assert.That(Get(Character, "RecastAvailable"), Is.False);
        Assert.That(Get(Availability(ability), "Ready"), Is.False);
        int afterSlam = Hp(enemy);
        Cast(ability, "FlareonTakedown", 0, 4L, Vector3.forward, Id(enemy), castId);
        yield return null;
        Assert.That(Hp(enemy), Is.EqualTo(afterSlam));
    }

    [UnityTest]
    public IEnumerator ImmobilizedTraversalIsRejectedWithoutSpendingCooldown()
    {
        object ability = Equip("FlareonTackle", 1);
        object status = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Immobilized"), 2f, true, (ushort)60500);
        Call(actor, "AddStatusEffect", status);
        object session = Cast(ability, "FlareonTackle", 1, 1L, Vector3.forward);
        Assert.That(session == null || !(bool)Get(session, "Accepted"), Is.True);
        Assert.That(Get(Availability(ability), "Ready"), Is.True);
        yield return null;
    }

    [UnityTest]
    public IEnumerator BasicAttacksCycleAndBurnedTargetDamageBuildsPassiveStacks()
    {
        Component enemy = SpawnTarget(Vector3.forward);
        object attack = Get(moves, "ServerBasicAttack");
        Assert.That(Get(attack, "ServerResolved"), Is.True);
        for (int index = 0; index < 3; index++)
        {
            Call(attack, "PerformOnServer", enemy);
            yield return new WaitForSeconds(0.2f);
            Assert.That(Get(Character, "AttackCharge"), Is.EqualTo((index + 1) % 3));
            yield return new WaitForSeconds(0.45f);
        }
        Assert.That(Hp(enemy), Is.LessThan(5000));
        T("FlareonMoveRules").GetMethod("Burn").Invoke(null, new object[] { enemy, Id(actor), 5f });
        Assert.That(Call(enemy, "HasStatusEffect", E("StatusType", "Burned")), Is.True);
        object ability = Equip("FlareonHeatWave", 0);
        object session = Cast(ability, "FlareonHeatWave", 0, 1L, Vector3.forward);
        yield return AwaitAcceptance(session);
        yield return new WaitForSeconds(1.1f);
        Assert.That(Get(Character, "Stacks"), Is.GreaterThan(0));
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Get(Character, "AttackCharge"), Is.Zero);
        Assert.That(Get(Character, "Stacks"), Is.Zero);
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator DeathDuringUniteWindupRefundsNinetyPercentOnlyOnce()
    {
        Component enemy = SpawnTarget(Vector3.forward * 3);
        object ability = Equip("FlareonUnite", 2);
        Call(Combat, "GrantUniteEnergy", int.MaxValue);
        int maximum = (int)Get(Combat, "UniteEnergy");
        Set(Combat, "nextEnergyTick", (double)Get(Combat, "Now") + 60d);
        object session = Cast(ability, "FlareonUnite", 2, 1L, Vector3.forward, Id(enemy));
        yield return AwaitAcceptance(session);
        Assert.That(Get(Combat, "UniteEnergy"), Is.Zero);
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Get(Combat, "UniteEnergy"), Is.EqualTo(maximum * 90 / 100));
        Assert.That(Hp(enemy), Is.EqualTo(5000));
        Call(Combat, "CancelSlot", 2, (int)Get(ability, "Revision"));
        Assert.That(Get(Combat, "UniteEnergy"), Is.EqualTo(maximum * 90 / 100));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator ReplacingAMoveCancelsItsIndependentField()
    {
        Component enemy = SpawnTarget(Vector3.forward * 2);
        object ability = Equip("FlareonSwift", 0);
        object session = Cast(ability, "FlareonSwift", 0, 1L, Vector3.forward);
        yield return AwaitAcceptance(session);
        yield return AwaitCompletion(session);
        Equip("FlareonHeatWave", 0);
        Call(Combat, "Tick");
        yield return null;
        int afterReplacement = Hp(enemy);
        yield return new WaitForSeconds(0.8f);
        Assert.That(Hp(enemy), Is.EqualTo(afterReplacement));
        Assert.That(Get(Character, "RecastAvailable"), Is.False);
    }

    [UnityTest]
    public IEnumerator MigratedActorCannotBypassCombatThroughLegacySpawnOrDespawnRpcs()
    {
        Component enemy = SpawnTarget(Vector3.forward);
        Call(moves, "DespawnNetworkObjectRPC", Id(enemy));
        Assert.That(Get(enemy, "IsSpawned"), Is.True);
        Call(moves, "SpawnNetworkObjectFromStringRPC", "Assets/NonexistentLegacyCombatBypass.prefab");
        Call(moves, "SpawnNetworkObjectFromStringRPC", "Assets/NonexistentLegacyCombatBypass.prefab", 1UL);
        yield return new WaitForSeconds(0.1f);
        Assert.That(Get(enemy, "IsSpawned"), Is.True);
        Assert.That(Get(Get(Combat, "Coordinator"), "ActiveCount"), Is.Zero);
    }
}

public class FlareonHostTests : FlareonServerTests
{
    protected override bool Host => true;
}
