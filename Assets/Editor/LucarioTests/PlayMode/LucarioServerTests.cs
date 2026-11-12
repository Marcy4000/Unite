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

public class LucarioServerTests
{
    protected virtual bool Host => false;
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> definitions = new List<ScriptableObject>();
    private Component server, actor, player, moves;
    private GameObject fixture;
    private object Combat => Get(moves, "LucarioCombat");
    private double Now => (double)Get(Get(server, "ServerTime"), "Time");
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object E(string type, string name) => Enum.Parse(T(type), name);
    private static object Get(object instance, string name) => instance.GetType().GetProperty(name).GetValue(instance);
    private static void Set(object instance, string name, object value) => instance.GetType().GetField(name, Fields).SetValue(instance, value);
    private static object Call(object instance, string name, params object[] args) => instance.GetType().GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, args.Select(a => a.GetType()).ToArray(), null).Invoke(instance, args);
    private static void Variable(object instance, string name, object value) => instance.GetType().GetField(name, Fields).GetValue(instance).GetType().GetProperty("Value").SetValue(instance.GetType().GetField(name, Fields).GetValue(instance), value);
    private static int Hp(Component target) => (int)Get(target, "CurrentHp");
    private GameObject Node(string name) { var node = new GameObject(name); objects.Add(node); return node; }
    private object Damage(Component source, short amount = 100, string flags = "None", string kind = "Direct")
    {
        var d = Activator.CreateInstance(T("DamageInfo"), Get(source, "NetworkObjectId"), 0f, (short)0, amount, E("DamageType", "Physical"), E("DamageProprieties", flags));
        d.GetType().GetField("source").SetValue(d, E("DamageSource", kind)); return d;
    }
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (T("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("External network session active.");
        foreach (string name in new[] { "WalkableWithWalls", "WalkableNoWalls" })
        { var map = Node(name); map.tag = name; map.transform.position = Vector3.down * (name == "WalkableWithWalls" ? 30 : 40); map.AddComponent<BoxCollider>().size = new Vector3(100, 0.1f, 100); }
        var root = Node("Lucario test server"); server = root.AddComponent(T("Unity.Netcode.NetworkManager")); var transport = root.AddComponent(T("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        var config = Activator.CreateInstance(T("Unity.Netcode.NetworkConfig")); server.GetType().GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport); config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        fixture = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/AbsolTests/Fixtures/CombatActor.prefab"); Call(server, "AddNetworkPrefab", fixture);
        foreach (string name in Names) Call(server, "AddNetworkPrefab", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Lucario/Lucario" + name + ".prefab"));
#else
        Assert.Ignore("Editor fixtures required.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True); yield return null;
        actor = Pokemon(Vector3.zero, true, "Blue"); player = actor.GetComponent(T("PlayerManager")); moves = actor.GetComponent(T("MovesController")); Call(Combat, "Tick");
    }
    private static readonly string[] Names = { "QuickAttack", "MeteorMash", "ExtremeSpeed", "PowerUpPunch", "BoneRush", "CloseCombat", "AuraCannon" };
    private Component Pokemon(Vector3 position, bool lucario = false, string team = "Orange")
    {
        var root = UnityEngine.Object.Instantiate(fixture); root.transform.position = position; objects.Add(root);
        var p = root.GetComponent(T("Pokemon")); var manager = root.GetComponent(T("PlayerManager")); var controller = root.GetComponent(T("MovesController"));
        var definition = ScriptableObject.CreateInstance(T("PokemonBase")); definitions.Add(definition);
        Set(definition, "pokemonName", lucario ? "Lucario" : "Test opponent"); Set(definition, "maxHp", Enumerable.Repeat(20000, 15).ToArray());
        Set(definition, "attack", Enumerable.Repeat(100, 15).ToArray()); Set(definition, "spAttack", Enumerable.Repeat(100, 15).ToArray());
        foreach (string name in new[] { "defense", "spDefense", "critRate", "cdr", "lifeSteal" }) Set(definition, name, new int[15]);
        Set(definition, "speed", Enumerable.Repeat(3650, 15).ToArray()); Set(definition, "atkSpeed", Enumerable.Repeat(10f, 15).ToArray());
        Set(definition, "passive", E("AvailablePassives", lucario ? "LucarioPassive" : "EmptyPassive")); Set(definition, "learnableMoves", Array.CreateInstance(T("LearnableMove"), 0));
        Set(p, "baseStats", definition); Set(controller, "pokemon", p); Set(controller, "playerManager", manager);
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "SpawnWithOwnership", 1ul, true);
        Variable(p, "currentHp", 20000); Variable(p, "currentLevel", 4); Call(p, "UpdateTeamRPC", E("Team", team));
        var hp = p.GetType().GetField("currentHp", Fields).GetValue(p); var changed = hp.GetType().GetField("OnValueChanged");
        changed.SetValue(hp, Delegate.CreateDelegate(changed.FieldType, p, p.GetType().GetMethod("CurrentHpChanged", Fields)));
        return p;
    }
    private void Learn(string first, string second = "BoneRush")
    {
        var ids = (Array)moves.GetType().GetField("prefabLearnedMoves", Fields).GetValue(moves);
        ids.SetValue(E("AvailableMoves", "Lucario" + first), 0); ids.SetValue(E("AvailableMoves", "Lucario" + second), 1);
    }
    private Component Effect(string name, bool plus = false, Component caster = null)
    {
#if UNITY_EDITOR
        var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/Moves/Lucario/Lucario" + name + ".prefab")); objects.Add(root);
        var effect = root.GetComponent(T("PrefabMoveEffect")); effect.GetType().GetProperty("MoveId").SetValue(effect, E("AvailableMoves", "Lucario" + name));
        Call(root.GetComponent(T("Unity.Netcode.NetworkObject")), "Spawn", true);
        effect.GetType().GetMethod("Initialize").Invoke(effect, new object[] { caster ?? player, Vector3.forward, null, plus }); return effect;
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
        objects.Clear(); definitions.Clear(); yield return null;
    }
    [UnityTest]
    public IEnumerator SteadfastTriggersAtHalfHealthAndCooldownSurvivesDeath()
    {
        var enemy = Pokemon(Vector3.forward * 3); Variable(actor, "currentHp", 10100);
        Call(actor, "ResolveDamage", Damage(enemy));
        Assert.That(Hp(actor), Is.EqualTo(10000));
        double ready = (double)Get(Combat, "PassiveReadyAt"); Assert.That(ready, Is.EqualTo(Now + 45).Within(0.05));
        Call(actor, "ResolveDamage", Damage(enemy)); Assert.That(Hp(actor), Is.EqualTo(10000), "Steadfast shield absorbs subsequent damage.");
        Assert.That(Get(Combat, "PassiveReadyAt"), Is.EqualTo(ready));
        Variable(actor, "currentHp", 0); Variable(actor, "currentHp", 20000); Call(Combat, "Tick"); Assert.That(Get(Combat, "PassiveReadyAt"), Is.EqualTo(ready)); yield return null;
    }
    [UnityTest]
    public IEnumerator MoveTicksReducePunchCooldownButBasicsAndItemsDoNot()
    {
        Learn("PowerUpPunch"); var enemy = Pokemon(Vector3.forward * 2);
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Fields).GetValue(moves); clocks[0] = Now + 20;
        Call(enemy, "ResolveDamage", Damage(actor, 100, "IsBasicAttack")); Call(enemy, "ResolveDamage", Damage(actor, 100, "None", "Item"));
        Assert.That(clocks[0], Is.EqualTo(Now + 20).Within(0.05));
        Call(enemy, "ResolveDamage", Damage(actor)); Call(enemy, "ResolveDamage", Damage(actor)); Assert.That(clocks[0], Is.EqualTo(Now + 18).Within(0.05)); yield return null;
    }
    [UnityTest]
    public IEnumerator MarkersArePerCasterAndOnlyOneIsConsumedPerDash()
    {
        Learn("ExtremeSpeed"); var first = Pokemon(Vector3.forward * 1.5f); var second = Pokemon(Vector3.forward * 4);
        var other = Pokemon(Vector3.right * 2, true, "Blue"); var otherMoves = other.GetComponent(T("MovesController"));
        var ids = (Array)otherMoves.GetType().GetField("prefabLearnedMoves", Fields).GetValue(otherMoves); ids.SetValue(E("AvailableMoves", "LucarioExtremeSpeed"), 0);
        var otherCombat = Get(otherMoves, "LucarioCombat"); Call(Combat, "Tick"); Call(otherCombat, "Tick");
        Effect("ExtremeSpeed"); yield return new WaitForSeconds(0.5f);
        Assert.That(Call(Combat, "MarkReady", first), Is.False); Assert.That(Call(Combat, "MarkReady", second), Is.True);
        Assert.That(Call(otherCombat, "MarkReady", first), Is.True); Assert.That(Hp(first), Is.LessThan(20000)); Assert.That(Hp(second), Is.LessThan(20000));
        Call(Combat, "RestoreMarker", first); Assert.That(Call(Combat, "MarkReady", first), Is.True);
    }
    [UnityTest]
    public IEnumerator CloseCombatHitsElevenTimesAndHealsForEveryEnemyPerPunch()
    {
        Learn("PowerUpPunch", "CloseCombat"); var first = Pokemon(new Vector3(-0.7f, 0, 2)); var second = Pokemon(new Vector3(0.7f, 0, 2));
        Variable(actor, "currentHp", 11000); Physics.SyncTransforms(); Effect("CloseCombat"); yield return new WaitForSeconds(1.6f);
        Assert.That(Hp(first), Is.EqualTo(20000 - 11 * 118)); Assert.That(Hp(second), Is.EqualTo(20000 - 11 * 118));
        Assert.That(Hp(actor), Is.EqualTo(11000 + 11 * 68)); Assert.That(Get(Combat, "KickReady"), Is.True);
    }
    [UnityTest]
    public IEnumerator FullPunchPiercesAndPreservesAuraUntilRelease()
    {
        Learn("PowerUpPunch"); var first = Pokemon(Vector3.forward * 1.5f); var second = Pokemon(Vector3.forward * 4.5f);
        Call(Combat, "EmpowerAura"); Call(moves, "PrepareLucarioInput", Vector3.forward); Physics.SyncTransforms(); Effect("PowerUpPunch", true);
        yield return new WaitForSeconds(1.05f); Assert.That(Get(Combat, "AuraEmpowered"), Is.True);
        Set(moves, "lucarioPunchReleased", true); yield return new WaitForSeconds(0.6f);
        Assert.That(Hp(first), Is.LessThan(20000)); Assert.That(Hp(second), Is.LessThan(20000));
        Assert.That(Get(Combat, "AuraEmpowered"), Is.False); Assert.That(Get(Combat, "Charge"), Is.EqualTo(2));
    }
    [UnityTest]
    public IEnumerator CancelledPunchDoesNotHitAndUsesShortCooldown()
    {
        Learn("PowerUpPunch"); var enemy = Pokemon(Vector3.forward * 2); Call(moves, "PrepareLucarioInput", Vector3.forward); Call(Combat, "EmpowerAura");
        Effect("PowerUpPunch", true); yield return new WaitForSeconds(0.15f);
        Set(moves, "lucarioPunchReleased", true); Set(moves, "lucarioPunchCancelled", true); yield return new WaitForSeconds(0.1f);
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Fields).GetValue(moves);
        Assert.That(clocks[0] - Now, Is.InRange(1.1, 1.4)); Assert.That(Hp(enemy), Is.EqualTo(20000)); Assert.That(Get(Combat, "AuraEmpowered"), Is.True);
        Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator BoneOverlapsTwicePerStrikeAndUpgradedCatchResetsFirstMove()
    {
        Learn("PowerUpPunch"); var enemy = Pokemon(Vector3.forward * 1.5f); Physics.SyncTransforms();
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Fields).GetValue(moves); clocks[0] = Now + 20;
        var bone = Effect("BoneRush", true); yield return new WaitForSeconds(0.42f);
        Assert.That(Hp(enemy), Is.EqualTo(20000 - 6 * 116));
        Assert.That(Call(bone, "TryRecast", Vector3.forward), Is.True); yield return new WaitForSeconds(0.6f);
        Assert.That(clocks[0], Is.LessThanOrEqualTo(Now)); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator AuraHitsMoreOftenCloseUpAndEmpowersPunch()
    {
        Learn("PowerUpPunch"); var near = Pokemon(Vector3.forward * 2); var far = Pokemon(Vector3.forward * 30); Physics.SyncTransforms();
        Effect("AuraCannon"); yield return new WaitForSeconds(2.9f);
        Assert.That(20000 - Hp(near), Is.GreaterThan(20000 - Hp(far))); Assert.That(Hp(far), Is.LessThan(20000));
        Assert.That(Get(Combat, "AuraEmpowered"), Is.True); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator DeathDuringChargeCleansRestrictionsAndChargingState()
    {
        Learn("PowerUpPunch"); Call(moves, "PrepareLucarioInput", Vector3.forward); var effect = Effect("PowerUpPunch", true); yield return new WaitForSeconds(0.1f);
        Variable(actor, "currentHp", 0); yield return null; yield return null;
        Assert.That(effect == null || !(bool)Get(effect, "IsSpawned"), Is.True); Assert.That((int)Get(moves, "ServerActionLocks"), Is.Zero);
        Assert.That(Get(Combat, "Punch"), Is.Null);
    }
    [UnityTest]
    public IEnumerator KickAndSpinPreserveNormalBasicCharge()
    {
        Call(Combat, "SpendBasic"); Assert.That(Get(Combat, "Charge"), Is.EqualTo(1));
        Call(Combat, "ReadyKick"); Call(Combat, "SpendSpecial", false); Assert.That(Get(Combat, "Charge"), Is.EqualTo(1));
        Call(Combat, "ReadySpin"); Call(Combat, "SpendSpecial", true); Assert.That(Get(Combat, "Charge"), Is.EqualTo(1)); yield return null;
    }
    [UnityTest]
    public IEnumerator ActualBoostedAttackDealsTwoHitsAndThrows()
    {
        var enemy = Pokemon(Vector3.forward * 2); Call(Combat, "ReadyBoosted");
        var attack = Get(moves, "ServerBasicAttack"); Physics.SyncTransforms(); Call(attack, "PerformOnServer", enemy);
        yield return new WaitForSeconds(0.4f);
        Assert.That(Hp(enemy), Is.EqualTo(19870)); Assert.That(Call(enemy, "HasStatusEffect", E("StatusType", "Incapacitated")), Is.True);
        Assert.That(Get(Combat, "Charge"), Is.Zero);
    }
    [UnityTest]
    public IEnumerator ActualKickPreservesReadyBoostedAttack()
    {
        var enemy = Pokemon(Vector3.forward * 3.5f); Call(Combat, "ReadyBoosted"); Call(Combat, "ReadyKick");
        var attack = Get(moves, "ServerBasicAttack"); Physics.SyncTransforms(); Call(attack, "PerformOnServer", enemy);
        yield return new WaitForSeconds(0.7f);
        Assert.That(Hp(enemy), Is.EqualTo(19900)); Assert.That(Get(Combat, "Charge"), Is.EqualTo(2)); Assert.That(Get(Combat, "KickReady"), Is.False);
    }
    [UnityTest]
    public IEnumerator SpinUsesItsOwnRangeAndLeavesKickAndOrdinaryChargeReady()
    {
        var far = Pokemon(Vector3.forward * 3.8f); Call(Combat, "SpendBasic"); Call(Combat, "ReadyKick"); Call(Combat, "ReadySpin");
        var attack = Get(moves, "ServerBasicAttack"); Call(attack, "PerformOnServer", far); yield return null;
        Assert.That(Hp(far), Is.EqualTo(20000)); Assert.That(Get(Combat, "SpinReady"), Is.True);
        var near = Pokemon(Vector3.forward * 2); Physics.SyncTransforms(); Call(attack, "PerformOnServer", near); yield return new WaitForSeconds(0.6f);
        Assert.That(Hp(near), Is.EqualTo(19925)); Assert.That(Get(Combat, "Charge"), Is.EqualTo(1));
        Assert.That(Get(Combat, "KickReady"), Is.True); Assert.That(Get(Combat, "SpinReady"), Is.False);
    }
    [UnityTest]
    public IEnumerator ActualDispatcherHonorsResetAndBoneCooldownStartsOnFirstUse()
    {
#if UNITY_EDITOR
        OwnActor();
        var local = Array.CreateInstance(T("MoveBase"), 2);
        for (int i = 0; i < 2; i++)
        {
            var move = Activator.CreateInstance(T(i == 0 ? "LucarioExtremeSpeed" : "LucarioBoneRush"));
            T("MoveBase").GetField("playerManager").SetValue(move, player); T("LucarioMove").GetField("request", Fields).SetValue(move, i + 1); local.SetValue(move, i);
        }
        Set(moves, "moves", local);
        var asset = AssetDatabase.LoadMainAssetAtPath("Assets/Pokemons/PlayableMons/Lucario.asset");
        Set(Get(actor, "BaseStats"), "learnableMoves", T("PokemonBase").GetField("learnableMoves", Fields).GetValue(asset));
        Variable(actor, "currentLevel", 14); Learn("ExtremeSpeed"); Pokemon(Vector3.forward * 2); Physics.SyncTransforms();
        Call(moves, "SpawnMoveEffectRPC", E("AvailableMoves", "LucarioExtremeSpeed"), Vector3.forward, 0ul, 1);
        yield return new WaitForSeconds(0.6f);
        var clocks = (double[])moves.GetType().GetField("prefabNextCast", Fields).GetValue(moves);
        Assert.That(clocks[0], Is.LessThanOrEqualTo(Now));
        Call(moves, "SpawnMoveEffectRPC", E("AvailableMoves", "LucarioBoneRush"), Vector3.forward, 0ul, 2);
        yield return new WaitForSeconds(0.6f);
        double originalExpiry = clocks[1]; Assert.That(originalExpiry - Now, Is.InRange(8.0, 9.0));
        Call(moves, "SpawnMoveEffectRPC", E("AvailableMoves", "LucarioBoneRush"), Vector3.forward, 0ul, 2);
        yield return new WaitForSeconds(0.6f); Assert.That(clocks[1], Is.EqualTo(originalExpiry));
#else
        yield break;
#endif
    }
    [UnityTest]
    public IEnumerator OldPunchInputCannotReleaseNewCharge()
    {
        OwnActor();
        Learn("PowerUpPunch"); var requests = (int[])moves.GetType().GetField("prefabOwnerRequests", Fields).GetValue(moves); requests[0] = 7;
        Call(moves, "PrepareLucarioInput", Vector3.forward); Effect("PowerUpPunch", true); yield return new WaitForSeconds(0.1f);
        Call(moves, "LucarioPunchInputRPC", Vector3.right, true, false, 6); yield return null;
        Assert.That(Get(Get(Combat, "Punch"), "Charging"), Is.True);
        Call(moves, "LucarioPunchInputRPC", Vector3.forward, true, true, 7); yield return new WaitForSeconds(0.1f);
        Assert.That(Get(Combat, "Punch"), Is.Null);
    }
    private void OwnActor()
    {
        Call(actor.GetComponent(T("Unity.Netcode.NetworkObject")), "ChangeOwnership", 0ul);
        var statuses = Array.CreateInstance(T("BattleActionStatus"), 2);
        for (int i = 0; i < 2; i++) statuses.SetValue(Activator.CreateInstance(T("BattleActionStatus"), 0f), i);
        Set(moves, "moveStatuses", statuses);
    }
}

public class LucarioHostTests : LucarioServerTests { protected override bool Host => true; }
