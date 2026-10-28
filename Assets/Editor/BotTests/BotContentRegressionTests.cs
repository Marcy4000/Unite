using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BotContentRegressionTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);

    [Test]
    public void CatalogContainsLegalVariedBuildsForImplementedPokemonOnly()
    {
        var roster = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Resources/Bots/Roster.asset");
        Assert.That(roster, Is.Not.Null);
        var builds = (Array)roster.GetType().GetField("loadouts").GetValue(roster);
        Assert.That(builds.Length, Is.GreaterThanOrEqualTo(26));
        var names = builds.Cast<object>().Select(b => ((UnityEngine.Object)b.GetType().GetField("pokemon").GetValue(b)).name).Distinct().ToArray();
        Assert.That(names.Length, Is.EqualTo(13));
        Assert.That(names, Does.Not.Contain("Lapras")); Assert.That(names, Does.Not.Contain("Sableye"));
        foreach (var build in builds)
            Assert.That(build.GetType().GetProperty("IsValid").GetValue(build), Is.True, ((UnityEngine.Object)build).name);
        Assert.That(builds.Cast<object>().Select(b => b.GetType().GetField("battleItem").GetValue(b)).Distinct().Count(), Is.GreaterThan(1));
    }

    [TestCase("RemoatStadium")]
    [TestCase("TheiaSkyRuins")]
    public void EveryCampAndOpeningLaneIsExplicitlyBound(string name)
    {
        string path = "Assets/Scenes/" + name + ".unity";
        var scene = SceneManager.GetSceneByPath(path); bool loaded = scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var context = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(GameType("BotMapContext"), true)).Single();
            var camps = ((IEnumerable)context.GetType().GetField("camps").GetValue(context)).Cast<object>().ToArray();
            var spawners = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(GameType("WildPokemonSpawner"), true)).ToArray();
            Assert.That(camps.Select(c => c.GetType().GetField("spawner").GetValue(c)), Is.EquivalentTo(spawners));
            Assert.That(camps.Count(c => c.GetType().GetField("kind").GetValue(c).ToString() == "FinalObjective"), Is.EqualTo(1));
            foreach (string team in new[] { "Blue", "Orange" }) foreach (string lane in new[] { "top", "bottom" })
                Assert.That(camps.Count(c => c.GetType().GetField("kind").GetValue(c).ToString() == "Opening" &&
                    c.GetType().GetField("team").GetValue(c).ToString() == team && (string)c.GetType().GetField("lane").GetValue(c) == lane), Is.EqualTo(4));
            Assert.That(((Array)context.GetType().GetField("goals").GetValue(context)).Length, Is.EqualTo(10));
        }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void ExistingAllyPrefabUsesItsOwnInactiveUniteDot()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/AllyInfo.prefab");
        var ui = prefab.GetComponent(GameType("AllyInfoUI"));
        var dot = new SerializedObject(ui).FindProperty("uniteReadyDot").objectReferenceValue as GameObject;
        Assert.That(dot, Is.Not.Null); Assert.That(dot.name, Is.EqualTo("UniteChargeDot"));
        Assert.That(dot.transform.IsChildOf(prefab.transform), Is.True);
        Assert.That(dot.activeSelf, Is.False);
    }

    [Test]
    public void ReadinessSnapshotIsPublicButDeliveryIsServerOnlyAndTargeted()
    {
        var type = GameType("MovesController");
        Assert.That(type.GetMethod("RequestUniteReadyRpc", Instance).GetCustomAttribute<RpcAttribute>().InvokePermission, Is.EqualTo(RpcInvokePermission.Everyone));
        Assert.That(type.GetMethod("ReceiveUniteReadyRpc", Instance).GetCustomAttribute<RpcAttribute>().InvokePermission, Is.EqualTo(RpcInvokePermission.Server));
        Assert.That(type.GetMethod("ReceiveUniteReadyRpc", Instance).GetParameters().Last().ParameterType, Is.EqualTo(typeof(RpcParams)));
    }

    [Test]
    public void DraftConfirmAndBanRequestsCarryAuthenticatedSenderParameters()
    {
        var type = GameType("DraftSelectController");
        foreach (string method in new[] { "ConfirmSelectionRPC", "SubmitBanRequestRpc" })
        {
            var info = type.GetMethod(method, Instance);
            Assert.That(info.GetCustomAttribute<RpcAttribute>().InvokePermission, Is.EqualTo(RpcInvokePermission.Everyone));
            Assert.That(info.GetParameters().Last().ParameterType, Is.EqualTo(typeof(RpcParams)));
        }
    }

    [Test]
    public void RecoveryDefaultsHaveHysteresisAndLeaveBeforeEightyFivePercent()
    {
        var config = ScriptableObject.CreateInstance(GameType("BotRosterConfig"));
        try
        {
            float low = (float)config.GetType().GetField("recoverBelow").GetValue(config);
            float high = (float)config.GetType().GetField("resumeAbove").GetValue(config);
            Assert.That(high, Is.GreaterThan(low).And.LessThan(.85f));
            Assert.That((float)config.GetType().GetField("recallBelow").GetValue(config), Is.LessThan(low));
            Assert.That((float)config.GetType().GetField("finalObjectiveAttendance").GetValue(config), Is.EqualTo(.97f));
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }

    [Test]
    public void RemoteUniteReadinessUsesReceivedSnapshot()
    {
        var root = new GameObject("readiness regression"); root.SetActive(false);
        try
        {
            var moves = root.AddComponent(GameType("MovesController"));
            // An unspawned/non-owner component exposes the received ally cache.
            var field = moves.GetType().GetField("allyUniteReady", Instance);
            field.SetValue(moves, true);
            Assert.That(moves.GetType().GetProperty("UniteIsReady").GetValue(moves), Is.True);
            field.SetValue(moves, false);
            Assert.That(moves.GetType().GetProperty("UniteIsReady").GetValue(moves), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private class CountingRandom : System.Random
    {
        public int rolls;
        public override double NextDouble() { rolls++; return .5; }
    }

    [Test]
    public void FinalAttendanceRollPersistsUntilTheNextSpawn()
    {
        var root = new GameObject("final-objective decision"); root.SetActive(false);
        try
        {
            var brain = root.AddComponent(GameType("BotBrain"));
            var spawner = root.AddComponent(GameType("WildPokemonSpawner"));
            var campType = GameType("BotMapContext").GetNestedType("Camp");
            var camp = Activator.CreateInstance(campType); campType.GetField("spawner").SetValue(camp, spawner);
            brain.GetType().GetField("finalCamp", Instance).SetValue(brain, camp);
            var random = new CountingRandom(); brain.GetType().GetField("random", Instance).SetValue(brain, random);
            var spawned = (NetworkVariable<bool>)spawner.GetType().GetField("isSpawned", Instance).GetValue(spawner);
            var decide = brain.GetType().GetMethod("UpdateFinalDecision", Instance);
            decide.Invoke(brain, null); Assert.That(random.rolls, Is.Zero);
            spawned.Value = true;
            for (int i = 0; i < 100; i++) decide.Invoke(brain, null);
            Assert.That(random.rolls, Is.EqualTo(1));
            Assert.That(brain.GetType().GetField("attendFinal", Instance).GetValue(brain), Is.True);
            spawned.Value = false; decide.Invoke(brain, null);
            spawned.Value = true; decide.Invoke(brain, null);
            Assert.That(random.rolls, Is.EqualTo(2));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void OwnerReadinessRequiresFullLearnedChargeButNotUnlockedActions()
    {
        var root = new GameObject("owner readiness"); root.SetActive(false);
        try
        {
            var moves = root.AddComponent(GameType("MovesController"));
            typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(moves, true);
            var type = moves.GetType();
            type.GetField("uniteMoveMaxCharge", Instance).SetValue(moves, 100);
            type.GetField("uniteMoveCharge", Instance).SetValue(moves, 99);
            Assert.That(type.GetProperty("UniteIsReady").GetValue(moves), Is.False);
            type.GetField("uniteMoveCharge", Instance).SetValue(moves, 100);
            Assert.That(type.GetProperty("UniteIsReady").GetValue(moves), Is.True);
            type.GetField("uniteMoveMaxCharge", Instance).SetValue(moves, 0);
            Assert.That(type.GetProperty("UniteIsReady").GetValue(moves), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
