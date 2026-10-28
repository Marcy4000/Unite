using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unite.Multiplayer;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class RematchRespawnTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private ScriptableObject map;
    private Component coordinator;
    private PropertyInfo catalogInstance;
    private PropertyInfo lobbyInstance;
    private object oldCatalog;
    private object oldLobby;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Value(object variable) => variable.GetType().GetProperty("Value").GetValue(variable);
    private static void SetValue(object variable, object value) => variable.GetType().GetProperty("Value").SetValue(variable, value);

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Same-lobby rematch regression");
        root.SetActive(false);
        var catalogType = GameType("CharactersList");
        var lobbyType = GameType("LobbyController");
        catalogInstance = catalogType.GetProperty("Instance");
        lobbyInstance = lobbyType.GetProperty("Instance");
        oldCatalog = catalogInstance.GetValue(null);
        oldLobby = lobbyInstance.GetValue(null);

        map = ScriptableObject.CreateInstance(GameType("MapInfo"));
        map.GetType().GetField("resultScreenScene").SetValue(map, SceneManager.GetActiveScene().name);
        var maps = Array.CreateInstance(map.GetType(), 1);
        maps.SetValue(map, 0);
        var catalog = root.AddComponent(catalogType);
        Set(catalog, "maps", maps);
        catalogInstance.SetValue(null, catalog);

        var lobby = root.AddComponent(lobbyType);
        var player = new GameSessionPlayer("rematch-player");
        var view = new GameSessionView();
        view.Players.Add(player);
        view.Data["SelectedMap"] = new SessionProperty(Convert.ToBase64String(BitConverter.GetBytes((short)0)));
        Set(lobby, "localPlayer", player);
        Set(lobby, "partyLobby", view);
        lobbyInstance.SetValue(null, lobby);

        coordinator = root.AddComponent(GameType("PlayerNetworkManager"));
        coordinator.GetType().GetProperty("IsOwner").SetValue(coordinator, true);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        UnityEngine.Object.DestroyImmediate(map);
        catalogInstance.SetValue(null, oldCatalog);
        lobbyInstance.SetValue(null, oldLobby);
        LogAssert.NoUnexpectedReceived();
    }

    private void LeaveResults() => coordinator.GetType().GetMethod("OnSceneUnloaded", Private)
        .Invoke(coordinator, new object[] { SceneManager.GetActiveScene() });

    private float EarlyGameRespawnTime() => (float)GameType("RespawnSystem").GetMethod("CalculateRespawnTime")
        .Invoke(null, new object[] { 0, Get(coordinator, "killsSinceLastDeath"), Get(coordinator, "pointsSinceLastDeath"), 590f });

    [TestCase(16, 480, 20f)]
    [TestCase(0, 0, 0.5f)]
    public void ReturningToSameLobbyClearsStreaksAndBothDeathTimers(int kills, int points, float timer)
    {
        // Reuse the persistent coordinator for several matches, without a menu visit.
        for (int match = 0; match < 3; match++)
        {
            Set(coordinator, "killsSinceLastDeath", kills);
            Set(coordinator, "pointsSinceLastDeath", points);
            Set(coordinator, "localDeathTimer", timer);
            SetValue(Get(coordinator, "deathTimer"), timer);
            Set(coordinator, "matchStarted", true);
            SetValue(Get(coordinator, "isInResultScreen"), true);
            coordinator.GetType().GetMethod("OnGoalScored", Private).Invoke(coordinator, new object[] { points });

            if (kills > 0) Assert.That(EarlyGameRespawnTime(), Is.GreaterThan(4f));
            LeaveResults();

            Assert.That(Get(coordinator, "killsSinceLastDeath"), Is.EqualTo(0));
            Assert.That(Get(coordinator, "pointsSinceLastDeath"), Is.EqualTo(0));
            Assert.That(Get(coordinator, "localDeathTimer"), Is.EqualTo(5f));
            Assert.That(Value(Get(coordinator, "deathTimer")), Is.EqualTo(5f));
            Assert.That(Get(coordinator, "matchStarted"), Is.False);
            Assert.That(Value(Get(coordinator, "isInResultScreen")), Is.False);
            Assert.That(EarlyGameRespawnTime(), Is.EqualTo(4f), "The first early-game death must have no previous-match penalty.");
            var stats = coordinator.GetType().GetProperty("PlayerStats").GetValue(coordinator);
            Assert.That(stats.GetType().GetField("score").GetValue(stats), Is.EqualTo(0));
            Assert.That(stats.GetType().GetField("playerId").GetValue(stats).ToString(), Is.EqualTo("rematch-player"));
        }
    }

    [TestCase(false, true)]
    [TestCase(true, false)]
    public void OtherClientsAndUnrelatedSceneUnloadsDoNotResetLocalStreaks(bool owner, bool resultsScene)
    {
        coordinator.GetType().GetProperty("IsOwner").SetValue(coordinator, owner);
        if (!resultsScene) map.GetType().GetField("resultScreenScene").SetValue(map, "Another result scene");
        Set(coordinator, "killsSinceLastDeath", 10);
        Set(coordinator, "pointsSinceLastDeath", 300);
        Set(coordinator, "localDeathTimer", 20f);
        SetValue(Get(coordinator, "deathTimer"), 20f);

        LeaveResults();

        Assert.That(Get(coordinator, "killsSinceLastDeath"), Is.EqualTo(10));
        Assert.That(Get(coordinator, "pointsSinceLastDeath"), Is.EqualTo(300));
        Assert.That(Get(coordinator, "localDeathTimer"), Is.EqualTo(20f));
        Assert.That(Value(Get(coordinator, "deathTimer")), Is.EqualTo(20f));
    }
}
