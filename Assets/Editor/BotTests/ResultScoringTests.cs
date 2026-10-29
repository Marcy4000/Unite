using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class ResultScoringTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object TeamValue(string name) => Enum.Parse(GameType("Team"), name);
    private static void Set(object target, string name, object value) => target.GetType()
        .GetField(name, Fields | BindingFlags.Public).SetValue(target, value);
    private static object Get(object target, string name) => target.GetType()
        .GetField(name, Fields | BindingFlags.Public).GetValue(target);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, Fields | BindingFlags.Public).Invoke(target, args);

    [SetUp]
    public void Setup()
    {
        root = new GameObject("Result scoring regression fixture");
        root.SetActive(false);
    }

    [TearDown]
    public void Cleanup() => Object.DestroyImmediate(root);

    private static Array Scores(params object[] scores)
    {
        var array = Array.CreateInstance(GameType("ResultScoreInfo"), scores.Length);
        for (int index = 0; index < scores.Length; index++) array.SetValue(scores[index], index);
        return array;
    }

    private static object Score(ushort points, string playerID, float time) => Activator.CreateInstance(
        GameType("ResultScoreInfo"), new object[] { points, playerID, time });

    private static object Results(ushort blue, ushort orange, Array blueEvents, Array orangeEvents)
    {
        var results = Activator.CreateInstance(GameType("GameResults"));
        Set(results, "BlueTeamScore", blue);
        Set(results, "OrangeTeamScore", orange);
        Set(results, "BlueTeamScores", blueEvents);
        Set(results, "OrangeTeamScores", orangeEvents);
        return results;
    }

    [TestCase("Blue")]
    [TestCase("Orange")]
    public void SoldierAndPlayerScoresAnimateOnceAtTheirRecordedTimes(string team)
    {
        var events = Scores(Score(20, string.Empty, 30f), Score(40, "player", 60f));
        var results = Results(60, 60, events, events);
        Assert.That(Call(results, "GetAnimatedScore", TeamValue(team), 29f, false), Is.EqualTo(0));
        Assert.That(Call(results, "GetAnimatedScore", TeamValue(team), 30f, false), Is.EqualTo(20));
        Assert.That(Call(results, "GetAnimatedScore", TeamValue(team), 60f, false), Is.EqualTo(60));
        Assert.That(Call(results, "GetAnimatedScore", TeamValue(team), 60f, false), Is.EqualTo(60));
        Assert.That(Call(results, "GetAnimatedScore", TeamValue(team), 60f, true), Is.EqualTo(60));
        Assert.That(events.Length, Is.EqualTo(2));
    }

    [TestCase("Blue")]
    [TestCase("Orange")]
    public void CompletionUsesAuthoritativeTotalWithMissingOrLateEvents(string team)
    {
        var results = Results(80, 40, Scores(Score(60, "player", 10f)), Scores(Score(40, "", 601f)));
        int expected = team == "Blue" ? 80 : 40;
        Assert.That(Call(results, "GetAnimatedScore", TeamValue(team), 600f, true), Is.EqualTo(expected));
        Set(results, "BlueTeamScores", null);
        Set(results, "OrangeTeamScores", Scores());
        Assert.That(Call(results, "GetAnimatedScore", TeamValue(team), 600f, false), Is.EqualTo(0));
        Assert.That(Call(results, "GetAnimatedScore", TeamValue(team), 600f, true), Is.EqualTo(expected));
    }

    [Test]
    public void ExcessTimelinePointsCannotExceedAuthoritativeTotal()
    {
        var results = Results(20, 0, Scores(Score(20, "", 10f), Score(20, "", 10f)), Scores());
        Assert.That(Call(results, "GetAnimatedScore", TeamValue("Blue"), 10f, false), Is.EqualTo(20));
        Assert.That(Call(results, "GetAnimatedScore", TeamValue("Orange"), 10f, true), Is.EqualTo(0));
    }

    [TestCase("Blue", "")]
    [TestCase("Orange", "")]
    [TestCase("Blue", "player")]
    [TestCase("Orange", "player")]
    public void RecordingAcceptedScorePreservesTeamIdentityAndDoesNotAwardExtraPoints(string team, string playerID)
    {
        var manager = root.AddComponent(GameType("GameManager"));
        var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(GameType("ResultScoreInfo"));
        Set(manager, "blueTeamScores", Activator.CreateInstance(listType));
        Set(manager, "orangeTeamScores", Activator.CreateInstance(listType));
        var clock = Get(manager, "gameTime");
        clock.GetType().GetProperty("Value").SetValue(clock, 123f);
        Call(manager, "RecordResultScore", TeamValue(team), (ushort)20, playerID);
        var recorded = (IList)Get(manager, team == "Blue" ? "blueTeamScores" : "orangeTeamScores");
        var other = (IList)Get(manager, team == "Blue" ? "orangeTeamScores" : "blueTeamScores");
        Assert.That(recorded.Count, Is.EqualTo(1));
        Assert.That(other.Count, Is.Zero);
        Assert.That(Get(recorded[0], "ScoredPoints"), Is.EqualTo(20));
        Assert.That(Get(recorded[0], "PlayerID"), Is.EqualTo(playerID));
        Assert.That(Get(recorded[0], "Time"), Is.EqualTo(123f));
        foreach (string field in new[] { "blueTeamScore", "orangeTeamScore" })
        {
            var total = Get(manager, field);
            Assert.That(total.GetType().GetProperty("Value").GetValue(total), Is.EqualTo(0));
        }
    }

    [Test]
    public void BarsRecoverAfterZeroScoreAndClampToNormalizedTotals()
    {
        var bars = root.AddComponent(GameType("ResultBarsUI"));
        var blue = new GameObject("Blue", typeof(RectTransform));
        var orange = new GameObject("Orange", typeof(RectTransform));
        blue.transform.SetParent(root.transform);
        orange.transform.SetParent(root.transform);
        Set(bars, "blueBar", blue.AddComponent(GameType("UnityEngine.UI.Image")));
        Set(bars, "orangeBar", orange.AddComponent(GameType("UnityEngine.UI.Image")));
        Call(bars, "InitializeUI", 0);
        Assert.That(blue.transform.localScale, Is.EqualTo(new Vector3(1, 0, 1)));
        Call(bars, "InitializeUI", 80);
        Call(bars, "SetBars", 80, 40);
        Assert.That(blue.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(orange.transform.localScale, Is.EqualTo(new Vector3(1, 0.5f, 1)));
        Call(bars, "SetBars", 100, -10);
        Assert.That(blue.transform.localScale.y, Is.EqualTo(1));
        Assert.That(orange.transform.localScale.y, Is.Zero);
    }
}
