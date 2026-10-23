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

// A real dedicated server, with plain Pokemon objects: no PlayerManager, passive controller, UI or lobby.
public class PeriodicDamageServerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly List<ScriptableObject> assets = new List<ScriptableObject>();
    private Component server;
    private GameObject pokemonPrefab;
    private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name).Invoke(obj, args);
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
        pokemonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab");
        Assert.That(pokemonPrefab, Is.Not.Null); Call(server, "AddNetworkPrefab", pokemonPrefab);
#else
        Assert.Ignore("The isolated network fixture is Editor-only.");
#endif
        Assert.That(Call(server, "StartServer"), Is.True); yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (server != null)
        {
            // Despawn before shutting down so network lists dispose through the normal NGO lifecycle.
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
        SetField(pokemon, "baseStats", stats);
        Call(network, "Spawn", true);
        object hp = pokemon.GetType().GetField("currentHp", Private).GetValue(pokemon); hp.GetType().GetProperty("Value").SetValue(hp, 5000);
        return pokemon;
    }
    private object Burn(Component source, float duration = 0.15f)
    {
        object damage = Activator.CreateInstance(Type("DamageInfo"), (ulong)Get(source, "NetworkObjectId"), 0.2f, (short)0, (short)0,
            Enum.Parse(Type("DamageType"), "Physical"), Enum.Parse(Type("DamageProprieties"), "None"));
        return Type("StatusEffect").GetMethod("DamageOverTime").Invoke(null, new object[] { Enum.Parse(Type("StatusType"), "Burned"), damage, duration, 0.05f, (ushort)0, 0f, (short)0 });
    }
    [UnityTest]
    public IEnumerator BurnsTickWithoutAVictimControllerAndCreditTheSourceIncludingTheFinalTick()
    {
        Component source = Pokemon("Source"), victim = Pokemon("Wild victim");
        victim.GetType().GetProperty("Type").SetValue(victim, Enum.Parse(Type("PokemonType"), "Wild"));
        Call(victim, "AddStatusEffect", Burn(source));
        Assert.That(Get(victim, "CurrentHp"), Is.EqualTo(5000));
        yield return new WaitForSeconds(0.25f);
        Assert.That(Get(victim, "CurrentHp"), Is.EqualTo(4940), "Three 20-damage ticks, including the expiry boundary.");
        object lastHit = victim.GetType().GetField("lastHit", Private).GetValue(victim);
        Assert.That(lastHit.GetType().GetField("attackerId").GetValue(lastHit), Is.EqualTo(Get(source, "NetworkObjectId")));
        Assert.That(Call(victim, "HasDamageOverTime", Enum.Parse(Type("StatusType"), "Burned"), Get(source, "NetworkObjectId")), Is.False);
    }
    [UnityTest]
    public IEnumerator IndependentSourcesStackAndCleansingOneDoesNotStopTheOther()
    {
        Component first = Pokemon("Source one"), second = Pokemon("Source two", 200), victim = Pokemon("Objective victim");
        victim.GetType().GetProperty("Type").SetValue(victim, Enum.Parse(Type("PokemonType"), "Objective"));
        Call(victim, "AddStatusEffect", Burn(first, 0.5f)); Call(victim, "AddStatusEffect", Burn(second, 0.5f));
        object burned = Enum.Parse(Type("StatusType"), "Burned");
        Assert.That(Call(victim, "HasDamageOverTime", burned, Get(first, "NetworkObjectId")), Is.True);
        Assert.That(Call(victim, "HasDamageOverTime", burned, Get(second, "NetworkObjectId")), Is.True);
        Call(victim, "RemoveDamageOverTime", burned, Get(first, "NetworkObjectId"));
        yield return new WaitForSeconds(0.12f);
        Assert.That((int)Get(victim, "CurrentHp"), Is.LessThan(5000));
        object lastHit = victim.GetType().GetField("lastHit", Private).GetValue(victim);
        Assert.That(lastHit.GetType().GetField("attackerId").GetValue(lastHit), Is.EqualTo(Get(second, "NetworkObjectId")));
        Call(victim, "ClearStatusEffectsRPC"); int hp = (int)Get(victim, "CurrentHp");
        yield return new WaitForSeconds(0.15f); Assert.That(Get(victim, "CurrentHp"), Is.EqualTo(hp));
    }
    [UnityTest]
    public IEnumerator RefreshDoesNotDuplicateDamageAndSourceDespawnStopsIt()
    {
        Component source = Pokemon("Source"), victim = Pokemon("Victim"); object burn = Burn(source, 0.5f);
        Call(victim, "AddStatusEffect", burn); Call(victim, "AddStatusEffect", burn);
        object statuses = Get(victim, "StatusEffects"); Assert.That(Get(statuses, "Count"), Is.EqualTo(1));
        Assert.That(Get(victim.GetType().GetField("periodicStatuses", Private).GetValue(victim), "Count"), Is.EqualTo(1));
        yield return new WaitForSeconds(0.07f);
        Assert.That((int)Get(victim, "CurrentHp"), Is.LessThan(5000));
        Call(source.GetComponent(Type("Unity.Netcode.NetworkObject")), "Despawn", true);
        int hp = (int)Get(victim, "CurrentHp"); yield return new WaitForSeconds(0.12f);
        Assert.That(Get(victim, "CurrentHp"), Is.EqualTo(hp)); Assert.That(Get(statuses, "Count"), Is.EqualTo(0));
    }
    [UnityTest]
    public IEnumerator VictimDeathClearsPeriodicDamageBeforeRespawn()
    {
        Component source = Pokemon("Source"), victim = Pokemon("Victim"); Call(victim, "AddStatusEffect", Burn(source, 0.5f));
        object hp = victim.GetType().GetField("currentHp", Private).GetValue(victim);
        hp.GetType().GetProperty("Value").SetValue(hp, 0); yield return null;
        hp.GetType().GetProperty("Value").SetValue(hp, 5000); yield return new WaitForSeconds(0.15f);
        Assert.That(Get(victim, "CurrentHp"), Is.EqualTo(5000));
        Assert.That(Call(victim, "HasDamageOverTime", Enum.Parse(Type("StatusType"), "Burned"), Get(source, "NetworkObjectId")), Is.False);
    }
    [UnityTest]
    public IEnumerator ShorteningPeriodicStatusTimeAlsoShortensItsDamageSchedule()
    {
        Component source = Pokemon("Source"), victim = Pokemon("Victim"); object burn = Burn(source, 0.5f);
        Call(victim, "AddStatusEffect", burn); Call(victim, "UpdateStatusEffectTimeRPC", burn, 0.01f);
        yield return new WaitForSeconds(0.15f);
        Assert.That(Get(victim, "CurrentHp"), Is.EqualTo(5000));
        Assert.That(Get(Get(victim, "StatusEffects"), "Count"), Is.EqualTo(0));
    }
}
