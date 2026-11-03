using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public abstract class HeldItemServerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private Component server;
    private Component client;
    private ushort port;
    private GameObject prefab;
    protected virtual bool Host => false;
    private static Type Find(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static FieldInfo FindField(object target, string name)
    {
        for (Type type = target.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Private);
            if (field != null) return field;
        }
        throw new MissingFieldException(target.GetType().Name, name);
    }
    private static object Field(object target, string name) => FindField(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => FindField(target, name).SetValue(target, value);
    private static object Call(object target, string name, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .First(candidate => candidate.Name == name && candidate.GetParameters().Length >= arguments.Length
                && candidate.GetParameters().Take(arguments.Length).Select((parameter, index) => parameter.ParameterType.IsInstanceOfType(arguments[index])).All(matches => matches)
                && candidate.GetParameters().Skip(arguments.Length).All(parameter => parameter.IsOptional));
        return method.Invoke(target, arguments.Concat(method.GetParameters().Skip(arguments.Length).Select(parameter => parameter.DefaultValue)).ToArray());
    }
    private static object EnumValue(string type, string value) => Enum.Parse(Find(type), value);
    private GameObject Node(string name) { var root = new GameObject(name); objects.Add(root); return root; }
    private static void Health(Component pokemon, int amount) => Field(pokemon, "currentHp").GetType().GetProperty("Value").SetValue(Field(pokemon, "currentHp"), amount);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Type manager = Find("Unity.Netcode.NetworkManager");
        if (manager.GetProperty("Singleton").GetValue(null) != null) Assert.Ignore("An external network session is active.");
        var root = Node("Held item network fixture"); server = root.AddComponent(manager);
        var transport = root.AddComponent(Find("Unity.Netcode.Transports.UTP.UnityTransport"));
        port = (ushort)UnityEngine.Random.Range(20000, 60000);
        transport.GetType().GetMethods().First(method => method.Name == "SetConnectionData" && method.GetParameters().Length == 3 && method.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", port, "127.0.0.1" });
        object config = Activator.CreateInstance(Find("Unity.Netcode.NetworkConfig"));
        manager.GetField("NetworkConfig").SetValue(server, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/HeldItemTests/Fixtures/HeldPokemon.prefab");
        Assert.That(prefab, Is.Not.Null);
        Call(server, "AddNetworkPrefab", prefab);
        Assert.That(Call(server, Host ? "StartHost" : "StartServer"), Is.True);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (client != null) { Call(client, "Shutdown", false); yield return null; client = null; }
        if (server != null)
        {
            foreach (GameObject root in objects.ToArray())
            {
                if (root == null) continue;
                var network = root.GetComponent(Find("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
            }
            Call(server, "Shutdown", false);
            yield return null;
        }
        foreach (GameObject root in objects) if (root != null) UnityEngine.Object.Destroy(root);
        foreach (ScriptableObject asset in assets) if (asset != null) UnityEngine.Object.Destroy(asset);
        objects.Clear(); assets.Clear();
        yield return null;
    }

    private Component Pokemon(string name, string team = "Blue", int attack = 100)
    {
        var root = UnityEngine.Object.Instantiate(prefab); root.name = name; objects.Add(root);
        var pokemon = root.GetComponent(Find("Pokemon"));
        var stats = ScriptableObject.CreateInstance(Find("PokemonBase")); assets.Add(stats);
        Set(stats, "maxHp", Enumerable.Repeat(5000, 15).ToArray());
        Set(stats, "attack", Enumerable.Repeat(attack, 15).ToArray());
        Set(stats, "spAttack", Enumerable.Repeat(100, 15).ToArray());
        Set(stats, "speed", Enumerable.Repeat(4000, 15).ToArray());
        Set(stats, "atkSpeed", Enumerable.Repeat(100f, 15).ToArray());
        Set(stats, "evolutions", Array.CreateInstance(Find("PokemonEvolution"), 0));
        Set(stats, "learnableMoves", Array.CreateInstance(Find("LearnableMove"), 0));
        Set(pokemon, "baseStats", stats);
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, EnumValue("PokemonType", "Player"));
        Call(root.GetComponent(Find("Unity.Netcode.NetworkObject")), "Spawn", true);
        Health(pokemon, 5000);
        Call(pokemon, "UpdateTeamRPC", EnumValue("Team", team));
        return pokemon;
    }

    private object Equip(Component pokemon, string kind)
    {
        var manager = pokemon.GetComponent(Find("HeldItemManager"));
        var view = Node("Inactive item controller"); view.SetActive(false);
        var player = view.AddComponent(Find("PlayerManager"));
        Set(player, "pokemon", pokemon); Set(player, "heldItemManager", manager);
        Set(manager, "player", player); Set(manager, "initialized", true); Set(manager, "nextRegeneration", Time.time + 100);
        object equipment = Field(manager, "equipment");
        ulong bits = (ulong)Get(equipment, "Value");
        object itemKind = EnumValue("AvailableHeldItems", kind);
        equipment.GetType().GetProperty("Value").SetValue(equipment, bits | (1UL << Convert.ToInt32(itemKind)));
        object item = Find("HeldItemDatabase").GetMethod("GetHeldItem").Invoke(null, new[] { itemKind });
        Call(item, "Initialize", player);
        ((IList)Field(manager, "items")).Add(item);
        return item;
    }

    private object Damage(Component source, int amount, bool basic = true, string type = "Physical", bool critical = false)
    {
        int flags = basic ? 2 : 0;
        if (critical) flags |= 32;
        return Activator.CreateInstance(Find("DamageInfo"), Get(source, "NetworkObjectId"), 0f, (short)0, (short)amount,
            EnumValue("DamageType", type), Enum.ToObject(Find("DamageProprieties"), flags));
    }

    private static object Shield(int amount, ushort id) => Activator.CreateInstance(Find("ShieldInfo"), amount, id, (ushort)0, 10f, true, (ushort)0);

    [UnityTest]
    public IEnumerator MuscleBandUsesPreHitRemainingHpOnceOnBothServerModes()
    {
        var source = Pokemon("Muscle source"); var target = Pokemon("Muscle target", "Orange");
        Equip(source, "MuscleBand"); Health(target, 1000);
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(870));
        yield return null;
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(870));
    }

    [UnityTest]
    public IEnumerator ItemDamageNeverRecursivelyTriggersOtherItemEffects()
    {
        var source = Pokemon("Mixed item source"); var target = Pokemon("Mixed item target", "Orange");
        Equip(source, "ChoiceSpecs"); Equip(source, "ShellBell"); Equip(source, "ScopeLens");
        Health(source, 1000);
        Call(target, "ResolveDamage", Damage(source, 100, false));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4800));
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1140));
        Call(target, "ResolveDamage", Damage(source, 100, false));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4700));
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1140));
        yield return null;
    }

    [UnityTest]
    public IEnumerator DrainCrownExcludesShieldsAndOverkillAndUsesFifteenPercent()
    {
        var source = Pokemon("Drain source"); var target = Pokemon("Drain target", "Orange");
        Equip(source, "DrainCrown"); Health(source, 1000);
        Call(target, "AddShieldRPC", Shield(100, 59999));
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1000));
        Health(target, 40);
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1006));
        yield return null;
    }

    [UnityTest]
    public IEnumerator AssaultVestIgnoresPhysicalDamageButAbsorbsSpecialDamage()
    {
        var source = Pokemon("Vest source"); var target = Pokemon("Vest target", "Orange");
        Call(target, "GrantHeldItemShield", Shield(1000, 26001), target);
        Call(target, "ResolveDamage", Damage(source, 100, false));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4900));
        Assert.That(Get(target, "ShieldHp"), Is.EqualTo(1000));
        Call(target, "ResolveDamage", Damage(source, 200, false, "Special"));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4900));
        Assert.That(Get(target, "ShieldHp"), Is.EqualTo(800));
        yield return null;
    }

    [UnityTest]
    public IEnumerator CurseDoesNotStackAndBigRootExcludesEnvironmentalHealing()
    {
        var source = Pokemon("Curse source"); var target = Pokemon("Curse target", "Orange");
        Equip(source, "CurseBangle"); Equip(source, "CurseIncense"); Equip(target, "BigRoot");
        Health(target, 1000);
        Call(target, "ResolveDamage", Damage(source, 100));
        Call(target, "HealDamageRPC", 100);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(984));
        Call(target, "HealEnvironmentRPC", 100);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(1054));
        yield return new WaitForSeconds(1.6f);
        Call(target, "HealDamageRPC", 100);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(1174));
    }

    [UnityTest]
    public IEnumerator RescueHoodOnlyBoostsOtherAlliesHealingAndShields()
    {
        var source = Pokemon("Rescue source"); var ally = Pokemon("Rescue ally"); var enemy = Pokemon("Rescue enemy", "Orange");
        Equip(source, "RescueHood"); Health(source, 1000); Health(ally, 1000); Health(enemy, 1000);
        Call(source, "HealDamageRPC", 100);
        Call(ally, "HealDamageRPC", 100, Get(source, "NetworkObjectId"));
        Call(enemy, "HealDamageRPC", 100, Get(source, "NetworkObjectId"));
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1100));
        Assert.That(Get(ally, "CurrentHp"), Is.EqualTo(1123));
        Assert.That(Get(enemy, "CurrentHp"), Is.EqualTo(1100));
        Call(ally, "GrantHeldItemShield", Shield(100, 59999), source);
        Assert.That(Get(ally, "ShieldHp"), Is.EqualTo(123));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ScoreShieldAbsorbsFirstAndItsBreakingHitImmediatelyInterruptsScoring()
    {
        var source = Pokemon("Score attacker"); var target = Pokemon("Score target", "Orange");
        object score = Activator.CreateInstance(Find("ShieldInfo"), 100, (ushort)26000, ushort.MaxValue, 10f, true, (ushort)0);
        Call(target, "AddShieldRPC", score); Call(target, "AddShieldRPC", Shield(500, 59999));
        Call(target, "ResolveDamage", Damage(source, 50));
        Assert.That(Get(target, "LastDamageProtectedScoring"), Is.True);
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(Get(target, "LastDamageProtectedScoring"), Is.False);
        Assert.That(Get(target, "ShieldHp"), Is.EqualTo(450));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(5000));
        yield return null;
    }

    [UnityTest]
    public IEnumerator StackItemsStopAtSixAndKeepTheirStacksAcrossReset()
    {
        var source = Pokemon("Score stack source");
        object weight = Equip(source, "AttackWeight");
        for (int index = 0; index < 10; index++) Call(weight, "Score");
        Assert.That(Call(source, "GetAttack"), Is.EqualTo(172));
        Call(weight, "Reset"); Call(weight, "Score");
        Assert.That(Call(source, "GetAttack"), Is.EqualTo(172));
        yield return null;
    }

    [UnityTest]
    public IEnumerator EnemyKnockoutsAwardTwoStacksAndRealDamageAssistsAwardOne()
    {
        var killer = Pokemon("KO source"); var assistant = Pokemon("Assist source"); var victim = Pokemon("KO victim", "Orange");
        Equip(killer, "AccelBracer"); Equip(assistant, "DriveLens");
        Call(victim, "ResolveDamage", Damage(assistant, 100));
        Call(victim, "ResolveDamage", Damage(killer, 5000));
        Assert.That(Call(killer, "GetPreciseStatChange", EnumValue("Stat", "Attack")), Is.EqualTo(1.2f).Within(0.0001));
        Assert.That(Call(assistant, "GetPreciseStatChange", EnumValue("Stat", "SpAttack")), Is.EqualTo(0.6f).Within(0.0001));
        yield return null;
    }

    [UnityTest]
    public IEnumerator EnergyAmplifierMultipliesDamageWithoutChangingAttackAndNeverProcsTwice()
    {
        var source = Pokemon("Energy source"); var target = Pokemon("Energy target", "Orange");
        var energy = Equip(source, "EnergyAmp");
        Call(energy, "Move", EnumValue("MoveType", "UniteMove"));
        Assert.That(Call(source, "GetAttack"), Is.EqualTo(100));
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4879));
        Call(energy, "Move", EnumValue("MoveType", "UniteMove"));
        Assert.That(Get(energy, "DamageMultiplier"), Is.EqualTo(1.21f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FocusBandUsesMissingHpAndResetStopsAnOngoingRecovery()
    {
        var source = Pokemon("Focus source"); var focus = Equip(source, "FocusBand");
        Health(source, 1000); Call(focus, "Update");
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1480));
        Call(focus, "Reset");
        yield return new WaitForSeconds(1.1f);
        Call(focus, "Update");
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1480));
    }

    [UnityTest]
    public IEnumerator FractionalCooldownAndAttackSpeedAreNotTruncated()
    {
        var source = Pokemon("Fractional source");
        foreach (var pair in new[] { Tuple.Create("Cdr", 4.5f), Tuple.Create("AtkSpeed", 7.5f), Tuple.Create("CritRate", 2.1f) })
        {
            object change = Activator.CreateInstance(Find("StatChange"), pair.Item2, EnumValue("Stat", pair.Item1), 0f, false, true, false, (ushort)59999, false);
            Call(source, "AddStatChange", change);
        }
        Assert.That(Call(source, "GetCDR"), Is.EqualTo(4.5f));
        Assert.That(Call(source, "GetAtkSpeed"), Is.EqualTo(107.5f));
        Assert.That(Call(source, "GetCritRate"), Is.EqualTo(2.1f).Within(0.0001));
        yield return null;
    }

    [UnityTest]
    public IEnumerator LateJoiningRemoteOwnerReceivesEquipmentFractionalStatsAndExactlyOneProc()
    {
        var source = Pokemon("Remote-owned item source"); var target = Pokemon("Replicated target", "Orange");
        Equip(source, "MuscleBand");
        object change = Activator.CreateInstance(Find("StatChange"), 4.5f, EnumValue("Stat", "Cdr"), 0f, false, true, false, (ushort)59999, false);
        Call(source, "AddStatChange", change);
        var root = Node("Held item remote client");
        Type managerType = Find("Unity.Netcode.NetworkManager"); client = root.AddComponent(managerType);
        var transport = root.AddComponent(Find("Unity.Netcode.Transports.UTP.UnityTransport"));
        transport.GetType().GetMethods().First(method => method.Name == "SetConnectionData" && method.GetParameters().Length == 3 && method.GetParameters()[0].ParameterType == typeof(string))
            .Invoke(transport, new object[] { "127.0.0.1", port, "127.0.0.1" });
        object config = Activator.CreateInstance(Find("Unity.Netcode.NetworkConfig"));
        managerType.GetField("NetworkConfig").SetValue(client, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        Call(client, "AddNetworkPrefab", prefab);
        Assert.That(Call(client, "StartClient"), Is.True);
        float until = Time.realtimeSinceStartup + 8;
        while (!(bool)Get(client, "IsConnectedClient") && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(Get(client, "IsConnectedClient"), Is.True);
        var sourceNetwork = source.GetComponent(Find("Unity.Netcode.NetworkObject"));
        Call(sourceNetwork, "ChangeOwnership", Get(client, "LocalClientId"));
        var spawnManager = Get(client, "SpawnManager");
        var spawned = (IDictionary)spawnManager.GetType().GetField("SpawnedObjects").GetValue(spawnManager);
        object sourceId = Get(source, "NetworkObjectId"), targetId = Get(target, "NetworkObjectId");
        until = Time.realtimeSinceStartup + 8;
        while ((!spawned.Contains(sourceId) || !spawned.Contains(targetId)) && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(spawned.Contains(sourceId), Is.True);
        var remoteSource = ((Component)spawned[sourceId]).GetComponent(Find("Pokemon"));
        var remoteTarget = ((Component)spawned[targetId]).GetComponent(Find("Pokemon"));
        Assert.That(Call(remoteSource, "HasHeldItem", EnumValue("AvailableHeldItems", "MuscleBand")), Is.True);
        Assert.That(Call(remoteSource, "GetPreciseStatChange", EnumValue("Stat", "Cdr")), Is.EqualTo(4.5f));
        until = Time.realtimeSinceStartup + 4;
        var remoteItems = remoteSource.GetComponent(Find("HeldItemManager"));
        while (!(bool)Get(remoteItems, "IsOwner") && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(Get(remoteItems, "IsOwner"), Is.True);
        Assert.That(Get(source.GetComponent(Find("HeldItemManager")), "IsOwner"), Is.False);
        Call(target, "ResolveDamage", Damage(source, 100));
        until = Time.realtimeSinceStartup + 4;
        while ((int)Get(remoteTarget, "CurrentHp") != 4750 && Time.realtimeSinceStartup < until) yield return null;
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4750));
        Assert.That(Get(remoteTarget, "CurrentHp"), Is.EqualTo(4750));
        yield return new WaitForSeconds(0.15f);
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4750));
    }

    [UnityTest]
    public IEnumerator AllyShieldsPickOneLivingLowestHpAllyAndNeverStack()
    {
        var source = Pokemon("Barrier source"); var first = Pokemon("First ally"); var second = Pokemon("Second ally");
        var dead = Pokemon("Dead ally"); var enemy = Pokemon("Barrier enemy", "Orange");
        Health(first, 2000); Health(second, 1000); Health(dead, 0); Health(enemy, 1);
        object barrier = Equip(source, "BuddyBarrier");
        Call(barrier, "Move", EnumValue("MoveType", "UniteMove"));
        Assert.That(Get(source, "ShieldHp"), Is.EqualTo(1250));
        Assert.That(Get(second, "ShieldHp"), Is.EqualTo(1250));
        Assert.That(Get(first, "ShieldHp"), Is.EqualTo(0));
        Assert.That(Get(dead, "ShieldHp"), Is.EqualTo(0));
        Call(second, "GrantHeldItemShield", Shield(1250, 26002), source);
        Assert.That(Get(second, "ShieldHp"), Is.EqualTo(1250));
        var target = Pokemon("Guard enemy", "Orange");
        Equip(source, "ResonantGuard");
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(Get(source, "ShieldHp"), Is.EqualTo(1650));
        Assert.That(Get(second, "ShieldHp"), Is.EqualTo(1650));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ScarfRequiresThreeHitsAndGrantsTwentyFivePercentagePoints()
    {
        var source = Pokemon("Scarf source"); var target = Pokemon("Scarf target", "Orange");
        object scarf = Equip(source, "RapidFireScarf");
        for (int index = 0; index < 2; index++) Call(target, "ResolveDamage", Damage(source, 10));
        Assert.That(Call(source, "GetAtkSpeed"), Is.EqualTo(100f));
        Call(target, "ResolveDamage", Damage(source, 10));
        Assert.That(Call(source, "GetAtkSpeed"), Is.EqualTo(125f));
        for (int index = 0; index < 3; index++) Call(target, "ResolveDamage", Damage(source, 10));
        Assert.That(Call(source, "GetAtkSpeed"), Is.EqualTo(125f));
        Call(scarf, "Reset");
        yield return null;
    }

    [UnityTest]
    public IEnumerator WeaknessPolicyHasFractionalStacksAndATenPercentCap()
    {
        var source = Pokemon("Policy attacker", "Orange"); var target = Pokemon("Policy holder");
        Equip(target, "WeaknessPolicy");
        Call(target, "ResolveDamage", Damage(source, 10));
        Assert.That(Call(target, "GetPreciseStatChange", EnumValue("Stat", "Attack")), Is.EqualTo(2.5f));
        for (int index = 0; index < 6; index++) Call(target, "ResolveDamage", Damage(source, 10));
        Assert.That(Call(target, "GetAttack"), Is.EqualTo(110));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpoonPenetrationIsSpecialOnlyAndHelmetUsesCurrentThresholdAndCooldown()
    {
        var source = Pokemon("Spoon attacker"); var target = Pokemon("Helmet holder", "Orange");
        Equip(source, "SlickSpoon");
        Set(Get(target, "BaseStats"), "spDefense", Enumerable.Repeat(600, 15).ToArray());
        Assert.That(Call(target, "CalculateDamage", Damage(source, 600, false, "Special"), source), Is.EqualTo(324));
        Assert.That(Call(target, "CalculateDamage", Damage(source, 600), source), Is.EqualTo(600));
        object helmet = Equip(target, "RockyHelmet");
        Call(target, "ResolveDamage", Damage(source, 149));
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(5000));
        Call(target, "ResolveDamage", Damage(source, 150));
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(4875));
        Call(target, "ResolveDamage", Damage(source, 150));
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(4875));
        Call(helmet, "Reset");
        yield return null;
    }

    [UnityTest]
    public IEnumerator ScopeLensOnlyProcsOnCriticalBasicAttacks()
    {
        var source = Pokemon("Scope attacker"); var target = Pokemon("Scope target", "Orange");
        Equip(source, "ScopeLens");
        Call(target, "ResolveDamage", Damage(source, 100, false, "Physical", true));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4800));
        Call(target, "ResolveDamage", Damage(source, 100, true, "Physical", true));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4525));
        Call(target, "ResolveDamage", Damage(source, 100, true, "Physical", true));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4325));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ChargingCharmUsesMovementAndBasicHitsButNotTeleportDistance()
    {
        var source = Pokemon("Charm attacker"); var target = Pokemon("Charm target", "Orange");
        object charm = Equip(source, "ChargingCharm");
        source.transform.position = Vector3.right * 100; Call(charm, "Update");
        Assert.That(Field(charm, "charge"), Is.EqualTo(0f));
        for (int index = 0; index < 10; index++) { source.transform.position += Vector3.right; Call(charm, "Update"); }
        for (int index = 0; index < 10; index++) Call(target, "ResolveDamage", Damage(source, 10));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4790));
        Assert.That(Field(charm, "charge"), Is.EqualTo(0f));
        yield return null;
    }

    [UnityTest]
    public IEnumerator LeftoversFloatStoneAndAssaultVestRespectTheirActivationConditions()
    {
        var source = Pokemon("Out of combat holder"); Health(source, 1000);
        object leftovers = Equip(source, "Leftovers"); object stone = Equip(source, "FloatStone"); object vest = Equip(source, "AssaultVest");
        Set(leftovers, "nextTick", Time.time - 1);
        Call(leftovers, "Update"); Call(stone, "Update");
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1200));
        Assert.That(Call(source, "GetSpeed"), Is.EqualTo(4800));
        Field(source, "isOutOfCombat").GetType().GetProperty("Value").SetValue(Field(source, "isOutOfCombat"), false);
        Call(stone, "Update");
        Assert.That(Call(source, "GetSpeed"), Is.EqualTo(4000));
        Set(vest, "nextUse", Time.time - 1); Call(vest, "Update");
        Assert.That(Get(source, "ShieldHp"), Is.EqualTo(1000));
        yield return null;
    }

    [UnityTest]
    public IEnumerator ExperienceShareUsesWholeMatchProgressAndPreservesEvolutionStorage()
    {
        var source = Pokemon("Share holder"); var ally = Pokemon("Share ally");
        object share = Equip(source, "ExpShare");
        Field(ally, "currentLevel").GetType().GetProperty("Value").SetValue(Field(ally, "currentLevel"), 1);
        Set(share, "nextTick", Time.time - 1); Call(share, "Update");
        Assert.That(Get(source, "CurrentExp"), Is.EqualTo(5));
        object evolution = Activator.CreateInstance(Find("PokemonEvolution"));
        evolution.GetType().GetField("level").SetValue(evolution, 1);
        Array evolutions = Array.CreateInstance(Find("PokemonEvolution"), 1); evolutions.SetValue(evolution, 0);
        Set(Get(source, "BaseStats"), "evolutions", evolutions);
        Set(share, "nextTick", Time.time - 1); Call(share, "Update");
        Assert.That(Get(source, "StoredExp"), Is.EqualTo(5));
        Assert.That(Get(source, "CurrentExp"), Is.EqualTo(5));
        Health(source, 0); Set(share, "nextTick", Time.time - 1); Call(share, "Update");
        Assert.That(Get(source, "CurrentExp"), Is.EqualTo(5));
        yield return null;
    }

    [UnityTest]
    public IEnumerator CookieSpecsGlassesAndRazorClawApplyTheirDistinctEffects()
    {
        var source = Pokemon("Stat item holder"); var target = Pokemon("Razor target", "Orange");
        object cookie = Equip(source, "AeosCookie"); object specs = Equip(source, "SpAtkSpecs");
        for (int index = 0; index < 8; index++) { Call(cookie, "Score"); Call(specs, "Score"); }
        Assert.That(Call(source, "GetMaxHp"), Is.EqualTo(6200));
        Assert.That(Call(source, "GetSpAttack"), Is.EqualTo(196));
        Equip(source, "WiseGlasses");
        Assert.That(Call(source, "GetSpAttack"), Is.EqualTo(210));
        object razor = Equip(source, "RazorClaw");
        Call(razor, "Move", EnumValue("MoveType", "MoveA"));
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4830));
        Call(razor, "Move", EnumValue("MoveType", "MoveA"));
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4730));
        yield return null;
    }

    [UnityTest]
    public IEnumerator VanguardBellOnlyTriggersForEnemyPlayerHindrancesOncePerCooldown()
    {
        var source = Pokemon("Vanguard holder"); var target = Pokemon("Hindered enemy", "Orange"); var ally = Pokemon("Hindered ally");
        object bell = Equip(source, "VanguardBell"); Health(source, 1000);
        Call(bell, "Hindrance", ally); Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1000));
        Call(bell, "Hindrance", target); Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1500));
        Call(bell, "Hindrance", target); Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1500));
        yield return null;
    }

    [UnityTest]
    public IEnumerator MarkersDoNotConsumeVanguardBellButAcceptedHindrancesDo()
    {
        var source = Pokemon("Marker source"); var target = Pokemon("Marker target", "Orange");
        Equip(source, "VanguardBell"); Health(source, 1000);
        foreach (string kind in new[] { "SnowMarked", "FutureSightMarked", "Immobilized" })
        {
            object status = Activator.CreateInstance(Find("StatusEffect"), EnumValue("StatusType", kind), 1f, true, (ushort)500);
            status = Call(status, "WithSource", Get(source, "NetworkObjectId"));
            Call(target, "AddStatusEffectRPC", status);
            Assert.That(Get(source, "CurrentHp"), Is.EqualTo(kind == "Immobilized" ? 1500 : 1000), kind);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator SecondaryDamageCanDischargeCharmAndGuardButItemDamageCannot()
    {
        var source = Pokemon("Secondary source"); var target = Pokemon("Secondary target", "Orange");
        object charm = Equip(source, "ChargingCharm"); Equip(source, "ResonantGuard");
        Set(charm, "charge", 100f);
        object itemDamage = Damage(source, 10, false);
        itemDamage.GetType().GetField("source").SetValue(itemDamage, EnumValue("DamageSource", "Item"));
        Call(target, "ResolveDamage", itemDamage);
        Assert.That(Field(charm, "charge"), Is.EqualTo(100f));
        Assert.That(Get(source, "ShieldHp"), Is.EqualTo(0));
        object periodic = Damage(source, 10, false);
        periodic.GetType().GetField("source").SetValue(periodic, EnumValue("DamageSource", "Secondary"));
        periodic.GetType().GetField("suppressOnHitEffects").SetValue(periodic, true);
        Call(target, "ResolveDamage", periodic);
        Assert.That(Field(charm, "charge"), Is.EqualTo(0f));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4870));
        Assert.That(Get(source, "ShieldHp"), Is.EqualTo(400));
        yield return null;
    }

    [UnityTest]
    public IEnumerator SpoonFollowsTheScalingStatWhenWonderRoomInvertsIt()
    {
        var source = Pokemon("Inverted spoon source"); var target = Pokemon("Inverted spoon target", "Orange");
        Equip(source, "SlickSpoon");
        Set(Get(target, "BaseStats"), "defense", Enumerable.Repeat(600, 15).ToArray());
        Set(Get(target, "BaseStats"), "spDefense", Enumerable.Repeat(600, 15).ToArray());
        Field(source, "flipAtkStat").GetType().GetProperty("Value").SetValue(Field(source, "flipAtkStat"), true);
        Assert.That(Call(target, "CalculateDamage", Damage(source, 600, false, "Special"), source), Is.EqualTo(300));
        Assert.That(Call(target, "CalculateDamage", Damage(source, 600), source), Is.EqualTo(324));
        yield return null;
    }

    [UnityTest]
    public IEnumerator AssaultVestFollowsSpecialAttackScalingUnderWonderRoom()
    {
        var source = Pokemon("Inverted vest attacker"); var target = Pokemon("Inverted vest holder", "Orange");
        object vest = Equip(target, "AssaultVest");
        Call(target, "GrantHeldItemShield", Shield(1000, 26001), target);
        Field(source, "flipAtkStat").GetType().GetProperty("Value").SetValue(Field(source, "flipAtkStat"), true);
        Set(vest, "nextUse", Time.time - 1);
        Call(target, "ResolveDamage", Damage(source, 100, false, "Special"));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4900));
        Assert.That(Get(target, "ShieldHp"), Is.EqualTo(1000));
        Assert.That((float)Field(vest, "nextUse"), Is.LessThan(Time.time));
        Call(target, "ResolveDamage", Damage(source, 100));
        Assert.That(Get(target, "CurrentHp"), Is.EqualTo(4900));
        Assert.That(Get(target, "ShieldHp"), Is.EqualTo(900));
        Assert.That((float)Field(vest, "nextUse"), Is.GreaterThan(Time.time));
        yield return null;
    }

    [UnityTest]
    public IEnumerator LegacyItemCastsRequireLearnedSelectionCooldownAndServerEnergy()
    {
        var source = Pokemon("Legacy item source");
        var controller = source.GetComponent(Find("MovesController"));
        Set(controller, "pokemon", source);
        object swift = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Sylveon/SylvSwift.asset");
        object unite = AssetDatabase.LoadMainAssetAtPath("Assets/Moves/Glaceon/GlaceonUnite.asset");
        Assert.That(Call(controller, "AcceptLegacyItemCast", swift), Is.False);
        Array selected = (Array)Field(controller, "prefabLearnedMoves");
        selected.SetValue(swift.GetType().GetField("move").GetValue(swift), 0);
        selected.SetValue(unite.GetType().GetField("move").GetValue(unite), 2);
        object learn = Activator.CreateInstance(Find("LearnableMove"));
        Array moves = Array.CreateInstance(Find("MoveAsset"), 2); moves.SetValue(swift, 0); moves.SetValue(unite, 1);
        learn.GetType().GetField("moves").SetValue(learn, moves);
        Array learnable = Array.CreateInstance(Find("LearnableMove"), 1); learnable.SetValue(learn, 0);
        Set(Get(source, "BaseStats"), "learnableMoves", learnable);
        Assert.That(Call(controller, "AcceptLegacyItemCast", swift), Is.True);
        Assert.That(Call(controller, "AcceptLegacyItemCast", swift), Is.False);
        Call(controller, "ReduceLegacyItemCooldown", EnumValue("MoveType", "MoveA"), 10f);
        Assert.That(Call(controller, "AcceptLegacyItemCast", swift), Is.True);
        Assert.That(Call(controller, "AcceptLegacyItemCast", unite), Is.False);
        Call(controller, "GrantLegacyItemEnergy", 100000);
        Assert.That(Call(controller, "AcceptLegacyItemCast", unite), Is.True);
        Assert.That(Call(controller, "AcceptLegacyItemCast", unite), Is.False);
        Assert.That(Find("HeldItemManager").GetMethod("ReportMoveRPC", Private), Is.Null);
        yield return null;
    }

    [UnityTest]
    public IEnumerator PooledDespawnReleasesItemModifiersAndShieldsBeforeNetworkTeardown()
    {
        var source = Pokemon("Pooled item holder");
        object weight = Equip(source, "AttackWeight"); Equip(source, "AssaultVest");
        Call(weight, "Score");
        Call(source, "GrantHeldItemShield", Shield(1000, 26001), source);
        Assert.That(Call(source, "GetAttack"), Is.EqualTo(112));
        var network = source.GetComponent(Find("Unity.Netcode.NetworkObject"));
        Call(network, "Despawn", false);
        Assert.That(Call(source, "GetAttack"), Is.EqualTo(100));
        Assert.That(Get(source, "ShieldHp"), Is.EqualTo(0));
        Call(network, "Spawn", true);
        Call(Equip(source, "AttackWeight"), "Score");
        Assert.That(Call(source, "GetAttack"), Is.EqualTo(112));
        yield return null;
    }

    [UnityTest]
    public IEnumerator FractionalCriticalChanceDoesNotRoundUpAndPreservesZeroAndHundredPercent()
    {
        var source = Pokemon("Fractional critical holder");
        object modifier = Activator.CreateInstance(Find("StatChange"), 2.1f, EnumValue("Stat", "CritRate"), 0f,
            false, true, false, (ushort)59998, false);
        Call(source, "SetHeldItemModifier", (ushort)59998, modifier);
        var previousRandom = UnityEngine.Random.state;
        try
        {
            foreach (bool critical in new[] { true, false })
            {
                int selectedSeed = -1;
                for (int seed = 1; seed < 10000; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    float roll = UnityEngine.Random.value * 100f;
                    if ((critical && roll < 2.1f) || (!critical && roll > 2.1f && roll < 3f))
                    { selectedSeed = seed; break; }
                }
                Assert.That(selectedSeed, Is.GreaterThanOrEqualTo(0));
                UnityEngine.Random.InitState(selectedSeed);
                Assert.That(Call(source, "RollCriticalHit", 0f), Is.EqualTo(critical));
            }
            Assert.That(Call(source, "RollCriticalHit", -2.1f), Is.False);
            Assert.That(Call(source, "RollCriticalHit", 100f), Is.True);
        }
        finally { UnityEngine.Random.state = previousRandom; }
        yield return null;
    }

    [UnityTest]
    public IEnumerator StartingHpIncludesItemBonusesWithoutHealingInjuredOrDeadHolders()
    {
        var source = Pokemon("Initial item HP holder");
        object modifier = Activator.CreateInstance(Find("StatChange"), (short)450, EnumValue("Stat", "MaxHp"), 0f,
            false, true, false, (ushort)59998, false);
        Call(source, "SetHeldItemModifier", (ushort)59998, modifier);
        Call(source, "RefreshHeldItemStartingHealth", 5000);
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(5450));
        Health(source, 1000); Call(source, "RefreshHeldItemStartingHealth", 5000);
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(1000));
        Health(source, 0); Call(source, "RefreshHeldItemStartingHealth", 5000);
        Assert.That(Get(source, "CurrentHp"), Is.EqualTo(0));
        yield return null;
    }
}

public class HeldItemDedicatedServerTests : HeldItemServerTests { }
public class HeldItemHostTests : HeldItemServerTests { protected override bool Host => true; }
