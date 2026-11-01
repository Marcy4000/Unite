using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unite.Combat;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Real NGO host/dedicated-server fixture; no live lobby or input dependencies.
public class ZoroarkServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
    private Component server, actor, player, moves;
    private GameObject fixture;
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string value) => Enum.Parse(T(type), value);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(target, args);
    private static object Invoke(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Variable(object target, string name, object value)
    {
        var variable = target.GetType().GetField(name, Private).GetValue(target);
        variable.GetType().GetProperty("Value").SetValue(variable, value);
    }
    private static int Hp(Component target) => (int)Get(target, "CurrentHp");
    private GameObject Node(string name) { var root = new GameObject(name); objects.Add(root); return root; }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("External network session active.");
        foreach (var name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        {
            var map = Node(name); map.tag = name;
            map.transform.position = new Vector3(0, name == "WalkableWithWalls" ? -30.5f : -40.5f, 0);
            map.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100);
        }
        var root = Node("Zoroark test server"); server = root.AddComponent(T("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig")); server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport); config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab");
        Call(server, "AddNetworkPrefab", fixture);
        foreach (string name in Names) Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Zoroark/" + name + ".prefab"));
        foreach (string path in GlaceonPaths) Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(path));
        foreach (object kind in Enum.GetValues(T("CinderCast")))
        {
            string path = (string)T("CinderMoveRules").GetMethod("Path").Invoke(null, new[] { kind });
            Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        yield return null;
        actor = Pokemon(Vector3.zero, "Blue"); player = actor.GetComponent(T("PlayerManager")); moves = actor.GetComponent(T("MovesController"));
        Invoke(moves, "UpdateZoroarkRuntime");
#else
        Assert.Ignore("Editor fixture required."); yield break;
#endif
    }
    private static readonly string[] Names = { "ZoroarkSlash", "ZoroarkFurySwipes", "ZoroarkShadowClaw", "ZoroarkCut", "ZoroarkNightSlash", "ZoroarkFeintAttack", "ZoroarkNightfallDaze" };
    private static readonly string[] GlaceonPaths = {
        "Assets/Prefabs/Objects/BasicAtk/GlaceonBasicAtk.prefab",
        "Assets/Prefabs/Objects/BasicAtk/CinderBasicAtk.prefab",
        "Assets/Prefabs/Objects/Moves/Glaceon/GlaceonTailWhip.prefab",
        "Assets/Prefabs/Objects/Passives/Glaceon/IciclesHolder.prefab",
        "Assets/Prefabs/Objects/Moves/Glaceon/FreezeDryProjectile.prefab",
        "Assets/Prefabs/Objects/Moves/Glaceon/IcicleSpear.prefab",
        "Assets/Prefabs/Objects/Moves/Glaceon/GlaceonUnite.prefab" };

    private Component Pokemon(Vector3 position, string team = "Orange")
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(fixture); root.transform.position = position; objects.Add(root);
        var pokemon = root.GetComponent(T("Pokemon")); var combat = root.GetComponent(T("MovesController")); var manager = root.GetComponent(T("PlayerManager"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase")); definitions.Add(definition);
        Set(definition, "pokemonName", "Zoroark"); Set(definition, "maxHp", Enumerable.Repeat(5000, 15).ToArray()); Set(definition, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "defense", new int[15]); Set(definition, "spDefense", new int[15]); Set(definition, "critRate", new int[15]); Set(definition, "speed", Enumerable.Repeat(3650, 15).ToArray());
        Set(definition, "passive", E("AvailablePassives", "ZoroarkPassive")); Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0));
        var original = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Zoroark.asset");
        Set(definition, "evolutions", Get(original, "Evolutions"));
        Set(pokemon, "baseStats", definition); Set(combat, "pokemon", pokemon); Set(combat, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1ul, true);
        Variable(pokemon, "currentHp", 5000); Call(pokemon, "UpdateTeamRPC", E("Team", team));
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
        return pokemon;
#else
        return null;
#endif
    }
    private Component Effect(string name)
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Zoroark/" + name + ".prefab")); objects.Add(root);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true);
        var effect = root.GetComponent(T("ZoroarkMoveEffect"));
        var scope = new CombatScope();
        object context = Activator.CreateInstance(T("CombatExecutionContext"));
        context.GetType().GetField("Actor").SetValue(context, player);
        context.GetType().GetField("Direction").SetValue(context, Vector3.forward);
        context.GetType().GetField("MoveId").SetValue(context, E("AvailableMoves", name));
        context.GetType().GetField("Slot").SetValue(context, name == "ZoroarkNightfallDaze" ? 2 : name == "ZoroarkNightSlash" || name == "ZoroarkFeintAttack" ? 1 : 0);
        context.GetType().GetField("Scope").SetValue(context, scope);
        object execution = Activator.CreateInstance(T("ZoroarkExecution"), context, Get(Get(moves, "Combat"), "Character"));
        context.GetType().GetField("Finish").SetValue(context, (Action<CastOutcome>)(outcome =>
        {
            Call(execution, "Stop", outcome);
            scope.Dispose();
        }));
        Invoke(execution, "AttachEffect", effect);
        effect.GetType().GetMethod("Initialize").Invoke(effect, new object[] { player, Vector3.forward, null, false });
        return effect;
#else
        return null;
#endif
    }
    private static object Damage(Component source, int amount) => Activator.CreateInstance(T("DamageInfo"), Get(source, "NetworkObjectId"), 0f, (short)0, (short)amount, E("DamageType", "Physical"), E("DamageProprieties", "None"));

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (server != null)
        {
            foreach (var root in objects.ToArray()) if (root != null)
            {
                var network = root.GetComponent(T("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
            }
            Call(server, "Shutdown", false); yield return null;
        }
        foreach (var root in objects) if (root != null) UnityEngine.Object.Destroy(root);
        foreach (var definition in definitions) if (definition != null) UnityEngine.Object.Destroy(definition);
        objects.Clear(); definitions.Clear(); yield return null;
    }

    [UnityTest]
    public IEnumerator GlaceonSpearReleasePreservesConsumptionAndResumesTheSameCast()
    {
        object combat = PrepareMigratedActor(3);
        object character = Get(combat, "Character");
        Call(character, "SetCrystals", 4);
        object moveId = E("AvailableMoves", "GlaceIcycleSpear");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 0);
        int revision = (int)Get(ability, "Revision");
        Call(combat, "Receive", 1L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        object coordinator = Get(combat, "Coordinator");
        object session = Call(coordinator, "FindRequest", 1L);
        float deadline = Time.time + 8;
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        yield return null;
        Assert.That(Get(character, "CanStartBasicAttack"), Is.False);
        Call(combat, "ReceiveInputActive", 0, revision, false);
        Assert.That(Get(character, "CanStartBasicAttack"), Is.True);
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Executing"));
        object paused = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(paused, "AwaitingExecutionEnd"), Is.True);
        Assert.That(Get(paused, "Remaining"), Is.EqualTo(0));
        object snapshot = Get(moves.GetType().GetField("combatSlotA", Private).GetValue(moves), "Value");
        Assert.That(snapshot.GetType().GetField("FollowUpReady").GetValue(snapshot), Is.EqualTo(true));
        long castId = (long)Get(session, "CastId");
        Call(combat, "Receive", 2L, 0, revision, moveId, Vector3.right, ulong.MaxValue, castId);
        Assert.That(Call(coordinator, "FindRequest", 2L), Is.Null);
        Assert.That(Get(coordinator, "ActiveCount"), Is.EqualTo(1));
        yield return null;
        Assert.That(Get(character, "CanStartBasicAttack"), Is.False);
        Call(character, "SetCrystals", 1);
        Call(combat, "ReceiveInputActive", 0, revision, false);
        deadline = Time.time + 2;
        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(character, "Crystals"), Is.EqualTo((byte)0));
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        object ended = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(ended, "AwaitingExecutionEnd"), Is.False);
        Assert.That((double)Get(ended, "Remaining"), Is.GreaterThan(3.5));
    }

    [UnityTest]
    public IEnumerator UniteEnergyAccumulatesBeforeUnlockAndSurvivesLearningForMigratedKits()
    {
        foreach (string name in new[] { "Glaceon", "Zoroark", "Cinderace", "Sylveon", "Cresselia", "Vaporeon", "Jolteon", "Flareon" })
        {
            object combat = PrepareMigratedActor(0, name);
            string uniteName = name == "Zoroark" ? "ZoroarkNightfallDaze" : name == "Cinderace" ? "BlazingBycicleKick" : name + "Unite";
            object moveId = E("AvailableMoves", uniteName);
            Assert.That(Call(combat, "Equip", moveId), Is.False);
            Assert.That(Call(Get(combat, "Loadout"), "Get", 2), Is.Null);
            Call(combat, "GrantUniteEnergy", 1000);
            Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(1000), name);
            Set(combat, "nextEnergyTick", (double)Get(combat, "Now") - 1);
            Call(combat, "Tick");
            Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(1900), name);
            Variable(actor, "currentLevel", 8);
            Assert.That(Call(combat, "Equip", moveId), Is.True);
            Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(1900), name);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator StartingFeintResetsExistingCutAndShadowClawCooldownsEvenWithoutAHit()
    {
        foreach (string name in new[] { "ZoroarkCut", "ZoroarkShadowClaw" })
        {
            object combat = PrepareMigratedActor(7, "Zoroark");
            object moveId = E("AvailableMoves", name);
            Assert.That(Call(combat, "Equip", moveId), Is.True);
            object ability = Call(Get(combat, "Loadout"), "Get", 0);
            int revision = (int)Get(ability, "Revision");
            Call(combat, "Receive", 1L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
            object coordinator = Get(combat, "Coordinator");
            object first = Call(coordinator, "FindRequest", 1L);
            float deadline = Time.time + 8;
            while (!(bool)Get(first, "Accepted") && Get(first, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
            Assert.That(Get(first, "Accepted"), Is.True);
            Call(combat, "CancelSlot", 0, revision);
            object cooling = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
            Assert.That((double)Get(cooling, "Remaining"), Is.GreaterThan(1));
            object feint = E("AvailableMoves", "ZoroarkFeintAttack");
            Assert.That(Call(combat, "Equip", feint), Is.True);
            object other = Call(Get(combat, "Loadout"), "Get", 1);
            Call(combat, "Receive", 2L, 1, (int)Get(other, "Revision"), feint, Vector3.right, ulong.MaxValue, 0L);
            object second = Call(coordinator, "FindRequest", 2L);
            deadline = Time.time + 8;
            while (!(bool)Get(second, "Accepted") && Get(second, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
            Assert.That(Get(second, "Accepted"), Is.True);
            object ready = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
            Assert.That(Get(ready, "Ready"), Is.True, name);
            Call(combat, "CancelSlot", 1, (int)Get(other, "Revision"));
        }
    }

    protected IEnumerator VerifyGlaceonBasicAttackIdleRecovery()
    {
#if UNITY_EDITOR
        foreach (int level in new[] { 0, 3 })
        {
            object combat = PrepareMigratedActor(level);
            string modelName = level == 0 ? "Eevee" : "Glaceon";
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/" + modelName + "/" + modelName + ".prefab"), actor.transform);
            var animator = model.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Component animations = actor.GetComponent(T("AnimationManager"));
            Call(animations, "AssignAnimator", animator);
            ((Behaviour)animations).enabled = true;
            string idle = level == 0 ? "ani_idle_bat_0133" : "ani_idle_bat_0471";
            string attackName = level == 0 ? "ani_atk4_bat_0133" : "ani_atk1_bat_0471";
            animator.Play(idle, 0, 0);
            animator.Update(0);
            Component target = Pokemon(Vector3.forward * 2);
            object character = Get(combat, "Character");
            var routine = (IEnumerator)Invoke(character, "BasicRecovery", target, attackName);
            ((MonoBehaviour)player).StartCoroutine(routine);
            yield return new WaitForSeconds((float)Call(moves, "GetAtkSpeedCooldown") * 0.5f + 0.15f);
            animator.Update(0.1f);
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.shortNameHash, Is.EqualTo(Animator.StringToHash(idle)), modelName);
        }
#else
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator GlaceonHeldChannelCancelsAndBeginsCooldownOnEnd()
    {
        object combat = PrepareMigratedActor(3);
        object character = Get(combat, "Character");
        Call(character, "SetCrystals", 8);
        object moveId = E("AvailableMoves", "GlaceIcycleSpear");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 0);
        int revision = (int)Get(ability, "Revision");
        Call(combat, "Receive", 1L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8;
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        yield return null;
        Assert.That(Get(character, "CanStartBasicAttack"), Is.False);
        object during = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(during, "AwaitingExecutionEnd"), Is.True);
        Call(combat, "CancelSlot", 0, revision);
        Assert.That(Get(character, "CanStartBasicAttack"), Is.True);
        object after = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(after, "AwaitingExecutionEnd"), Is.False);
        Assert.That((double)Get(after, "Remaining"), Is.GreaterThan(4));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator GlaceonFreezeDryIgnoresAlliesAndGrantsServerCrystals()
    {
        object combat = PrepareMigratedActor(5);
        Component ally = Pokemon(Vector3.forward, "Blue");
        Component enemy = Pokemon(Vector3.forward * 2);
        Physics.SyncTransforms();
        object moveId = E("AvailableMoves", "GlaceFreezeDry");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 1);
        int revision = (int)Get(ability, "Revision");
        Call(combat, "Receive", 1L, 1, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8;
        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        Assert.That(Hp(ally), Is.EqualTo(5000));
        Assert.That(Hp(enemy), Is.LessThan(5000));
        Assert.That(Get(Get(combat, "Character"), "Crystals"), Is.EqualTo((byte)2));
    }

    private object PrepareMigratedActor(int level, string name = "Glaceon")
    {
#if UNITY_EDITOR
        GameObject root = UnityEngine.Object.Instantiate(fixture);
        root.transform.position = Vector3.right * 10;
        objects.Add(root);
        actor = root.GetComponent(T("Pokemon"));
        player = root.GetComponent(T("PlayerManager"));
        moves = root.GetComponent(T("MovesController"));
        var definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/" + name + ".asset"));
        definitions.Add((ScriptableObject)definition);
        Set(actor, "baseStats", definition);
        Set(moves, "pokemon", actor);
        Set(moves, "playerManager", player);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1UL, true);
        Variable(actor, "currentHp", 5000);
        Variable(actor, "currentLevel", level);
        Call(actor, "UpdateTeamRPC", E("Team", "Blue"));
        root.transform.position = Vector3.zero;
#endif
        return Get(moves, "Combat");
    }

    [UnityTest]
    public IEnumerator CombatPipelineAcceptsOneExecutionAndRejectsStaleRevision()
    {
        object combat = PrepareCombatLoadout();
        object loadout = Get(combat, "Loadout");
        object ability = Call(loadout, "Get", 0);
        int revision = (int)Get(ability, "Revision");
        object moveId = E("AvailableMoves", "ZoroarkSlash");
        Call(combat, "Receive", 1L, 0, revision + 1, moveId, Vector3.forward, ulong.MaxValue, 0L);
        object coordinator = Get(combat, "Coordinator");
        Assert.That(Call(coordinator, "FindRequest", 1L), Is.Null);
        Call(combat, "Receive", 2L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        float deadline = Time.time + 8;
        object session = Call(coordinator, "FindRequest", 2L);
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        Call(combat, "Receive", 2L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        Assert.That(Call(coordinator, "FindRequest", 2L), Is.SameAs(session));
        Call(combat, "CancelSlot", 0, revision);
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(Get(coordinator, "ActiveCount"), Is.Zero);
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator CombatPipelineKeepsDuplicateBufferedRequestPendingUntilAcceptance()
    {
        object combat = PrepareCombatLoadout();
        object ability = Call(Get(combat, "Loadout"), "Get", 0);
        int revision = (int)Get(ability, "Revision");
        object moveId = E("AvailableMoves", "ZoroarkSlash");
        moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 1);
        Call(combat, "Receive", 1L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        Call(combat, "Receive", 1L, 0, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        var receipts = (IDictionary)combat.GetType().GetField("receipts", Private).GetValue(combat);
        Assert.That(receipts.Contains(1L), Is.False);
        moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 0);
        Call(combat, "Tick");
        object coordinator = Get(combat, "Coordinator");
        object session = Call(coordinator, "FindRequest", 1L);
        Assert.That(session, Is.Not.Null);
        float deadline = Time.time + 8;
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        Assert.That(receipts[1L], Is.EqualTo(true));
        Call(combat, "CancelSlot", 0, revision);
    }

    [UnityTest]
    public IEnumerator CombatPipelineCancelsQueuedRequestWithoutSpendingResources()
    {
        object combat = PrepareCombatLoadout();
        object ability = Call(Get(combat, "Loadout"), "Get", 0);
        int revision = (int)Get(ability, "Revision");
        moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 1);
        Call(combat, "Receive", 1L, 0, revision, E("AvailableMoves", "ZoroarkSlash"), Vector3.forward, ulong.MaxValue, 0L);
        Call(combat, "CancelSlot", 0, revision);
        moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 0);
        Call(combat, "Tick");
        Assert.That(Call(Get(combat, "Coordinator"), "FindRequest", 1L), Is.Null);
        var receipts = (IDictionary)combat.GetType().GetField("receipts", Private).GetValue(combat);
        Assert.That(receipts[1L], Is.EqualTo(false));
        object availability = Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(availability, "Ready"), Is.True);
        yield return null;
    }

    private object PrepareCombatLoadout()
    {
#if UNITY_EDITOR
        object original = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Zoroark.asset");
        Set(Get(actor, "BaseStats"), "learnableMoves", Get(original, "LearnableMoves"));
#endif
        object combat = Get(moves, "Combat");
        Assert.That(Call(combat, "Equip", E("AvailableMoves", "ZoroarkSlash")), Is.True);
        return combat;
    }

    [UnityTest]
    public IEnumerator CutDamagesOnceAndHealsForEachEnemy()
    {
        var first = Pokemon(Vector3.forward * 2); var second = Pokemon(new Vector3(0.4f, 0, 2));
        Variable(actor, "currentHp", 3000); Physics.SyncTransforms(); Effect("ZoroarkCut");
        yield return new WaitForSeconds(0.6f);
        Assert.That(Hp(first), Is.EqualTo(4616)); Assert.That(Hp(second), Is.EqualTo(4616));
        Assert.That(Hp(actor), Is.EqualTo(3422));
        Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator FurySwipesHasExactlyOneHitGatedRecast()
    {
        Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); var effect = Effect("ZoroarkFurySwipes");
        yield return new WaitForSeconds(0.5f);
        Assert.That(Call(effect, "TryRecast", Vector3.back, 0ul), Is.True);
        Assert.That(Call(effect, "TryRecast", Vector3.forward, 0ul), Is.False);
        float deadline = Time.time + 2;
        while ((bool)Get(effect, "IsSpawned") && Time.time < deadline) yield return null;
        Assert.That(Get(effect, "IsSpawned"), Is.False);
        actor.transform.position = Vector3.right * 20; effect = Effect("ZoroarkFurySwipes");
        yield return new WaitForSeconds(0.5f);
        Assert.That(Get(effect, "IsSpawned"), Is.False);
    }

    [UnityTest]
    public IEnumerator NightDashCountsOnceForMultipleTargetsThenRequiresSpecialBasic()
    {
        var first = Pokemon(Vector3.forward * 2); var second = Pokemon(new Vector3(0.4f, 0, 2)); Physics.SyncTransforms();
        var effect = Effect("ZoroarkNightSlash"); Call(effect, "Movement", Vector2.up);
        yield return new WaitForSeconds(0.5f);
        Assert.That(Hp(first), Is.LessThan(5000)); Assert.That(Hp(second), Is.LessThan(5000));
        Assert.That(Get(effect, "Execution").GetType().GetField("steps", Private).GetValue(Get(effect, "Execution")), Is.EqualTo(1));
        Assert.That(Get(effect, "UniqueReady"), Is.True);
        Call(effect, "Movement", Vector2.up); yield return null;
        Assert.That(Get(effect, "IsSpawned"), Is.True, "Holding a direction must preserve the earned special basic.");
        Assert.That(Get(effect, "UniqueReady"), Is.True);
        Call(effect, "UniqueAttackLanded", false);
        Call(effect, "Movement", Vector2.up); yield return null;
        Assert.That(Get(effect, "IsSpawned"), Is.False, "Movement can end a failed chain with no dash or special basic available.");
        Assert.That(Get(moves, "ZoroarkUniqueReady"), Is.False);
    }

    [UnityTest]
    public IEnumerator NightFinaleCrossDoesNotDoubleDamageButCanHealTwice()
    {
        var target = Pokemon(Vector3.forward * 0.25f); Physics.SyncTransforms(); Variable(actor, "currentHp", 3000);
        var effect = Effect("ZoroarkNightSlash");
        for (int i = 0; i < 6; i++) Call(effect, "ValidHit", false);
        yield return new WaitForSeconds(1.1f);
        Assert.That(Hp(target), Is.EqualTo(5000)); Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "Invincible")), Is.True);
        yield return new WaitForSeconds(0.55f);
        Assert.That(Hp(target), Is.EqualTo(4214)); Assert.That(Hp(actor), Is.EqualTo(3600));
        Assert.That(Get(effect, "IsSpawned"), Is.False); Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
        Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "Invincible")), Is.False);
    }

    [UnityTest]
    public IEnumerator NightAerialSlotARepositionsWithoutDamageAndCanBeSpentOnlyOnce()
    {
        var target = Pokemon(Vector3.forward); Physics.SyncTransforms();
        var effect = Effect("ZoroarkNightSlash");
        for (int i = 0; i < 6; i++) Call(effect, "ValidHit", false);
        yield return new WaitForSeconds(0.15f);
        Assert.That(Call(effect, "TrySpendAerialReposition"), Is.True);
        Assert.That(Call(effect, "TrySpendAerialReposition"), Is.False);
        Effect("ZoroarkCut"); yield return new WaitForSeconds(0.5f);
        Assert.That(Hp(target), Is.EqualTo(5000), "The airborne Cut is movement only.");
        Assert.That(actor.transform.position.z, Is.EqualTo(4).Within(0.15f));
        yield return new WaitForSeconds(1);
        Assert.That(Get(effect, "IsSpawned"), Is.False);
        Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator NightJumpCanSteerItsLandingWithinASmallBudgetAndStopsAfterLanding()
    {
        var target = Pokemon(Vector3.right * 2.5f); Physics.SyncTransforms();
        var effect = Effect("ZoroarkNightSlash");
        for (int i = 0; i < 6; i++) Call(effect, "ValidHit", false);
        Set(Get(Get(moves, "Combat"), "Character"), "zoroarkMovement", Vector2.right);
        yield return new WaitForSeconds(1.65f);
        Assert.That(actor.transform.position.x, Is.InRange(1.35f, 1.55f));
        Assert.That(Hp(target), Is.EqualTo(4214), "The cross must sample the steered landing, not the jump's original position.");
        var landed = actor.transform.position;
        Set(Get(Get(moves, "Combat"), "Character"), "zoroarkMovement", Vector2.left);
        yield return new WaitForSeconds(0.2f);
        Assert.That(actor.transform.position, Is.EqualTo(landed));
    }

    [UnityTest]
    public IEnumerator FeintCountsOnePerCastAndMarkMovesToOtherMoveOrigin()
    {
        Pokemon(Vector3.forward * 2); Pokemon(new Vector3(0.4f, 0, 2)); Physics.SyncTransforms();
        var effect = Effect("ZoroarkFeintAttack"); yield return new WaitForSeconds(0.5f);
        Assert.That(Get(effect, "Execution").GetType().GetField("steps", Private).GetValue(Get(effect, "Execution")), Is.EqualTo(1));
        Vector3 mark = new Vector3(2, 0, 6); Call(effect, "MoveMark", mark);
        Assert.That(Get(effect, "Execution").GetType().GetField("mark", Private).GetValue(Get(effect, "Execution")), Is.EqualTo(mark));
        Call(effect, "ValidHit", false); Call(effect, "ValidHit", false); Call(effect, "ValidHit", false);
        Assert.That(Get(moves, "ZoroarkFeintReady"), Is.True);
        Assert.That(Call(effect, "TryRecast", Vector3.forward, ulong.MaxValue), Is.False);
    }

    [UnityTest]
    public IEnumerator FeintFinisherUsesMovementBounceAndCannotDoubleHitItsPrimary()
    {
        var target = Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var effect = Effect("ZoroarkFeintAttack"); yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < 3; i++) Call(effect, "ValidHit", false);
        Set(Get(Get(moves, "Combat"), "Character"), "zoroarkMovement", Vector2.down);
        Assert.That(Call(effect, "TryRecast", Vector3.forward, Get(target, "NetworkObjectId")), Is.True);
        yield return new WaitForSeconds(0.95f);
        Assert.That(Hp(target), Is.EqualTo(3540));
        Assert.That(actor.transform.position.z, Is.LessThan(0));
        Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
        Assert.That(Get(effect, "IsSpawned"), Is.False);
    }

    [UnityTest]
    public IEnumerator FeintFinisherPausesBeforeLungingAndRechecksItsSelectedTarget()
    {
        var target = Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var effect = Effect("ZoroarkFeintAttack"); yield return new WaitForSeconds(0.5f);
        for (int i = 0; i < 3; i++) Call(effect, "ValidHit", false);
        Vector3 start = actor.transform.position;
        int before = Hp(target);
        Assert.That(Call(effect, "TryRecast", Vector3.forward, Get(target, "NetworkObjectId")), Is.True);
        yield return new WaitForSeconds(0.06f);
        Assert.That(actor.transform.position, Is.EqualTo(start), "The finisher windup must remain stationary.");
        Assert.That(Hp(target), Is.EqualTo(before));
        Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(1));
        Variable(target, "currentHp", 0);
        yield return new WaitForSeconds(0.25f);
        Assert.That(Get(effect, "IsSpawned"), Is.False);
        Assert.That(actor.transform.position, Is.EqualTo(start));
        Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator SecondaryWindowTracksFuryRecastAndClearsWhenSpent()
    {
        Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var effect = Effect("ZoroarkFurySwipes");
        yield return new WaitForSeconds(0.5f);
        Assert.That(Get(moves, "ZoroarkWindowMove"), Is.EqualTo(E("AvailableMoves", "ZoroarkFurySwipes")));
        float before = (float)Get(moves, "ZoroarkWindowRemaining");
        Assert.That(before, Is.InRange(3.4f, 4f));
        yield return new WaitForSeconds(0.15f);
        Assert.That((float)Get(moves, "ZoroarkWindowRemaining"), Is.LessThan(before));
        Assert.That(Call(effect, "TryRecast", Vector3.back, 0ul), Is.True);
        Assert.That(Get(moves, "ZoroarkWindowRemaining"), Is.EqualTo(0f));
    }

    [UnityTest]
    public IEnumerator SecondaryComboWindowsRefreshOnHitsAndClearForFinishersAndDeath()
    {
        var night = Effect("ZoroarkNightSlash");
        Assert.That((float)Get(moves, "ZoroarkWindowRemaining"), Is.InRange(2.4f, 2.5f));
        yield return new WaitForSeconds(0.3f);
        float before = (float)Get(moves, "ZoroarkWindowRemaining");
        Call(night, "ValidHit", false);
        Assert.That((float)Get(moves, "ZoroarkWindowRemaining"), Is.GreaterThan(before + 0.2f));
        for (int i = 0; i < 5; i++) Call(night, "ValidHit", false);
        yield return null; yield return null;
        Assert.That(Get(moves, "ZoroarkWindowRemaining"), Is.EqualTo(0f));
        Call(night, "Despawn");
        var target = Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var feint = Effect("ZoroarkFeintAttack");
        yield return new WaitForSeconds(0.5f);
        Assert.That((float)Get(moves, "ZoroarkWindowRemaining"), Is.InRange(2.5f, 3f));
        for (int i = 0; i < 3; i++) Call(feint, "ValidHit", false);
        Assert.That(Call(feint, "TryRecast", Vector3.forward, Get(target, "NetworkObjectId")), Is.True);
        Assert.That(Get(moves, "ZoroarkWindowRemaining"), Is.EqualTo(0f));
        Variable(actor, "currentHp", 0); yield return null;
        Assert.That(Get(moves, "ZoroarkWindowRemaining"), Is.EqualTo(0f));
    }

    [UnityTest]
    public IEnumerator AuthoritativeCutAndShadowClawResetClearsTheRunningHudCooldown()
    {
#if UNITY_EDITOR
        var uiRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MoveUI.prefab"));
        objects.Add(uiRoot);
        var ui = uiRoot.GetComponent(T("MoveUI"));
        var background = (GameObject)ui.GetType().GetField("cdBg", Private).GetValue(ui);
        var line = (GameObject)ui.GetType().GetField("cdLine", Private).GetValue(ui);
        var text = (Component)ui.GetType().GetField("cdText", Private).GetValue(ui);
        var endsAt = ui.GetType().GetField("cooldownEndsAt", Private);
        var holderRoot = Node("Inactive cooldown HUD routing");
        holderRoot.SetActive(false);
        var holder = holderRoot.AddComponent(T("MovesHolderUI"));
        var moveUis = Array.CreateInstance(T("MoveUI"), 2);
        moveUis.SetValue(ui, 0);
        moveUis.SetValue(ui, 1);
        Set(holder, "moveUIs", moveUis);
        var battle = holderRoot.AddComponent(T("BattleUIManager"));
        Set(battle, "currentUI", holder);
        var singleton = T("BattleUIManager").GetField("instance");
        object previousBattle = singleton.GetValue(null);
        try
        {
            singleton.SetValue(null, battle);
            foreach (string name in new[] { "ZoroarkCut", "ZoroarkShadowClaw" })
            {
                object combat = PrepareMigratedActor(7, "Zoroark");
                object moveId = E("AvailableMoves", name);
                Assert.That(Call(combat, "Equip", moveId), Is.True);
                var runtimeMove = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { moveId });
                var slots = (Array)moves.GetType().GetField("moves", Private).GetValue(moves);
                slots.SetValue(runtimeMove, 0);
                var statuses = (Array)moves.GetType().GetField("moveStatuses", Private).GetValue(moves);
                statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), 0);
                object snapshot = Activator.CreateInstance(T("CombatSlotSnapshot"));
                snapshot.GetType().GetField("MoveId").SetValue(snapshot, moveId);
                snapshot.GetType().GetField("CooldownUntil").SetValue(snapshot, (double)Get(combat, "Now") + 8);
                try
                {
                    player.GetType().GetProperty("IsOwner").SetValue(player, true);
                    moves.GetType().GetProperty("IsOwner").SetValue(moves, true);
                    Call(combat, "ApplySnapshot", 0, snapshot);
                    Assert.That(background.activeSelf, Is.True, name);
                    Assert.That((float)endsAt.GetValue(ui), Is.GreaterThan(Time.time), name);
                    snapshot.GetType().GetField("CooldownUntil").SetValue(snapshot, 0d);
                    Call(combat, "ApplySnapshot", 0, snapshot);
                    Assert.That(background.activeSelf, Is.False, name);
                    Assert.That(line.activeSelf, Is.False, name);
                    Assert.That(text.gameObject.activeSelf, Is.False, name);
                    Assert.That((float)endsAt.GetValue(ui), Is.LessThanOrEqualTo(Time.time), name);
                }
                finally
                {
                    player.GetType().GetProperty("IsOwner").SetValue(player, false);
                    moves.GetType().GetProperty("IsOwner").SetValue(moves, false);
                }
                yield return null;
                Assert.That(background.activeSelf, Is.False, "The old timer must not resume after the reset.");
            }
        }
        finally { singleton.SetValue(null, previousBattle); }
#else
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator MoveUiSecondaryRingCountsDownRefreshesAndHidesWhenConsumed()
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MoveUI.prefab"));
        objects.Add(root);
        var ui = root.GetComponent(T("MoveUI"));
        var ring = (UnityEngine.UI.Image)ui.GetType().GetField("secondaryCd", Private).GetValue(ui);
        Call(ui, "ShowSecondaryCooldown", 1f);
        yield return new WaitForSeconds(0.15f);
        Assert.That(ring.gameObject.activeSelf, Is.True);
        Assert.That(ring.fillAmount, Is.InRange(0.5f, 0.99f));
        Call(ui, "ShowSecondaryCooldown", 3f);
        Assert.That(ring.fillAmount, Is.EqualTo(1f).Within(0.01f));
        Call(ui, "ShowSecondaryCooldown", 0f);
        Assert.That(ring.gameObject.activeSelf, Is.False);
#else
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator ThreeBasicsBoostAndUniqueBasicAddsOnlyOneComboStep()
    {
        var target = Pokemon(Vector3.forward * 1.5f); Physics.SyncTransforms();
        var attack = Get(moves, "ServerBasicAttack");
        for (int i = 0; i < 3; i++) { Call(attack, "PerformOnServer", target); yield return new WaitForSeconds(0.4f); }
        Assert.That(Hp(target), Is.EqualTo(4670));
        var effect = Effect("ZoroarkNightSlash");
        Set(Get(effect, "Execution"), "uniqueReady", true);
        Call(attack, "PerformOnServer", target); yield return new WaitForSeconds(0.4f);
        Assert.That(Get(effect, "Execution").GetType().GetField("steps", Private).GetValue(Get(effect, "Execution")), Is.EqualTo(1));
        Assert.That(Get(effect, "UniqueReady"), Is.False);
    }

    [UnityTest]
    public IEnumerator UnitePulsesHaveIncreasingRadiiAndCleanUpLocks()
    {
        var near = Pokemon(Vector3.forward * 2); var far = Pokemon(Vector3.forward * 4); Physics.SyncTransforms();
        var effect = Effect("ZoroarkNightfallDaze");
        yield return new WaitForSeconds(1.6f);
        Assert.That(Hp(near), Is.EqualTo(3236)); Assert.That(Hp(far), Is.EqualTo(4412));
        Assert.That(Get(actor, "ShieldHp"), Is.EqualTo(1000)); Assert.That(Get(effect, "IsSpawned"), Is.False);
        Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator UniteDeathRefundsNinetyPercentOnlyBeforeTheFirstPulseIncludingOwnerSnapshot()
    {
        foreach (bool reachCheckpoint in new[] { false, true })
        {
            object combat = PrepareMigratedActor(14, "Zoroark");
            Set(combat, "nextEnergyTick", (double)Get(combat, "Now") + 60);
            object moveId = E("AvailableMoves", "ZoroarkNightfallDaze");
            if (Host)
            {
                Call(actor.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", 0UL);
                var statuses = Array.CreateInstance(T("BattleActionStatus"), 2);
                for (int slot = 0; slot < 2; slot++) statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), slot);
                Set(moves, "moveStatuses", statuses);
                object bridge = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { moveId });
                Set(moves, "uniteMove", bridge);
                Set(moves, "uniteMoveMaxCharge", 80000);
            }
            Assert.That(Call(combat, "Equip", moveId), Is.True);
            Call(combat, "GrantUniteEnergy", 80000);
            int revision = (int)Get(Call(Get(combat, "Loadout"), "Get", 2), "Revision");
            Call(combat, "Receive", 1L, 2, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
            object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
            float deadline = Time.time + 8;
            while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
                yield return null;
            Assert.That(Get(session, "Accepted"), Is.True);
            Assert.That(Get(combat, "UniteEnergy"), Is.Zero);
            Component effect = ((IEnumerable)Get(combat, "PrefabEffects")).Cast<Component>().Single();
            if (reachCheckpoint)
                while (!(bool)Get(effect, "HasSucceeded") && Time.time < deadline) yield return null;
            Assert.That(Get(effect, "HasSucceeded"), Is.EqualTo(reachCheckpoint));

            Variable(actor, "currentHp", 0);
            Call(combat, "Tick");
            int expectedEnergy = reachCheckpoint ? 0 : 72000;
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
            Assert.That(Get(session, "Outcome").ToString(), Is.EqualTo("Interrupted"));
            Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(expectedEnergy));
            Assert.That(Get(moves.GetType().GetField("combatUniteEnergy", Private).GetValue(moves), "Value"), Is.EqualTo(expectedEnergy));
            if (Host)
            {
                Assert.That(Get(moves, "UniteMoveCharge"), Is.EqualTo(expectedEnergy));
                object status = moves.GetType().GetField("uniteMoveStatus", Private).GetValue(moves);
                Assert.That(Call(status, "HasStatus", E("ActionStatusType", "Cooldown")), Is.True);
            }
            Call(combat, "Tick");
            Call(combat, "Receive", 1L, 2, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
            Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(expectedEnergy), "Duplicate requests and cleanup must not refund twice.");
            Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
        }
    }

    [UnityTest]
    public IEnumerator RejectedUniteBeforeSpendingRetainsAllEnergy()
    {
        object combat = PrepareMigratedActor(14, "Zoroark");
        object moveId = E("AvailableMoves", "ZoroarkNightfallDaze");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        Call(combat, "GrantUniteEnergy", 80000);
        int revision = (int)Get(Call(Get(combat, "Loadout"), "Get", 2), "Revision");
        Call(combat, "Receive", 1L, 2, revision + 1, moveId, Vector3.forward, ulong.MaxValue, 0L);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(80000));
        Assert.That(Call(Get(combat, "Coordinator"), "FindRequest", 1L), Is.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator IllusionPrefersPlayersSnapshotsHpAndBreaksOnDamage()
    {
        var enemy = Pokemon(Vector3.forward * 20); var wild = Pokemon(Vector3.forward);
        wild.GetType().GetProperty("Type").SetValue(wild, E("PokemonType", "Wild"));
        Variable(enemy, "currentHp", 3200);
        Invoke(moves, "ActivateZoroarkIllusion");
        Assert.That(Get(moves, "HasZoroarkDisguise"), Is.True);
        Assert.That(Get(moves, "ZoroarkDisguiseSource"), Is.SameAs(enemy));
        object runtime = Get(Get(moves, "Combat"), "Character");
        Assert.That(runtime.GetType().GetField("illusionHp", Private).GetValue(runtime), Is.EqualTo(3200));
        Variable(enemy, "currentHp", 1000);
        Assert.That(runtime.GetType().GetField("illusionHp", Private).GetValue(runtime), Is.EqualTo(3200));
        Call(actor, "ResolveDamage", Damage(enemy, 100));
        Assert.That(Get(moves, "HasZoroarkDisguise"), Is.False);
        Invoke(moves, "ActivateZoroarkIllusion");
        Assert.That(Get(moves, "HasZoroarkDisguise"), Is.False, "Illusion's cooldown is not cleared by breaking its disguise.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator IllusionAvailabilityDoesNotDependOnBeingInAGoalZone()
    {
        var scoring = Get(player, "ScoreStatus");
        Call(scoring, "AddStatus", E("ActionStatusType", "Disabled"));
        Assert.That(Invoke(moves, "CanUseZoroarkIllusion"), Is.True, "Illusion must work outside goals.");
        Call(scoring, "RemoveStatus", E("ActionStatusType", "Disabled"));
        Assert.That(Invoke(moves, "CanUseZoroarkIllusion"), Is.True, "Entering a goal must not change availability.");
        Call(Get(moves, "BasicAttackStatus"), "AddStatus", E("ActionStatusType", "Disabled"));
        Assert.That(Invoke(moves, "CanUseZoroarkIllusion"), Is.False, "Match-wide action disabling must still be respected.");
        yield return null;
    }

    [UnityTest]
    public IEnumerator NightSixthDashCannotBeCancelledByHeldMovementBeforeTheLeap()
    {
        Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var effect = Effect("ZoroarkNightSlash");
        for (int i = 0; i < 5; i++) Call(effect, "ValidHit", false);
        Call(effect, "Movement", Vector2.up);
        float deadline = Time.time + 2;
        while ((int)Get(effect, "Execution").GetType().GetField("steps", Private).GetValue(Get(effect, "Execution")) < 6 && Time.time < deadline)
        {
            Call(effect, "Movement", Vector2.up);
            yield return null;
        }
        Assert.That(Get(effect, "Execution").GetType().GetField("steps", Private).GetValue(Get(effect, "Execution")), Is.EqualTo(6));
        Call(effect, "Movement", Vector2.up);
        yield return null;
        Assert.That(Get(effect, "IsSpawned"), Is.True);
        Assert.That(Get(effect, "Finishing"), Is.True);
        yield return new WaitForSeconds(1.6f);
        Assert.That(Get(effect, "IsSpawned"), Is.False);
        Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator NightCanChainAcrossTargetsWithHeldDirectionsAndRepeatAfterSuccess()
    {
        var targets = new[] { Pokemon(Vector3.forward * 2), Pokemon(new Vector3(2, 0, 4)), Pokemon(new Vector3(4, 0, 2)) };
        var directions = new[] { Vector2.up, Vector2.right, Vector2.down };
        var attack = Get(moves, "ServerBasicAttack");
        for (int cycle = 0; cycle < 2; cycle++)
        {
            actor.transform.position = Vector3.zero;
            foreach (var target in targets) Variable(target, "currentHp", 5000);
            Physics.SyncTransforms();
            var effect = Effect("ZoroarkNightSlash");
            for (int step = 0; step < 3; step++)
            {
                float deadline = Time.time + 2;
                while (!(bool)Get(effect, "UniqueReady") && (bool)Get(effect, "IsSpawned") && Time.time < deadline)
                {
                    Call(effect, "Movement", directions[step]);
                    yield return null;
                }
                Assert.That(Get(effect, "IsSpawned"), Is.True, "The held dash direction must not cancel its follow-up.");
                Assert.That(Get(effect, "UniqueReady"), Is.True);
                Assert.That(Get(effect, "Execution").GetType().GetField("steps", Private).GetValue(Get(effect, "Execution")), Is.EqualTo(step * 2 + 1));
                Call(effect, "Movement", directions[step]);
                Call(attack, "PerformOnServer", targets[step]);
                deadline = Time.time + 1;
                while ((bool)Get(effect, "UniqueReady") && Time.time < deadline)
                {
                    Call(effect, "Movement", directions[step]);
                    yield return null;
                }
                Assert.That(Get(effect, "Execution").GetType().GetField("steps", Private).GetValue(Get(effect, "Execution")), Is.EqualTo(step * 2 + 2));
                // Change direction during recovery, without releasing to neutral.
                deadline = Time.time + 1;
                while ((int)Get(moves, "ServerActionLocks") > 0 && Time.time < deadline)
                {
                    Call(effect, "Movement", directions[Math.Min(step + 1, 2)]);
                    yield return null;
                }
            }
            float finishDeadline = Time.time + 1;
            while (!(bool)Get(effect, "Finishing") && Time.time < finishDeadline)
            {
                Call(effect, "Movement", Vector2.down);
                yield return null;
            }
            Assert.That(Get(effect, "Finishing"), Is.True);
            yield return new WaitForSeconds(1.6f);
            Assert.That(Get(effect, "IsSpawned"), Is.False);
            Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
            Assert.That(Get(moves, "ServerBasicAttack"), Is.SameAs(attack), "The same attack definition must work for the next combo.");
        }
    }

    [UnityTest]
    public IEnumerator HoldingMovementWithoutAnotherHitDoesNotKeepNightSlashAliveForever()
    {
        Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        var effect = Effect("ZoroarkNightSlash");
        float deadline = Time.time + 4;
        while ((bool)Get(effect, "IsSpawned") && Time.time < deadline)
        {
            Call(effect, "Movement", Vector2.up);
            yield return null;
        }
        Assert.That(Get(effect, "IsSpawned"), Is.False);
        Assert.That(Get(moves, "ZoroarkUniqueReady"), Is.False);
    }

    [UnityTest]
    public IEnumerator EvolutionBreaksIllusionWithoutClientSideModelEvents()
    {
        Pokemon(Vector3.forward * 10);
        Invoke(moves, "ActivateZoroarkIllusion");
        Assert.That(Get(moves, "HasZoroarkDisguise"), Is.True);
        Variable(actor, "currentLevel", 4);
        Invoke(moves, "UpdateZoroarkRuntime");
        Assert.That(Get(moves, "HasZoroarkDisguise"), Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DeathReleasesChainMovementAndStatusState()
    {
        var effect = Effect("ZoroarkNightSlash");
        for (int i = 0; i < 6; i++) Call(effect, "ValidHit", false);
        yield return new WaitForSeconds(0.2f);
        Variable(actor, "currentHp", 0); yield return null;
        Assert.That(Get(effect, "IsSpawned"), Is.False); Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
        Assert.That(Get(moves, "ZoroarkUniqueReady"), Is.False);
        Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "Invincible")), Is.False);
    }

    [UnityTest]
    public IEnumerator CinderaceProjectileRetainsItsAssetAfterCastRecoveryAndReleasesOnDestruction()
    {
        foreach (string kind in new[] { "Ember", "Unite" })
        {
            object combat = PrepareMigratedActor(8, "Cinderace");
            float range = kind == "Ember" ? 7.5f : 10.5f;
            Component enemy = Pokemon(Vector3.forward * range);
            enemy.transform.position = Vector3.forward * range;
            Physics.SyncTransforms();
            int initialHp = Hp(enemy);
            int slot = kind == "Ember" ? 0 : 2;
            object moveId = E("AvailableMoves", kind == "Ember" ? "CinderEmber" : "BlazingBycicleKick");
            Assert.That(Call(combat, "Equip", moveId), Is.True);
            Call(combat, "GrantUniteEnergy", int.MaxValue);
            object ability = Call(Get(combat, "Loadout"), "Get", slot);
            Call(combat, "Receive", 1L, slot, (int)Get(ability, "Revision"), moveId, Vector3.forward, (ulong)Get(enemy, "NetworkObjectId"), 0L);
            object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
            float deadline = Time.time + 8;
            while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
                yield return null;
            Assert.That(Get(session, "Accepted"), Is.True, kind);
            var effect = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Single(component => T("CinderMoveObject").IsInstanceOfType(component)
                    && component.GetType().GetField("kind", Private).GetValue(component).Equals(E("CinderCast", kind)));
            Assert.That(effect.GetType().GetField("holdsAsset", Private).GetValue(effect), Is.True, kind);
            object retained = effect.GetType().GetField("handle", Private).GetValue(effect);
            while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
                yield return null;
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"), kind);
            Assert.That(effect != null && (bool)Get(effect, "IsSpawned"), Is.True, "The projectile must outlive its cast recovery: " + kind);
            Assert.That(Call(retained, "IsValid"), Is.True, "The cast scope must not release the live projectile's last asset lease: " + kind);
            while (effect != null && (bool)Get(effect, "IsSpawned") && Time.time < deadline)
                yield return null;
            Assert.That(Hp(enemy), Is.LessThan(initialHp), "The projectile must still hit after recovery: " + kind);
            Assert.That(effect == null || !(bool)Get(effect, "IsSpawned"), Is.True, kind);
            yield return null;
            Assert.That(Call(retained, "IsValid"), Is.False, "Destroying the final instance must release its independent lease: " + kind);
        }
    }

    [UnityTest]
    public IEnumerator CinderaceMoveCommitFillsBoostedChargeAndBasicAttacksCycleAndDetonatePassive()
    {
        object combat = PrepareMigratedActor(10, "Cinderace");
        object character = Get(combat, "Character");
        Component enemy = Pokemon(Vector3.forward * 2);
        Physics.SyncTransforms();

        object emberId = E("AvailableMoves", "CinderEmber");
        Assert.That(Call(combat, "Equip", emberId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 0);
        int revision = (int)Get(ability, "Revision");
        Call(combat, "Receive", 1L, 0, revision, emberId, Vector3.forward, (ulong)Get(enemy, "NetworkObjectId"), 0L);
        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8;
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        // Move commit immediately fills boosted attack charge to 2.
        Assert.That(Get(character, "Charge"), Is.EqualTo((byte)2));
        Assert.That(Get(moves, "CinderaceAttackCharge"), Is.EqualTo(2));
        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;

        // Wait for Ember projectile to hit and apply 2 Cinders marks + Burn.
        deadline = Time.time + 3;
        while ((int)Call(character, "GetMarks", enemy) < 2 && Time.time < deadline) yield return null;
        Assert.That(Call(character, "GetMarks", enemy), Is.EqualTo(2));
        Assert.That(Call(character, "HasBurn", enemy), Is.True);

        // Perform 3 server basic attacks: boosted (2 -> 0, +2 marks = 4), normal (0 -> 1, +1 mark = 5 -> Flare detonation -> 0), normal (1 -> 2, +1 mark = 1).
        Call(character, "PerformBasicAttack", enemy);
        Assert.That(Get(character, "Charge"), Is.EqualTo((byte)0));
        deadline = Time.time + 3;
        while ((int)Call(character, "GetMarks", enemy) < 4 && Time.time < deadline) yield return null;
        Assert.That(Call(character, "GetMarks", enemy), Is.EqualTo(4));

        int hpBeforeFlare = Hp(enemy);
        Call(character, "PerformBasicAttack", enemy);
        Assert.That(Get(character, "Charge"), Is.EqualTo((byte)1));
        deadline = Time.time + 3;
        while ((int)Call(character, "GetMarks", enemy) != 0 && Time.time < deadline) yield return null;
        Assert.That(Call(character, "GetMarks", enemy), Is.EqualTo(0));
        Assert.That(Hp(enemy), Is.LessThan(hpBeforeFlare - 150));

        Call(character, "PerformBasicAttack", enemy);
        Assert.That(Get(character, "Charge"), Is.EqualTo((byte)2));
    }

    [UnityTest]
    public IEnumerator CinderaceImmobilizedRejectsDashMovesWithoutSpendingCooldownAndAllowsNonDashMoves()
    {
        object combat = PrepareMigratedActor(7, "Cinderace");
        object flameChargeId = E("AvailableMoves", "CinderFlameCharge");
        object feintId = E("AvailableMoves", "CinderFeint");
        Assert.That(Call(combat, "Equip", flameChargeId), Is.True);
        object slotB = Call(Get(combat, "Loadout"), "Get", 1);
        int revB = (int)Get(slotB, "Revision");

        object immobilized = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Immobilized"), 3f, true, (ushort)0);
        Call(actor, "AddStatusEffect", immobilized);

        Call(combat, "Receive", 1L, 1, revB, flameChargeId, Vector3.forward, ulong.MaxValue, 0L);
        object coordinator = Get(combat, "Coordinator");
        object dashSession = Call(coordinator, "FindRequest", 1L);
        if (dashSession != null)
        {
            float deadline = Time.time + 4;
            while (Get(dashSession, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
            Assert.That(Get(dashSession, "Accepted"), Is.False);
        }
        object dashAvail = Call(Get(slotB, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(dashAvail, "Ready"), Is.True, "Rejected immobilized dash must not spend cooldown.");

        // Non-dash move (Pyro Ball) is still allowed while immobilized.
        object pyroBallId = E("AvailableMoves", "CinderPyroball");
        Assert.That(Call(combat, "Equip", pyroBallId), Is.True);
        object slotA = Call(Get(combat, "Loadout"), "Get", 0);
        int revA = (int)Get(slotA, "Revision");
        Call(combat, "Receive", 2L, 0, revA, pyroBallId, Vector3.forward, ulong.MaxValue, 0L);
        object pyroSession = Call(coordinator, "FindRequest", 2L);
        float wait = Time.time + 8;
        while (!(bool)Get(pyroSession, "Accepted") && Get(pyroSession, "Phase").ToString() != "Ended" && Time.time < wait) yield return null;
        Assert.That(Get(pyroSession, "Accepted"), Is.True);
        Call(combat, "CancelSlot", 0, revA);

        // Non-dash mobility move (Feint) is also allowed while immobilized.
        object feintCombat = PrepareMigratedActor(12, "Cinderace");
        Call(actor, "AddStatusEffect", Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Immobilized"), 3f, true, (ushort)0));
        Assert.That(Call(feintCombat, "Equip", feintId), Is.True);
        object feintSlot = Call(Get(feintCombat, "Loadout"), "Get", 1);
        int feintRev = (int)Get(feintSlot, "Revision");
        Call(feintCombat, "Receive", 1L, 1, feintRev, feintId, Vector3.forward, ulong.MaxValue, 0L);
        object feintSession = Call(Get(feintCombat, "Coordinator"), "FindRequest", 1L);
        wait = Time.time + 8;
        while (!(bool)Get(feintSession, "Accepted") && Get(feintSession, "Phase").ToString() != "Ended" && Time.time < wait) yield return null;
        Assert.That(Get(feintSession, "Accepted"), Is.True);
        Call(feintCombat, "CancelSlot", 1, feintRev);
    }

    [UnityTest]
    public IEnumerator CinderaceUpgradedPyroBallFlameChargeAndFeintApplyServerEmpowermentsAndReduceSlotBCooldown()
    {
        object combat = PrepareMigratedActor(12, "Cinderace");
        object character = Get(combat, "Character");
        Component enemy = Pokemon(Vector3.forward * 2);
        Physics.SyncTransforms();

        object pyroId = E("AvailableMoves", "CinderPyroball");
        object flameId = E("AvailableMoves", "CinderFlameCharge");
        Assert.That(Call(combat, "Equip", pyroId), Is.True);
        Assert.That(Call(combat, "Equip", flameId), Is.True);
        object slotA = Call(Get(combat, "Loadout"), "Get", 0);
        object slotB = Call(Get(combat, "Loadout"), "Get", 1);

        // Cast upgraded Flame Charge: dashes, damages enemy, and arms FlameSlowArmed.
        int revB = (int)Get(slotB, "Revision");
        Call(combat, "Receive", 1L, 1, revB, flameId, Vector3.forward, ulong.MaxValue, 0L);
        object dashSession = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8;
        while (Get(dashSession, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(dashSession, "Accepted"), Is.True);
        Assert.That(Get(character, "FlameSlowArmed"), Is.True);
        Assert.That(Get(character, "Charge"), Is.EqualTo((byte)2));

        double remainingBeforePyro = (double)Get(Call(Get(slotB, "Resources"), "Read", (double)Get(combat, "Now")), "Remaining");
        Assert.That(remainingBeforePyro, Is.GreaterThan(4.5));

        // Reset actor position and cast upgraded Pyro Ball into enemy: reduces Slot B cooldown by 4s.
        actor.transform.position = Vector3.zero;
        enemy.transform.position = Vector3.forward * 2;
        Physics.SyncTransforms();
        int revA = (int)Get(slotA, "Revision");
        Call(combat, "Receive", 2L, 0, revA, pyroId, Vector3.forward, ulong.MaxValue, 0L);
        object pyroSession = Call(Get(combat, "Coordinator"), "FindRequest", 2L);
        deadline = Time.time + 8;
        while (!(bool)Get(pyroSession, "Accepted") && Get(pyroSession, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(pyroSession, "Accepted"), Is.True);
        deadline = Time.time + 3;
        while ((double)Get(Call(Get(slotB, "Resources"), "Read", (double)Get(combat, "Now")), "Remaining") > remainingBeforePyro - 2.5 && Time.time < deadline)
            yield return null;
        double remainingAfterPyro = (double)Get(Call(Get(slotB, "Resources"), "Read", (double)Get(combat, "Now")), "Remaining");
        Assert.That(remainingAfterPyro, Is.LessThan(remainingBeforePyro - 3.0));
        while (Get(pyroSession, "Phase").ToString() != "Ended" && Time.time < deadline + 4) yield return null;

        // Boosted basic attack consumes FlameSlowArmed and slows target.
        Call(character, "PerformBasicAttack", enemy);
        Assert.That(Get(character, "FlameSlowArmed"), Is.False);

        // Fresh Cinderace with upgraded Feint: cleanses removable slow, grants Invincible + Speed + 3 healing attacks.
        combat = PrepareMigratedActor(12, "Cinderace");
        character = Get(combat, "Character");
        enemy.transform.position = Vector3.forward * 2;
        Variable(enemy, "currentHp", 5000);
        Physics.SyncTransforms();
        Assert.That(Call(combat, "Equip", E("AvailableMoves", "CinderFeint")), Is.True);
        slotB = Call(Get(combat, "Loadout"), "Get", 1);
        revB = (int)Get(slotB, "Revision");
        object slowChange = Activator.CreateInstance(T("StatChange"), (short)30, E("Stat", "Speed"), 4f, true, false, true, (ushort)777, true);
        Call(actor, "AddStatChange", slowChange);
        Variable(actor, "currentHp", 2000);
        Call(combat, "Receive", 3L, 1, revB, E("AvailableMoves", "CinderFeint"), Vector3.forward, ulong.MaxValue, 0L);
        object feintSession = Call(Get(combat, "Coordinator"), "FindRequest", 3L);
        deadline = Time.time + 8;
        while (!(bool)Get(feintSession, "Accepted") && Get(feintSession, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(feintSession, "Accepted"), Is.True);
        Assert.That(Get(character, "HealingAttacks"), Is.EqualTo(3));
        Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "Invincible")), Is.True);
        Call(character, "PerformBasicAttack", enemy);
        Assert.That(Get(character, "HealingAttacks"), Is.EqualTo(2));
        deadline = Time.time + 3;
        while (Hp(actor) <= 2000 && Time.time < deadline) yield return null;
        Assert.That(Hp(actor), Is.GreaterThan(2000));
        while (Get(feintSession, "Phase").ToString() != "Ended" && Time.time < deadline + 3) yield return null;
    }

    [UnityTest]
    public IEnumerator CinderaceBlazeKickPassiveBlazeAndDeathCleanupReleaseLocksAndStatuses()
    {
        object combat = PrepareMigratedActor(10, "Cinderace");
        object character = Get(combat, "Character");
        Component enemy = Pokemon(Vector3.forward * 3);
        Physics.SyncTransforms();

        // Trigger passive Blaze below 50% HP.
        Variable(actor, "currentHp", 1500);
        Call(combat, "Tick");
        Assert.That((double)character.GetType().GetField("blazeReady", Private).GetValue(character), Is.GreaterThan((double)Get(combat, "Now") + 20));

        // Equip and cast upgraded Blaze Kick: approaches target, crits/shoves/burns, rebounds in movement input direction.
        object kickId = E("AvailableMoves", "CinderBlazeKick");
        Assert.That(Call(combat, "Equip", kickId), Is.True);
        object slotA = Call(Get(combat, "Loadout"), "Get", 0);
        int revA = (int)Get(slotA, "Revision");
        Call(character, "ReceiveMovement", Vector2.right);
        int enemyHpBefore = Hp(enemy);
        Call(combat, "Receive", 1L, 0, revA, kickId, Vector3.forward, (ulong)Get(enemy, "NetworkObjectId"), 0L);
        object kickSession = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8;
        while (Get(kickSession, "Phase").ToString() != "Ended" && Time.time < deadline) yield return null;
        Assert.That(Get(kickSession, "Accepted"), Is.True);
        Assert.That(Hp(enemy), Is.LessThan(enemyHpBefore));
        Assert.That(Call(character, "HasBurn", enemy), Is.True);
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
        Assert.That(Get(character, "MovementLocked"), Is.False);

        // Death cleans up charge, empowerments, and target marks/burns.
        Call(character, "FillCharge");
        Call(character, "ArmFlameSlow");
        Variable(actor, "currentHp", 0);
        Call(combat, "Tick");
        Assert.That(Get(character, "Charge"), Is.EqualTo((byte)0));
        Assert.That(Get(character, "FlameSlowArmed"), Is.False);
        Assert.That(Get(character, "HealingAttacks"), Is.Zero);
    }

    protected IEnumerator VerifyCinderaceBasicAttackIdleRecovery()
    {
#if UNITY_EDITOR
        foreach (int level in new[] { 0, 4, 6 })
        {
            object combat = PrepareMigratedActor(level, "Cinderace");
            var original = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Cinderace.asset");
            var evolutions = (IList)Get(original, "Evolutions");
            int evoIndex = level == 0 ? 0 : (level == 4 ? 1 : 2);
            object evo = evolutions[evoIndex];
            var modelPrefab = (GameObject)evo.GetType().GetField("newModel").GetValue(evo);
            var model = UnityEngine.Object.Instantiate(modelPrefab, actor.transform);
            var animator = model.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Component animations = actor.GetComponent(T("AnimationManager"));
            Call(animations, "AssignAnimator", animator);
            ((Behaviour)animations).enabled = true;
            const string idle = "ani_idle_bat_0815";
            const string attackName = "ani_atk1_bat_0815";
            animator.Play(idle, 0, 0);
            animator.Update(0);
            Component target = Pokemon(Vector3.forward * 2);
            object character = Get(combat, "Character");
            var routine = (IEnumerator)Invoke(character, "BasicRecovery", target, attackName);
            ((MonoBehaviour)player).StartCoroutine(routine);
            yield return new WaitForSeconds((float)Call(moves, "GetAtkSpeedCooldown") * 0.5f + 0.15f);
            animator.Update(0.1f);
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.shortNameHash, Is.EqualTo(Animator.StringToHash(idle)), $"Level {level}");
        }
#else
        yield break;
#endif
    }
}

public class ZoroarkHostTests : ZoroarkServerTests
{
    protected override bool Host => true;

    [UnityTest]
    public IEnumerator GlaceonBasicAttacksRecoverToTheCorrectIdleBeforeAndAfterEvolution() => VerifyGlaceonBasicAttackIdleRecovery();

    [UnityTest]
    public IEnumerator CinderaceBasicAttacksRecoverToTheCorrectIdleAcrossAllEvolutionStages() => VerifyCinderaceBasicAttackIdleRecovery();
}
