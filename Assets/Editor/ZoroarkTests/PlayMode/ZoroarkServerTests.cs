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
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        yield return null;
        actor = Pokemon(Vector3.zero, "Blue"); player = actor.GetComponent(T("PlayerManager")); moves = actor.GetComponent(T("MovesController"));
        Invoke(moves, "UpdateZoroarkRuntime");
#else
        Assert.Ignore("Editor fixture required."); yield break;
#endif
    }
    private static readonly string[] Names = { "ZoroarkSlash", "ZoroarkFurySwipes", "ZoroarkShadowClaw", "ZoroarkCut", "ZoroarkNightSlash", "ZoroarkFeintAttack", "ZoroarkNightfallDaze" };

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
        Assert.That(effect.GetType().GetField("steps", Private).GetValue(effect), Is.EqualTo(1));
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
        Set(moves, "zoroarkMovement", Vector2.right);
        yield return new WaitForSeconds(1.65f);
        Assert.That(actor.transform.position.x, Is.InRange(1.35f, 1.55f));
        Assert.That(Hp(target), Is.EqualTo(4214), "The cross must sample the steered landing, not the jump's original position.");
        var landed = actor.transform.position;
        Set(moves, "zoroarkMovement", Vector2.left);
        yield return new WaitForSeconds(0.2f);
        Assert.That(actor.transform.position, Is.EqualTo(landed));
    }

    [UnityTest]
    public IEnumerator FeintCountsOnePerCastAndMarkMovesToOtherMoveOrigin()
    {
        Pokemon(Vector3.forward * 2); Pokemon(new Vector3(0.4f, 0, 2)); Physics.SyncTransforms();
        var effect = Effect("ZoroarkFeintAttack"); yield return new WaitForSeconds(0.5f);
        Assert.That(effect.GetType().GetField("steps", Private).GetValue(effect), Is.EqualTo(1));
        Vector3 mark = new Vector3(2, 0, 6); Call(effect, "MoveMark", mark);
        Assert.That(effect.GetType().GetField("mark", Private).GetValue(effect), Is.EqualTo(mark));
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
        Set(moves, "zoroarkMovement", Vector2.down);
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
        Set(effect, "uniqueReady", true);
        Call(attack, "PerformOnServer", target); yield return new WaitForSeconds(0.4f);
        Assert.That(effect.GetType().GetField("steps", Private).GetValue(effect), Is.EqualTo(1));
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
    public IEnumerator IllusionPrefersPlayersSnapshotsHpAndBreaksOnDamage()
    {
        var enemy = Pokemon(Vector3.forward * 20); var wild = Pokemon(Vector3.forward);
        wild.GetType().GetProperty("Type").SetValue(wild, E("PokemonType", "Wild"));
        Variable(enemy, "currentHp", 3200);
        Invoke(moves, "ActivateZoroarkIllusion");
        Assert.That(Get(moves, "HasZoroarkDisguise"), Is.True);
        Assert.That(Get(moves, "ZoroarkDisguiseSource"), Is.SameAs(enemy));
        Assert.That(moves.GetType().GetField("illusionHp", Private).GetValue(moves), Is.EqualTo(3200));
        Variable(enemy, "currentHp", 1000);
        Assert.That(moves.GetType().GetField("illusionHp", Private).GetValue(moves), Is.EqualTo(3200));
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
        while ((int)effect.GetType().GetField("steps", Private).GetValue(effect) < 6 && Time.time < deadline)
        {
            Call(effect, "Movement", Vector2.up);
            yield return null;
        }
        Assert.That(effect.GetType().GetField("steps", Private).GetValue(effect), Is.EqualTo(6));
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
                Assert.That(effect.GetType().GetField("steps", Private).GetValue(effect), Is.EqualTo(step * 2 + 1));
                Call(effect, "Movement", directions[step]);
                Call(attack, "PerformOnServer", targets[step]);
                deadline = Time.time + 1;
                while ((bool)Get(effect, "UniqueReady") && Time.time < deadline)
                {
                    Call(effect, "Movement", directions[step]);
                    yield return null;
                }
                Assert.That(effect.GetType().GetField("steps", Private).GetValue(effect), Is.EqualTo(step * 2 + 2));
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
}

public class ZoroarkHostTests : ZoroarkServerTests { protected override bool Host => true; }
