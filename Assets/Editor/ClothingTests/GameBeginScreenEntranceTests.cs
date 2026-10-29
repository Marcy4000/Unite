using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public class GameBeginScreenEntranceTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component screen;
    private RectTransform blueTeam;
    private RectTransform orangeTeam;
    private RectTransform[] players;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private void Set(string field, object value) => screen.GetType().GetField(field, Private).SetValue(screen, value);
    private object Call(string method, params object[] args) => screen.GetType()
        .GetMethod(method, Private | BindingFlags.Public).Invoke(screen, args);

    private Sprite[] PlaceholderSprites(string state, string gender) => Enumerable.Range(1, 5)
        .Select(index => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Sprites/Lobby/PartyScreen/t_matchroom_{state}_{gender}{index}.png")).ToArray();

    private GameObject Child(string name, Transform parent)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.transform.SetParent(parent, false);
        return child;
    }

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Match entrance regression", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root.SetActive(false);
        screen = root.AddComponent(GameType("GameBeginScreenUI"));
        blueTeam = (RectTransform)Child("Blue team", root.transform).transform;
        orangeTeam = (RectTransform)Child("Orange team", root.transform).transform;
        var blueHolder = Child("Blue players", blueTeam);
        var orangeHolder = Child("Orange players", orangeTeam);
        var bluePlayers = Enumerable.Range(0, 5).Select(index =>
            (RectTransform)Child("Blue player " + index, blueHolder.transform).transform).ToArray();
        var orangePlayers = Enumerable.Range(0, 5).Select(index =>
            (RectTransform)Child("Orange player " + index, orangeHolder.transform).transform).ToArray();
        players = bluePlayers.Concat(orangePlayers).ToArray();
        foreach (var player in players)
        {
            player.anchoredPosition = new Vector2(12.5f, 7.25f);
            var preview = Child("Real preview", player);
            preview.AddComponent(GameType("UI.ThreeDimensional.UIObject3D"));
        }
        Set("blueTeam", blueTeam);
        Set("orangeTeam", orangeTeam);
        Set("bluePlayerHolder", blueHolder);
        Set("orangePlayerHolder", orangeHolder);
        Set("lineObject", Child("Line", root.transform));
        Set("blueTeamPlayers", bluePlayers);
        Set("orangeTeamPlayers", orangePlayers);
        Set("purpleMalePlaceholders", PlaceholderSprites("off", "male"));
        Set("purpleFemalePlaceholders", PlaceholderSprites("off", "female"));
        Set("orangeMalePlaceholders", PlaceholderSprites("on", "male"));
        Set("orangeFemalePlaceholders", PlaceholderSprites("on", "female"));
        Call("CachePresentation");
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
    }

    [Test]
    public void EntranceStartsPanelsWithoutWaitingForAnyTrainer()
    {
        var entrance = (IEnumerator)Call("FadeInRoutine");
        Assert.That(entrance.MoveNext(), Is.True);
        var panels = entrance.Current as IEnumerator;
        Assert.That(panels, Is.Not.Null);
        Assert.That(panels.MoveNext(), Is.True);
        float width = root.GetComponent<CanvasScaler>().referenceResolution.x;
        Assert.That(blueTeam.anchoredPosition.x, Is.EqualTo(-width));
        Assert.That(orangeTeam.anchoredPosition.x, Is.EqualTo(width));
        Assert.That(screen.GetType().GetProperty("IsClosed").GetValue(screen), Is.False);
        foreach (var player in players)
            Assert.That(player.Find("Trainer placeholder").gameObject.activeSelf, Is.True);
    }

    [Test]
    public void CacheIsIdempotentAndCancellationRestoresFractionalPositions()
    {
        Call("CachePresentation");
        foreach (var player in players) player.anchoredPosition = Vector2.one * -2000f;
        Call("CancelPresentation");
        foreach (var player in players)
        {
            Assert.That(player.anchoredPosition, Is.EqualTo(new Vector2(12.5f, 7.25f)));
            Assert.That(player.Cast<Transform>().Count(child => child.name == "Trainer placeholder"), Is.EqualTo(1));
        }
    }

    [TestCase(true, true, "off", "male")]
    [TestCase(true, false, "off", "female")]
    [TestCase(false, true, "on", "male")]
    [TestCase(false, false, "on", "female")]
    public void PlaceholderMatchesTeamGenderAndAllFivePositions(bool blue, bool male, string state, string gender)
    {
        var slots = (IEnumerable)screen.GetType().GetField("slots", Private).GetValue(screen);
        var expected = PlaceholderSprites(state, gender);
        foreach (object slot in slots)
        {
            var slotType = slot.GetType();
            if ((bool)slotType.GetField("Blue").GetValue(slot) != blue) continue;
            slotType.GetField("IsMale").SetValue(slot, male);
            Call("UpdatePlaceholderSprite", slot);
            int index = (int)slotType.GetField("Index").GetValue(slot);
            var image = (Image)slotType.GetField("Placeholder").GetValue(slot);
            Assert.That(expected[index], Is.Not.Null);
            Assert.That(image.sprite, Is.SameAs(expected[index]));
            Assert.That(image.enabled, Is.True);
            Assert.That(image.color, Is.EqualTo(Color.white));
            Assert.That(image.preserveAspect, Is.True);
            Assert.That(image.transform.childCount, Is.Zero);
        }
    }

    [Test]
    public void TargetCreationAloneDoesNotMeanFirstRenderCompleted()
    {
        var preview = players[0].GetComponentInChildren(GameType("UI.ThreeDimensional.UIObject3D"), true);
        var previewType = preview.GetType();
        previewType.GetField("started", Private).SetValue(preview, true);
        previewType.GetField("displayReady", Private).SetValue(preview, true);
        previewType.GetField("_target", Private).SetValue(preview, blueTeam);
        Assert.That(previewType.GetProperty("IsReady").GetValue(preview), Is.True);
        Assert.That(previewType.GetProperty("HasRendered").GetValue(preview), Is.False);
        previewType.GetField("_target", Private).SetValue(preview, null);
    }

    [Test]
    public void HideImmediatelyResetsPublicStateAndPlaceholders()
    {
        Call("HideImmediately");
        Assert.That(root.activeSelf, Is.False);
        Assert.That(screen.GetType().GetProperty("IsClosed").GetValue(screen), Is.False);
        Assert.That(screen.GetType().GetProperty("IsOpen").GetValue(screen), Is.True);
        Assert.That(screen.GetType().GetField("fadeInRoutine", Private).GetValue(screen), Is.Null);
        Assert.That(screen.GetType().GetField("fadeOutRoutine", Private).GetValue(screen), Is.Null);
    }
}
