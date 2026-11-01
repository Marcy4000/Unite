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

public class VaporeonServerTests
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
        var floor = Node("VaporeonTestGround");
        floor.transform.position = new Vector3(0, -0.1f, 0);
        floor.AddComponent<BoxCollider>().size = new Vector3(100, 0.2f, 100);

        var root = Node("Vaporeon test server");
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
        PrepareVaporeonActor(12, Vector3.zero, "Blue");
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

    private object PrepareVaporeonActor(int level, Vector3 position, string team = "Blue")
    {
#if UNITY_EDITOR
        GameObject root = UnityEngine.Object.Instantiate(fixture);
        root.name = "VaporeonActor";
        root.layer = LayerMask.NameToLayer("Players");
        root.transform.position = position;
        objects.Add(root);
        actor = root.GetComponent(T("Pokemon"));
        player = root.GetComponent(T("PlayerManager"));
        moves = root.GetComponent(T("MovesController"));
        var definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Vaporeon.asset"));
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
        Assert.That(Get(Get(moves, "Combat"), "Character").GetType().Name, Is.EqualTo("VaporeonCharacterRuntime"));
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

    private object Equip(object combat, string name, int slot)
    {
        object moveId = E("AvailableMoves", name);
        Assert.That(Call(combat, "Equip", moveId), Is.True, name);
        return Call(Get(combat, "Loadout"), "Get", slot);
    }

    private object Cast(object combat, object ability, string name, int slot, long request, Vector3 direction, ulong target = ulong.MaxValue, long followUp = 0)
    {
        Call(combat, "Receive", request, slot, (int)Get(ability, "Revision"), E("AvailableMoves", name), direction, target, followUp);
        return Call(Get(combat, "Coordinator"), "FindRequest", request);
    }

    private object Availability(object combat, object ability) => Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));

    [UnityTest]
    public IEnumerator UniteEnergyAccumulatesBeforeLearning()
    {
        object combat = PrepareVaporeonActor(0, Vector3.zero);
        object moveId = E("AvailableMoves", "VaporeonUnite");
        Assert.That(Call(combat, "Equip", moveId), Is.False);
        Assert.That(Call(Get(combat, "Loadout"), "Get", 2), Is.Null);
        Call(combat, "GrantUniteEnergy", 2500);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(2500));
        Set(combat, "nextEnergyTick", (double)Get(combat, "Now") - 1d);
        Call(combat, "Tick");
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(3400));
        Variable(actor, "currentLevel", 7);
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(3400));
        yield return null;
    }

    [UnityTest]
    public IEnumerator AquaRingSelfCastSurvivesRecoveryAndEndsOnDeath()
    {
        object combat = Get(moves, "Combat");
        object moveId = E("AvailableMoves", "VaporAquaRing");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 0);
        int revision = (int)Get(ability, "Revision");
        Call(combat, "Receive", 1L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        Assert.That(session, Is.Not.Null);
        yield return new WaitForSeconds(0.3f);
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Get(actor, "ShieldHp"), Is.GreaterThan(0));
        Assert.That(Get(Availability(combat, ability), "Ready"), Is.False);
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
        Assert.That(Get(actor, "ShieldHp"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator HelpingHandRejectsInvalidTargetsWithoutSpendingCooldown()
    {
        object combat = Get(moves, "Combat");
        Component enemy = SpawnTarget(Vector3.forward * 3f, "Orange");
        Component distant = SpawnTarget(Vector3.forward * 12f, "Blue");
        object moveId = E("AvailableMoves", "VaporHelpingHand");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 1);
        int revision = (int)Get(ability, "Revision");
        Call(combat, "Receive", 1L, 1, revision, moveId, Vector3.forward, (ulong)Get(enemy, "NetworkObjectId"), 0L);
        Call(combat, "Receive", 2L, 1, revision, moveId, Vector3.forward, (ulong)Get(distant, "NetworkObjectId"), 0L);
        Call(combat, "Receive", 3L, 1, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        object availability = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(availability, "Ready"), Is.True);
        Assert.That(Call(Get(combat, "Coordinator"), "FindRequest", 1L), Is.Null);
        Assert.That(Call(Get(combat, "Coordinator"), "FindRequest", 2L), Is.Null);
        Assert.That(Call(Get(combat, "Coordinator"), "FindRequest", 3L), Is.Null);
        Component ally = SpawnTarget(Vector3.right * 3f, "Blue");
        int attackBefore = (int)Call(ally, "GetAttack");
        object accepted = Cast(combat, ability, "VaporHelpingHand", 1, 4L, Vector3.forward, (ulong)Get(ally, "NetworkObjectId"));
        Assert.That(accepted, Is.Not.Null);
        yield return null;
        Assert.That((int)Call(ally, "GetAttack"), Is.GreaterThan(attackBefore));
        Assert.That(Get(Availability(combat, ability), "Ready"), Is.False);
    }

    [UnityTest]
    public IEnumerator SwiftVolleyOutlivesRecoveryAndStopsAfterDeath()
    {
        object combat = Get(moves, "Combat");
        Component enemy = SpawnTarget(Vector3.forward * 2f);
        object ability = Equip(combat, "VaporSwift", 0);
        object session = Cast(combat, ability, "VaporSwift", 0, 1L, Vector3.forward * 2f);
        Assert.That(session, Is.Not.Null);
        yield return new WaitForSeconds(0.5f);
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Hp(enemy), Is.EqualTo(5000));
        yield return new WaitForSeconds(0.55f);
        Assert.That(Hp(enemy), Is.LessThan(5000));
        Variable(actor, "currentHp", 0);
        int healthAfterDeath = Hp(enemy);
        yield return new WaitForSeconds(0.85f);
        Assert.That(Hp(enemy), Is.EqualTo(healthAfterDeath));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator WhirlpoolDealsPulsesAndReleasesSlowOnDeath()
    {
        object combat = Get(moves, "Combat");
        Component enemy = SpawnTarget(Vector3.forward * 2f);
        int speedBefore = (int)Call(enemy, "GetSpeed");
        object ability = Equip(combat, "VaporWhirlpool", 1);
        object session = Cast(combat, ability, "VaporWhirlpool", 1, 1L, Vector3.forward * 2f);
        yield return new WaitForSeconds(0.45f);
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Hp(enemy), Is.LessThan(5000));
        Assert.That((int)Call(enemy, "GetSpeed"), Is.LessThan(speedBefore));
        int firstHealth = Hp(enemy);
        yield return new WaitForSeconds(0.55f);
        Assert.That(Hp(enemy), Is.LessThan(firstHealth));
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That((int)Call(enemy, "GetSpeed"), Is.EqualTo(speedBefore));
        int healthAfterDeath = Hp(enemy);
        yield return new WaitForSeconds(0.6f);
        Assert.That(Hp(enemy), Is.EqualTo(healthAfterDeath));
    }

    [UnityTest]
    public IEnumerator RainFieldSurvivesHopAndReleasesOccupantsOnDeath()
    {
        object combat = Get(moves, "Combat");
        Component ally = SpawnTarget(Vector3.forward, "Blue");
        Set(Get(ally, "BaseStats"), "defense", Enumerable.Repeat(100, 15).ToArray());
        Variable(ally, "currentHp", 3000);
        int defenseBefore = (int)Call(ally, "GetDefense");
        object ability = Equip(combat, "VaporRainDance", 0);
        object session = Cast(combat, ability, "VaporRainDance", 0, 1L, Vector3.zero);
        yield return new WaitForSeconds(0.65f);
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That((int)Call(ally, "GetDefense"), Is.GreaterThan(defenseBefore));
        Assert.That(Get(Availability(combat, ability), "Ready"), Is.False);
        yield return new WaitForSeconds(1.15f);
        Assert.That(Hp(ally), Is.GreaterThan(3000));
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That((int)Call(ally, "GetDefense"), Is.EqualTo(defenseBefore));
    }

    [UnityTest]
    public IEnumerator DiveRecastStartsCooldownAtEmergenceAndResolvesImpact()
    {
        object combat = Get(moves, "Combat");
        Component enemy = SpawnTarget(Vector3.forward);
        object ability = Equip(combat, "VaporDive", 1);
        object session = Cast(combat, ability, "VaporDive", 1, 1L, Vector3.zero);
        Assert.That(session, Is.Not.Null);
        yield return new WaitForSeconds(0.15f);
        Assert.That(Get(Availability(combat, ability), "AwaitingExecutionEnd"), Is.True);
        Assert.That(Get(Get(combat, "Character"), "Diving"), Is.True);
        long castId = (long)Get(session, "CastId");
        Cast(combat, ability, "VaporDive", 1, 2L, player.transform.position, ulong.MaxValue, castId);
        yield return new WaitForSeconds(0.2f);
        Assert.That(Get(Get(combat, "Character"), "Diving"), Is.True);
        Cast(combat, ability, "VaporDive", 1, 3L, player.transform.position, ulong.MaxValue, castId);
        yield return new WaitForSeconds(0.1f);
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Get(Get(combat, "Character"), "Diving"), Is.False);
        Assert.That((double)Get(Availability(combat, ability), "Remaining"), Is.GreaterThan(8d));
        yield return new WaitForSeconds(0.3f);
        Assert.That(Hp(enemy), Is.LessThan(5000));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator UniteInterruptedDuringWindupRefundsOnce()
    {
        object combat = Get(moves, "Combat");
        object ability = Equip(combat, "VaporeonUnite", 2);
        Call(combat, "GrantUniteEnergy", 100000);
        object session = Cast(combat, ability, "VaporeonUnite", 2, 1L, Vector3.forward);
        yield return new WaitForSeconds(0.1f);
        Assert.That(Get(combat, "UniteEnergy"), Is.Zero);
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Get(session, "Outcome").ToString(), Is.EqualTo("Interrupted"));
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(90000));
        Call(combat, "Tick");
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(90000));
    }

    [UnityTest]
    public IEnumerator UniteImpactCommitsSuccessBeforeLaterDeath()
    {
        object combat = Get(moves, "Combat");
        Component enemy = SpawnTarget(Vector3.forward * 2f);
        object ability = Equip(combat, "VaporeonUnite", 2);
        Call(combat, "GrantUniteEnergy", 100000);
        object session = Cast(combat, ability, "VaporeonUnite", 2, 1L, Vector3.forward);
        yield return new WaitForSeconds(0.68f);
        Assert.That(Hp(enemy), Is.LessThan(5000));
        Assert.That(Get(combat, "UniteEnergy"), Is.Zero);
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Get(combat, "UniteEnergy"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator HostOwnerBuffersReleasedDiveThroughRecoveryAndExpiresWithoutReplay()
    {
        if (!Host) Assert.Ignore("Owner input requires a host.");
        yield return null;
        object combat = Get(moves, "Combat");
        object ring = Equip(combat, "VaporAquaRing", 0);
        Equip(combat, "VaporDive", 1);
        var statuses = (Array)moves.GetType().GetField("moveStatuses", Private).GetValue(moves);
        for (int slot = 0; slot < statuses.Length; slot++)
            if (statuses.GetValue(slot) == null) statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), slot);
        var moveArray = (Array)moves.GetType().GetField("moves", Private).GetValue(moves);
        object dive = Activator.CreateInstance(T("VaporDive"));
        moveArray.SetValue(Activator.CreateInstance(T("VaporAquaRing")), 0);
        moveArray.SetValue(dive, 1);
        Call(player.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", 0UL);
        Assert.That(Get(moves, "IsOwner"), Is.True);
        object runtime = Get(combat, "Character");
        object session = Cast(combat, ring, "VaporAquaRing", 0, 101L, Vector3.forward);
        Assert.That(session, Is.Not.Null);
        Set(combat, "nextRequestId", 101L);
        yield return new WaitForSeconds(0.04f);
        Assert.That(Get(runtime, "CanBufferMoveInput"), Is.True);
        object status = Invoke(moves, "CombatMoveStatus", 1);
        object busy = E("ActionStatusType", "Busy");
        Call(status, "AddStatus", busy);
        Call(combat, "HandleInput", dive, 1, status, false);
        Call(combat, "HandleInput", dive, 1, status, true);
        object buffer = combat.GetType().GetField("inputBuffer", Private).GetValue(combat);
        Assert.That(Get(buffer, "Count"), Is.EqualTo(1));
        yield return new WaitForSeconds(0.16f);
        Call(combat, "Tick");
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Get(buffer, "Count"), Is.EqualTo(1));
        Assert.That(Get(runtime, "Diving"), Is.False);
        Call(status, "RemoveStatus", busy);
        Call(combat, "Tick");
        Assert.That(Get(buffer, "Count"), Is.Zero);
        yield return null;
        Assert.That(Get(runtime, "Diving"), Is.True);
        Call(combat, "Tick");
        Assert.That(Get(buffer, "Count"), Is.Zero);

        object freshCombat = PrepareVaporeonActor(12, Vector3.right * 10f);
        yield return null;
        object freshRing = Equip(freshCombat, "VaporAquaRing", 0);
        Equip(freshCombat, "VaporDive", 1);
        var freshStatuses = (Array)moves.GetType().GetField("moveStatuses", Private).GetValue(moves);
        for (int slot = 0; slot < freshStatuses.Length; slot++)
            if (freshStatuses.GetValue(slot) == null) freshStatuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), slot);
        var freshMoves = (Array)moves.GetType().GetField("moves", Private).GetValue(moves);
        object freshDive = Activator.CreateInstance(T("VaporDive"));
        freshMoves.SetValue(Activator.CreateInstance(T("VaporAquaRing")), 0);
        freshMoves.SetValue(freshDive, 1);
        Call(player.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", 0UL);
        Cast(freshCombat, freshRing, "VaporAquaRing", 0, 201L, Vector3.forward);
        yield return new WaitForSeconds(0.04f);
        object freshStatus = Invoke(moves, "CombatMoveStatus", 1);
        Call(freshStatus, "AddStatus", busy);
        Call(freshCombat, "HandleInput", freshDive, 1, freshStatus, false);
        yield return new WaitForSeconds(0.12f);
        Call(freshCombat, "HandleInput", freshDive, 1, freshStatus, true);
        object freshBuffer = freshCombat.GetType().GetField("inputBuffer", Private).GetValue(freshCombat);
        Assert.That(Get(freshBuffer, "Count"), Is.EqualTo(1));
        yield return new WaitForSeconds(0.13f);
        Call(freshCombat, "Tick");
        Assert.That(Get(freshBuffer, "Count"), Is.Zero);
        Call(freshStatus, "RemoveStatus", busy);
        Call(freshCombat, "Tick");
        yield return null;
        Assert.That(Get(Get(freshCombat, "Character"), "Diving"), Is.False);
    }

    [UnityTest]
    public IEnumerator SnapshotRoundTripsActiveBindingsAndRecoveryDeadline()
    {
        object combat = Get(moves, "Combat");
        object ability = Equip(combat, "VaporAquaRing", 0);
        object session = Cast(combat, ability, "VaporAquaRing", 0, 301L, Vector3.forward);
        Assert.That(session, Is.Not.Null);
        yield return new WaitForSeconds(0.04f);
        object runtime = Get(combat, "Character");
        string encoded = (string)Call(runtime, "CapturePresentationState");
        Assert.That(encoded.Length, Is.LessThanOrEqualTo(4090));
        Type runtimeType = runtime.GetType();
        object snapshot = runtimeType.GetMethod("DecodeSnapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { encoded });
        Type snapshotType = snapshot.GetType();
        Assert.That((double)snapshotType.GetField("RecoveryUntil").GetValue(snapshot), Is.GreaterThan((double)Get(combat, "Now")));
        Array visuals = (Array)snapshotType.GetField("Visuals").GetValue(snapshot);
        Assert.That(visuals.Length, Is.GreaterThan(0));
        object ringVisual = visuals.Cast<object>().First(visual => visual.GetType().GetField("Effect").GetValue(visual).ToString() == "AquaRing");
        Assert.That(ringVisual.GetType().GetField("Target").GetValue(ringVisual), Is.EqualTo(Get(actor, "NetworkObjectId")));
        Assert.That((double)ringVisual.GetType().GetField("Started").GetValue(ringVisual), Is.GreaterThanOrEqualTo(0d));
        MethodInfo stale = runtimeType.GetMethod("IsStaleRevision", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.That(stale.Invoke(null, new object[] { 4L, 5L }), Is.True);
        Assert.That(stale.Invoke(null, new object[] { 5L, 5L }), Is.False);
        Assert.That(stale.Invoke(null, new object[] { 6L, 5L }), Is.False);
#if UNITY_EDITOR
        GameObject observer = UnityEngine.Object.Instantiate(fixture);
        objects.Add(observer);
        Component observerController = observer.GetComponent(T("MovesController"));
        Assert.That(Get(observerController, "IsServer"), Is.False);
        object observerRuntime = Activator.CreateInstance(runtimeType, new object[] { observer.GetComponent(T("PlayerManager")) });
        long incomingRevision = (long)snapshotType.GetField("Revision").GetValue(snapshot);
        snapshotType.GetField("Revision").SetValue(snapshot, incomingRevision + 1);
        snapshotType.GetField("Charge").SetValue(snapshot, (byte)2);
        string newer = (string)runtimeType.GetMethod("EncodeSnapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new[] { snapshot });
        Call(observerRuntime, "ApplyPresentationState", newer);
        Assert.That(Get(observerRuntime, "Charge"), Is.EqualTo((byte)2));
        Call(observerRuntime, "ApplyPresentationState", encoded);
        Assert.That(Get(observerRuntime, "Charge"), Is.EqualTo((byte)2));
        ((IDisposable)observerRuntime).Dispose();
#endif
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Get(runtime, "CanBufferMoveInput"), Is.False);
        Assert.That((double)runtimeType.GetField("recoveryUntil", Private).GetValue(runtime), Is.Zero);
    }

    [UnityTest]
    public IEnumerator SnapshotFitsWithTeamSizedPersistentEffectsAndTransientBurst()
    {
        object runtime = Get(Get(moves, "Combat"), "Character");
        for (int index = 0; index < 10; index++)
        {
            Invoke(runtime, "ShowEffect", E("VaporEffect", "AquaRing"), new Vector3(index * 1.21f, 0.1f, -index * 0.37f), 6f, (ulong)(1000 + index), 1f, index);
            Invoke(runtime, "ShowEffect", E("VaporEffect", "HelpingHand"), new Vector3(index * 0.73f, 0.2f, index * 1.14f), 3.5f, (ulong)(1000 + index), 1f, index);
        }
        foreach (string effect in new[] { "Basic", "Boosted", "Swift", "Rain", "Whirlpool", "DiveWake", "DiveWarning", "TempestWarning" })
            Invoke(runtime, "ShowEffect", E("VaporEffect", effect), Vector3.forward, 3f, ulong.MaxValue, 1f, 0);
        for (int index = 0; index < 100; index++)
            Invoke(runtime, "ShowEffect", E("VaporEffect", "HealPulse"), new Vector3(index * 0.31f, index * 0.07f, index * 0.43f), 0.3f, (ulong)(5000 + index), 0.85f, index);
        string encoded = (string)Call(runtime, "CapturePresentationState");
        Assert.That(encoded.Length, Is.LessThanOrEqualTo(4090));
        object snapshot = runtime.GetType().GetMethod("DecodeSnapshot", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { encoded });
        Array visuals = (Array)snapshot.GetType().GetField("Visuals").GetValue(snapshot);
        var persistent = visuals.Cast<object>().Where(visual => new[] { "AquaRing", "HelpingHand", "Basic", "Boosted", "Swift", "Rain", "Whirlpool", "DiveWake", "DiveWarning", "TempestWarning" }
            .Contains(visual.GetType().GetField("Effect").GetValue(visual).ToString())).ToArray();
        Assert.That(persistent.Length, Is.EqualTo(28));
        Assert.That(persistent.Count(visual => visual.GetType().GetField("Effect").GetValue(visual).ToString() == "AquaRing"), Is.EqualTo(10));
        yield return null;
    }

    [UnityTest]
    public IEnumerator BasicChargeAndEvolvedHydrationAreServerOwned()
    {
        object runtime = Get(Get(moves, "Combat"), "Character");
        Component enemy = SpawnTarget(Vector3.forward * 2f);
        Component ally = SpawnTarget(Vector3.right * 2f, "Blue");
        for (int attack = 0; attack < 3; attack++)
        {
            Call(runtime, "PerformBasicAttack", enemy);
            Assert.That(Get(runtime, "Charge"), Is.EqualTo((byte)((attack + 1) % 3)));
        }
        Variable(actor, "currentLevel", 2);
        int speed = (int)Call(actor, "GetSpeed");
        Invoke(runtime, "SupportAlly", ally);
        Assert.That((int)Call(actor, "GetSpeed"), Is.EqualTo(speed));
        Variable(actor, "currentLevel", 3);
        int evolvedSpeed = (int)Call(actor, "GetSpeed");
        Invoke(runtime, "SupportAlly", ally);
        Assert.That((int)Call(actor, "GetSpeed"), Is.GreaterThan(evolvedSpeed));
        double ready = (double)runtime.GetType().GetField("hydrationReady", Private).GetValue(runtime);
        Invoke(runtime, "SupportAlly", ally);
        Assert.That(runtime.GetType().GetField("hydrationReady", Private).GetValue(runtime), Is.EqualTo(ready));
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Get(runtime, "Charge"), Is.EqualTo((byte)0));
    }
}

public sealed class VaporeonHostTests : VaporeonServerTests
{
    protected override bool Host => true;
}
