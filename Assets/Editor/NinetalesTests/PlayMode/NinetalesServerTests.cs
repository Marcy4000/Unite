using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Exercise real NGO host and dedicated-server damage/status/effect lifecycles.
// Reuses the minimal shared combat actor fixture, not the lobby/player bootstrap.
public class NinetalesServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
    private Component server, actor, player, moves;
    private GameObject fixture;
    private NavMeshData wildNavMesh;
    private NavMeshDataInstance wildNavMeshInstance;
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(target, args);
    private static void Variable(object target, string name, object value) { var variable = target.GetType().GetField(name, Private).GetValue(target); variable.GetType().GetProperty("Value").SetValue(variable, value); }
    private static int Hp(Component target) => (int)Get(target, "CurrentHp");
    private static object Rule(string name, params object[] args) => T("NinetalesMoveRules").GetMethod(name).Invoke(null, args);
    private GameObject Node(string name) { var root = new GameObject(name); objects.Add(root); return root; }
    private object Combat => Get(moves, "NinetalesCombat");
    private static bool Has(Component target, string type) => (bool)Call(target, "HasStatusEffect", E("StatusType", type));

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("External network session active.");
        foreach (var name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        { var map = Node(name); map.tag = name; map.transform.position = new Vector3(0, name == "WalkableWithWalls" ? -30.5f : -40.5f, 0); map.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100); }
        var root = Node("Ninetales regression server"); server = root.AddComponent(T("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string)).Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig")); server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport); config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab"); Call(server, "AddNetworkPrefab", fixture);
        Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WildPokemons/WildPokemon.prefab"));
        foreach (var name in new[] { "NinetalesPowderSnow", "NinetalesDazzlingGleam", "NinetalesAvalanche", "NinetalesIcyWind", "NinetalesBlizzard", "NinetalesAuroraVeil", "NinetalesSnowGlobe", "NinetalesBasicProjectile" })
            Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/AlolanNinetales/" + name + ".prefab"));
#else
        Assert.Ignore("Editor fixtures required.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True); yield return null;
        actor = Pokemon(Vector3.zero, "Blue", true); player = actor.GetComponent(T("PlayerManager")); moves = actor.GetComponent(T("MovesController"));
    }
    private Component Pokemon(Vector3 position, string team = "Orange", bool ninetales = false)
    {
        var root = UnityEngine.Object.Instantiate(fixture); root.transform.position = position; objects.Add(root);
        var pokemon = root.GetComponent(T("Pokemon")); var combat = root.GetComponent(T("MovesController")); var manager = root.GetComponent(T("PlayerManager"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase")); definitions.Add(definition);
        Set(definition, "pokemonName", ninetales ? "Alolan Ninetales" : "Test opponent"); Set(definition, "maxHp", Enumerable.Repeat(20000, 15).ToArray());
        Set(definition, "attack", Enumerable.Repeat(100, 15).ToArray()); Set(definition, "spAttack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "defense", new int[15]); Set(definition, "spDefense", new int[15]); Set(definition, "critRate", new int[15]); Set(definition, "speed", Enumerable.Repeat(3650, 15).ToArray());
        Set(definition, "atkSpeed", Enumerable.Repeat(10f, 15).ToArray()); Set(definition, "passive", E("AvailablePassives", ninetales ? "NinetalesPassive" : "EmptyPassive"));
        Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0)); Set(pokemon, "baseStats", definition); Set(combat, "pokemon", pokemon); Set(combat, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1ul, true);
        Variable(pokemon, "currentHp", 20000); Call(pokemon, "UpdateTeamRPC", E("Team", team));
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); var onChanged = hp.GetType().GetField("OnValueChanged");
        onChanged.SetValue(hp, Delegate.CreateDelegate(onChanged.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
        return pokemon;
    }
    private Component Effect(string name, Vector3 direction, bool upgraded = false, Component target = null)
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/AlolanNinetales/" + name + ".prefab")); root.transform.position = actor.transform.position + Vector3.up * 0.8f; objects.Add(root);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true); var effect = root.GetComponent(T("PrefabMoveEffect"));
        effect.GetType().GetMethod("Initialize").Invoke(effect, new object[] { player, direction, target, upgraded }); return effect;
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
        foreach (var root in objects) if (root != null) UnityEngine.Object.Destroy(root);
        foreach (var definition in definitions) if (definition != null) UnityEngine.Object.Destroy(definition);
        if (wildNavMeshInstance.valid) wildNavMeshInstance.Remove();
        if (wildNavMesh != null) UnityEngine.Object.Destroy(wildNavMesh);
        wildNavMesh = null; wildNavMeshInstance = default;
        objects.Clear(); definitions.Clear(); yield return null;
    }
    [UnityTest]
    public IEnumerator FourSnowHitsFreezeAndCleanseOrExpirationResetStacks()
    {
        var target = Pokemon(Vector3.forward * 2); var combat = Combat;
        for (int i = 0; i < 3; i++) Call(combat, "AddSnow", target);
        Assert.That(Call(combat, "SnowCount", target), Is.EqualTo(3)); Assert.That(Has(target, "Frozen"), Is.False);
        Call(combat, "AddSnow", target); Assert.That(Has(target, "Frozen"), Is.True); Assert.That(Call(combat, "SnowCount", target), Is.EqualTo(0));
        Call(target, "ClearStatusEffectsRPC"); Call(combat, "AddSnow", target); Call(combat, "AddSnow", target);
        Call(target, "ClearStatusEffectsRPC"); Call(combat, "AddSnow", target); Assert.That(Call(combat, "SnowCount", target), Is.EqualTo(1));
        T("CresseliaMoveRuntime").GetMethod("Cleanse").Invoke(null, new object[] { target }); Assert.That(Has(target, "SnowMarked"), Is.False);
        Call(combat, "AddSnow", target);
        var dictionary = (IDictionary)combat.GetType().GetField("snow", Private).GetValue(combat);
        var stacks = dictionary[target]; stacks.GetType().GetProperty("Expires").SetValue(stacks, Time.time - 0.01f);
        // Avoid an unrelated automatic snowfall while exercising the expiry scan.
        Set(combat, "nextPassive", Time.time + 100); Call(combat, "Tick"); Assert.That(Call(combat, "SnowCount", target), Is.EqualTo(0));
        yield return null;
    }
    [UnityTest]
    public IEnumerator OpposingNinetalesMaintainIndependentSnowAndInvincibilityRejectsHits()
    {
        var target = Pokemon(Vector3.forward * 2); var other = Pokemon(Vector3.right * 2, "Orange", true);
        var theirs = Get(other.GetComponent(T("MovesController")), "NinetalesCombat");
        Call(Combat, "AddSnow", target); Call(Combat, "AddSnow", target); Call(theirs, "AddSnow", actor);
        Assert.That(Call(Combat, "SnowCount", target), Is.EqualTo(2)); Assert.That(Call(theirs, "SnowCount", actor), Is.EqualTo(1));
        var invincible = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Invincible"), 5f, true, (ushort)77); Call(target, "AddStatusEffect", invincible);
        Assert.That(Call(Combat, "Hit", target, Rule("PowderDamage", Get(actor, "NetworkObjectId")), true), Is.EqualTo(0)); Assert.That(Call(Combat, "SnowCount", target), Is.EqualTo(2));
        yield return null;
    }
    [UnityTest]
    public IEnumerator SnowWarningFindsGrassHiddenPlayersBeforeCloserWildPokemonAndUsesFourSecondCooldown()
    {
        var wild = Pokemon(Vector3.forward); wild.GetType().GetProperty("Type").SetValue(wild, E("PokemonType", "Wild"));
        var hidden = Pokemon(Vector3.forward * 5); var invisible = Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Invisible"), 5f, true, (ushort)78); Call(hidden, "AddStatusEffect", invisible);
        Physics.SyncTransforms(); var combat = Combat; Call(combat, "Tick");
        float began = Time.time;
        Assert.That(Hp(hidden), Is.EqualTo(20000)); Assert.That(Hp(wild), Is.EqualTo(20000)); Assert.That(Call(combat, "SnowCount", hidden), Is.EqualTo(0));
        Assert.That(Get(combat, "PassiveWarningActive"), Is.True); Assert.That(Get(combat, "PassiveCenter"), Is.EqualTo(hidden.transform.position));
        if (Host) Assert.That((UnityEngine.Object)moves.GetType().GetField("ninetalesPassiveWarning", Private).GetValue(moves), Is.Not.Null);
        yield return new WaitForSeconds(0.55f); Call(combat, "Tick");
        Assert.That(Hp(hidden), Is.EqualTo(20000), "No damage may occur during the one-second floor warning."); Assert.That(Call(combat, "SnowCount", hidden), Is.EqualTo(0));
        yield return new WaitForSeconds(0.5f); Call(combat, "Tick");
        Assert.That(Hp(hidden), Is.EqualTo(19840)); Assert.That(Hp(wild), Is.EqualTo(20000)); Assert.That(Call(combat, "SnowCount", hidden), Is.EqualTo(1));
        Assert.That(Get(combat, "PassiveWarningActive"), Is.False);
        float next = (float)combat.GetType().GetField("nextPassive", Private).GetValue(combat); Assert.That(next - began, Is.EqualTo(4).Within(0.1f));
        Call(combat, "Tick"); Assert.That(Hp(hidden), Is.EqualTo(19840)); yield return null;
    }
    [UnityTest]
    public IEnumerator SnowWarningIsADodgeableFixedAreaAndHitsEnemiesWhoEnterIt()
    {
        var selected = Pokemon(Vector3.forward * 2); var nearby = Pokemon(new Vector3(1.2f, 0, 2));
        var entrant = Pokemon(Vector3.right * 10); var ally = Pokemon(new Vector3(-0.5f, 0, 2), "Blue");
        var combat = Combat; Physics.SyncTransforms(); Call(combat, "Tick");
        Vector3 center = (Vector3)Get(combat, "PassiveCenter"); Assert.That(center, Is.EqualTo(selected.transform.position));
        selected.transform.position = Vector3.forward * 6;
        entrant.transform.position = center + Vector3.right * 0.5f; Physics.SyncTransforms();
        Assert.That(Get(combat, "PassiveCenter"), Is.EqualTo(center), "The floor warning must not follow its original target.");
        yield return new WaitForSeconds(1.05f); Call(combat, "Tick");
        Assert.That(Hp(selected), Is.EqualTo(20000), "Moving out must dodge Snow Warning."); Assert.That(Call(combat, "SnowCount", selected), Is.EqualTo(0));
        Assert.That(Hp(nearby), Is.EqualTo(19840)); Assert.That(Hp(entrant), Is.EqualTo(19840)); Assert.That(Hp(ally), Is.EqualTo(20000));
        Assert.That(Call(combat, "SnowCount", nearby), Is.EqualTo(1)); Assert.That(Call(combat, "SnowCount", entrant), Is.EqualTo(1));
    }
    [UnityTest]
    public IEnumerator SnowWarningCannotFreezeEarlyAndDeathCancelsAPendingStrike()
    {
        var target = Pokemon(Vector3.forward * 2); var combat = Combat;
        for (int i = 0; i < 3; i++) Call(combat, "AddSnow", target);
        Physics.SyncTransforms(); Call(combat, "Tick");
        Assert.That(Has(target, "Frozen"), Is.False); Assert.That(Call(combat, "SnowCount", target), Is.EqualTo(3));
        yield return new WaitForSeconds(1.05f); Call(combat, "Tick");
        Assert.That(Hp(target), Is.EqualTo(19840)); Assert.That(Has(target, "Frozen"), Is.True);
        Set(combat, "nextPassive", 0f); Call(combat, "Tick"); Assert.That(Get(combat, "PassiveWarningActive"), Is.True);
        Variable(actor, "currentHp", 0);
        Assert.That(Get(combat, "PassiveWarningActive"), Is.False);
        if (Host) Assert.That(moves.GetType().GetField("ninetalesPassiveWarning", Private).GetValue(moves), Is.Null);
        Variable(actor, "currentHp", 20000); // A respawn must not revive the old pending strike.
        yield return new WaitForSeconds(1.05f); Call(combat, "Tick");
        Assert.That(Hp(target), Is.EqualTo(19840)); Assert.That(Call(combat, "SnowCount", target), Is.EqualTo(0));
    }
    [UnityTest]
    public IEnumerator ConeMovesRespectTheirShapeAndApplySnowAndUpgradeStun()
    {
        var front = Pokemon(Vector3.forward * 4); var side = Pokemon(Vector3.right * 4); Physics.SyncTransforms();
        Effect("NinetalesPowderSnow", Vector3.forward); yield return new WaitForSeconds(0.4f);
        Assert.That(Hp(front), Is.EqualTo(19682)); Assert.That(Hp(side), Is.EqualTo(20000)); Assert.That(Call(Combat, "SnowCount", front), Is.EqualTo(1));
        Effect("NinetalesDazzlingGleam", Vector3.forward, true); yield return new WaitForSeconds(0.4f);
        Assert.That(Hp(front), Is.EqualTo(19197)); Assert.That(Has(front, "Incapacitated"), Is.True); Assert.That(Call(Combat, "SnowCount", front), Is.EqualTo(2));
    }
    [UnityTest]
    public IEnumerator SnowGlobeUsesPreHitFrozenStateThenAppliesTheFourthSnowStack()
    {
        var fourth = Pokemon(Vector3.forward * 2); var frozen = Pokemon(new Vector3(1, 0, 2));
        for (int i = 0; i < 3; i++) Call(Combat, "AddSnow", fourth);
        Call(frozen, "AddStatusEffect", Activator.CreateInstance(T("StatusEffect"), E("StatusType", "Frozen"), 5f, true, (ushort)79));
        Physics.SyncTransforms(); Effect("NinetalesSnowGlobe", Vector3.forward); Assert.That(Has(actor, "Unstoppable"), Is.True);
        yield return new WaitForSeconds(1.1f);
        Assert.That(Hp(fourth), Is.EqualTo(18691)); Assert.That(Hp(frozen), Is.EqualTo(18118)); Assert.That(Has(fourth, "Frozen"), Is.True);
        Assert.That(Get(actor, "ShieldHp"), Is.EqualTo(4000)); Assert.That(Call(actor, "GetCDR"), Is.EqualTo(30)); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator VeilOnlyProtectsInsideButHasteAndOneHealPerBoostedSplashPersistOutside()
    {
        var ally = Pokemon(Vector3.forward * 4, "Blue"); var veil = Effect("NinetalesAuroraVeil", Vector3.forward, true);
        yield return new WaitForSeconds(0.6f);
        Assert.That(Call(actor, "GetDamageReduction"), Is.EqualTo(0.4f).Within(0.001f)); Assert.That(Call(ally, "GetDamageReduction"), Is.EqualTo(0.4f).Within(0.001f)); Assert.That(Get(moves, "NinetalesInsideVeil"), Is.True);
        actor.transform.position = Vector3.right * 10; Physics.SyncTransforms(); yield return null; yield return null;
        Assert.That(Call(actor, "GetDamageReduction"), Is.EqualTo(0)); Assert.That(Call(ally, "GetDamageReduction"), Is.EqualTo(0.4f).Within(0.001f)); Assert.That(Get(moves, "NinetalesInsideVeil"), Is.False);
        Assert.That(Call(actor, "HasStatChangeWithID", (ushort)65011), Is.False); Assert.That(Call(actor, "HasStatChangeWithID", (ushort)65010), Is.True);
        Variable(actor, "currentHp", 10000);
        var first = Pokemon(new Vector3(10, 0, 2)); var second = Pokemon(new Vector3(10.4f, 0, 2)); Physics.SyncTransforms();
        Effect("NinetalesBasicProjectile", Vector3.forward, true, first); yield return new WaitForSeconds(0.35f);
        Assert.That(Hp(first), Is.EqualTo(19673)); Assert.That(Hp(second), Is.EqualTo(19673)); Assert.That(Hp(actor), Is.EqualTo(10050), "Splash damage must heal once, not once per victim.");
        Call(veil, "Despawn"); yield return null;
        Assert.That(Call(ally, "GetDamageReduction"), Is.EqualTo(0)); Assert.That(Call(actor, "HasStatChangeWithID", (ushort)65010), Is.True, "Leaving/removing the field must not revoke the independent 5s haste.");
    }
    [UnityTest]
    public IEnumerator BlizzardFirstBlastCrossesAvalancheSecondCreatesFrostWithoutDestroyingWall()
    {
        var near = Pokemon(Vector3.forward * 3.5f); var behind = Pokemon(Vector3.forward * 8); Physics.SyncTransforms();
        var wall = Effect("NinetalesAvalanche", Vector3.forward * 5); yield return new WaitForSeconds(0.4f);
        Assert.That(Get(wall, "WallActive"), Is.True);
        var wind = Effect("NinetalesBlizzard", Vector3.forward); yield return new WaitForSeconds(1.1f);
        Assert.That(Get(wall, "WallActive"), Is.True, "Blizzard must not destroy Avalanche early.");
        Assert.That(Hp(behind), Is.EqualTo(19711), "Only the first blast may pass through the wall.");
        var frost = (GameObject)wind.GetType().GetField("frost", Private).GetValue(wind); Assert.That(frost.activeSelf, Is.True);
        Assert.That(Hp(near), Is.LessThan(19000));
        yield return new WaitForSeconds(3.8f); Assert.That((bool)Get(wind, "IsSpawned"), Is.False);
    }
    [UnityTest]
    public IEnumerator AvalancheBlocksCasterAlliesAndEnemiesOnTheRealPlayerLayerButAllowsWallCrossing()
    {
        int players = LayerMask.NameToLayer("Players"), walls = LayerMask.NameToLayer("Wall");
        bool ignored = Physics.GetIgnoreLayerCollision(players, walls);
        try
        {
            // The real project disables this pair globally; the old Default-layer fixture hid the bug.
            Physics.IgnoreLayerCollision(players, walls, true);
            actor.gameObject.layer = players;
            var ally = Pokemon(new Vector3(1.1f, 0, 0), "Blue"); ally.gameObject.layer = players;
            var enemy = Pokemon(new Vector3(-1.1f, 0, 5)); enemy.gameObject.layer = players;
            var wall = Effect("NinetalesAvalanche", Vector3.forward * 2.5f);
            yield return new WaitForSeconds(0.4f); Physics.SyncTransforms();
            foreach (var target in new[] { actor, ally, enemy })
            {
                var movement = target.GetComponent(T("PlayerMovement"));
                for (int i = 0; i < 40; i++) Call(movement, "MoveWithWalls", (target == enemy ? Vector3.back : Vector3.forward) * 0.1f);
            }
            Assert.That(actor.transform.position.z, Is.LessThan(2), "The caster must not walk through its wall.");
            Assert.That(ally.transform.position.z, Is.LessThan(2), "Allies must be blocked too.");
            Assert.That(enemy.transform.position.z, Is.GreaterThan(3), "Enemies must be blocked from the reverse side.");
            Assert.That(Physics.GetIgnoreLayerCollision(players, walls), Is.True, "Do not change map-wide collision settings to fix this one wall.");
            var traversal = Activator.CreateInstance(T("WallCrossingTraversal"));
            Call(traversal, "Begin", actor.GetComponent(T("PlayerMovement")));
            Call(traversal, "Move", Vector3.forward * 4); Call(traversal, "End");
            Assert.That(actor.transform.position.z, Is.GreaterThan(3.5f)); Assert.That(Get(wall, "WallActive"), Is.True);
            actor.transform.position = Vector3.zero; Physics.SyncTransforms();
            yield return new WaitForSeconds(2.1f);
            Call(actor.GetComponent(T("PlayerMovement")), "MoveWithWalls", Vector3.forward * 4);
            Assert.That(actor.transform.position.z, Is.GreaterThan(3.5f), "The wall must stop blocking after collapse.");
        }
        finally { Physics.IgnoreLayerCollision(players, walls, ignored); }
    }

    private Component RealWildPokemon(Vector3 position)
    {
#if UNITY_EDITOR
        var ground = Node("Wild displacement ground"); ground.transform.position = Vector3.down * 0.5f; ground.AddComponent<BoxCollider>().size = new Vector3(40, 1, 40);
        var source = new NavMeshBuildSource { shape = NavMeshBuildSourceShape.Box, size = new Vector3(40, 1, 40), transform = ground.transform.localToWorldMatrix, area = 0 };
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/WildPokemons/WildPokemon.prefab");
        wildNavMesh = NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(prefab.GetComponent<NavMeshAgent>().agentTypeID), new List<NavMeshBuildSource> { source }, new Bounds(Vector3.zero, new Vector3(40, 10, 40)), Vector3.zero, Quaternion.identity);
        Assert.That(wildNavMesh, Is.Not.Null); wildNavMeshInstance = NavMesh.AddNavMeshData(wildNavMesh);
        var root = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity); objects.Add(root);
        var pokemon = root.GetComponent(T("Pokemon")); Set(pokemon, "baseStats", Get(actor, "BaseStats"));
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true); Variable(pokemon, "currentHp", 20000);
        Call(root.GetComponent(T("WildPokemonAI")), "Initialize", Activator.CreateInstance(T("WildPokemonAISettings")));
        Assert.That(root.GetComponent<NavMeshAgent>().isOnNavMesh, Is.True, "Fixture must use the real Farm agent type."); Assert.That(root.GetComponent<Rigidbody>().isKinematic, Is.True);
        return pokemon;
#else
        return null;
#endif
    }
    [UnityTest]
    public IEnumerator LegacyWallCrossingDashBypassesAvalancheThenRestoresWalkingCollisions()
    {
        actor.gameObject.layer = LayerMask.NameToLayer("Players");
        var movement = actor.GetComponent(T("PlayerMovement")); var controller = actor.GetComponent<CharacterController>();
        int include = controller.includeLayers.value, exclude = controller.excludeLayers.value, priority = controller.layerOverridePriority;
        Effect("NinetalesAvalanche", Vector3.forward * 2.5f); yield return new WaitForSeconds(0.4f); Physics.SyncTransforms();
        Call(movement, "StartDash", Vector3.forward, 4f, 0.25f);
        // The isolated fixture has input/update disabled; drive the same controller used by the live dash.
        for (int i = 0; i < 40; i++) controller.Move(Vector3.forward * 0.1f);
        Assert.That(actor.transform.position.z, Is.GreaterThan(3.5f));
        yield return new WaitForSeconds(0.3f);
        Assert.That(controller.includeLayers.value, Is.EqualTo(include)); Assert.That(controller.excludeLayers.value, Is.EqualTo(exclude)); Assert.That(controller.layerOverridePriority, Is.EqualTo(priority));
        actor.transform.position = Vector3.zero; Physics.SyncTransforms();
        for (int i = 0; i < 40; i++) controller.Move(Vector3.forward * 0.1f);
        Assert.That(actor.transform.position.z, Is.LessThan(2), "Normal walking must block again as soon as the dash ends.");
        Call(movement, "StartDash", Vector3.forward, 4f, 0.25f);
        Call(movement, "Teleport", Vector3.zero, Quaternion.identity, true);
        Assert.That(controller.excludeLayers.value, Is.EqualTo(exclude), "Teleport/death must immediately release the dash collision override.");
    }
    private IEnumerator BlizzardDisplacesRealWildPokemon(bool freezeOnHit)
    {
        var wild = RealWildPokemon(Vector3.forward * 4);
        if (freezeOnHit) { Call(Combat, "AddSnow", wild); Call(Combat, "AddSnow", wild); }
        Physics.SyncTransforms(); float start = wild.transform.position.z;
        Effect("NinetalesBlizzard", Vector3.forward); yield return new WaitForSeconds(1.1f);
        Assert.That(Hp(wild), Is.LessThan(20000), "Both blasts must still deal damage.");
        Assert.That(wild.transform.position.z - start, Is.EqualTo(1.5f).Within(0.15f), "The real NavMesh-driven, kinematic wild prefab must be shoved, including on a freezing hit.");
        if (freezeOnHit)
        {
            Assert.That(Has(wild, "Frozen"), Is.True);
            yield return new WaitForSeconds(0.9f);
            Assert.That(Has(wild, "Frozen"), Is.False);
            Assert.That(wild.GetComponent<NavMeshAgent>().isActiveAndEnabled, Is.True);
            Assert.That(wild.transform.position.z - start, Is.EqualTo(1.5f).Within(0.15f), "Unfreezing must not send a stationary wild Pokémon back to its pre-shove position.");
            Assert.That(wild.GetComponent<Rigidbody>().isKinematic, Is.True);
        }
        else { Assert.That(wild.GetComponent<Rigidbody>().isKinematic, Is.True); Assert.That(wild.GetComponent<NavMeshAgent>().isStopped, Is.False); }
    }
    [UnityTest]
    public IEnumerator BlizzardShovesRealNavMeshWildPokemon() => BlizzardDisplacesRealWildPokemon(false);
    [UnityTest]
    public IEnumerator BlizzardStillShovesWhenItsSecondBlastFreezesAWildPokemon() => BlizzardDisplacesRealWildPokemon(true);

    [UnityTest]
    public IEnumerator WildShoveStopsItsFullBodyAtPhysicalWallsAndDoesNotMoveObjectives()
    {
        var wild = RealWildPokemon(Vector3.forward * 3); var ai = wild.GetComponent(T("WildPokemonAI"));
        var wall = Node("Physical shove boundary"); wall.layer = LayerMask.NameToLayer("Wall"); wall.transform.position = Vector3.forward * 5;
        var collider = wall.AddComponent<BoxCollider>(); collider.center = Vector3.up * 1.25f; collider.size = new Vector3(4, 2.5f, 0.6f);
        Physics.SyncTransforms();
        Assert.That(Call(ai, "TryDisplace", Vector3.forward, 4f, 0.2f), Is.True);
        yield return new WaitForSeconds(0.35f);
        Assert.That(wild.transform.position.z, Is.GreaterThan(3.1f)); Assert.That(wild.transform.position.z, Is.LessThan(4.22f), "The capsule's full 0.5 radius must stop at the wall, not the agent's smaller avoidance radius.");
        Assert.That(wild.GetComponent<NavMeshAgent>().isStopped, Is.False);
        wild.GetType().GetProperty("Type").SetValue(wild, E("PokemonType", "Objective"));
        Assert.That(Call(ai, "TryDisplace", Vector3.back, 1.5f, 0.2f), Is.False, "Objectives remain immune to displacement.");
    }

    [UnityTest]
    public IEnumerator ServerBasicAttacksAdvanceOnlyOnLaunchAndThirdAttackAppliesSnow()
    {
        var target = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); var attack = Get(moves, "ServerBasicAttack");
        Call(attack, "PerformOnServer", target); yield return new WaitForSeconds(0.65f);
        Assert.That(Hp(target), Is.EqualTo(19900)); Assert.That(Get(attack, "Charge"), Is.EqualTo(1)); Assert.That(Call(Combat, "SnowCount", target), Is.EqualTo(0));
        Call(attack, "PerformOnServer", target); yield return new WaitForSeconds(0.65f);
        Assert.That(Hp(target), Is.EqualTo(19800)); Assert.That(Get(attack, "Charge"), Is.EqualTo(2));
        Call(attack, "PerformOnServer", target); yield return new WaitForSeconds(0.65f);
        Assert.That(Hp(target), Is.EqualTo(19473)); Assert.That(Get(attack, "Charge"), Is.EqualTo(0)); Assert.That(Call(Combat, "SnowCount", target), Is.EqualTo(1));
        Call(moves, "SetNinetalesVeilPresence", true);
        Call(attack, "PerformOnServer", target); yield return new WaitForSeconds(0.65f);
        Assert.That(Hp(target), Is.EqualTo(19146)); Assert.That(Get(attack, "Charge"), Is.EqualTo(0)); Assert.That(Call(Combat, "SnowCount", target), Is.EqualTo(2));
        Call(moves, "SetNinetalesVeilPresence", false);
    }
    [UnityTest]
    public IEnumerator InterruptedBasicNeverLaunchesAndReleasesItsActionLock()
    {
        var target = Pokemon(Vector3.forward * 2); var attack = Get(moves, "ServerBasicAttack");
        Call(attack, "PerformOnServer", target); Assert.That((int)Get(moves, "ServerActionLocks"), Is.GreaterThan(0));
        Variable(actor, "currentHp", 0); yield return new WaitForSeconds(0.4f);
        Assert.That(Hp(target), Is.EqualTo(20000)); Assert.That(Get(attack, "Charge"), Is.EqualTo(0)); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator DeathDuringUniteAndVeilReleasesLocksAndAreaModifiers()
    {
        var ally = Pokemon(Vector3.forward * 4, "Blue"); var veil = Effect("NinetalesAuroraVeil", Vector3.forward);
        yield return new WaitForSeconds(0.6f); Assert.That(Call(ally, "GetDamageReduction"), Is.EqualTo(0.35f).Within(0.001f));
        var globe = Effect("NinetalesSnowGlobe", Vector3.forward); Assert.That((int)Get(moves, "ServerActionLocks"), Is.GreaterThan(0));
        Variable(actor, "currentHp", 0); yield return null; yield return null;
        Assert.That((bool)Get(veil, "IsSpawned"), Is.False); Assert.That((bool)Get(globe, "IsSpawned"), Is.False);
        Assert.That(Call(ally, "GetDamageReduction"), Is.EqualTo(0)); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero); Assert.That(Get(moves, "NinetalesInsideVeil"), Is.False); Assert.That(Has(actor, "Unstoppable"), Is.False);
    }
}

public class NinetalesHostTests : NinetalesServerTests
{
    protected override bool Host => true;
}
