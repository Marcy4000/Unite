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

public class JolteonServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
    private readonly List<GameObject> networkPrefabs = new List<GameObject>();
    private Component server, client, lateClient, actor, player, moves;
    private GameObject fixture;

    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static object Field(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static object Call(object target, string name, params object[] arguments) => target.GetType()
        .GetMethod(name, arguments.Select(argument => argument.GetType()).ToArray()).Invoke(target, arguments);
    private static object Invoke(object target, string name, params object[] arguments) => target.GetType().GetMethod(name, Private).Invoke(target, arguments);
    private static void Variable(object target, string name, object value) => Field(target, name).GetType().GetProperty("Value").SetValue(Field(target, name), value);
    private static int Hp(Component pokemon) => (int)Get(pokemon, "CurrentHp");
    private GameObject Node(string name) { var node = new GameObject(name); objects.Add(node); return node; }

    private Component Manager(string name, ushort port)
    {
        GameObject root = Node(name);
        Component manager = root.AddComponent(T("Unity.Netcode.NetworkManager"));
        Component transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        Call(transport, "SetConnectionData", "127.0.0.1", port, "127.0.0.1");
        object config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig"));
        manager.GetType().GetField("NetworkConfig").SetValue(manager, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        foreach (GameObject prefab in networkPrefabs) Call(manager, "AddNetworkPrefab", prefab);
        return manager;
    }

    private static IEnumerator Until(Func<bool> condition, string message, float timeout = 8f)
    {
        float deadline = Time.realtimeSinceStartup + timeout;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, message);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
#if UNITY_EDITOR
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("External network session active.");
        foreach (string name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        {
            GameObject map = Node(name);
            map.tag = name;
            map.transform.position = new Vector3(0f, name == "WalkableWithWalls" ? -30.5f : -40.5f, 0f);
            map.AddComponent<BoxCollider>().size = new Vector3(100f, 1f, 100f);
        }
        GameObject floor = Node("Jolteon test ground");
        floor.transform.position = new Vector3(0f, -0.1f, 0f);
        floor.AddComponent<BoxCollider>().size = new Vector3(100f, 0.2f, 100f);
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab");
        networkPrefabs.Add(fixture);
        networkPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Jolteon/JoltSwift.prefab"));
        foreach (string name in new[] { "Agility", "Charge", "ThunderFang", "Discharge", "Electroweb", "WebMark", "LightningBlitz", "Overcharge", "Ready" })
            networkPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Jolteon/Rework/" + name + ".prefab"));
        foreach (string name in new[] { "ElectrowebProjectile", "JoltElectroWebMark", "JolteonDischarge" })
            networkPrefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Jolteon/" + name + ".prefab"));
        Assert.That(networkPrefabs, Has.All.Not.Null);
        server = Manager("Jolteon server", 0);
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        Component transport = server.GetComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        ushort port = (ushort)Get(Call(transport, "GetLocalEndpoint"), "Port");
        client = Manager("Jolteon remote client", port);
        Assert.That(Call(client, "StartClient"), Is.True);
        yield return Until(() => (bool)Get(client, "IsConnectedClient"), "Remote client did not connect");
#else
        Assert.Ignore("Editor fixture required.");
        yield break;
#endif
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (lateClient != null) Call(lateClient, "Shutdown", false);
        if (client != null) Call(client, "Shutdown", false);
        yield return null;
        if (server != null)
        {
            foreach (GameObject root in objects.ToArray())
            {
                if (root == null) continue;
                Component network = root.GetComponent(T("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned") && (bool)Get(server, "IsServer")) Call(network, "Despawn", true);
            }
            Call(server, "Shutdown", false);
        }
        yield return null;
        if (client != null) UnityEngine.Object.Destroy(client.gameObject);
        if (lateClient != null) UnityEngine.Object.Destroy(lateClient.gameObject);
        yield return null;
        foreach (GameObject root in objects) if (root != null) UnityEngine.Object.Destroy(root);
        foreach (ScriptableObject definition in definitions) if (definition != null) UnityEngine.Object.Destroy(definition);
        objects.Clear();
        definitions.Clear();
        networkPrefabs.Clear();
        yield return null;
    }

    private object PrepareActor(int level, bool hostOwned = false)
    {
#if UNITY_EDITOR
        GameObject root = UnityEngine.Object.Instantiate(fixture);
        root.name = "Jolteon actor";
        root.layer = LayerMask.NameToLayer("Players");
        objects.Add(root);
        actor = root.GetComponent(T("Pokemon"));
        player = root.GetComponent(T("PlayerManager"));
        moves = root.GetComponent(T("MovesController"));
        var definition = UnityEngine.Object.Instantiate(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Jolteon.asset"));
        definitions.Add((ScriptableObject)definition);
        Set(actor, "baseStats", definition);
        Set(moves, "pokemon", actor);
        Set(moves, "playerManager", player);
        Component network = root.GetComponent(T("Unity.Netcode.NetworkObject"));
        Call(network, "SpawnWithOwnership", 99UL, true);
        if (hostOwned) Call(network, "ChangeOwnership", 0UL);
        Variable(actor, "currentHp", 5000);
        Variable(actor, "currentLevel", level);
        Call(actor, "UpdateTeamRPC", E("Team", "Blue"));
        BindHp(actor);
        Physics.SyncTransforms();
        object combat = Get(moves, "Combat");
        Assert.That(Get(combat, "Character").GetType().Name, Is.EqualTo("JolteonCharacterRuntime"));
        return combat;
#else
        return null;
#endif
    }

    private void BindHp(Component pokemon)
    {
        object hp = Field(pokemon, "currentHp");
        FieldInfo changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
    }

    private Component Enemy(Vector3 position)
    {
#if UNITY_EDITOR
        GameObject root = UnityEngine.Object.Instantiate(fixture);
        root.transform.position = position;
        root.layer = LayerMask.NameToLayer("Players");
        objects.Add(root);
        if (root.GetComponent<Collider>() == null) root.AddComponent<CapsuleCollider>();
        Component pokemon = root.GetComponent(T("Pokemon"));
        Component controller = root.GetComponent(T("MovesController"));
        Component manager = root.GetComponent(T("PlayerManager"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase"));
        definitions.Add(definition);
        Set(definition, "pokemonName", "JolteonTarget");
        Set(definition, "maxHp", Enumerable.Repeat(5000, 15).ToArray());
        Set(definition, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "spAttack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "defense", new int[15]);
        Set(definition, "spDefense", new int[15]);
        Set(definition, "critRate", new int[15]);
        Set(definition, "speed", Enumerable.Repeat(3500, 15).ToArray());
        Set(definition, "passive", E("AvailablePassives", "EmptyPassive"));
        Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0));
        Set(definition, "evolutions", Get(AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Jolteon.asset"), "Evolutions"));
        Set(pokemon, "baseStats", definition);
        Set(controller, "pokemon", pokemon);
        Set(controller, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 99UL, true);
        Variable(pokemon, "currentHp", 5000);
        Call(pokemon, "UpdateTeamRPC", E("Team", "Orange"));
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, E("PokemonType", "Player"));
        BindHp(pokemon);
        Physics.SyncTransforms();
        return pokemon;
#else
        return null;
#endif
    }

    private object Equip(object combat, string name, int slot)
    {
        Assert.That(Call(combat, "Equip", E("AvailableMoves", name)), Is.True, name);
        return Call(Get(combat, "Loadout"), "Get", slot);
    }

    private object Cast(object combat, object ability, string name, int slot, long request, Vector3 direction, long followUp = 0)
    {
        Call(combat, "Receive", request, slot, (int)Get(ability, "Revision"), E("AvailableMoves", name), direction, ulong.MaxValue, followUp);
        return Call(Get(combat, "Coordinator"), "FindRequest", request);
    }

    private static IEnumerable<Component> Effects(string effect)
    {
        return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(component => T("JoltEffectObject").IsInstanceOfType(component) && (bool)Get(component, "IsServer")
                && Field(component, "effect").ToString() == effect).Cast<Component>();
    }

    private static IEnumerable<Component> Stars()
    {
        return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(component => T("JoltSwiftProjectile").IsInstanceOfType(component) && (bool)Get(component, "IsServer")).Cast<Component>();
    }

    private object Availability(object combat, object ability) => Call(Get(ability, "Resources"), "Read", (double)Get(combat, "Now"));

    [UnityTest]
    public IEnumerator SevenMovesUseRealServerCastsAndCooldowns()
    {
        string[] names = { "JoltSwift", "JoltAgility", "JoltThunderFang", "JoltDischarge", "JoltCharge", "JoltElectroWeb", "JolteonUnite" };
        int[] slots = { 0, 1, 0, 0, 1, 1, 2 };
        foreach (int index in Enumerable.Range(0, names.Length))
        {
            object combat = PrepareActor(12);
            object ability = Equip(combat, names[index], slots[index]);
            if (slots[index] == 2) Call(combat, "GrantUniteEnergy", 100000);
            object session = Cast(combat, ability, names[index], slots[index], 1L, Vector3.forward);
            Assert.That(session, Is.Not.Null, names[index]);
            yield return Until(() => (bool)Get(session, "Accepted") || Get(session, "Phase").ToString() == "Ended", names[index]);
            Assert.That(Get(session, "Accepted"), Is.True, names[index]);
            if (slots[index] != 2) Assert.That(Get(Availability(combat, ability), "Ready"), Is.False, names[index]);
            if (slots[index] == 2) Assert.That(Get(combat, "UniteEnergy"), Is.Zero);
            Variable(actor, "currentHp", 0);
            yield return null;
            Assert.That(Get(session, "Phase").ToString(), Is.EqualTo("Ended"), names[index]);
            Call(actor.GetComponent(T("Unity.Netcode.NetworkObject")), "Despawn", true);
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator ChargePreservesMeterAndElectrowebConsumesIt()
    {
        object combat = PrepareActor(12);
        object runtime = Get(combat, "Character");
        Call(runtime, "BeginUniteOvercharge", 0f);
        object charge = Equip(combat, "JoltCharge", 1);
        object chargeSession = Cast(combat, charge, "JoltCharge", 1, 1L, Vector3.forward);
        yield return Until(() => Get(chargeSession, "Phase").ToString() == "Ended", "Charge did not complete");
        Assert.That(Get(runtime, "PassiveReady"), Is.True);
        combat = PrepareActor(12);
        runtime = Get(combat, "Character");
        Call(runtime, "BeginUniteOvercharge", 0f);
        object web = Equip(combat, "JoltElectroWeb", 1);
        object webSession = Cast(combat, web, "JoltElectroWeb", 1, 2L, Vector3.forward);
        yield return Until(() => (bool)Get(webSession, "Accepted") || Get(webSession, "Phase").ToString() == "Ended", "Web not accepted");
        Assert.That(Get(webSession, "Accepted"), Is.True);
        Assert.That((float)Get(runtime, "PassiveAmount"), Is.LessThan(5f));
    }

    [UnityTest]
    public IEnumerator RepeatedRecastPresentationDoesNotRestartTheCooldownRing()
    {
#if UNITY_EDITOR
        object runtime = Get(PrepareActor(12, true), "Character");
        GameObject hud = Node("Jolteon cooldown HUD");
        hud.SetActive(false);
        Component ui = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/MoveUI.prefab"),
            hud.transform).GetComponent(T("MoveUI"));
        Component holder = hud.AddComponent(T("MovesHolderUI"));
        Array slots = Array.CreateInstance(T("MoveUI"), 2);
        slots.SetValue(ui, 0);
        slots.SetValue(ui, 1);
        Set(holder, "moveUIs", slots);
        Component battleUi = hud.AddComponent(T("BattleUIManager"));
        Set(battleUi, "currentUI", holder);
        FieldInfo singleton = T("BattleUIManager").GetField("instance");
        object previous = singleton.GetValue(null);
        singleton.SetValue(null, battleUi);
        try
        {
            Assert.That(Get(player, "IsLocalHuman"), Is.True);
            Call(ui, "StartCooldown", 7f, 7f);
            double until = (double)Get(Get(moves, "Combat"), "Now") + 2d;
            Call(runtime, "SetRecastWindow", E("AvailableMoves", "JoltThunderFang"), true, until, true);
            Invoke(runtime, "Present");
            float duration = (float)Field(ui, "secondaryCooldownDuration");
            yield return new WaitForSeconds(0.2f);
            Call(runtime, "SetRecastWindow", E("AvailableMoves", "JoltThunderFang"), true, until, true);
            Invoke(runtime, "Present");
            Invoke(runtime, "Present");
            Invoke(ui, "LateUpdate");
            Assert.That(Field(ui, "secondaryCooldownDuration"), Is.EqualTo(duration));
            Component ring = (Component)Field(ui, "secondaryCd");
            Assert.That(ring.gameObject.activeSelf, Is.True);
            Assert.That(Get(ring, "fillAmount"), Is.LessThan(0.95f).And.GreaterThan(0f));
            Assert.That(((GameObject)Field(ui, "cdBg")).activeSelf, Is.False);
            Call(runtime, "SetRecastWindow", E("AvailableMoves", "JoltThunderFang"), true, until + 2d, true);
            Invoke(runtime, "Present");
            Assert.That((float)Field(ui, "secondaryCooldownDuration"), Is.GreaterThan(duration));
            Assert.That(Get(ring, "fillAmount"), Is.EqualTo(1f).Within(0.01f));
            Call(runtime, "SetRecastWindow", E("AvailableMoves", "JoltThunderFang"), false, 0d, false);
            Invoke(runtime, "Present");
            Assert.That(ring.gameObject.activeSelf, Is.False);
        }
        finally { singleton.SetValue(null, previous); }
#else
        Assert.Ignore("Editor fixture required.");
        yield break;
#endif
    }

    [UnityTest]
    public IEnumerator DashAimCancellationPreservesEarnedRecastAndHud()
    {
        yield return DashWindowScenario("JoltThunderFang", false);
        yield return DashWindowScenario("JoltDischarge", false);
    }

    [UnityTest]
    public IEnumerator DeathInterruptsCommittedDashOnceAndClearsRecast()
    {
        yield return DashWindowScenario("JoltThunderFang", true);
        yield return DashWindowScenario("JoltDischarge", true);
    }

    private IEnumerator DashWindowScenario(string name, bool kill)
    {
        object combat = PrepareActor(12);
        object runtime = Get(combat, "Character");
        Call(runtime, "BeginUniteOvercharge", 0f);
        Enemy(Vector3.forward * 0.8f);
        object ability = Equip(combat, name, 0);
        object session = Cast(combat, ability, name, 0, 1L, Vector3.forward);
        Assert.That(session, Is.Not.Null);
        yield return Until(() => (bool)Get(runtime, "RecastAvailable"), name + " did not open its window");
        Assert.That(Get(runtime, "RecastEmpowered"), Is.True);
        Array replicas = (Array)Field(combat, "replicas");
        object slot = replicas.GetValue(0);
        Assert.That(slot.GetType().GetField("FollowUpReady").GetValue(slot), Is.True);
        long castId = (long)Get(session, "CastId");
        Assert.That(slot.GetType().GetField("CastId").GetValue(slot), Is.EqualTo(castId));
        if (kill)
        {
            object coordinator = Get(combat, "Coordinator");
            var observer = new EndObserver(session);
            EventInfo changed = coordinator.GetType().GetEvent("Changed");
            Delegate handler = typeof(EndObserver).GetMethod("OnChanged").MakeGenericMethod(T("Unite.Combat.CastSession"))
                .CreateDelegate(changed.EventHandlerType, observer);
            changed.AddEventHandler(coordinator, handler);
            Variable(actor, "currentHp", 0);
            yield return null;
            Assert.That(Get(session, "Outcome").ToString(), Is.EqualTo("Interrupted"));
            Assert.That(Get(runtime, "RecastAvailable"), Is.False);
            Call(coordinator, "End", session, E("Unite.Combat.CastOutcome", "Interrupted"));
            Assert.That(observer.Ends, Is.EqualTo(1));
            changed.RemoveEventHandler(coordinator, handler);
        }
        else
        {
            object aim = Activator.CreateInstance(T(name));
            aim.GetType().GetField("IsActive").SetValue(aim, true);
            Call(aim, "Cancel");
            Assert.That(Get(runtime, "RecastAvailable"), Is.True);
            Assert.That(Get(runtime, "RecastEmpowered"), Is.True);
            Cast(combat, ability, name, 0, 2L, Vector3.right, castId);
            yield return null;
            Assert.That(Get(runtime, "RecastAvailable"), Is.False);
        }
        Call(actor.GetComponent(T("Unity.Netcode.NetworkObject")), "Despawn", true);
        yield return null;
    }

    public sealed class EndObserver
    {
        private readonly object watched;
        public int Ends;
        public EndObserver(object session) { watched = session; }
        public void OnChanged<TSession>(TSession session)
        {
            if (ReferenceEquals(watched, session) && Get(session, "Phase").ToString() == "Ended") Ends++;
        }
    }

    [UnityTest]
    public IEnumerator PrelearnUniteEnergyBasicCycleAndDeathAreServerOwned()
    {
        object combat = PrepareActor(0);
        Assert.That(Call(combat, "Equip", E("AvailableMoves", "JolteonUnite")), Is.False);
        Call(combat, "GrantUniteEnergy", 2500);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(2500));
        Set(combat, "nextEnergyTick", (double)Get(combat, "Now") - 1d);
        Call(combat, "Tick");
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(3400));
        object runtime = Get(combat, "Character");
        yield return Until(() => Remote(player) != null, "Basic attack observer did not spawn");
        Component observer = Remote(player);
        Set(observer.GetComponent(T("MovesController")), "playerManager", observer);
        Component enemy = Enemy(Vector3.forward * 1.5f);
        for (int attack = 0; attack < 3; attack++)
        {
            Call(runtime, "PerformBasicAttack", enemy);
            Assert.That(Get(runtime, "AttackCharge"), Is.EqualTo((byte)((attack + 1) % 3)));
        }
        Variable(actor, "currentLevel", 7);
        Assert.That(Call(combat, "Equip", E("AvailableMoves", "JolteonUnite")), Is.True);
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(3400));
        Call(runtime, "BeginUniteOvercharge", 5f);
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Get(runtime, "AttackCharge"), Is.EqualTo((byte)0));
        Assert.That(Get(runtime, "PassiveAmount"), Is.EqualTo(0f));
    }

    private Component Remote(Component source)
    {
        object spawnManager = Get(client, "SpawnManager");
        var spawned = (IDictionary)spawnManager.GetType().GetField("SpawnedObjects").GetValue(spawnManager);
        object id = Get(source, "NetworkObjectId");
        return spawned.Contains(id) ? ((Component)spawned[id]).GetComponent(source.GetType()) : null;
    }

    [UnityTest]
    public IEnumerator OwnerPositionCorrectionsRejectStaleAndNonFinitePackets()
    {
        object combat = PrepareActor(12);
        object runtime = Get(combat, "Character");
        yield return Until(() => Remote(player) != null, "Owner replica did not spawn");
        Component remotePlayer = Remote(player);
        Component remotePokemon = remotePlayer.GetComponent(T("Pokemon"));
        Component remoteMoves = remotePlayer.GetComponent(T("MovesController"));
        Set(remotePokemon, "baseStats", Get(actor, "BaseStats"));
        Set(remoteMoves, "pokemon", remotePokemon);
        Set(remoteMoves, "playerManager", remotePlayer);
        Call(player.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", (ulong)Get(client, "LocalClientId"));
        yield return Until(() => (bool)Get(remotePlayer, "IsOwner"), "Replica did not gain ownership");
        yield return Until(() => Get(remotePlayer.GetComponent(T("MovesController")), "Combat") != null
            && Get(Get(remotePlayer.GetComponent(T("MovesController")), "Combat"), "Character") != null, "Owner runtime unavailable");
        object remoteRuntime = Get(Get(remotePlayer.GetComponent(T("MovesController")), "Combat"), "Character");
        Vector3 correction = new Vector3(2f, 0f, 1f);
        Call(runtime, "SetTraversalPosition", 7L, 2, correction, Vector3.forward, true);
        yield return Until(() => (remotePlayer.transform.position - correction).sqrMagnitude < 0.01f, "Owner missed correction");
        object snapshot = JsonUtility.FromJson((string)Call(runtime, "CapturePresentationState"), T("JolteonCharacterRuntime+PresentationState"));
        Assert.That(snapshot.GetType().GetField("TraversalCastId").GetValue(snapshot), Is.EqualTo(7L));
        Assert.That(snapshot.GetType().GetField("TraversalSequence").GetValue(snapshot), Is.EqualTo(2));
        Assert.That(snapshot.GetType().GetField("TraversalActive").GetValue(snapshot), Is.True);
        Call(remoteRuntime, "ReceiveTraversalPosition", 7L, 1, Vector3.one * 20f, Vector3.forward, true);
        Call(remoteRuntime, "ReceiveTraversalPosition", 6L, 99, Vector3.one * 20f, Vector3.forward, true);
        Call(remoteRuntime, "ReceiveTraversalPosition", 7L, 3, new Vector3(float.NaN, 0f, 0f), Vector3.forward, true);
        Assert.That((remotePlayer.transform.position - correction).sqrMagnitude, Is.LessThan(0.01f));
        Vector3 final = new Vector3(3f, 0f, 1f);
        Call(runtime, "SetTraversalPosition", 7L, 3, final, Vector3.right, false);
        yield return Until(() => (remotePlayer.transform.position - final).sqrMagnitude < 0.01f, "Owner missed release correction");
        Assert.That(Field(runtime, "traversalActive"), Is.False);
        Assert.That(Field(remoteRuntime, "receivedTraversalActive"), Is.False);
    }

    [UnityTest]
    public IEnumerator HostOwnedTraversalDoesNotApplyItsRpcTwice()
    {
        if (!Host) yield break;
        object combat = PrepareActor(12, true);
        object runtime = Get(combat, "Character");
        Vector3 position = new Vector3(2f, 0f, 3f);
        actor.transform.position = position;
        Call(runtime, "SetTraversalPosition", 1L, 1, position, Vector3.forward, true);
        yield return null;
        Assert.That(actor.transform.position, Is.EqualTo(position));
        Assert.That(Field(runtime, "receivedTraversalCastId"), Is.EqualTo(0L));
        Call(runtime, "SetTraversalPosition", 1L, 2, position, Vector3.forward, false);
        Assert.That(Field(runtime, "traversalActive"), Is.False);
    }

    [UnityTest]
    public IEnumerator ServerDashKeepsItsBaselineAndReleasesItOnDeath()
    {
        object combat = PrepareActor(12);
        object runtime = Get(combat, "Character");
        object ability = Equip(combat, "JoltDischarge", 0);
        object session = Cast(combat, ability, "JoltDischarge", 0, 1L, Vector3.forward);
        yield return Until(() => (bool)Field(runtime, "traversalActive"), "Dash never began traversal");
        Assert.That((long)Field(runtime, "traversalCastId"), Is.EqualTo(Get(session, "CastId")));
        Assert.That((int)Field(runtime, "traversalSequence"), Is.GreaterThan(0));
        actor.transform.position += Vector3.right * 50f;
        yield return null;
        Assert.That(actor.transform.position.x, Is.LessThan(10f));
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Field(runtime, "traversalActive"), Is.False);
    }

    [UnityTest]
    public IEnumerator InvalidRequestsDoNotSpendCooldownOrMeter()
    {
        object combat = PrepareActor(12);
        object runtime = Get(combat, "Character");
        Call(runtime, "BeginUniteOvercharge", 0f);
        object ability = Equip(combat, "JoltElectroWeb", 1);
        object rejected = Cast(combat, ability, "JoltElectroWeb", 1, 1L, new Vector3(float.NaN, 0f, 0f));
        yield return null;
        Assert.That(rejected, Is.Null);
        Assert.That(Get(Availability(combat, ability), "Ready"), Is.True);
        Assert.That(Get(runtime, "PassiveReady"), Is.True);
    }

    [UnityTest]
    public IEnumerator EarnedFangWindowExpiresWithoutARecast()
    {
        object combat = PrepareActor(12);
        object runtime = Get(combat, "Character");
        Call(runtime, "BeginUniteOvercharge", 0f);
        object ability = Equip(combat, "JoltThunderFang", 0);
        object session = Cast(combat, ability, "JoltThunderFang", 0, 1L, Vector3.forward);
        yield return Until(() => (bool)Get(runtime, "RecastAvailable"), "Fang did not open its window");
        yield return Until(() => Get(session, "Phase").ToString() == "Ended", "Fang window did not expire", 6f);
        Assert.That(Get(runtime, "RecastAvailable"), Is.False);
        Array replicas = (Array)Field(combat, "replicas");
        Assert.That(replicas.GetValue(0).GetType().GetField("FollowUpReady").GetValue(replicas.GetValue(0)), Is.False);
    }

    [UnityTest]
    public IEnumerator AgilityEffectOutlivesCastAndDiesWithActor()
    {
        object combat = PrepareActor(12);
        object ability = Equip(combat, "JoltAgility", 1);
        object session = Cast(combat, ability, "JoltAgility", 1, 1L, Vector3.forward);
        yield return Until(() => Get(session, "Phase").ToString() == "Ended", "Agility did not finish");
        yield return Until(() => Effects("Agility").Any(), "Agility lease did not survive recovery");
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Effects("Agility").Any(), Is.False);
    }

    [UnityTest]
    public IEnumerator DeathBeforeUniteRushRefundsEnergyOnlyOnce()
    {
        object combat = PrepareActor(12);
        object runtime = Get(combat, "Character");
        object ability = Equip(combat, "JolteonUnite", 2);
        Call(combat, "GrantUniteEnergy", 100000);
        bool spentBeforeInterruption = false;
        Action interrupt = () =>
        {
            spentBeforeInterruption = (int)Get(combat, "UniteEnergy") == 0;
            Variable(actor, "currentHp", 0);
        };
        EventInfo offensiveAction = actor.GetType().GetEvent("OnOffensiveAction");
        offensiveAction.AddEventHandler(actor, interrupt);
        object session;
        try
        {
            session = Cast(combat, ability, "JolteonUnite", 2, 1L, Vector3.forward);
            yield return Until(() => Get(session, "Phase").ToString() == "Ended", "Unite interruption did not finish");
        }
        finally { offensiveAction.RemoveEventHandler(actor, interrupt); }
        Assert.That(spentBeforeInterruption, Is.True);
        Assert.That(Get(session, "Accepted"), Is.True);
        Assert.That(Field(runtime, "uniteCastId"), Is.EqualTo(0L));
        Assert.That(Get(session, "Outcome").ToString(), Is.EqualTo("Interrupted"));
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(90000));
        Call(combat, "Tick");
        Assert.That(Get(combat, "UniteEnergy"), Is.EqualTo(90000));
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator UniteRejectsInvalidSteeringAndCleansUpOnDeath()
    {
        object combat = PrepareActor(12);
        object runtime = Get(combat, "Character");
        object ability = Equip(combat, "JolteonUnite", 2);
        Call(combat, "GrantUniteEnergy", 100000);
        object session = Cast(combat, ability, "JolteonUnite", 2, 1L, Vector3.forward);
        yield return Until(() => (long)Field(runtime, "uniteCastId") != 0, "Unite did not reach its rush checkpoint");
        long castId = (long)Get(session, "CastId");
        object execution = Field(runtime, "uniteExecution");
        Set(runtime, "nextSteeringReceive", 0f);
        Call(runtime, "ReceiveUniteSteering", castId + 1, Vector2.right);
        Call(runtime, "ReceiveUniteSteering", castId, new Vector2(float.NaN, 0f));
        Assert.That(Field(execution, "steering"), Is.EqualTo(Vector3.zero));
        Call(runtime, "ReceiveUniteSteering", castId, Vector2.right);
        Assert.That(Field(execution, "steering"), Is.EqualTo(Vector3.right));
        Assert.That(Get(combat, "UniteEnergy"), Is.Zero);
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Field(runtime, "traversalActive"), Is.False);
        Assert.That(Field(runtime, "uniteCastId"), Is.EqualTo(0L));
        Assert.That(Get(combat, "UniteEnergy"), Is.Zero);
    }

    [UnityTest]
    public IEnumerator SwiftChildrenOwnLeasesAndDisappearOnActorDeath()
    {
        object combat = PrepareActor(12);
        Component first = Enemy(new Vector3(10f, 0f, 10f));
        Component second = Enemy(new Vector3(12f, 0f, 10f));
        object ability = Equip(combat, "JoltSwift", 0);
        object session = Cast(combat, ability, "JoltSwift", 0, 1L, Vector3.forward);
        yield return Until(() => Stars().Any(), "Swift root did not spawn");
        Component root = Stars().First(star => (bool)Field(star, "isBigStar"));
        int before = Hp(first);
        Invoke(root, "OnTriggerEnter", first.GetComponent<Collider>());
        Assert.That(Hp(first), Is.LessThan(before));
        yield return Until(() => Stars().Count(star => !(bool)Field(star, "isBigStar")) == 3, "Swift children did not spawn");
        yield return Until(() => Get(session, "Phase").ToString() == "Ended", "Swift recovery did not end");
        Component[] children = Stars().Where(star => !(bool)Field(star, "isBigStar")).ToArray();
        Assert.That(children.Length, Is.EqualTo(3));
        foreach (Component child in children)
        {
            object handle = Field(child, "asset");
            Assert.That(handle.GetType().GetMethod("IsValid").Invoke(handle, null), Is.True);
        }
        before = Hp(second);
        Invoke(children[0], "OnTriggerEnter", second.GetComponent<Collider>());
        Assert.That(Hp(second), Is.LessThan(before));
        yield return null;
        Assert.That(Stars().Count(), Is.EqualTo(2));
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(Stars().Any(), Is.False);
    }

    [UnityTest]
    public IEnumerator WebMarksHaveIndependentTargetsAndLateObserverState()
    {
        PrepareActor(12);
        Component first = Enemy(new Vector3(5f, 0f, 5f));
        Component second = Enemy(new Vector3(7f, 0f, 5f));
        Type requestType = T("JoltEffectRequest");
        foreach (Component target in new[] { first, second })
        {
            object request = Activator.CreateInstance(requestType, new object[] { player, null });
            requestType.GetMethod("Load").Invoke(request, new object[] { E("JoltEffect", "WebMark"), target.transform.position,
                Vector3.forward, false, false, false, (ulong)Get(target, "NetworkObjectId") });
        }
        yield return Until(() => Effects("WebMark").Count() == 2, "Web marks did not initialize");
        Component firstMark = Effects("WebMark").Single(mark => ReferenceEquals(Field(mark, "target"), first));
        Component secondMark = Effects("WebMark").Single(mark => ReferenceEquals(Field(mark, "target"), second));
        Component transport = server.GetComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        ushort port = (ushort)Get(Call(transport, "GetLocalEndpoint"), "Port");
        lateClient = Manager("Jolteon late observer", port);
        Assert.That(Call(lateClient, "StartClient"), Is.True);
        yield return Until(() => (bool)Get(lateClient, "IsConnectedClient"), "Late observer did not connect");
        object spawnManager = Get(lateClient, "SpawnManager");
        var spawned = (IDictionary)spawnManager.GetType().GetField("SpawnedObjects").GetValue(spawnManager);
        ulong markId = (ulong)Get(secondMark, "NetworkObjectId");
        yield return Until(() => spawned.Contains(markId), "Late observer missed mark");
        Component observed = ((Component)spawned[markId]).GetComponent(T("JoltEffectObject"));
        yield return Until(() => (bool)Field(observed, "initialized"), "Late observer did not bind mark state");
        Assert.That(Get(Field(observed, "target"), "NetworkObjectId"), Is.EqualTo(Get(second, "NetworkObjectId")));
        Variable(first, "currentHp", 0);
        yield return null;
        Assert.That(firstMark == null || !(bool)Get(firstMark, "IsSpawned"), Is.True);
        Assert.That((bool)Get(secondMark, "IsSpawned"), Is.True);
        Variable(actor, "currentHp", 0);
        yield return null;
        Assert.That(secondMark == null || !(bool)Get(secondMark, "IsSpawned"), Is.True);
    }
}

public sealed class JolteonHostTests : JolteonServerTests
{
    protected override bool Host => true;
}
