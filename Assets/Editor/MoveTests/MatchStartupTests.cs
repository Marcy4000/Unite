using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class MatchStartupTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<UnityEngine.Object> objects = new List<UnityEngine.Object>();
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Call(object target, string method, params object[] arguments) => target.GetType()
        .GetMethod(method, Private | BindingFlags.Public).Invoke(target, arguments);

    private GameObject Node(string name)
    {
        var root = new GameObject(name);
        root.SetActive(false);
        objects.Add(root);
        return root;
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var root in objects)
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
        objects.Clear();
    }

    [Test]
    public void LockedMoveCooldownDoesNotRequireLoadedPokemonStats()
    {
        var root = Node("Player waiting for addressable stats");
        root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        var pokemon = root.AddComponent(GameType("Pokemon"));
        var controller = root.AddComponent(GameType("MovesController"));
        Set(controller, "pokemon", pokemon);
        Assert.That(pokemon.GetType().GetProperty("BaseStats").GetValue(pokemon), Is.Null);
        var locked = Activator.CreateInstance(GameType("LockedMove"));
        Assert.That(Call(controller, "GetMoveCooldown", locked), Is.EqualTo(0f));
    }

    [Test]
    public void LearnedMoveCooldownStillUsesLoadedPokemonCdr()
    {
        var root = Node("Player with loaded cooldown stats");
        root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        var pokemon = root.AddComponent(GameType("Pokemon"));
        Call(pokemon, "Awake");
        var controller = root.AddComponent(GameType("MovesController"));
        Set(controller, "pokemon", pokemon);
        var stats = ScriptableObject.CreateInstance(GameType("PokemonBase"));
        objects.Add(stats);
        Set(stats, "cdr", Enumerable.Repeat(20, 15).ToArray());
        Set(pokemon, "baseStats", stats);
        var move = Activator.CreateInstance(GameType("ZoroarkSlash"));
        float duration = (float)move.GetType().GetField("Cooldown").GetValue(move);
        int cdr = (int)Call(pokemon, "GetCDR");
        Assert.That(duration, Is.GreaterThan(0));
        Assert.That(cdr, Is.EqualTo(20));
        Assert.That(Call(controller, "GetMoveCooldown", move), Is.EqualTo(duration * (1 - cdr / 100f)).Within(0.001f));
    }

    [Test]
    public void ZoroarkCleanupDoesNotSendRpcsAfterMovesControllerDespawn()
    {
        var root = Node("Partially despawned Zoroark");
        root.AddComponent(GameType("Unity.Netcode.NetworkObject"));
        var controller = root.AddComponent(GameType("MovesController"));
        var manager = root.AddComponent(GameType("PlayerManager"));
        Set(manager, "movesController", controller);
        var networkBehaviour = GameType("Unity.Netcode.NetworkBehaviour");
        networkBehaviour.GetProperty("IsServer").SetValue(manager, true);
        networkBehaviour.GetProperty("IsSpawned").SetValue(manager, true);
        try
        {
            var attack = Activator.CreateInstance(GameType("ZoroarkBasicAtk"));
            Call(attack, "InitializeServer", manager);
            Assert.DoesNotThrow(() => Call(attack, "ResetAttack"));
            Assert.DoesNotThrow(() => Call(attack, "Dispose"));
        }
        finally
        {
            networkBehaviour.GetProperty("IsSpawned").SetValue(manager, false);
            networkBehaviour.GetProperty("IsServer").SetValue(manager, false);
        }
    }

    [Test]
    public void MainMenuDoesNotAnimateAnUninitializedTrainer()
    {
        var root = Node("Main menu waiting for trainer clothes");
        var trainer = root.AddComponent(GameType("TrainerModel"));
        var menu = root.AddComponent(GameType("MainMenuUI"));
        Set(menu, "trainerModel", trainer);
        Assert.DoesNotThrow(() => Call(menu, "PlayTrainerModelAnimation"));
    }

    [Test]
    public void PreloadedTrainerInheritsItsPersistentParent()
    {
        var root = Node("Persistent trainer preloader");
        var preloader = root.AddComponent(GameType("PlayerClothesPreloader"));
        var prefab = Node("Inactive trainer source");
        prefab.AddComponent(GameType("TrainerModel"));
        Set(preloader, "trainerModelPrefab", prefab);
        var player = Activator.CreateInstance(GameType("Unite.Multiplayer.GameSessionPlayer"), "local-player", false);
        var clothes = Activator.CreateInstance(GameType("PlayerClothesInfo"));
        var data = (IDictionary)player.GetType().GetProperty("Data").GetValue(player);
        data["ClothingInfo"] = Activator.CreateInstance(GameType("Unity.Services.Multiplayer.PlayerProperty"),
            Call(clothes, "Serialize"), Enum.Parse(GameType("Unity.Services.Multiplayer.VisibilityPropertyOptions"), "Public"));
        Call(preloader, "LoadPlayerModel", player);
        Assert.That(root.transform.childCount, Is.EqualTo(1));
        Assert.That(root.transform.GetChild(0).name, Is.EqualTo("local-player"));
    }
}
