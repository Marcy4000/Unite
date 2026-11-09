using System;
using System.Linq;
using NUnit.Framework;
using Unite.Multiplayer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SpecialModeBotTests
{
    private static Type T(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).First(t => t != null);
    [TestCase("ElectrodeVolleyball", true, BotGameMode.Volleyball)]
    [TestCase("PsyduckRacing", true, BotGameMode.Racing)]
    [TestCase("UniteRoyale", true, BotGameMode.Royale)]
    [TestCase("TheiaSkyRuins", true, BotGameMode.Lanes)]
    [TestCase("TheiaSkyRuinsDraft", true, BotGameMode.Lanes)]
    [TestCase("RemoatStadium", true, BotGameMode.Lanes)]
    [TestCase("RemoatStadium500Points", false, BotGameMode.Lanes)]
    [TestCase("MerStadium", false, BotGameMode.Lanes)]
    [TestCase("MerStadiumDraft", false, BotGameMode.Lanes)]
    public void MapAssetsDeclareBotSupportAndBrain(string asset, bool supported, BotGameMode brain)
    {
        var map = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/Maps/" + asset + ".asset");
        Assert.That(map, Is.Not.Null);
        var settings = new SerializedObject(map);
        Assert.That(settings.FindProperty("supportsBots").boolValue, Is.EqualTo(supported));
        Assert.That(settings.FindProperty("botBrain").intValue, Is.EqualTo((int)brain));
    }

    [TestCase(false, true, true, 0, 2, 5, true)]
    [TestCase(true, false, true, 0, 2, 5, false)]
    [TestCase(true, false, true, 8, 2, 5, true)]
    [TestCase(true, true, true, 25, 2, 2, true)]
    [TestCase(true, true, true, 8, 2, 5, false)]
    [TestCase(true, true, false, 25, 2, 0, false)]
    public void BackupCoverageIncludesKnockoutsAndUnreachableIncomingBalls(bool alive, bool movable, bool incoming, float distance, float radius, float time, bool expected) =>
        Assert.That(BotModeRules.NeedsBackup(alive, movable, incoming, distance, radius, time), Is.EqualTo(expected));

    [TestCase("BotVolleyballBrain")]
    [TestCase("BotRacingBrain")]
    [TestCase("BotRoyaleBrain")]
    public void SpecialBrainsDoNotConstructNativeNavigationOrReadHumanInputBeforeStart(string type)
    {
        var root = new GameObject("isolated brain"); root.SetActive(false);
        try { Assert.DoesNotThrow(() => root.AddComponent(T(type))); }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [TestCase("RoundStarting", true, true, true)]
    [TestCase("RoundStarting", false, false, false)]
    [TestCase("Playing", true, false, true)]
    [TestCase("WaitingToStart", true, false, false)]
    [TestCase("BallLanding", true, false, false)]
    [TestCase("RoundEnding", true, false, false)]
    [TestCase("GameOver", true, false, false)]
    public void VolleyballBotsApproachDuringOpeningCountdownButRespectSubsequentRoundLocks(
        string state, bool opening, bool canApproach, bool canAct)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var managerType = T("VolleyballManager");
        var instance = managerType.GetProperty("Instance");
        var previous = instance.GetValue(null);
        var root = new GameObject("volleyball opening fixture"); root.SetActive(false);
        try
        {
            var manager = root.AddComponent(managerType);
            instance.SetValue(null, manager);
            managerType.GetField("openingRound", flags).SetValue(manager, opening);
            var field = managerType.GetField("currentState", flags);
            field.SetValue(manager, Activator.CreateInstance(field.FieldType, new object[] {
                Enum.Parse(T("VolleyballState"), state),
                Enum.Parse(T("Unity.Netcode.NetworkVariableReadPermission"), "Everyone"),
                Enum.Parse(T("Unity.Netcode.NetworkVariableWritePermission"), "Server") }));
            Assert.That(managerType.GetProperty("CanBotsApproach").GetValue(manager), Is.EqualTo(canApproach));
            var brain = root.AddComponent(T("BotVolleyballBrain"));
            Assert.That(T("BotVolleyballBrain").GetProperty("CanAct", flags).GetValue(brain), Is.EqualTo(canAct));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            instance.SetValue(null, previous);
        }
    }

    [Test]
    public void RacingStallRecoverySteersAroundTheLandingObstacleWithoutChangingHeight()
    {
        var method = T("BotRacingBrain").GetMethod("RecoveryTarget", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var position = new Vector3(-13.6f, -.03f, 28.9f);
        var result = (Vector3)method.Invoke(null, new object[] { position, new Vector3(-23, 5.42f, 28.9f) });
        Assert.That(result.z, Is.LessThan(position.z - 5), "Sidestep toward the track interior, away from the rotating table.");
        Assert.That(result.x, Is.LessThan(position.x));
        Assert.That(result.y, Is.EqualTo(position.y));
    }

    [TestCase(0f, 0f, true)]
    [TestCase(0f, 2f, true)]
    [TestCase(1.5f, 2f, true)]
    [TestCase(3f, 2f, false)]
    [TestCase(0f, -2f, false)]
    [TestCase(0f, 10f, false)]
    public void RacingGuidesAcceptNearbyOvershootsButKeepDistantAndUnreachedGuides(float lateral, float along, bool expected)
    {
        var method = T("BotRacingBrain").GetMethod("HasReachedGuide", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Assert.That(method.Invoke(null, new object[] { new Vector3(lateral, 9, along), Vector3.back * 10, Vector3.zero }), Is.EqualTo(expected));
    }

    [Test]
    public void RacingSteeringBrakesBeforeWaypointsAndThroughTightTurns()
    {
        var method = T("BotRacingBrain").GetMethod("SteeringInput", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        Vector2 Input(Vector3 direction) => (Vector2)method.Invoke(null, new object[] { Vector3.forward, direction });
        Assert.That(Input(Vector3.forward * 20).magnitude, Is.EqualTo(1).Within(.001));
        Assert.That(Input(Vector3.forward).magnitude, Is.LessThan(.3f));
        Assert.That(Input(Vector3.right * 20).magnitude, Is.LessThan(.2f));
        Assert.That(Input(Vector3.back * 20).magnitude, Is.LessThan(.2f));
        Assert.That(Input(Vector3.zero), Is.EqualTo(Vector2.zero));
    }

    [TestCase(0, 0, 0, false)]
    [TestCase(3, 2, 3, false)]
    [TestCase(2, 2, 3, true)]
    [TestCase(3, 3, 3, true)]
    public void RaceCompletionUsesRemainingCountersNotHistoricalResultCount(int active, int finished, int historical, bool expected)
    {
        var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var root = new GameObject("race completion fixture"); root.SetActive(false);
        try
        {
            var manager = root.AddComponent(T("RaceManager"));
            var counters = (System.Collections.IDictionary)T("RaceManager").GetField("playerLapCounters", flags).GetValue(manager);
            var results = (System.Collections.IList)T("RaceManager").GetField("racePlayerResults", flags).GetValue(manager);
            for (int i = 0; i < historical; i++) results.Add(Activator.CreateInstance(T("RacePlayerResult")));
            for (int i = 0; i < active; i++)
            {
                var child = new GameObject("counter"); child.transform.SetParent(root.transform);
                var counter = child.AddComponent(T("RaceLapCounter"));
                var field = T("RaceLapCounter").GetField("raceFinished", flags);
                field.SetValue(counter, Activator.CreateInstance(field.FieldType, new object[] { i < finished,
                    Enum.Parse(T("Unity.Netcode.NetworkVariableReadPermission"), "Everyone"),
                    Enum.Parse(T("Unity.Netcode.NetworkVariableWritePermission"), "Server") }));
                counters.Add((ulong)i, counter);
            }
            Assert.That(T("RaceManager").GetMethod("AreRemainingRacersFinished", flags).Invoke(manager, null), Is.EqualTo(expected));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [TestCase("PsyduckRacing", "BotRaceRoute", "segments", 20)]
    [TestCase("UniteRoyale", "BotArenaContext", "objectives", 4)]
    public void SpecialScenesContainExplicitBindings(string name, string type, string field, int count)
    {
        var scene = SceneManager.GetSceneByPath("Assets/Scenes/" + name + ".unity"); bool loaded = scene.isLoaded;
        if (!loaded) scene = EditorSceneManager.OpenScene("Assets/Scenes/" + name + ".unity", OpenSceneMode.Additive);
        try
        {
            var context = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(T(type), true)).Single();
            var values = (Array)context.GetType().GetField(field).GetValue(context);
            Assert.That(values.Length, Is.EqualTo(count));
            if (type == "BotRaceRoute")
            {
                var indices = values.Cast<object>().Select(s => s.GetType().GetField("checkpoint").GetValue(s)).Cast<Component>()
                    .Select(c => (int)c.GetType().GetProperty("CheckpointIndex").GetValue(c));
                Assert.That(indices, Is.EqualTo(Enumerable.Range(0, 20)));
                var segment = values.GetValue(9);
                var guides = (Vector3[])segment.GetType().GetField("guides").GetValue(segment);
                var launch = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren(T("Launchpad"), true)).Single();
                Assert.That(guides.Any(point => Vector3.Distance(point, launch.transform.position) < .1f), Is.True,
                    "The baked NavMesh alone crosses a physical barrier here; the racing route must enter the launchpad trigger.");
            }
        }
        finally { if (!loaded) EditorSceneManager.CloseScene(scene, true); }
    }
}
