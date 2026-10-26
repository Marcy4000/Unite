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

// Real host/client RPCs and the authored Psyduck model/controller, without lobby,
// UI, or input singletons. Reflection bridges the Assembly-CSharp boundary.
public class PsyduckRacingAnimationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> objects = new List<GameObject>();
    private readonly Dictionary<Component, int> evolutionCount = new Dictionary<Component, int>();
    private Component host, client;
    private GameObject prefab;
    private ScriptableObject stats;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(a => a.GetType(name)).First(t => t != null);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, args.Select(a => a.GetType()).ToArray(), null).Invoke(target, args);
    private static object Get(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    private static GameObject Model(Component pokemon) => (GameObject)Get(pokemon, "ActiveModel");
    private static Animator AnimatorOf(Component pokemon) => Model(pokemon).GetComponentInChildren<Animator>(true);
    private static Component AnimationOf(Component pokemon) => pokemon.GetComponent(GameType("AnimationManager"));

    private Component Manager(string name, ushort port)
    {
        var root = new GameObject(name); objects.Add(root);
        var manager = root.AddComponent(GameType("Unity.Netcode.NetworkManager"));
        var transport = root.AddComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        Call(transport, "SetConnectionData", "127.0.0.1", port, "127.0.0.1");
        object config = Activator.CreateInstance(GameType("Unity.Netcode.NetworkConfig"));
        manager.GetType().GetField("NetworkConfig").SetValue(manager, config);
        config.GetType().GetField("NetworkTransport").SetValue(config, transport);
        config.GetType().GetField("EnableSceneManagement").SetValue(config, false);
        Call(manager, "AddNetworkPrefab", prefab);
        return manager;
    }

    private static IEnumerator Until(Func<bool> condition, string message)
    {
        float deadline = Time.realtimeSinceStartup + 5f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, message);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        if (GameType("Unity.Netcode.NetworkManager").GetProperty("Singleton").GetValue(null) != null)
            Assert.Ignore("An external network session is active.");
#if UNITY_EDITOR
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Editor/PsyduckRacingTests/Fixtures/Racer.prefab");
        stats = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Pokemons/PlayableMons/Psyduck.asset"));
        Assert.That(prefab, Is.Not.Null); Assert.That(stats, Is.Not.Null);
#else
        Assert.Ignore("The authored fixture is Editor-only.");
#endif
        host = Manager("Psyduck animation host", 0);
        Assert.That(Call(host, "StartHost"), Is.True);
        var transport = host.GetComponent(GameType("Unity.Netcode.Transports.UTP.UnityTransport"));
        ushort port = (ushort)Get(Call(transport, "GetLocalEndpoint"), "Port");
        client = Manager("Psyduck animation remote client", port);
        Assert.That(Call(client, "StartClient"), Is.True);
        yield return Until(() => (bool)Get(client, "IsConnectedClient"), "Remote client did not connect.");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (client != null) Call(client, "Shutdown", false);
        yield return null;
        if (host != null)
        {
            foreach (var node in objects.ToArray())
            {
                if (node == null) continue;
                var network = node.GetComponent(GameType("Unity.Netcode.NetworkObject"));
                if (network != null && (bool)Get(network, "IsSpawned")) Call(network, "Despawn", true);
            }
            Call(host, "Shutdown", false);
        }
        yield return null;
        if (client != null) UnityEngine.Object.Destroy(client.gameObject);
        yield return null;
        foreach (var node in objects) if (node != null) UnityEngine.Object.Destroy(node);
        if (stats != null) UnityEngine.Object.Destroy(stats);
        objects.Clear(); evolutionCount.Clear();
        yield return null;
    }

    private Component Spawn(bool clientOwned)
    {
        var node = UnityEngine.Object.Instantiate(prefab); objects.Add(node);
        var pokemon = node.GetComponent(GameType("Pokemon"));
        var network = node.GetComponent(GameType("Unity.Netcode.NetworkObject"));
        Call(network, "SpawnWithOwnership", clientOwned ? (ulong)Get(client, "LocalClientId") : 0UL, true);
        BindEvolution(pokemon);
        return pokemon;
    }

    private Component Remote(Component pokemon)
    {
        object spawnManager = Get(client, "SpawnManager");
        var spawned = (IDictionary)spawnManager.GetType().GetField("SpawnedObjects").GetValue(spawnManager);
        object id = Get(pokemon, "NetworkObjectId");
        return spawned.Contains(id) ? ((Component)spawned[id]).GetComponent(GameType("Pokemon")) : null;
    }

    private void BindEvolution(Component pokemon)
    {
        evolutionCount[pokemon] = 0;
        // Same local Animator binding performed by PlayerManager.HandleEvolution.
        Action bind = () =>
        {
            evolutionCount[pokemon]++;
            Call(AnimationOf(pokemon), "AssignAnimator", AnimatorOf(pokemon));
        };
        pokemon.GetType().GetEvent("OnEvolution").AddEventHandler(pokemon, bind);
    }

    private static void AssertRacingIdle(Component pokemon)
    {
        var animator = AnimatorOf(pokemon); animator.Update(0f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.ani_spell1bidle_bat_0054"), Is.True,
            "Every peer must enter the racing branch when its Animator is assigned, without an owner passive or timer.");
    }

    private IEnumerator DelayedPeerInitialization(bool clientOwned)
    {
        var serverPokemon = Spawn(clientOwned);
        yield return Until(() => Remote(serverPokemon) != null, "Remote racer did not spawn.");
        var remote = Remote(serverPokemon); BindEvolution(remote);
        var first = clientOwned ? remote : serverPokemon;
        var second = clientOwned ? serverPokemon : remote;
        Assert.That(Get(first, "IsOwner"), Is.True);
        Call(first, "SetNewPokemon", stats);
        AssertRacingIdle(first);
        GameObject initialModel = Model(first);

        // Longer than the old 150ms coroutine. The other peer announces the same
        // evolution after this racer has already entered its animation branch.
        yield return new WaitForSeconds(0.25f);
        Call(second, "SetNewPokemon", stats);
        yield return Until(() => Model(second) != null, "Delayed peer did not load the model.");
        yield return new WaitForSeconds(0.3f);
        Assert.That(Model(first), Is.SameAs(initialModel), "Delayed duplicate evolution must preserve the existing model.");
        Assert.That(evolutionCount[first], Is.EqualTo(1));
        Assert.That(evolutionCount[second], Is.EqualTo(1));
        AssertRacingIdle(first); AssertRacingIdle(second);

        Call(AnimationOf(first), "SetBool", "Walking", true);
        Call(AnimationOf(first), "SetFloat", "TurnDirection", 0.6f);
        yield return Until(() => AnimatorOf(second).GetBool("Walking") && Mathf.Approximately(AnimatorOf(second).GetFloat("TurnDirection"), 0.6f),
            "Racing movement parameters did not replicate.");
        foreach (var pokemon in new[] { first, second })
        {
            var animator = AnimatorOf(pokemon); animator.Update(0.3f); animator.Update(0.3f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.SurfBlendTree"), Is.True);
            // ChangeSelectedPokemonRpc also reassigns the same Animator after
            // SetNewPokemon returns; this must not restart racing idle.
            Call(AnimationOf(pokemon), "AssignAnimator", animator); animator.Update(0f);
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.SurfBlendTree"), Is.True);
        }
        Call(AnimationOf(first), "SetBool", "Walking", false);
        yield return Until(() => !AnimatorOf(second).GetBool("Walking"), "Stopping did not replicate.");
        foreach (var pokemon in new[] { first, second })
        {
            AnimatorOf(pokemon).Update(0.3f); AnimatorOf(pokemon).Update(0.3f); AssertRacingIdle(pokemon);
            Model(pokemon).SetActive(false); Model(pokemon).SetActive(true); AssertRacingIdle(pokemon);
        }
    }

    [UnityTest]
    public IEnumerator DelayedHostInitializationPreservesClientOwnedRacingAnimations() => DelayedPeerInitialization(true);

    [UnityTest]
    public IEnumerator DelayedClientInitializationPreservesHostOwnedRacingAnimations() => DelayedPeerInitialization(false);

    [UnityTest]
    public IEnumerator RecreatedObserverModelReentersRacingWithoutAnOwningPassive()
    {
        var pokemon = Spawn(false);
        yield return Until(() => Remote(pokemon) != null, "Remote racer did not spawn.");
        var remote = Remote(pokemon); BindEvolution(remote);
        Call(pokemon, "SetNewPokemon", stats); Call(remote, "SetNewPokemon", stats);
        yield return new WaitForSeconds(0.3f);
        var oldModel = Model(remote);
        UnityEngine.Object.Destroy(oldModel); yield return null;
        Call(pokemon, "EvolveRPC", 0);
        yield return Until(() => Model(remote) != null, "Observer model was not recreated.");
        Assert.That(Model(remote), Is.Not.SameAs(oldModel));
        Assert.That(Get(remote, "IsOwner"), Is.False);
        Assert.That(remote.GetComponent(GameType("PassiveController")), Is.Null);
        AssertRacingIdle(remote);
        Assert.That(evolutionCount[pokemon], Is.EqualTo(1), "The intact host model should not be recreated.");
    }

    [UnityTest]
    public IEnumerator DifferentEvolutionStillReplacesTheModel()
    {
        Array original = (Array)Get(stats, "Evolutions");
        object next = Activator.CreateInstance(GameType("PokemonEvolution"));
        next.GetType().GetField("level").SetValue(next, 1);
        next.GetType().GetField("newModel").SetValue(next, original.GetValue(0).GetType().GetField("newModel").GetValue(original.GetValue(0)));
        Array evolutions = Array.CreateInstance(GameType("PokemonEvolution"), 2);
        evolutions.SetValue(original.GetValue(0), 0); evolutions.SetValue(next, 1); Set(stats, "evolutions", evolutions);
        var pokemon = Spawn(false);
        yield return Until(() => Remote(pokemon) != null, "Remote racer did not spawn.");
        var remote = Remote(pokemon); BindEvolution(remote);
        Call(pokemon, "SetNewPokemon", stats); Call(remote, "SetNewPokemon", stats);
        yield return new WaitForSeconds(0.3f);
        var hostModel = Model(pokemon); var remoteModel = Model(remote);
        Call(pokemon, "EvolveRPC", 1);
        yield return Until(() => evolutionCount[remote] == 2, "New evolution did not replicate.");
        Assert.That(Model(pokemon), Is.Not.SameAs(hostModel)); Assert.That(Model(remote), Is.Not.SameAs(remoteModel));
        Assert.That(evolutionCount[pokemon], Is.EqualTo(2));
        AssertRacingIdle(pokemon); AssertRacingIdle(remote);
    }
}
