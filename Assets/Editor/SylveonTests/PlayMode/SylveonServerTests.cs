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

public class SylveonServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
    protected Component server, actor, player, moves;
    private GameObject fixture;

    protected static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);
    protected static object E(string type, string value) => Enum.Parse(T(type), value);
    protected static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    protected static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    protected static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, args.Select(argument => argument.GetType()).ToArray()).Invoke(target, args);
    protected static object Invoke(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private).Invoke(target, args);

    protected static void Variable(object target, string name, object value)
    {
        var variable = target.GetType().GetField(name, Private).GetValue(target);
        variable.GetType().GetProperty("Value").SetValue(variable, value);
    }

    protected static int Hp(Component target) => (int)Get(target, "CurrentHp");
    protected GameObject Node(string name) { var root = new GameObject(name); objects.Add(root); return root; }

    private static readonly string[] SylveonPrefabPaths =
    {
        "Assets/Prefabs/Objects/Moves/Sylveon/SylvBasicAttack.prefab",
        "Assets/Prefabs/Objects/Moves/Sylveon/SylvBoostedAttack.prefab",
        "Assets/Prefabs/Objects/Moves/Sylveon/SylvSwift.prefab",
        "Assets/Prefabs/Objects/Moves/Sylveon/SylvBabyDollEyes.prefab",
        "Assets/Prefabs/Objects/Moves/Sylveon/HypervoiceHitbox.prefab",
        "Assets/Prefabs/Objects/Moves/Sylveon/SylvMysticalFire.prefab",
        "Assets/Prefabs/Objects/Moves/Sylveon/SylvDrainingKiss.prefab",
        "Assets/Prefabs/Objects/Moves/Sylveon/SylvCalmMind.prefab",
        "Assets/Prefabs/Objects/Moves/Sylveon/SylvFairyFrolic.prefab"
    };

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("External network session active.");
        foreach (var name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        {
            var map = Node(name);
            map.tag = name;
            map.transform.position = new Vector3(0, name == "WalkableWithWalls" ? -30.5f : -40.5f, 0);
            map.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100);
        }
        var root = Node("Sylveon test server");
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
        foreach (string path in SylveonPrefabPaths)
            Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(path));
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        yield return null;
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

    protected object PrepareSylveonActor(int level)
    {
#if UNITY_EDITOR
        GameObject root = UnityEngine.Object.Instantiate(fixture);
        root.transform.position = Vector3.zero;
        objects.Add(root);
        actor = root.GetComponent(T("Pokemon"));
        player = root.GetComponent(T("PlayerManager"));
        moves = root.GetComponent(T("MovesController"));
        var definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Sylveon.asset"));
        definitions.Add((ScriptableObject)definition);
        Set(actor, "baseStats", definition);
        Set(moves, "pokemon", actor);
        Set(moves, "playerManager", player);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1UL, true);
        Variable(actor, "currentHp", 5000);
        Variable(actor, "currentLevel", level);
        Call(actor, "UpdateTeamRPC", E("Team", "Blue"));
        var hp = actor.GetType().GetField("currentHp", Private).GetValue(actor);
        var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, actor, actor.GetType().GetMethod("CurrentHpChanged", Private)));
        root.transform.position = Vector3.zero;
        Physics.SyncTransforms();
        return Call(moves, "EnsureSylveonCombatRuntime", player);
#else
        return null;
#endif
    }

    protected Component Enemy(Vector3 position, string team = "Orange", string pokemonType = "Player")
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(fixture);
        root.transform.position = position;
        objects.Add(root);
        var pokemon = root.GetComponent(T("Pokemon"));
        var combat = root.GetComponent(T("MovesController"));
        var manager = root.GetComponent(T("PlayerManager"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase"));
        definitions.Add(definition);
        Set(definition, "pokemonName", "Dummy");
        Set(definition, "maxHp", Enumerable.Repeat(5000, 15).ToArray());
        Set(definition, "attack", Enumerable.Repeat(200, 15).ToArray());
        Set(definition, "spAttack", Enumerable.Repeat(200, 15).ToArray());
        Set(definition, "defense", new int[15]);
        Set(definition, "spDefense", new int[15]);
        Set(definition, "critRate", new int[15]);
        Set(definition, "speed", Enumerable.Repeat(3650, 15).ToArray());
        Set(definition, "passive", E("AvailablePassives", "EmptyPassive"));
        Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0));
        var original = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Sylveon.asset");
        Set(definition, "evolutions", Get(original, "Evolutions"));
        Set(pokemon, "baseStats", definition);
        Set(combat, "pokemon", pokemon);
        Set(combat, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1UL, true);
        Variable(pokemon, "currentHp", 5000);
        Call(pokemon, "UpdateTeamRPC", E("Team", team));
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, E("PokemonType", pokemonType));
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon);
        var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
        root.transform.position = position;
        Physics.SyncTransforms();
        return pokemon;
#else
        return null;
#endif
    }

    [UnityTest]
    public IEnumerator SylveonBabyDollEyesAndSwiftProjectilesSurviveCastRecoveryRetainAssetsAndHitAtMaxRange()
    {
        object combat = PrepareSylveonActor(1);
        Component enemy = Enemy(Vector3.forward * 6.0f);
        int initialHp = Hp(enemy);
        int initialAtk = (int)Call(enemy, "GetAttack");
        int initialSpeed = (int)Call(enemy, "GetSpeed");

        object eyesId = E("AvailableMoves", "SylvBabyDollEyes");
        Assert.That(Call(combat, "Equip", eyesId), Is.True);
        object slotB = Call(Get(combat, "Loadout"), "Get", 1);
        int revB = (int)Get(slotB, "Revision");
        Call(combat, "Receive", 1L, 1, revB, eyesId, Vector3.forward, (ulong)Get(enemy, "NetworkObjectId"), 0L);

        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8f;
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);

        var eyesEffect = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Single(component => T("HomingMoveEffect").IsInstanceOfType(component));
        Assert.That(T("PrefabMoveEffect").GetField("holdsAsset", Private).GetValue(eyesEffect), Is.True);
        object eyesHandle = T("PrefabMoveEffect").GetField("handle", Private).GetValue(eyesEffect);

        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"));
        Assert.That(eyesEffect != null && (bool)Get(eyesEffect, "IsSpawned"), Is.True, "Baby-Doll Eyes projectile must survive cast recovery.");
        Assert.That(Call(eyesHandle, "IsValid"), Is.True, "Baby-Doll Eyes must retain its Addressables handle after cast scope disposal.");

        while (eyesEffect != null && (bool)Get(eyesEffect, "IsSpawned") && Time.time < deadline)
            yield return null;
        Assert.That(Hp(enemy), Is.LessThan(initialHp), "Baby-Doll Eyes must hit at max range after cast recovery.");
        Assert.That((int)Call(enemy, "GetAttack"), Is.LessThan(initialAtk), "Baby-Doll Eyes must apply Attack debuff.");
        Assert.That((int)Call(enemy, "GetSpeed"), Is.LessThan(initialSpeed), "Baby-Doll Eyes must apply Speed debuff.");
        yield return null;
        Assert.That(Call(eyesHandle, "IsValid"), Is.False, "Destroying Baby-Doll Eyes must release its Addressables handle.");

        Variable(enemy, "currentHp", 5000);
        enemy.transform.position = Vector3.forward * 4.8f;
        Physics.SyncTransforms();

        object swiftId = E("AvailableMoves", "SylvSwift");
        Assert.That(Call(combat, "Equip", swiftId), Is.True);
        object slotA = Call(Get(combat, "Loadout"), "Get", 0);
        int revA = (int)Get(slotA, "Revision");
        Call(combat, "Receive", 2L, 0, revA, swiftId, Vector3.forward, ulong.MaxValue, 0L);

        object swiftSession = Call(Get(combat, "Coordinator"), "FindRequest", 2L);
        deadline = Time.time + 8f;
        while (!(bool)Get(swiftSession, "Accepted") && Get(swiftSession, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(swiftSession, "Accepted"), Is.True);

        while (Get(swiftSession, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(swiftSession, "Phase").ToString(), Is.EqualTo("Ended"));

        var inFlightStar = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(component => T("SylvSwiftProjectile").IsInstanceOfType(component) && (bool)Get(component, "IsSpawned"));
        Assert.That(inFlightStar, Is.Not.Null, "Final Swift star must remain spawned in flight when cast recovery finishes.");
        Assert.That(Get(inFlightStar, "HasValidAssetHandle"), Is.True);
        object starHandle = inFlightStar.GetType().GetField("asset", Private).GetValue(inFlightStar);

        int hpAtCastEnd = Hp(enemy);
        while (inFlightStar != null && (bool)Get(inFlightStar, "IsSpawned") && Time.time < deadline)
            yield return null;
        Assert.That(Hp(enemy), Is.LessThan(hpAtCastEnd), "Final Swift star must hit max-range target after cast recovery.");
        yield return null;
        Assert.That(Call(starHandle, "IsValid"), Is.False, "Destroying Swift star must release its independent Addressables handle.");
    }

    [UnityTest]
    public IEnumerator SylveonPassiveAdaptabilityUpgradesToPixilateOnEvolutionIgnoresHealsAndClearsOnDeath()
    {
        object combat = PrepareSylveonActor(1);
        object character = Get(combat, "Character");
        Component enemy = Enemy(Vector3.forward * 2f);

        int baseSpAtk = (int)Call(actor, "GetSpAttack");
        int baseSpDef = (int)Call(actor, "GetSpDefense");

        object healInfo = Activator.CreateInstance(
            T("DamageInfo"),
            Get(actor, "NetworkObjectId"),
            0f,
            (short)0,
            (short)200,
            E("DamageType", "Special"),
            E("DamageProprieties", "IsHeal"));
        Call(actor, "HealDamageRPC", healInfo);
        Assert.That(Get(character, "PassiveStacks"), Is.EqualTo((byte)0));

        object dmgInfo = Activator.CreateInstance(
            T("DamageInfo"),
            Get(actor, "NetworkObjectId"),
            0.5f,
            (short)1,
            (short)100,
            E("DamageType", "Special"),
            E("DamageProprieties", "None"));
        Call(enemy, "TakeDamageRPC", dmgInfo);
        Assert.That(Get(character, "PassiveStacks"), Is.EqualTo((byte)1));
        Assert.That((int)Call(actor, "GetSpAttack"), Is.GreaterThan(baseSpAtk));
        Assert.That((int)Call(actor, "GetSpDefense"), Is.EqualTo(baseSpDef));

        Variable(actor, "currentLevel", 3);
        Call(enemy, "TakeDamageRPC", dmgInfo);
        Assert.That(Get(character, "PassiveStacks"), Is.EqualTo((byte)2));
        Assert.That((int)Call(actor, "GetSpDefense"), Is.GreaterThan(baseSpDef));

        Variable(actor, "currentHp", 0);
        Call(combat, "Tick");
        Assert.That(Get(character, "PassiveStacks"), Is.EqualTo((byte)0));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SylveonBasicAttacksCycleThreeHitsGrantBoostedSpeedOnSylveonAndExpireAfterTimeout()
    {
        object combat = PrepareSylveonActor(4);
        object character = Get(combat, "Character");
        Component enemy = Enemy(Vector3.forward * 2.5f);
        int baseSpeed = (int)Call(actor, "GetSpeed");

        Call(character, "PerformBasicAttack", enemy);
        Assert.That(Get(character, "Charge"), Is.EqualTo((byte)1));
        Call(character, "PerformBasicAttack", enemy);
        Assert.That(Get(character, "Charge"), Is.EqualTo((byte)2));

        float wait = Time.time + 3f;
        while (Hp(enemy) == 5000 && Time.time < wait)
            yield return null;
        int hpAfterTwoNormals = Hp(enemy);
        Assert.That(hpAfterTwoNormals, Is.LessThan(5000));
        Assert.That((int)Call(actor, "GetSpeed"), Is.EqualTo(baseSpeed));

        Call(character, "PerformBasicAttack", enemy);
        Assert.That(Get(character, "Charge"), Is.EqualTo((byte)0));
        wait = Time.time + 3f;
        while ((int)Call(actor, "GetSpeed") <= baseSpeed && Time.time < wait)
            yield return null;
        Assert.That((int)Call(actor, "GetSpeed"), Is.GreaterThan(baseSpeed));
        Assert.That(Hp(enemy), Is.LessThan(hpAfterTwoNormals));
    }

    [UnityTest]
    public IEnumerator SylveonPreUnlockUniteEnergyAccumulatesBeforeLevelNineAndSurvivesEquip()
    {
        object combat = PrepareSylveonActor(1);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(0));

        Call(combat, "GrantUniteEnergy", 45000);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(45000));

        Variable(actor, "currentLevel", 9);
        object uniteId = E("AvailableMoves", "SylveonUnite");
        Assert.That(Call(combat, "Equip", uniteId), Is.True);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(45000));

        object slotU = Call(Get(combat, "Loadout"), "Get", 2);
        int revU = (int)Get(slotU, "Revision");
        Call(combat, "Receive", 1L, 2, revU, uniteId, Vector3.forward, ulong.MaxValue, 0L);
        Assert.That(Call(Get(combat, "Coordinator"), "FindRequest", 1L), Is.Null);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(45000));

        Call(combat, "GrantUniteEnergy", 60000);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(100000));
        Call(combat, "Receive", 2L, 2, revU, uniteId, Vector3.forward, ulong.MaxValue, 0L);
        object session = Call(Get(combat, "Coordinator"), "FindRequest", 2L);
        float deadline = Time.time + 8f;
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(0));
    }

    [UnityTest]
    public IEnumerator SylveonMysticalFireRejectsImmobilizedDashesFiresFlamesAndReducesCooldownAndHud()
    {
        object combat = PrepareSylveonActor(4);
        Component enemy = Enemy(Vector3.forward * 4f);
        object fireId = E("AvailableMoves", "SylvMysticalCringe");
        Assert.That(Call(combat, "Equip", fireId), Is.True);
        object slotA = Call(Get(combat, "Loadout"), "Get", 0);
        int revA = (int)Get(slotA, "Revision");

        object immobilized = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Immobilized"), 0.2f, true, (ushort)9901);
        Call(actor, "AddStatusEffect", immobilized);
        Call(combat, "Receive", 1L, 0, revA, fireId, Vector3.forward, ulong.MaxValue, 0L);
        Assert.That(Call(Get(combat, "Coordinator"), "FindRequest", 1L), Is.Null);
        object avail = Call(Get(slotA, "Resources"), "Read", (double)Get(combat, "Now"));
        Assert.That(Get(avail, "Ready"), Is.True);

        Call(actor, "RemoveStatusEffectWithID", (ushort)9901);
        int initialEnemySpAtk = (int)Call(enemy, "GetSpAttack");

        Call(combat, "Receive", 2L, 0, revA, fireId, Vector3.forward, ulong.MaxValue, 0L);
        object session = Call(Get(combat, "Coordinator"), "FindRequest", 2L);
        float deadline = Time.time + 8f;
        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        Assert.That(actor.transform.position.z, Is.GreaterThan(2.0f), "Mystical Fire must dash forward.");

        double remainingAfterDash = (double)Get(Call(Get(slotA, "Resources"), "Read", (double)Get(combat, "Now")), "Remaining");
        Assert.That(remainingAfterDash, Is.GreaterThan(6.0));

        deadline = Time.time + 4f;
        while (Hp(enemy) > 4200 && Time.time < deadline)
            yield return null;
        Assert.That(Hp(enemy), Is.LessThan(5000));
        Assert.That((int)Call(enemy, "GetSpAttack"), Is.LessThan(initialEnemySpAtk));
        double remainingAfterHits = (double)Get(Call(Get(slotA, "Resources"), "Read", (double)Get(combat, "Now")), "Remaining");
        Assert.That(remainingAfterHits, Is.LessThan(remainingAfterDash - 0.5));
    }

    [UnityTest]
    public IEnumerator SylveonDrainingKissBouncesThreeTimesAppliesDecayingSlowAndHealsCaster()
    {
        object combat = PrepareSylveonActor(6);
        Variable(actor, "currentHp", 2000);
        Component enemy = Enemy(Vector3.forward * 3.5f);
        int initialEnemyHp = Hp(enemy);
        int initialEnemySpeed = (int)Call(enemy, "GetSpeed");

        object kissId = E("AvailableMoves", "SylvDrainingKiss");
        Assert.That(Call(combat, "Equip", kissId), Is.True);
        object slotB = Call(Get(combat, "Loadout"), "Get", 1);
        int revB = (int)Get(slotB, "Revision");
        Call(combat, "Receive", 1L, 1, revB, kissId, Vector3.forward, (ulong)Get(enemy, "NetworkObjectId"), 0L);

        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8f;
        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);

        var kissEffect = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .FirstOrDefault(component => T("SylvDrainingKissEffect").IsInstanceOfType(component));
        Assert.That(kissEffect, Is.Not.Null, "Draining Kiss effect must survive cast recovery while bouncing.");

        deadline = Time.time + 6f;
        while (kissEffect != null && (bool)Get(kissEffect, "IsSpawned") && Time.time < deadline)
            yield return null;
        Assert.That(Hp(enemy), Is.LessThan(initialEnemyHp - 400), "Draining Kiss must deal multi-bounce damage.");
        Assert.That(Hp(actor), Is.GreaterThan(2400), "Draining Kiss must heal Sylveon on return bounces.");
        Assert.That((int)Call(enemy, "GetSpeed"), Is.LessThanOrEqualTo(initialEnemySpeed));
    }

    [UnityTest]
    public IEnumerator SylveonStationaryCastIsAllowedWhileImmobilized()
    {
        object combat = PrepareSylveonActor(13);
        object status = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Immobilized"), 3f, true, (ushort)9903);
        Call(actor, "AddStatusEffect", status);
        object moveId = E("AvailableMoves", "SylvCalmMind");
        Assert.That(Call(combat, "Equip", moveId), Is.True);
        object ability = Call(Get(combat, "Loadout"), "Get", 1);
        int revision = (int)Get(ability, "Revision");
        int original = (int)Call(actor, "GetSpAttack");
        Call(combat, "Receive", 1L, 1, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8f;
        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        Assert.That(Get(session, "Outcome").ToString(), Is.EqualTo("Completed"));
        Assert.That((int)Call(actor, "GetSpAttack"), Is.GreaterThan(original));
    }

    [UnityTest]
    public IEnumerator SylveonCalmMindUpgradedBlocksMoveDamageAndGrantsShieldWithoutBlockingBasicAttacks()
    {
        object combat = PrepareSylveonActor(13);
        Component enemy = Enemy(Vector3.forward * 2f);
        int baseSpAtk = (int)Call(actor, "GetSpAttack");
        int baseSpeed = (int)Call(actor, "GetSpeed");

        object calmId = E("AvailableMoves", "SylvCalmMind");
        Assert.That(Call(combat, "Equip", calmId), Is.True);
        object slotB = Call(Get(combat, "Loadout"), "Get", 1);
        int revB = (int)Get(slotB, "Revision");
        Call(combat, "Receive", 1L, 1, revB, calmId, Vector3.forward, ulong.MaxValue, 0L);

        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8f;
        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        Assert.That((int)Call(actor, "GetSpAttack"), Is.GreaterThan(baseSpAtk));
        Assert.That((int)Call(actor, "GetSpeed"), Is.GreaterThan(baseSpeed));

        object basicDmg = Activator.CreateInstance(
            T("DamageInfo"),
            Get(enemy, "NetworkObjectId"),
            0f,
            (short)0,
            (short)300,
            E("DamageType", "Physical"),
            E("DamageProprieties", "IsBasicAttack"));
        Call(actor, "TakeDamageRPC", basicDmg);
        Assert.That(Hp(actor), Is.LessThan(5000));
        Assert.That(Get(actor, "ShieldHp"), Is.EqualTo(0));

        int hpBeforeMove = Hp(actor);
        object moveDmg = Activator.CreateInstance(
            T("DamageInfo"),
            Get(enemy, "NetworkObjectId"),
            0f,
            (short)0,
            (short)600,
            E("DamageType", "Special"),
            E("DamageProprieties", "None"));
        Call(actor, "TakeDamageRPC", moveDmg);
        Assert.That(Hp(actor), Is.EqualTo(hpBeforeMove), "Calm Mind+ must block the incoming move damage.");
        Assert.That((int)Get(actor, "ShieldHp"), Is.GreaterThan(100), "Blocking move damage must grant Calm Mind+ shield.");
    }

    [UnityTest]
    public IEnumerator SylveonHyperVoiceDealsMoreDamageAtFarRangeAndCleansUpOnCompletion()
    {
        object combat = PrepareSylveonActor(11);
        Component closeEnemy = Enemy(Vector3.forward * 2.0f);
        Component farEnemy = Enemy(Vector3.forward * 4.2f);

        object hyperId = E("AvailableMoves", "SylvHyperVoice");
        Assert.That(Call(combat, "Equip", hyperId), Is.True);
        object slotA = Call(Get(combat, "Loadout"), "Get", 0);
        int revA = (int)Get(slotA, "Revision");
        Call(combat, "Receive", 1L, 0, revA, hyperId, Vector3.forward, ulong.MaxValue, 0L);

        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8f;
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);
        Assert.That(Get(Get(combat, "Character"), "CanStartBasicAttack"), Is.False);

        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(Get(combat, "Character"), "CanStartBasicAttack"), Is.True);
        Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));

        int closeDamageTaken = 5000 - Hp(closeEnemy);
        int farDamageTaken = 5000 - Hp(farEnemy);
        Assert.That(closeDamageTaken, Is.GreaterThan(400));
        Assert.That(farDamageTaken, Is.GreaterThan(closeDamageTaken), "Hyper Voice far cone (>3.5u) must deal more damage than close cone.");
    }

    [UnityTest]
    public IEnumerator SylveonUniteFairyFrolicCleansesCapsWildDamageAndHealsFromActualHpDamage()
    {
        object combat = PrepareSylveonActor(14);
        Variable(actor, "currentHp", 1500);
        Set(Get(actor, "BaseStats"), "spellVamp", new float[15]);
        object power = Activator.CreateInstance(T("StatChange"), (short)1000, E("Stat", "SpAttack"), 10f, true, true, false, (ushort)9904, true);
        Call(actor, "AddStatChange", power);
        Component wildEnemy = Enemy(Vector3.forward * 2.0f, "Neutral", "Wild");

        object slowStatus = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "VisionObscuring"), 4f, true, (ushort)9902);
        Call(actor, "AddStatusEffect", slowStatus);
        Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "VisionObscuring")), Is.True);

        object uniteId = E("AvailableMoves", "SylveonUnite");
        Assert.That(Call(combat, "Equip", uniteId), Is.True);
        Call(combat, "GrantUniteEnergy", 100000);
        object slotU = Call(Get(combat, "Loadout"), "Get", 2);
        int revU = (int)Get(slotU, "Revision");
        Call(combat, "Receive", 1L, 2, revU, uniteId, Vector3.forward, ulong.MaxValue, 0L);

        object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
        float deadline = Time.time + 8f;
        while (!(bool)Get(session, "Accepted") && Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(session, "Accepted"), Is.True);

        yield return new WaitForSeconds(0.1f);
        Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "VisionObscuring")), Is.False, "Fairy Frolic must cleanse timed negative statuses.");
        Assert.That(Call(actor, "HasStatusEffect", E("StatusType", "Invincible")), Is.True, "Fairy Frolic must grant Invincible while airborne.");

        while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            yield return null;
        Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0));
        int wildDamageTaken = 5000 - Hp(wildEnemy);
        Assert.That(wildDamageTaken, Is.EqualTo(2000), "Fairy Frolic landing damage against wild Pokemon must be capped at 2000.");
        Assert.That(Hp(actor), Is.EqualTo(1500 + 1200), "Landing damage must heal 60% of actual HP damage dealt (0.6 * 2000 = 1200).");
        Assert.That((int)Get(actor, "ShieldHp"), Is.GreaterThan(500), "Fairy Frolic must grant its 20% max HP Unite shield.");

        int hpAfterLanding = Hp(actor);
        object followUpDmg = Activator.CreateInstance(
            T("DamageInfo"),
            Get(actor, "NetworkObjectId"),
            0f,
            (short)0,
            (short)400,
            E("DamageType", "Special"),
            E("DamageProprieties", "None"));
        Call(wildEnemy, "TakeDamageRPC", followUpDmg);
        Assert.That(Hp(actor), Is.EqualTo(hpAfterLanding + 200), "Post-landing damage during Fairy Frolic buff must heal 50% of actual HP damage.");
    }

    [UnityTest]
    public IEnumerator MobileCastsNeverAcquireExecutionMovementRestrictions()
    {
        foreach (string moveName in new[] { "SylvSwift", "SylvHyperVoice", "SylveonUnite" })
        {
            object combat = PrepareSylveonActor(14);
            object movement = Get(player, "PlayerMovement");
            movement.GetType().GetProperty("CanMove").SetValue(movement, true);
            int movementStops = 0;
            Action<bool> observeMovement = allowed => { if (!allowed) movementStops++; };
            EventInfo changed = movement.GetType().GetEvent("OnCanMoveChanged");
            changed.AddEventHandler(movement, observeMovement);
            object moveId = E("AvailableMoves", moveName);
            Assert.That(Call(combat, "Equip", moveId), Is.True);
            int slot = moveName == "SylveonUnite" ? 2 : 0;
            if (slot == 2) Call(combat, "GrantUniteEnergy", 100000);
            int revision = (int)Get(Call(Get(combat, "Loadout"), "Get", slot), "Revision");
            Call(combat, "Receive", 1L, slot, revision, moveId, Vector3.forward, ulong.MaxValue, 0L);
            object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
            float deadline = Time.time + 8f;
            while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            {
                Assert.That(Get(movement, "CanMove"), Is.True, moveName);
                Assert.That(Get(Get(combat, "Character"), "MovementLocked"), Is.False, moveName);
                yield return null;
            }
            changed.RemoveEventHandler(movement, observeMovement);
            Assert.That(Get(session, "Accepted"), Is.True, moveName);
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"), moveName);
            Assert.That(movementStops, Is.Zero, moveName + " must not briefly stop movement between sampled frames.");
            Assert.That(Get(moves, "ServerActionLocks"), Is.EqualTo(0), moveName);
        }
    }

    protected IEnumerator VerifyMobileOwnerSubmissionPreservesMovementAndOtherRestrictions()
    {
        foreach (string moveName in new[] { "SylvSwift", "SylvHyperVoice", "SylveonUnite" })
        {
            object combat = PrepareSylveonActor(14);
            Call(actor.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", 0UL);
            var statuses = Array.CreateInstance(T("BattleActionStatus"), 2);
            for (int slotIndex = 0; slotIndex < statuses.Length; slotIndex++)
                statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), slotIndex);
            Set(moves, "moveStatuses", statuses);
            object movement = Get(player, "PlayerMovement");
            movement.GetType().GetProperty("CanMove").SetValue(movement, true);
            object moveId = E("AvailableMoves", moveName);
            Assert.That(Call(combat, "Equip", moveId), Is.True);
            object bridge = T("MoveDatabase").GetMethod("GetMove").Invoke(null, new[] { moveId });
            bridge.GetType().GetField("playerManager").SetValue(bridge, player);
            int slot = moveName == "SylveonUnite" ? 2 : 0;
            if (slot == 2)
            {
                Set(moves, "uniteMove", bridge);
                Call(combat, "GrantUniteEnergy", 100000);
            }
            else
            {
                var bridges = (Array)moves.GetType().GetField("moves", Private).GetValue(moves);
                bridges.SetValue(bridge, slot);
            }

            Call(movement, "AddMovementRestriction");
            moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 1);
            combat.GetType().GetMethod("Submit").Invoke(combat, new object[] { bridge, Vector3.forward, null });
            Assert.That(movement.GetType().GetField("movementRestrictions", Private).GetValue(movement), Is.EqualTo(1), moveName);
            Assert.That(Get(movement, "CanMove"), Is.False, "An unrelated movement restriction must remain intact.");
            Assert.That(Call(Get(moves, "BasicAttackStatus"), "HasStatus", E("ActionStatusType", "Busy")), Is.True, moveName);
            Call(movement, "RemoveMovementRestriction");
            Assert.That(Get(movement, "CanMove"), Is.True, "Pending acceptance must not lock movement: " + moveName);

            int movementStops = 0;
            Action<bool> observeMovement = allowed => { if (!allowed) movementStops++; };
            EventInfo changed = movement.GetType().GetEvent("OnCanMoveChanged");
            changed.AddEventHandler(movement, observeMovement);
            moves.GetType().GetProperty("ServerActionLocks").SetValue(moves, 0);
            Call(combat, "Tick");
            float deadline = Time.time + 8f;
            object session = Call(Get(combat, "Coordinator"), "FindRequest", 1L);
            Assert.That(session, Is.Not.Null, moveName);
            while (Get(session, "Phase").ToString() != "Ended" && Time.time < deadline)
            {
                Assert.That(Get(movement, "CanMove"), Is.True, moveName);
                yield return null;
            }
            changed.RemoveEventHandler(movement, observeMovement);
            Assert.That(Get(session, "Accepted"), Is.True, moveName);
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"), moveName);
            Assert.That(movementStops, Is.Zero, moveName);
            Assert.That(Get(movement, "CanMove"), Is.True, moveName);
            Assert.That(Call(Get(moves, "BasicAttackStatus"), "HasStatus", E("ActionStatusType", "Busy")), Is.False, moveName);
        }
    }

    protected IEnumerator VerifySylveonBasicAttackIdleRecovery()
    {
#if UNITY_EDITOR
        foreach ((int level, int evoIndex, string idle, string attackName) in new[]
        {
            (1, 0, "ani_idle_bat_0133", "ani_atk4_bat_0133"),
            (3, 1, "ani_idle_bat_0700", "ani_atk1_bat_0700")
        })
        {
            object combat = PrepareSylveonActor(level);
            var original = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Sylveon.asset");
            var evolutions = (IList)Get(original, "Evolutions");
            object evo = evolutions[evoIndex];
            var modelPrefab = (GameObject)evo.GetType().GetField("newModel").GetValue(evo);
            var model = UnityEngine.Object.Instantiate(modelPrefab, actor.transform);
            var animator = model.GetComponentInChildren<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            Component animations = actor.GetComponent(T("AnimationManager"));
            Call(animations, "AssignAnimator", animator);
            ((Behaviour)animations).enabled = true;
            animator.Play(idle, 0, 0);
            animator.Update(0);
            Component target = Enemy(Vector3.forward * 2f);
            object character = Get(combat, "Character");
            var routine = (IEnumerator)Invoke(character, "BasicRecovery", target, attackName);
            ((MonoBehaviour)player).StartCoroutine(routine);
            yield return new WaitForSeconds((float)Call(moves, "GetAtkSpeedCooldown") * 0.5f + 0.15f);
            animator.Update(0.1f);
            var state = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            Assert.That(state.shortNameHash, Is.EqualTo(Animator.StringToHash(idle)), $"Level {level} should recover to {idle}");
        }
#else
        yield break;
#endif
    }
}

public class SylveonHostTests : SylveonServerTests
{
    protected override bool Host => true;

    [UnityTest]
    public IEnumerator MobileOwnerSubmissionPreservesMovementAndOtherRestrictions() =>
        VerifyMobileOwnerSubmissionPreservesMovementAndOtherRestrictions();

    [UnityTest]
    public IEnumerator SylveonBasicAttacksRecoverToEvolutionSpecificIdleForEeveeAndSylveon() =>
        VerifySylveonBasicAttackIdleRecovery();
}
