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
using Object = UnityEngine.Object;

public class FluxZoneLifecycleTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new();
    private Component host;
    private GameObject prefab;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Call(object target, string method, params object[] arguments) => target.GetType()
        .GetMethod(method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null, arguments.Select(argument => argument.GetType()).ToArray(), null).Invoke(target, arguments);
    private GameObject Node(string name)
    {
        var node = new GameObject(name);
        objects.Add(node);
        return node;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (GameType("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("An external network session is active.");
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/FlareonTests/Fixtures/PeriodicPokemon.prefab");
        Assert.That(prefab, Is.Not.Null);
#else
        Assert.Ignore("Requires the Editor-authored Pokemon fixture.");
#endif
        var root = Node("Flux lifecycle host");
        host = root.AddComponent(GameType("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        Call(transport, "SetConnectionData", "127.0.0.1", (ushort)0, "127.0.0.1");
        object config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
        host.GetType().GetField("NetworkConfig").SetValue(host, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        Call(host, "AddNetworkPrefab", prefab);
        Assert.That(Call(host, "StartHost"), Is.True);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var node in objects.ToArray())
        {
            if (node == null) continue;
            var network = node.GetComponent(GameType("Unity.Netcode.NetworkObject"));
            if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
        }
        if (host != null) Call(host, "Shutdown", false);
        yield return null;
        foreach (var node in objects) if (node != null) Object.Destroy(node);
        objects.Clear();
        yield return null;
    }

    private Component Pokemon()
    {
        var node = Object.Instantiate(prefab);
        objects.Add(node);
        var pokemon = node.GetComponent(GameType("Pokemon"));
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, Enum.Parse(GameType("PokemonType"), "Player"));
        Call(node.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Spawn", true);
        Call(pokemon, "UpdateTeamRPC", Enum.Parse(GameType("Team"), "Blue"));
        return pokemon;
    }

    private Component Zone()
    {
        var root = Object.Instantiate(prefab);
        objects.Add(root);
        root.name = "Flux zone";
        var network = root.GetComponent(GameType("Unity.Netcode.NetworkObject"));
        var zone = root.AddComponent(GameType("FluxZone"));
        Set(zone, "team", Enum.Parse(GameType("Team"), "Orange"));
        Call(network, "Spawn", true);
        return zone;
    }

    private static int Changes(Component pokemon) => (int)Get(Get(pokemon, "StatChanges"), "Count");

    [UnityTest]
    public IEnumerator DuplicateAndMultipleColliderEntersDoNotLeakPermanentSlow()
    {
        var pokemon = Pokemon();
        var first = pokemon.gameObject.AddComponent<BoxCollider>();
        var second = pokemon.gameObject.AddComponent<SphereCollider>();
        var zone = Zone();
        Call(zone, "OnTriggerEnter", first);
        Call(zone, "OnTriggerEnter", first);
        Call(zone, "OnTriggerEnter", second);
        Assert.That(Changes(pokemon), Is.EqualTo(1));
        Call(zone, "OnTriggerExit", first);
        Assert.That(Changes(pokemon), Is.EqualTo(1));
        Call(zone, "OnTriggerExit", second);
        Assert.That(Changes(pokemon), Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DisableAndDespawnReleaseSlowWithoutTriggerExitOrRemovingAnotherZonesEffect()
    {
        var pokemon = Pokemon();
        var collider = pokemon.gameObject.AddComponent<BoxCollider>();
        var first = Zone();
        var second = Zone();
        Call(first, "OnTriggerEnter", collider);
        Call(second, "OnTriggerEnter", collider);
        Assert.That(Changes(pokemon), Is.EqualTo(2));
        ((Behaviour)first).enabled = false;
        Assert.That(Changes(pokemon), Is.EqualTo(1));
        Call(first, "SetIsActive", false);
        Assert.That(Changes(pokemon), Is.EqualTo(1));
        Call(second.GetComponent(GameType("Unity.Netcode.NetworkObject")), "Despawn", false);
        Assert.That(Changes(pokemon), Is.Zero);
        yield return null;
    }

    [UnityTest]
    public IEnumerator DeactivatingZoneRemovesItsSlowAndAllowsCleanReentry()
    {
        var pokemon = Pokemon();
        var collider = pokemon.gameObject.AddComponent<BoxCollider>();
        var zone = Zone();
        Call(zone, "OnTriggerEnter", collider);
        Call(zone, "SetIsActive", false);
        Assert.That(Changes(pokemon), Is.Zero);
        Call(zone, "OnTriggerEnter", collider);
        Assert.That(Changes(pokemon), Is.Zero);
        Call(zone, "SetIsActive", true);
        Call(zone, "OnTriggerEnter", collider);
        Assert.That(Changes(pokemon), Is.EqualTo(1));
        zone.gameObject.SetActive(false);
        Assert.That(Changes(pokemon), Is.Zero);
        yield return null;
    }
}
