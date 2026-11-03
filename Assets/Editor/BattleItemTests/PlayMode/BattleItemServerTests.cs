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

public abstract class BattleItemServerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private Component server;
    private GameObject pokemonPrefab;
    protected virtual bool Host => false;
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, args.Select(argument => argument.GetType()).ToArray()).Invoke(obj, args);
    private static object Get(object obj, string name) => obj.GetType().GetProperty(name).GetValue(obj);
    private static void SetField(object obj, string name, object value) => obj.GetType().GetField(name, Private).SetValue(obj, value);
    private GameObject Node(string name) { var go = new GameObject(name); objects.Add(go); return go; }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Type managerType = Type("Unity.Netcode.NetworkManager");
        if (managerType.GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("An external NetworkManager is active; do not interfere with it.");
        var root = Node("Periodic damage dedicated server"); server = root.AddComponent(managerType);
        var transport = root.AddComponent(Type("Unity.Netcode.Transports.UTP.UnityTransport"));
        var setConnection = transport.GetType().GetMethods().First(m => m.Name == "SetConnectionData" && m.GetParameters().Length == 3 && m.GetParameters()[0].ParameterType == typeof(string));
        setConnection.Invoke(transport, new object[] { "127.0.0.1", (ushort)0, "127.0.0.1" });
        object config = Activator.CreateInstance(Type("Unity.Netcode.NetworkConfig"));
        managerType.GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
#if UNITY_EDITOR
        pokemonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/BattleItemTests/Fixtures/BattlePokemon.prefab");
        Assert.That(pokemonPrefab, Is.Not.Null); Call(server, "AddNetworkPrefab", pokemonPrefab);
#else
        Assert.Ignore("The isolated network fixture is Editor-only.");
#endif
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True); yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (server != null)
        {
            foreach (GameObject go in objects.ToArray())
            {
                if (go == null) continue;
                Component network = go.GetComponent(Type("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
            }
            Call(server, "Shutdown", false); yield return null;
        }
        foreach (GameObject go in objects) if (go != null) UnityEngine.Object.Destroy(go);
        foreach (ScriptableObject asset in assets) if (asset != null) UnityEngine.Object.Destroy(asset);
        objects.Clear(); assets.Clear(); yield return null;
    }
    private Component Pokemon(string name, int attack = 100)
    {
        var go = UnityEngine.Object.Instantiate(pokemonPrefab); go.name = name; objects.Add(go);
        var network = go.GetComponent(Type("Unity.Netcode.NetworkObject")); var pokemon = go.GetComponent(Type("Pokemon"));
        var stats = ScriptableObject.CreateInstance(Type("PokemonBase")); assets.Add(stats);
        SetField(stats, "maxHp", Enumerable.Repeat(5000, 15).ToArray()); SetField(stats, "attack", Enumerable.Repeat(attack, 15).ToArray());
        SetField(stats, "speed", Enumerable.Repeat(4000, 15).ToArray());
        SetField(stats, "atkSpeed", Enumerable.Repeat(100f, 15).ToArray());
        SetField(stats, "spAttack", Enumerable.Repeat(100, 15).ToArray());
        SetField(pokemon, "baseStats", stats);
        SetField(go.GetComponent(Type("MovesController")), "pokemon", pokemon);
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, Enum.Parse(Type("PokemonType"), "Wild"));
        Call(network, "Spawn", true);
        object hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); hp.GetType().GetProperty("Value").SetValue(hp, 5000);
        return pokemon;
    }
    private static object Status(string kind, float duration, ushort id = 60000) => Activator.CreateInstance(Type("StatusEffect"), Enum.Parse(Type("StatusType"), kind), duration, true, id);
    private static object Stat(string kind, short amount, bool buff, ushort id = 60000, ulong source = 0, bool percentage = true)
    {
        object change = Activator.CreateInstance(Type("StatChange"), amount, Enum.Parse(Type("Stat"), kind), 30f, true, buff, percentage, id, true);
        return Call(change, "WithSource", source);
    }
    private static bool Has(Component pokemon, string kind) => (bool)Call(pokemon, "HasStatusEffect", Enum.Parse(Type("StatusType"), kind));
    private static int Count(Component pokemon, string property) => (int)Get(Get(pokemon, property), "Count");

    [UnityTest]
    public IEnumerator BattleItemRestrictionNotificationsObserveTheCommittedNetworkList()
    {
        var target = Pokemon("Restriction replication ordering");
        int notifications = 0;
        bool observedImmunity = false;
        bool observedStasis = false;
        var signal = target.GetType().GetEvent("OnBattleItemStatusChange");
        Action changed = () =>
        {
            notifications++;
            observedImmunity = Has(target, "SpeedReductionImmunity");
            observedStasis = Has(target, "BattleItemStasis");
        };
        signal.AddEventHandler(target, changed);
        Call(target, "AddStatusEffect", Status("SpeedReductionImmunity", 0.1f, 64800));
        Assert.That(observedImmunity, Is.True);
        Call(target, "AddStatusEffect", Status("BattleItemStasis", 0.1f, 64802));
        Assert.That(observedStasis, Is.True);
        yield return new WaitForSeconds(0.2f);
        Assert.That(observedImmunity, Is.False);
        Assert.That(observedStasis, Is.False);
        Assert.That(notifications, Is.EqualTo(4));
        signal.RemoveEventHandler(target, changed);
    }

    [UnityTest]
    public IEnumerator TailBonusHasIndependentCooldownAndCannotProcItself()
    {
        var source = Pokemon("Tail user");
        var target = Pokemon("Tail wild target");
        var controller = source.GetComponent(Type("MovesController"));
        SetField(controller, "tailTarget", target);
        object time = Get(server, "ServerTime");
        double now = (double)Get(time, "Time");
        SetField(controller, "tailExpiresAt", now + 3);
        SetField(controller, "tailSubscribed", true);
        var signal = source.GetType().GetEvent("OnHealthDamageDealt");
        signal.AddEventHandler(source, Delegate.CreateDelegate(signal.EventHandlerType, controller, controller.GetType().GetMethod("ProcBattleTail", Private)));
        object damage = Activator.CreateInstance(Type("DamageInfo"), (ulong)Get(source, "NetworkObjectId"), 0f, (short)0, (short)100,
            Enum.Parse(Type("DamageType"), "True"), Enum.Parse(Type("DamageProprieties"), "None"));
        Call(target, "TakeDamageRPC", damage);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4640));
        Call(target, "TakeDamageRPC", damage);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4540));
        yield return new WaitForSeconds(0.55f);
        Call(target, "TakeDamageRPC", damage);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4180));
        SetField(controller, "tailExpiresAt", -1d);
        controller.GetType().GetMethod("UpdateBattleItemRuntime", Private).Invoke(controller, null);
        Call(target, "TakeDamageRPC", damage);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4080));
    }

    [UnityTest]
    public IEnumerator GoalHackerOverridesRefreshesAndRestoresLockedGoalDefenses()
    {
        var target = Pokemon("Goal defender");
        object blue = Enum.Parse(Type("Team"), "Blue"), orange = Enum.Parse(Type("Team"), "Orange");
        Call(target, "UpdateTeamRPC", blue);
        object health = target.GetType().GetField("currentHp", Private).GetValue(target);
        health.GetType().GetProperty("Value").SetValue(health, 4000);
        var playerRoot = Node("Inactive goal defender view"); playerRoot.SetActive(false);
        var player = playerRoot.AddComponent(Type("PlayerManager")); SetField(player, "pokemon", target);
        var goalRoot = Node("Inactive goal logic view"); goalRoot.SetActive(false);
        goalRoot.AddComponent(Type("Unity.Netcode.NetworkObject"));
        var goal = goalRoot.AddComponent(Type("GoalZone"));
        var behaviour = Type("Unity.Netcode.NetworkBehaviour");
        behaviour.GetProperty("IsServer").SetValue(goal, true);
        behaviour.GetProperty("IsSpawned").SetValue(goal, true);
        behaviour.GetProperty("IsSpawned").SetValue(player, true);
        SetField(goal, "team", blue); SetField(goal, "healAmount", 100); SetField(goal, "shieldAmount", 0.1f);
        ((IList)goal.GetType().GetField("playerManagerList", Private).GetValue(goal)).Add(player);
        object hack = goal.GetType().GetField("battleItemHack", Private).GetValue(goal);
        var changed = hack.GetType().GetField("OnValueChanged");
        changed.SetValue(hack, Delegate.CreateDelegate(changed.FieldType, goal, goal.GetType().GetMethod("HandleBattleItemHack", Private)));
        try
        {
            Call(goal, "ApplyGoalHacker", blue);
            goal.GetType().GetMethod("HealOccupants", Private).Invoke(goal, null);
            Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4100));
            object shield = Call(target, "GetShieldWithID", (ushort)64990);
            Assert.That(shield.GetType().GetField("Amount").GetValue(shield), Is.EqualTo(1000));
            Call(goal, "ApplyGoalHacker", orange);
            Assert.That(Call(target, "HasShieldWithID", (ushort)64990), Is.False);
            goal.GetType().GetMethod("HealOccupants", Private).Invoke(goal, null);
            Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4100));
            Call(goal, "ApplyGoalHacker", orange);
            Assert.That(Get(hack, "Value"), Is.EqualTo(-1));
            SetField(goal, "battleItemHackExpiresAt", -1d);
            goal.GetType().GetMethod("Update", Private).Invoke(goal, null);
            Assert.That(Get(hack, "Value"), Is.EqualTo(0));
            Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4200));
            shield = Call(target, "GetShieldWithID", (ushort)64990);
            Assert.That(shield.GetType().GetField("Amount").GetValue(shield), Is.EqualTo(500));
        }
        finally
        {
            behaviour.GetProperty("IsSpawned").SetValue(goal, false);
            behaviour.GetProperty("IsServer").SetValue(goal, false);
            behaviour.GetProperty("IsSpawned").SetValue(player, false);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator XAttackUsesAdditiveAttackSpeedAndTimedOffensiveBuffs()
    {
        var target = Pokemon("X Attack user");
        var controller = target.GetComponent(Type("MovesController"));
        object kind = Enum.Parse(Type("AvailableBattleItems"), "XAttack");
        var apply = controller.GetType().GetMethod("ApplyBattleItem", Private);
        Assert.That(apply.Invoke(controller, new[] { kind, (object)Vector3.zero, ulong.MaxValue, 0d }), Is.True);
        Assert.That(Call(target, "GetAttack"), Is.EqualTo(120));
        Assert.That(Call(target, "GetSpAttack"), Is.EqualTo(120));
        Assert.That(Call(target, "GetAtkSpeed"), Is.EqualTo(125f));
        object changes = Get(target, "StatChanges");
        object first = changes.GetType().GetProperty("Item").GetValue(changes, new object[] { 0 });
        Assert.That(first.GetType().GetField("Duration").GetValue(first), Is.EqualTo(6.5f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator PotionHealsOnDedicatedServerAndCannotOverhealOrRevive()
    {
        var target = Pokemon("Potion user");
        object health = target.GetType().GetField("currentHp", Private).GetValue(target);
        health.GetType().GetProperty("Value").SetValue(health, 4500);
        Call(target, "HealDamageRPC", 1160);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(5000));
        health.GetType().GetProperty("Value").SetValue(health, 0);
        Call(target, "HealDamageRPC", 1160);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(0));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FullHealRemovesConditionsButPreservesExistingBeneficialEffects()
    {
        var target = Pokemon("Full Heal user");
        Call(target, "AddStatusEffect", Status("Incapacitated", 30));
        Call(target, "AddStatusEffect", Status("Invisible", 30, 60001));
        Call(target, "AddStatChange", Stat("Speed", 30, false));
        Call(target, "AddStatChange", Stat("Attack", 20, true, 60001));
        Call(target, "CleanseBattleItemConditions");
        Assert.That(Has(target, "Incapacitated"), Is.False);
        Assert.That(Has(target, "Invisible"), Is.True);
        Assert.That(Count(target, "StatChanges"), Is.EqualTo(1));
        Call(target, "AddStatusEffect", Status("Unstoppable", 0.1f));
        Call(target, "AddStatusEffect", Status("Frozen", 10));
        Assert.That(Has(target, "Frozen"), Is.False);
        yield return new WaitForSeconds(0.2f);
        Assert.That(Has(target, "Unstoppable"), Is.False);
    }

    [UnityTest]
    public IEnumerator XSpeedIgnoresExistingAndNewSlowsOnlyDuringImmunity()
    {
        var target = Pokemon("X Speed user");
        Call(target, "AddStatChange", Stat("Speed", 45, true, 64800));
        Call(target, "AddStatChange", Stat("Speed", 80, false, 60000));
        Call(target, "AddStatusEffect", Status("SpeedReductionImmunity", 0.1f, 64800));
        Assert.That(Call(target, "GetSpeed"), Is.EqualTo(5625));
        Call(target, "AddStatChange", Stat("Speed", 10, false, 60001));
        Assert.That(Call(target, "GetSpeed"), Is.EqualTo(5625));
        yield return new WaitForSeconds(0.2f);
        Assert.That(Call(target, "GetSpeed"), Is.LessThan(4000));
    }

    [UnityTest]
    public IEnumerator OverlappingSmokeDoesNotStackAndEachSourceCleansUpIndependently()
    {
        var target = Pokemon("Smoke victim");
        foreach (ulong source in new[] { 10UL, 20UL })
        {
            Call(target, "AddStatChange", Stat("Speed", 80, false, 64801, source));
            Call(target, "AddStatChange", Stat("AtkSpeed", 50, false, 64801, source, false));
        }
        Assert.That(Call(target, "GetSpeed"), Is.EqualTo(1650));
        Assert.That(Call(target, "GetAtkSpeed"), Is.EqualTo(50f));
        Call(target, "RemoveBattleItemModifiers", (ushort)64801, 10UL);
        Assert.That(Count(target, "StatChanges"), Is.EqualTo(2));
        Assert.That(Call(target, "GetSpeed"), Is.EqualTo(1650));
        Call(target, "RemoveBattleItemModifiers", (ushort)64801, 20UL);
        Assert.That(Count(target, "StatChanges"), Is.Zero);
        Assert.That(Call(target, "GetSpeed"), Is.EqualTo(4000));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ShedinjaBlocksDamageAndConditionsAndExpiresWithoutClearingOtherBuffs()
    {
        var source = Pokemon("Attacker");
        var target = Pokemon("Shedinja user");
        Call(target, "AddStatusEffect", Status("Invincible", 0.1f, 64802));
        Call(target, "AddStatusEffect", Status("Untargetable", 0.1f, 64802));
        Call(target, "AddStatusEffect", Status("BattleItemStasis", 0.1f, 64802));
        Call(target, "AddStatusEffect", Status("Invisible", 30, 60001));
        Call(target, "AddStatusEffect", Status("Frozen", 10));
        Call(target, "AddStatChange", Stat("Speed", 80, false));
        object damage = Activator.CreateInstance(Type("DamageInfo"), (ulong)Get(source, "NetworkObjectId"), 0f, (short)0, (short)400,
            Enum.Parse(Type("DamageType"), "True"), Enum.Parse(Type("DamageProprieties"), "None"));
        Call(target, "TakeDamageRPC", damage);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(5000));
        Assert.That(Has(target, "Frozen"), Is.False);
        Assert.That(Count(target, "StatChanges"), Is.Zero);
        yield return new WaitForSeconds(0.2f);
        Assert.That(Has(target, "BattleItemStasis"), Is.False);
        Assert.That(Has(target, "Invisible"), Is.True);
        Call(target, "TakeDamageRPC", damage);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4600));
    }
}

public class BattleItemDedicatedServerTests : BattleItemServerTests { }
public class BattleItemHostTests : BattleItemServerTests
{
    protected override bool Host => true;
}
