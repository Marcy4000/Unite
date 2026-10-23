using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

// A real NGO host, isolated from lobby/UI singletons. These tests use production
// Pokemon RPCs and source leases, not simulated damage or copied shield rules.
public class MeowsticEffectPlayModeTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject networkRoot;
    private Component manager, recipient, attacker;
    private readonly List<GameObject> roots = new List<GameObject>();
    private readonly List<IDisposable> leases = new List<IDisposable>();
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Value(string type, string name) => Enum.Parse(GameType(type), name);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethods()
        .First(candidate => candidate.Name == method && candidate.GetParameters().Length == args.Length
            && candidate.GetParameters().Select((parameter, i) => args[i] == null || parameter.ParameterType.IsInstanceOfType(args[i])).All(match => match)).Invoke(target, args);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static void NetworkValue(Component pokemon, string field, object value)
    {
        object variable = GameType("Pokemon").GetField(field, Private).GetValue(pokemon);
        variable.GetType().GetProperty("Value").SetValue(variable, value);
    }
    private Component SpawnPokemon(string name)
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/MeowsticTests/Fixtures/Pokemon.prefab"));
#else
        Assert.Ignore("The isolated network fixture is an Editor-only prefab.");
        return null;
#endif
#if UNITY_EDITOR
        root.name = name; roots.Add(root);
        Component network = root.GetComponent(GameType("Unity.Netcode.NetworkObject"));
        Component pokemon = root.GetComponent(GameType("Pokemon"));
        GameType("Pokemon").GetField("baseStats", Private).SetValue(pokemon, AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/MeowsticMale.asset"));
        Call(network, "Spawn", true);
        NetworkValue(pokemon, "currentHp", (int)Call(pokemon, "GetMaxHp"));
        return pokemon;
#endif
    }
    private IDisposable OwnModifier(Component pokemon, string stat, short amount, int family = 99)
    {
        var lease = (IDisposable)Activator.CreateInstance(GameType("MeowsticModifier"), pokemon, Value("Stat", stat), amount, true, stat != "PhysicalDamageReduction", family, 0f);
        leases.Add(lease); return lease;
    }
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (GameType("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("An existing network session must not be modified by this isolated fixture.");
        networkRoot = new GameObject("Meowstic test host");
        manager = networkRoot.AddComponent(GameType("Unity.Netcode.NetworkManager"));
        Component transport = networkRoot.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethod("SetConnectionData", new[] { typeof(string), typeof(ushort), typeof(string) }).Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        object config = manager.GetType().GetField("NetworkConfig").GetValue(manager);
        if (config == null)
        {
            config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
            manager.GetType().GetField("NetworkConfig").SetValue(manager, config);
        }
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        config.GetType().GetField("ForceSamePrefabs").SetValue(config, false);
#if UNITY_EDITOR
        object prefabs = config.GetType().GetField("Prefabs").GetValue(config);
        object entry = Activator.CreateInstance(GameType("Unity.Netcode.NetworkPrefab"));
        var fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/MeowsticTests/Fixtures/Pokemon.prefab");
        Assert.That(fixture, Is.Not.Null, "Run Tools/Meowstic/Create Missing Rework Prefabs first.");
        entry.GetType().GetField("Prefab").SetValue(entry, fixture);
        Call(prefabs, "Add", entry);
#endif
        Assert.That(Call(manager, "StartHost"), Is.True);
        recipient = SpawnPokemon("Protected Meowstic fixture"); attacker = SpawnPokemon("Attacking Meowstic fixture");
        yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (IDisposable lease in leases) lease.Dispose(); leases.Clear();
        if (manager != null) Call(manager, "Shutdown", false);
        yield return null;
        foreach (GameObject root in roots) if (root != null) UnityEngine.Object.Destroy(root); roots.Clear();
        if (networkRoot != null) UnityEngine.Object.Destroy(networkRoot);
        yield return null;
    }
    [UnityTest]
    public IEnumerator BasicVisualFirstFrameStaysAtTheModelEvenAfterLoadingDelay() { return ProjectileFirstFrame(false); }
    [UnityTest]
    public IEnumerator BoostedVisualFirstFrameStaysAtTheModelEvenAfterLoadingDelay() { return ProjectileFirstFrame(true); }
    private IEnumerator ProjectileFirstFrame(bool boosted)
    {
#if UNITY_EDITOR
        var context = new GameObject("Isolated projectile presentation context");
        GameObject model = null, projectile = null;
        context.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        var controller = (Behaviour)context.AddComponent(GameType("MovesController")); controller.enabled = false;
        GameType("MovesController").GetField("pokemon", Private).SetValue(controller, attacker);
        // No move RPCs run through this disabled context; the Pokemon/target are
        // real spawned objects, and this flag enables only the visual's lifetime.
        PropertyInfo spawned = GameType("Unity.Netcode.NetworkBehaviour").GetProperty("IsSpawned");
        spawned.SetValue(controller, true);
        try
        {
            attacker.transform.position = new Vector3(10, 0, 10);
            recipient.transform.position = new Vector3(14, 0, 10);
            model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/PokemoModels/Meowstic/Meowstic(male).prefab"), attacker.transform);
            model.transform.localPosition = Vector3.up * 0.3f;
            GameType("Pokemon").GetField("activeModel", Private).SetValue(attacker, model);
            string name = boosted ? "Boosted" : "Basic";
            projectile = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Meowstic/Male/Rework/" + name + ".prefab"));
            projectile.transform.position = Vector3.zero; // Deliberately stale root/origin.
            Component visual = projectile.GetComponent(GameType("MeowsticVisual"));
            MethodInfo initialize = visual.GetType().GetMethod("Initialize");
            object handle = Activator.CreateInstance(initialize.GetParameters()[7].ParameterType);
            Vector3 expected = model.GetComponentsInChildren<Transform>(true).First(bone => bone.name == (boosted ? "EffHeadCenter01" : "EffHand01")).position;
            float startedAt = Time.time;
            initialize.Invoke(visual, new object[] { controller, Value("MeowsticEffect", name), 2f, 0.1f, (ulong)Get(recipient, "NetworkObjectId"), 1f, 0, handle, null });
            Assert.That(projectile.transform.position, Is.EqualTo(expected), "Loading age must not skip the launch point.");
            visual.GetType().GetMethod("Update", Private).Invoke(visual, null);
            Assert.That(projectile.transform.position, Is.EqualTo(expected), "Initialization plus Update must not consume two movement steps.");
            yield return null;
            Assert.That(projectile, Is.Not.Null);
            float distance = Vector3.Distance(projectile.transform.position, expected);
            Assert.That(distance, Is.GreaterThan(0));
            Assert.That(distance, Is.LessThanOrEqualTo((Time.time - startedAt) * 20 + 0.001f));
        }
        finally
        {
            spawned.SetValue(controller, false);
            if (projectile != null) UnityEngine.Object.Destroy(projectile);
            if (model != null) UnityEngine.Object.Destroy(model);
            UnityEngine.Object.Destroy(context);
        }
#else
        Assert.Ignore("Presentation fixtures inspect authored Editor assets.");
        yield break;
#endif
    }
    [UnityTest]
    public IEnumerator OverlappingRoomsDoNotUndoEachOthersInversion()
    {
        var first = new object(); var second = new object();
        Call(recipient, "SetWonderRoomSource", first, true); Call(recipient, "SetWonderRoomSource", second, true);
        Assert.That(Get(recipient, "SwitchAtkStat"), Is.True);
        Call(recipient, "SetWonderRoomSource", first, false); Call(recipient, "SetWonderRoomSource", first, false);
        Assert.That(Get(recipient, "SwitchAtkStat"), Is.True);
        Call(recipient, "SetWonderRoomSource", second, false);
        Assert.That(Get(recipient, "SwitchAtkStat"), Is.False);
        yield return null;
    }
    [UnityTest]
    public IEnumerator OverlappingProtectionUsesTheStrongestRemainingSource()
    {
        int initial = (int)Call(recipient, "GetDefense");
        IDisposable first = OwnModifier(recipient, "Defense", 15), second = OwnModifier(recipient, "Defense", 20);
        Assert.That(Call(recipient, "GetDefense"), Is.EqualTo(Mathf.RoundToInt(initial * 1.2f)));
        second.Dispose();
        Assert.That(Call(recipient, "GetDefense"), Is.EqualTo(Mathf.RoundToInt(initial * 1.15f)));
        Call(recipient, "RemoveAllStatChangesRPC");
        Call(first, "SetAmount", (short)15);
        Assert.That(Call(recipient, "GetDefense"), Is.EqualTo(Mathf.RoundToInt(initial * 1.15f)), "A same-count passive refresh can restore a cleared contribution.");
        first.Dispose(); first.Dispose();
        Assert.That(Call(recipient, "GetDefense"), Is.EqualTo(initial));
        yield return null;
    }
    [UnityTest]
    public IEnumerator HarmonyShieldsAreTimedAndIndependentlyOwned()
    {
        var type = GameType("MeowsticShield");
        var first = (IDisposable)Activator.CreateInstance(type, recipient, 0.15f);
        var second = (IDisposable)Activator.CreateInstance(type, recipient, 0.6f);
        leases.Add(first); leases.Add(second);
        ushort firstId = (ushort)Get(first, "Id"), secondId = (ushort)Get(second, "Id");
        Assert.That(firstId, Is.Not.EqualTo(secondId));
        Assert.That(Call(recipient, "HasShieldWithID", firstId), Is.True);
        yield return new WaitForSeconds(0.3f);
        Assert.That(Call(recipient, "HasShieldWithID", firstId), Is.False);
        Assert.That(Call(recipient, "HasShieldWithID", secondId), Is.True);
        first.Dispose();
        Assert.That(Call(recipient, "HasShieldWithID", secondId), Is.True);
        yield return new WaitForSeconds(0.45f);
        Assert.That(Call(recipient, "HasShieldWithID", secondId), Is.False);
    }
    [UnityTest]
    public IEnumerator ResolvedPhysicalDamageIncludesShieldAbsorptionWithoutRecalculation()
    {
        OwnModifier(recipient, "PhysicalDamageReduction", 35);
        var shield = (IDisposable)Activator.CreateInstance(GameType("MeowsticShield"), recipient, 2f); leases.Add(shield);
        int actual = 0;
        var damageEvent = GameType("Pokemon").GetEvent("OnResolvedDamage");
        ParameterExpression info = Expression.Parameter(GameType("DamageInfo")), amount = Expression.Parameter(typeof(int));
        Action<int> capture = value => actual = value;
        Delegate listener = Expression.Lambda(damageEvent.EventHandlerType, Expression.Invoke(Expression.Constant(capture), amount), info, amount).Compile();
        damageEvent.AddEventHandler(recipient, listener);
        try
        {
            ulong id = (ulong)Get(attacker, "NetworkObjectId");
            object damage = Activator.CreateInstance(GameType("DamageInfo"), id, 0f, (short)0, (short)1000, Value("DamageType", "Physical"), Value("DamageProprieties", "None"));
            int beforeHp = (int)Get(recipient, "CurrentHp"), beforeShield = (int)Get(recipient, "ShieldHp");
            int expected = (int)GameType("Pokemon").GetMethod("CalculateDamage", new[] { GameType("DamageInfo"), GameType("Pokemon") }).Invoke(recipient, new object[] { damage, attacker });
            Call(recipient, "TakeDamageRPC", damage);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(Get(recipient, "CurrentHp"), Is.EqualTo(beforeHp));
            Assert.That(Get(recipient, "ShieldHp"), Is.EqualTo(beforeShield - expected));
        }
        finally { damageEvent.RemoveEventHandler(recipient, listener); }
        yield return null;
    }
    [UnityTest]
    public IEnumerator LethalDamageReportsTheCombatPositionAndExcludesOverkillBeforeDeathTeleport()
    {
        Vector3 combatPosition = new Vector3(3, 0, 4), recordedPosition = Vector3.zero;
        recipient.transform.position = combatPosition;
        int reported = 0, hp = (int)Get(recipient, "CurrentHp");
        object variable = GameType("Pokemon").GetField("currentHp", Private).GetValue(recipient);
        FieldInfo changed = variable.GetType().GetField("OnValueChanged");
        Delegate previous = changed.GetValue(variable) as Delegate;
        ParameterExpression oldHp = Expression.Parameter(typeof(int)), newHp = Expression.Parameter(typeof(int));
        Action<int, int> teleport = (before, after) => { if (after == 0) recipient.transform.position = new Vector3(0, -50, -50); };
        Delegate onDeath = Expression.Lambda(changed.FieldType, Expression.Invoke(Expression.Constant(teleport), oldHp, newHp), oldHp, newHp).Compile();
        changed.SetValue(variable, Delegate.Combine(previous, onDeath));
        EventInfo resolved = GameType("Pokemon").GetEvent("OnResolvedDamage");
        ParameterExpression info = Expression.Parameter(GameType("DamageInfo")), amount = Expression.Parameter(typeof(int));
        Action<int> capture = value => { reported = value; recordedPosition = recipient.transform.position; };
        Delegate listener = Expression.Lambda(resolved.EventHandlerType, Expression.Invoke(Expression.Constant(capture), amount), info, amount).Compile();
        resolved.AddEventHandler(recipient, listener);
        try
        {
            object damage = Activator.CreateInstance(GameType("DamageInfo"), (ulong)Get(attacker, "NetworkObjectId"), 0f, (short)0, (short)9000, Value("DamageType", "Physical"), Value("DamageProprieties", "None"));
            Call(recipient, "TakeDamageRPC", damage);
            Assert.That(reported, Is.EqualTo(hp));
            Assert.That(recordedPosition, Is.EqualTo(combatPosition));
            Assert.That(recipient.transform.position.y, Is.EqualTo(-50));
        }
        finally { resolved.RemoveEventHandler(recipient, listener); changed.SetValue(variable, previous); }
        yield return null;
    }
    [UnityTest]
    public IEnumerator EligibleHindrancesCanBeRejectedBeforeTheStatusListChanges()
    {
        var statusEvent = GameType("Pokemon").GetEvent("BeforeStatusEffect");
        ParameterExpression parameter = Expression.Parameter(GameType("StatusEffect"));
        var reflected = Expression.Call(GameType("MeowsticMoveRules").GetMethod("Reflectable"), parameter);
        Delegate filter = Expression.Lambda(statusEvent.EventHandlerType, Expression.Not(reflected), parameter).Compile();
        statusEvent.AddEventHandler(recipient, filter);
        try
        {
            object stun = Activator.CreateInstance(GameType("StatusEffect"), Value("StatusType", "Incapacitated"), 1f, true, (ushort)0);
            Call(recipient, "AddStatusEffect", stun);
            var has = GameType("Pokemon").GetMethod("HasStatusEffect", new[] { GameType("StatusType") });
            Assert.That(has.Invoke(recipient, new[] { Value("StatusType", "Incapacitated") }), Is.False);
            statusEvent.RemoveEventHandler(recipient, filter);
            Call(recipient, "AddStatusEffect", stun);
            Assert.That(has.Invoke(recipient, new[] { Value("StatusType", "Incapacitated") }), Is.True);
        }
        finally { statusEvent.RemoveEventHandler(recipient, filter); }
        yield return null;
    }
}
