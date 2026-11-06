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

public class EspeonServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
    private Component server, actor, player, moves;
    private GameObject fixture;
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static object Get(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Private).SetValue(instance, value);
    private static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, args.Select(a => a.GetType()).ToArray()).Invoke(instance, args);
    private static void Variable(object instance, string name, object value) { var field = instance.GetType().GetField(name, Private).GetValue(instance); field.GetType().GetProperty("Value").SetValue(field, value); }
    private static int Hp(Component target) => (int)Get(target, "CurrentHp");
    private object Combat => Get(moves, "EspeonCombat");
    private static bool Has(Component target, string status) => (bool)Call(target, "HasStatusEffect", E("StatusType", status));
    private object Status(string type, float duration = 1, ulong? source = null)
    {
        object value = Activator.CreateInstance(T("StatusEffect"), E("StatusType", type), duration, true, (ushort)123);
        return source.HasValue ? Call(value, "WithSource", source.Value) : value;
    }
    private GameObject Node(string name) { var node = new GameObject(name); objects.Add(node); return node; }
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("External network session active.");
        foreach (string name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        { var map = Node(name); map.tag = name; map.transform.position = Vector3.down * 30; map.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100); }
        var root = Node("Espeon regression server"); server = root.AddComponent(T("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig")); server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport); config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab"); Call(server, "AddNetworkPrefab", fixture);
        Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab"));
        foreach (string name in new[] { "EspeonSwift", "EspeonGrowl", "EspeonPsyshock", "EspeonStoredPower", "EspeonPsybeam", "EspeonFutureSight", "EspeonPsychicSolare", "EspeonBasicProjectile" })
            Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Espeon/" + name + ".prefab"));
#else
        Assert.Ignore("Editor fixtures required.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True); yield return null;
        actor = Pokemon(Vector3.zero, true, "Blue"); player = actor.GetComponent(T("PlayerManager")); moves = actor.GetComponent(T("MovesController"));
        Call(Combat, "Tick");
    }
    private Component Pokemon(Vector3 position, bool espeon = false, string team = "Orange")
    {
        var root = UnityEngine.Object.Instantiate(fixture); root.transform.position = position; objects.Add(root);
        var pokemon = root.GetComponent(T("Pokemon")); var manager = root.GetComponent(T("PlayerManager")); var controller = root.GetComponent(T("MovesController"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase")); definitions.Add(definition);
        Set(definition, "pokemonName", espeon ? "Espeon" : "Test opponent");
        Set(definition, "maxHp", Enumerable.Repeat(20000, 15).ToArray()); Set(definition, "attack", Enumerable.Repeat(100, 15).ToArray());
        Set(definition, "spAttack", Enumerable.Repeat(100, 15).ToArray()); Set(definition, "defense", new int[15]); Set(definition, "spDefense", new int[15]);
        Set(definition, "critRate", new int[15]); Set(definition, "speed", Enumerable.Repeat(3650, 15).ToArray()); Set(definition, "atkSpeed", Enumerable.Repeat(10f, 15).ToArray());
        Set(definition, "passive", E("AvailablePassives", espeon ? "EspeonPassive" : "EmptyPassive")); Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0));
        Set(pokemon, "baseStats", definition); Set(controller, "pokemon", pokemon); Set(controller, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1ul, true);
        Variable(pokemon, "currentHp", 20000); Variable(pokemon, "currentLevel", 3); Call(pokemon, "UpdateTeamRPC", E("Team", team));
        var hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, pokemon, pokemon.GetType().GetMethod("CurrentHpChanged", Private)));
        return pokemon;
    }
    private Component Effect(string name, Vector3 direction, Component target = null, bool plus = false, bool empowered = false, int blasts = 0)
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Espeon/" + name + ".prefab")); objects.Add(root);
        root.transform.position = actor.transform.position + Vector3.up * 0.8f;
        var effect = root.GetComponent(T("PrefabMoveEffect"));
        effect.GetType().GetProperty("MoveId").SetValue(effect, E("AvailableMoves", name == "EspeonBasicProjectile" ? "EspeonSwift" : name));
        if (effect.GetType().GetProperty("Empowered") != null)
        { effect.GetType().GetProperty("Empowered").SetValue(effect, empowered); effect.GetType().GetProperty("BlastCount").SetValue(effect, blasts); }
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true);
        effect.GetType().GetMethod("Initialize").Invoke(effect, new object[] { player, direction, target, plus }); return effect;
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
    public IEnumerator MagicBounceBlocksBurnAndRetaliatesAgainstItsSourceWithoutRangeLimit()
    {
        var source = Pokemon(Vector3.forward * 30);
        Call(actor, "AddStatusEffect", Status("Burned", 2, (ulong)Get(source, "NetworkObjectId")));
        Assert.That(Has(actor, "Burned"), Is.False); Assert.That(Has(actor, "Unstoppable"), Is.True);
        Assert.That(Hp(source), Is.EqualTo(19802));
        Call(actor, "AddStatusEffect", Status("Frozen", 2, (ulong)Get(source, "NetworkObjectId")));
        Assert.That(Has(actor, "Frozen"), Is.False); Assert.That(Hp(source), Is.EqualTo(19802));
        yield return new WaitForSeconds(0.5f);
        // The fixture's Pokemon.Update is disabled; remove only the expired immunity.
        Call(actor, "ClearStatusEffectsRPC");
        Call(actor, "AddStatusEffect", Status("Poisoned", 2, (ulong)Get(source, "NetworkObjectId")));
        Assert.That(Has(actor, "Poisoned"), Is.True, "The 35s passive cooldown must not be reset by another condition.");
    }
    [UnityTest]
    public IEnumerator FluxZoneSlowReturnsAfterMagicBounceWithoutReentry()
    {
        GameObject zoneRoot;
#if UNITY_EDITOR
        var zonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab");
        zoneRoot = UnityEngine.Object.Instantiate(zonePrefab);
#else
        Assert.Ignore("Editor fixtures required.");
        yield break;
#endif
        objects.Add(zoneRoot);
        zoneRoot.transform.position = Vector3.forward * 30;
        var network = zoneRoot.GetComponent(T("Unity.Netcode.NetworkObject"));
        var zone = zoneRoot.AddComponent(T("FluxZone"));
        Set(zone, "team", E("Team", "Orange"));
        Call(network, "Spawn", true);
        var collider = actor.GetComponent<Collider>();
        var enter = zone.GetType().GetMethod("OnTriggerEnter", Private);
        var exit = zone.GetType().GetMethod("OnTriggerExit", Private);
        var tick = zone.GetType().GetMethod("LateUpdate", Private);
        int speed = (int)Call(actor, "GetSpeed");

        enter.Invoke(zone, new object[] { collider });
        Assert.That(Has(actor, "Unstoppable"), Is.True);
        Assert.That(Call(actor, "GetSpeed"), Is.EqualTo(speed));
        tick.Invoke(zone, null);
        Assert.That(Call(actor, "GetSpeed"), Is.EqualTo(speed));

        yield return new WaitForSeconds(0.5f);
        Call(actor, "ClearStatusEffectsRPC");
        tick.Invoke(zone, null);
        int slowed = (int)Call(actor, "GetSpeed");
        Assert.That(slowed, Is.LessThan(speed));
        tick.Invoke(zone, null);
        Assert.That(Call(actor, "GetSpeed"), Is.EqualTo(slowed));
        Assert.That(Has(actor, "Unstoppable"), Is.False);
        exit.Invoke(zone, new object[] { collider });
        Assert.That(Call(actor, "GetSpeed"), Is.EqualTo(speed));
    }
    [UnityTest]
    public IEnumerator StoredPowerPlayerPoolExcludesWildButFutureSightWildOverridesPlayers()
    {
        var wild = Pokemon(Vector3.forward); wild.GetType().GetProperty("Type").SetValue(wild, E("PokemonType", "Wild"));
        var enemy = Pokemon(Vector3.forward * 4); Physics.SyncTransforms();
        var poolMethod = T("EspeonStoredPowerEffect").GetMethod("TargetPool");
        var pool = (IList)poolMethod.Invoke(null, new object[] { player, null }); Assert.That(pool.Count, Is.EqualTo(1)); Assert.That(pool[0], Is.SameAs(enemy));
        pool = (IList)poolMethod.Invoke(null, new object[] { player, wild }); Assert.That(pool.Count, Is.EqualTo(1)); Assert.That(pool[0], Is.SameAs(wild));
        Effect("EspeonStoredPower", Vector3.forward, blasts: 5); yield return new WaitForSeconds(1);
        Assert.That(Hp(wild), Is.EqualTo(20000)); Assert.That(Hp(enemy), Is.LessThan(20000));
    }
    [UnityTest]
    public IEnumerator EmpoweredStoredPowerHealsOnlySuccessfulImpactsAndUsesEightBlasts()
    {
        var enemy = Pokemon(Vector3.forward * 2); Variable(actor, "currentHp", 10000); Physics.SyncTransforms();
        Effect("EspeonStoredPower", Vector3.forward, empowered: true, blasts: 8); yield return new WaitForSeconds(1.3f);
        Assert.That(Hp(actor), Is.EqualTo(11200)); Assert.That(Hp(enemy), Is.LessThan(19000));
        Variable(actor, "currentHp", 10000); enemy.transform.position = Vector3.forward * 30; Physics.SyncTransforms();
        Effect("EspeonStoredPower", Vector3.forward, empowered: true, blasts: 8); yield return new WaitForSeconds(0.7f);
        Assert.That(Hp(actor), Is.EqualTo(10000));
    }
    [UnityTest]
    public IEnumerator FutureSightCleanseCancelsExplosionAndOwnKoOffersSeparateBonusCast()
    {
        var enemy = Pokemon(Vector3.forward * 3); Physics.SyncTransforms();
        Effect("EspeonFutureSight", Vector3.forward, enemy); yield return new WaitForSeconds(0.3f);
        Assert.That(Has(enemy, "FutureSightMarked"), Is.True);
        Call(enemy, "CleanseConditions"); yield return new WaitForSeconds(2.1f); Assert.That(Hp(enemy), Is.EqualTo(20000));
        Effect("EspeonFutureSight", Vector3.forward, enemy); yield return new WaitForSeconds(0.3f);
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Private).GetValue(moves); clocks[0] = 9999;
        var damage = Activator.CreateInstance(T("DamageInfo"), Get(actor, "NetworkObjectId"), 0f, (short)0, (short)30000, E("DamageType", "Special"), E("DamageProprieties", "None"));
        Call(enemy, "ResolveDamage", damage);
        Assert.That(Get(moves, "EspeonBonusReady"), Is.True); Assert.That(clocks[0], Is.EqualTo(9999));
        object[] spend = { E("AvailableMoves", "EspeonPsyshock"), false, 0 };
        Assert.That(Combat.GetType().GetMethod("Spend").Invoke(Combat, spend), Is.True);
        Assert.That(spend[1], Is.True); Assert.That(clocks[0], Is.EqualTo(9999));
    }
    [UnityTest]
    public IEnumerator PsyshockConvergesAndFiveHitsStunWhileProjectilesContinuePastCloserFocus()
    {
        var enemy = Pokemon(Vector3.forward * 5); Physics.SyncTransforms();
        Effect("EspeonPsyshock", Vector3.forward * 5); yield return new WaitForSeconds(0.75f);
        Assert.That(Hp(enemy), Is.EqualTo(18928)); Assert.That(Has(enemy, "Incapacitated"), Is.True);
        enemy.transform.position = Vector3.right * 30;
        var beyond = Pokemon(Vector3.forward * 6); Physics.SyncTransforms();
        Effect("EspeonSwift", Vector3.forward * 2); yield return new WaitForSeconds(0.9f);
        Assert.That(Hp(beyond), Is.LessThan(20000));
    }
    [UnityTest]
    public IEnumerator SwiftPiercesMultipleEnemiesOncePerStar() => PiercingVolley("EspeonSwift", 781);
    [UnityTest]
    public IEnumerator PsyshockPiercesMultipleEnemiesOncePerProjectile() => PiercingVolley("EspeonPsyshock", 1072);
    private IEnumerator PiercingVolley(string move, int damage)
    {
        var first = Pokemon(Vector3.forward * 4); var second = Pokemon(Vector3.forward * 6);
        // Multiple colliders and several overlapping frames must not multiply a bolt's damage.
        var extra = first.gameObject.AddComponent<SphereCollider>(); extra.center = Vector3.up * 0.8f; extra.radius = 0.4f;
        Physics.SyncTransforms();
        Effect(move, Vector3.forward * 5); yield return new WaitForSeconds(1.1f);
        Assert.That(Hp(first), Is.EqualTo(20000 - damage)); Assert.That(Hp(second), Is.EqualTo(20000 - damage));
        if (move == "EspeonPsyshock")
        { Assert.That(Has(first, "Incapacitated"), Is.True); Assert.That(Has(second, "Incapacitated"), Is.True); }
    }
    [UnityTest]
    public IEnumerator GuidedPsyshockContinuesBeyondItsMarkedTargetWithoutCirclingBack()
    {
        var marked = Pokemon(Vector3.forward * 4); var behind = Pokemon(Vector3.forward * 6); Physics.SyncTransforms();
        Effect("EspeonFutureSight", Vector3.forward, marked); yield return new WaitForSeconds(0.3f);
        var volley = Effect("EspeonPsyshock", Vector3.forward * 4); yield return new WaitForSeconds(1.1f);
        Assert.That(Hp(marked), Is.EqualTo(18928)); Assert.That(Hp(behind), Is.LessThan(20000));
        Assert.That(volley == null, Is.True, "Piercing bolts must finish their forward travel rather than orbit the mark.");
    }
    [UnityTest]
    public IEnumerator EmpoweredPsyshockStillDamagesOnlyAtConvergence()
    {
        var before = Pokemon(Vector3.forward * 2); var focal = Pokemon(Vector3.forward * 5); var beyond = Pokemon(Vector3.forward * 8);
        Physics.SyncTransforms();
        Effect("EspeonPsyshock", Vector3.forward * 5, empowered: true); yield return new WaitForSeconds(1.1f);
        Assert.That(Hp(before), Is.EqualTo(20000)); Assert.That(Hp(focal), Is.EqualTo(18928)); Assert.That(Hp(beyond), Is.EqualTo(20000));
    }
    [UnityTest]
    public IEnumerator BoostGaugeUsesAuthoritativeRechargeAndCleansUpAcrossLifecycle()
    {
        var bar = Node("Boost gauge bar").AddComponent(T("HPBar"));
        var gauge = Node("Generic gauge"); var image = gauge.AddComponent<Image>();
        Set(bar, "generigGuage", gauge); Set(bar, "genericGuageBar", image); Set(player, "hpBar", bar);
        gauge.SetActive(false);
        var attack = Activator.CreateInstance(T("EspeonBasicAtk"));
        Call(attack, "Initialize", player);
        Assert.That(gauge.activeSelf, Is.False, "The remote attack must not claim the gauge.");
        Call(attack, "Update"); Assert.That(gauge.activeSelf, Is.False);
        Call(actor.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", 0ul);
        Call(attack, "Update"); Assert.That(gauge.activeSelf, Is.True); Assert.That(image.fillAmount, Is.EqualTo(1));
        double now = (double)Get(Get(server, "ServerTime"), "Time");
        Call(moves, "SetEspeonBoostReady", now + 5); Call(attack, "Update"); Assert.That(image.fillAmount, Is.EqualTo(0).Within(0.01f));
        Call(moves, "SetEspeonBoostReady", now + 2.5); Call(attack, "Update"); Assert.That(image.fillAmount, Is.EqualTo(0.5f).Within(0.01f));
        Call(moves, "SetEspeonBoostReady", now - 1); Call(attack, "Update"); Assert.That(image.fillAmount, Is.EqualTo(1));
        Variable(actor, "currentLevel", 2); Call(attack, "Update"); Assert.That(gauge.activeSelf, Is.False);
        Assert.That(attack.GetType().GetProperty("ShowAttackCounter", Private).GetValue(attack), Is.True);
        Variable(actor, "currentLevel", 3); Call(attack, "Update"); Assert.That(gauge.activeSelf, Is.True);
        Assert.That(attack.GetType().GetProperty("ShowAttackCounter", Private).GetValue(attack), Is.False);
        Variable(actor, "currentHp", 0); Call(attack, "Update"); Assert.That(gauge.activeSelf, Is.False);
        Variable(actor, "currentHp", 20000); Call(Combat, "Tick"); Call(attack, "Update"); Assert.That(image.fillAmount, Is.EqualTo(1));
        Set(player, "hpBar", null); Call(attack, "Update"); Assert.That(gauge.activeSelf, Is.False);
        Set(player, "hpBar", bar); Call(attack, "Update"); Assert.That(gauge.activeSelf, Is.True);
        Call(attack, "Dispose"); Assert.That(gauge.activeSelf, Is.False); Assert.That(image.fillAmount, Is.Zero);
        yield return null;
    }
    [UnityTest]
    public IEnumerator BasicAttackCanReachGrowlAimDistance()
    {
        float range = (float)Get(Activator.CreateInstance(T("EspeonGrowl")), "Range");
        var target = Pokemon(Vector3.forward * range); Physics.SyncTransforms();
        Call(Get(moves, "ServerBasicAttack"), "PerformOnServer", target); yield return new WaitForSeconds(0.5f);
        Assert.That(Hp(target), Is.EqualTo(19680));
    }
    [UnityTest]
    public IEnumerator PsybeamCopiesFullStunDurationAndExcludesEnemiesBehindItsFirstTarget()
    {
        var primary = Pokemon(Vector3.forward * 3); var forward = Pokemon(new Vector3(2, 0, 4)); var behind = Pokemon(new Vector3(2, 0, 1));
        Call(primary, "AddStatusEffect", Status("Incapacitated", 2)); Physics.SyncTransforms();
        Effect("EspeonPsybeam", Vector3.forward); yield return new WaitForSeconds(0.35f);
        Assert.That(Has(forward, "Incapacitated"), Is.True); Assert.That(Hp(forward), Is.LessThan(20000)); Assert.That(Hp(behind), Is.EqualTo(20000));
        var statuses = Get(forward, "StatusEffects");
        int count = (int)Get(statuses, "Count");
        Assert.That(Enumerable.Range(0, count).Select(i => statuses.GetType().GetProperty("Item").GetValue(statuses, new object[] { i }))
            .Any(s => (float)s.GetType().GetField("Duration").GetValue(s) == 2), Is.True);
    }
    [UnityTest]
    public IEnumerator BoostedBeamPiercesGeneratesBlastsAndIsReadyAgainAfterRespawn()
    {
        var learned = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves);
        learned.SetValue(E("AvailableMoves", "EspeonStoredPower"), 0); Call(Combat, "Tick");
        var first = Pokemon(Vector3.forward * 2); var second = Pokemon(Vector3.forward * 4); Physics.SyncTransforms();
        var attack = Get(moves, "ServerBasicAttack");
        Assert.That(Get(moves, "EspeonBoostReady"), Is.True);
        Call(attack, "PerformOnServer", first); yield return new WaitForSeconds(0.5f);
        Assert.That(Hp(first), Is.EqualTo(19680)); Assert.That(Hp(second), Is.EqualTo(19680));
        Assert.That(Get(moves, "EspeonBlastCount"), Is.EqualTo(2)); Assert.That(Get(moves, "EspeonBoostReady"), Is.False);
        Variable(actor, "currentHp", 0);
        var ammo = Combat.GetType().GetField("blasts", Private).GetValue(Combat);
        double now = (double)Get(Get(server, "ServerTime"), "Time");
        Call(ammo, "Reset", now - 100); // Simulate a long death without waiting in the test.
        Variable(actor, "currentHp", 20000); Call(Combat, "Tick");
        Assert.That(Get(moves, "EspeonBoostReady"), Is.True); Assert.That(Get(moves, "EspeonBlastCount"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator FutureSightPlusAmplifiesDamageAndQualifyingHitsReduceOnlyItsMoveCooldown()
    {
        var enemy = Pokemon(Vector3.forward * 3); Physics.SyncTransforms();
        var learned = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves);
        learned.SetValue(E("AvailableMoves", "EspeonFutureSight"), 1);
        var raw = Activator.CreateInstance(T("DamageInfo"), Get(actor, "NetworkObjectId"), 0f, (short)0, (short)1000, E("DamageType", "Special"), E("DamageProprieties", "None"));
        Assert.That(Call(enemy, "CalculateDamage", raw, actor), Is.EqualTo(1000));
        Effect("EspeonFutureSight", Vector3.forward, enemy, plus: true); yield return new WaitForSeconds(0.3f);
        Assert.That(Call(enemy, "CalculateDamage", raw, actor), Is.EqualTo(1150).Within(1));
        double now = (double)Get(Get(server, "ServerTime"), "Time");
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Private).GetValue(moves); clocks[1] = now + 7;
        Call(Combat, "Hit", enemy, raw, true);
        Assert.That(clocks[1], Is.EqualTo(now + 6.2).Within(0.01));
        yield return new WaitForSeconds(0.4f); Assert.That(Has(enemy, "FutureSightMarked"), Is.True);
        Call(enemy, "CleanseConditions"); Assert.That(Call(enemy, "CalculateDamage", raw, actor), Is.EqualTo(1000));
    }
    [UnityTest]
    public IEnumerator BonusAndNormalStoredPowerRefreshHasteInsteadOfStackingIt()
    {
        int before = (int)Call(actor, "GetSpeed");
        Call(Combat, "GrantStoredHaste"); int once = (int)Call(actor, "GetSpeed");
        Call(Combat, "GrantStoredHaste");
        Assert.That(once, Is.GreaterThan(before)); Assert.That(Call(actor, "GetSpeed"), Is.EqualTo(once));
        yield return null;
    }
    [UnityTest]
    public IEnumerator SwiftSetsTransitionOnlyWhileEevee()
    {
        var animation = player.GetComponent(T("AnimationManager"));
        Variable(actor, "currentLevel", 2);
        Effect("EspeonSwift", Vector3.forward * 5); yield return new WaitForSeconds(0.4f);
        if (Host)
        {
            Assert.That(animation.GetType().GetField("hasPendingTrigger", Private).GetValue(animation), Is.True);
            Assert.That(animation.GetType().GetField("pendingTrigger", Private).GetValue(animation), Is.EqualTo(Animator.StringToHash("Transition")));
        }
        Set(animation, "hasPendingTrigger", false);
        Variable(actor, "currentLevel", 3);
        Effect("EspeonSwift", Vector3.forward * 5); yield return new WaitForSeconds(0.4f);
        Assert.That(animation.GetType().GetField("hasPendingTrigger", Private).GetValue(animation), Is.False);
        Variable(actor, "currentLevel", 2);
        Effect("EspeonPsyshock", Vector3.forward * 5); yield return new WaitForSeconds(0.4f);
        Assert.That(animation.GetType().GetField("hasPendingTrigger", Private).GetValue(animation), Is.False);
    }
    [UnityTest]
    public IEnumerator TargetlessStoredPowerDoesNotSpendAmmoBonusOrCooldown()
    {
        Call(actor.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", 0ul);
        var learned = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves);
        learned.SetValue(E("AvailableMoves", "EspeonStoredPower"), 0); Call(Combat, "Tick"); Call(Combat, "AddBlasts", 3);
#if UNITY_EDITOR
        var row = Activator.CreateInstance(T("LearnableMove"));
        var assets = Array.CreateInstance(T("MoveAsset"), 1);
        assets.SetValue(AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Espeon/EspeonStoredPower.asset"), 0);
        row.GetType().GetField("moves").SetValue(row, assets);
        var rows = Array.CreateInstance(T("LearnableMove"), 1); rows.SetValue(row, 0);
        Set(Get(actor, "BaseStats"), "learnableMoves", rows);
#endif
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Private).GetValue(moves);
        Physics.SyncTransforms();
        Assert.That(Call(Combat, "CanCast", E("AvailableMoves", "EspeonStoredPower")), Is.False);
        Call(moves, "SpawnMoveEffectRPC", E("AvailableMoves", "EspeonStoredPower"), Vector3.forward, 0ul, 0);
        Assert.That(Get(Combat, "BlastCount"), Is.EqualTo(3)); Assert.That(clocks[0], Is.Zero);
        Set(Combat, "bonusUntil", 9999d); Call(Combat, "Tick");
        object[] spend = { E("AvailableMoves", "EspeonStoredPower"), false, 0 };
        Assert.That(Combat.GetType().GetMethod("Spend").Invoke(Combat, spend), Is.False);
        Call(moves, "SpawnMoveEffectRPC", E("AvailableMoves", "EspeonStoredPower"), Vector3.forward, 0ul, 0);
        Assert.That(Get(Combat, "BlastCount"), Is.EqualTo(3)); Assert.That(Get(Combat, "BonusReady"), Is.True); Assert.That(clocks[0], Is.Zero);
        var move = Activator.CreateInstance(T("EspeonStoredPower"));
        move.GetType().GetField("playerManager").SetValue(move, player); move.GetType().GetField("IsActive").SetValue(move, true);
        Call(move, "Finish"); Assert.That(move.GetType().GetField("IsActive").GetValue(move), Is.False);
        int speed = (int)Call(actor, "GetSpeed");
        var effect = Effect("EspeonStoredPower", Vector3.forward, empowered: true, blasts: 8); yield return new WaitForSeconds(0.1f);
        Assert.That(Call(actor, "GetSpeed"), Is.EqualTo(speed)); Assert.That(effect == null, Is.True);
        Assert.That(Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator StoredPowerRequiresAnAcquirableEnemyInsideItsRange()
    {
        var learned = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves);
        learned.SetValue(E("AvailableMoves", "EspeonStoredPower"), 0); Call(Combat, "Tick"); Call(Combat, "AddBlasts", 3);
        Pokemon(Vector3.forward * 2, team: "Blue");
        var enemy = Pokemon(Vector3.forward * 9); Physics.SyncTransforms();
        Assert.That(Call(Combat, "CanCast", E("AvailableMoves", "EspeonStoredPower")), Is.False);
        enemy.transform.position = Vector3.forward * 3; Physics.SyncTransforms();
        foreach (string status in new[] { "Invincible", "Untargetable", "Invisible" })
        {
            Call(enemy, "AddStatusEffect", Status(status));
            Assert.That(Call(Combat, "CanCast", E("AvailableMoves", "EspeonStoredPower")), Is.False, status);
            Call(enemy, "ClearStatusEffectsRPC");
        }
        Assert.That(Call(Combat, "CanCast", E("AvailableMoves", "EspeonStoredPower")), Is.True);
        enemy.GetType().GetProperty("Type").SetValue(enemy, E("PokemonType", "Wild"));
        Assert.That(Call(Combat, "CanCast", E("AvailableMoves", "EspeonStoredPower")), Is.True);
        yield return null;
    }
    [UnityTest]
    public IEnumerator StoredAmmoSpendingAndEmpoweredOpportunityDoNotOverwriteNormalResources()
    {
        var learned = (Array)moves.GetType().GetField("prefabLearnedMoves", Private).GetValue(moves);
        learned.SetValue(E("AvailableMoves", "EspeonStoredPower"), 0); Call(Combat, "Tick");
        Assert.That(Call(Combat, "CanCast", E("AvailableMoves", "EspeonStoredPower")), Is.False);
        Pokemon(Vector3.forward * 3); Physics.SyncTransforms();
        Call(Combat, "AddBlasts", 3);
        var runtime = Combat; Set(runtime, "bonusUntil", 9999d);
        object[] spend = { E("AvailableMoves", "EspeonStoredPower"), false, 0 };
        Assert.That(runtime.GetType().GetMethod("Spend").Invoke(runtime, spend), Is.True);
        Assert.That(spend[1], Is.True); Assert.That(spend[2], Is.EqualTo(8)); Assert.That(Get(runtime, "BlastCount"), Is.EqualTo(3));
        Assert.That(runtime.GetType().GetMethod("Spend").Invoke(runtime, spend), Is.True);
        Assert.That(spend[1], Is.False); Assert.That(spend[2], Is.EqualTo(3)); Assert.That(Get(runtime, "BlastCount"), Is.Zero);
        yield return null;
    }
    [UnityTest]
    public IEnumerator SolareFollowsDisplacementCapturesLateEntrantsAndDeathReleasesActionLocks()
    {
        var early = Pokemon(Vector3.forward * 2); var entrant = Pokemon(Vector3.right * 20); Physics.SyncTransforms();
        var effect = Effect("EspeonPsychicSolare", Vector3.forward); yield return new WaitForSeconds(0.3f);
        Assert.That(Has(early, "Incapacitated"), Is.True); Assert.That(Has(actor, "Unstoppable"), Is.True);
        actor.transform.position = Vector3.right * 10; entrant.transform.position = actor.transform.position + Vector3.forward; Physics.SyncTransforms();
        yield return new WaitForSeconds(0.2f); Assert.That(Has(entrant, "Incapacitated"), Is.True);
        Assert.That(effect.transform.position, Is.EqualTo(actor.transform.position));
        Variable(actor, "currentHp", 0); yield return null;
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero); Assert.That(Has(entrant, "Incapacitated"), Is.False);
    }
}

public class EspeonHostTests : EspeonServerTests { protected override bool Host => true; }
