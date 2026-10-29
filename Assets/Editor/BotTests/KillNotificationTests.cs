using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class KillNotificationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject root;
    private Component ui;
    private Component attacker;
    private Component victim;
    private Component background;
    private Component leftPreview;
    private Component rightPreview;
    private Component streakImage;
    private Component notificationText;
    private GameObject notificationHolder;
    private Sprite[] sprites;

    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Value(string type, string name) => Enum.Parse(GameType(type), name);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    private static object Get(object target, string property) => target.GetType().GetProperty(property).GetValue(target);
    private static object Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, Private | BindingFlags.Public).Invoke(target, args);

    private Component Actor(string name, string type, string team, bool player)
    {
        var node = new GameObject(name);
        node.transform.SetParent(root.transform);
        var pokemon = node.AddComponent(GameType("Pokemon"));
        pokemon.GetType().GetProperty("Type").SetValue(pokemon, Value("PokemonType", type));
        var teamVariable = pokemon.GetType().GetField("team", Private).GetValue(pokemon);
        teamVariable.GetType().GetProperty("Value").SetValue(teamVariable, Activator.CreateInstance(GameType("TeamMember"), Value("Team", team)));
        if (player) node.AddComponent(GameType("PlayerManager"));
        return pokemon;
    }

    private Component Image(string field)
    {
        var node = new GameObject(field, typeof(RectTransform));
        node.transform.SetParent(root.transform);
        var image = node.AddComponent(GameType("UnityEngine.UI.Image"));
        Set(ui, field, image);
        return image;
    }

    private object Snapshot(int streak, string message = null) => Activator.CreateInstance(GameType("KillInfo"),
        attacker, victim, streak, message, Value("Team", "Blue"));

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("KO notification regression");
        root.SetActive(false);
        ui = root.AddComponent(GameType("KillNotificationUI"));
        background = Image("background");
        leftPreview = Image("leftPreview");
        rightPreview = Image("rightPreview");
        streakImage = Image("streakImage");
        notificationHolder = new GameObject("Objective message");
        notificationHolder.transform.SetParent(root.transform);
        Set(ui, "notificationHolder", notificationHolder);
        var textNode = new GameObject("Message", typeof(RectTransform));
        textNode.transform.SetParent(root.transform);
        notificationText = textNode.AddComponent(GameType("TMPro.TextMeshProUGUI"));
        Set(ui, "notificationText", notificationText);
        sprites = Enumerable.Range(0, 16).Select(index => Sprite.Create(Texture2D.whiteTexture,
            new Rect(0, 0, 1, 1), Vector2.zero)).ToArray();
        Set(ui, "backgrounds", sprites.Take(6).ToArray());
        Set(ui, "blueKoSprites", sprites.Skip(6).Take(4).ToArray());
        Set(ui, "orangeKoSprites", sprites.Skip(10).Take(4).ToArray());
        attacker = Actor("Attacker", "Player", "Blue", true);
        victim = Actor("Victim", "Player", "Orange", true);
        Set(attacker, "portrait", sprites[14]);
        Set(victim, "portrait", sprites[15]);
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(root);
        foreach (var sprite in sprites) UnityEngine.Object.DestroyImmediate(sprite);
    }

    [TestCase("Blue", "Ally", "Opposing", 3)]
    [TestCase("Orange", "Opposing", "Ally", 2)]
    public void ObjectiveStillRendersAfterBothActorsAreDestroyed(string team, string friendly, string enemy, int backgroundIndex)
    {
        UnityEngine.Object.DestroyImmediate(victim.gameObject);
        victim = Actor("Objective", "Objective", "Neutral", false);
        Set(victim, "portrait", sprites[15]);
        var teamVariable = attacker.GetType().GetField("team", Private).GetValue(attacker);
        teamVariable.GetType().GetProperty("Value").SetValue(teamVariable, Activator.CreateInstance(GameType("TeamMember"), Value("Team", team)));
        object info = Snapshot(3, "{teamName}: {teamFriendlyName} secured, {!teamFriendlyName} lost");
        Call(ui, "EnqueueKill", info);
        UnityEngine.Object.DestroyImmediate(attacker.gameObject);
        UnityEngine.Object.DestroyImmediate(victim.gameObject);
        object queue = ui.GetType().GetField("killQueue", Private).GetValue(ui);
        object queued = Call(queue, "Dequeue");
        Assert.DoesNotThrow(() => Call(ui, "ApplyPresentation", queued));
        Assert.That(Get(background, "sprite"), Is.SameAs(sprites[backgroundIndex]));
        Assert.That(Get(leftPreview, "sprite"), Is.SameAs(sprites[14]));
        Assert.That(Get(rightPreview, "sprite"), Is.SameAs(sprites[15]));
        Assert.That(notificationHolder.activeSelf, Is.True);
        Assert.That(Get(notificationText, "text"), Is.EqualTo($"{team}: {friendly} secured, {enemy} lost"));
        Assert.That(streakImage.gameObject.activeSelf, Is.False);
        Assert.That(Call(ui, "ChooseSound", queued).ToString(), Is.EqualTo("Play_UI_Boss_Defeat"));
    }

    [Test]
    public void RapidKillsKeepTheirOwnStreaksAfterReplicationCatchesUpAndTimerExpires()
    {
        var liveStreak = attacker.GetType().GetField("killStreak", Private).GetValue(attacker);
        liveStreak.GetType().GetProperty("Value").SetValue(liveStreak, 0);
        foreach (int streak in new[] { 1, 2, 3 }) Call(ui, "EnqueueKill", Snapshot(streak));
        liveStreak.GetType().GetProperty("Value").SetValue(liveStreak, 5);
        Set(attacker, "portrait", sprites[0]);
        liveStreak.GetType().GetProperty("Value").SetValue(liveStreak, 0);
        UnityEngine.Object.DestroyImmediate(attacker.gameObject);
        UnityEngine.Object.DestroyImmediate(victim.gameObject);
        object queue = ui.GetType().GetField("killQueue", Private).GetValue(ui);
        string[] sounds = { "Play_UI_SingleKillr", "Play_UI_DoubleKillr", "Play_UI_TripleKillr" };
        for (int index = 0; index < 3; index++)
        {
            object queued = Call(queue, "Dequeue");
            Call(ui, "ApplyPresentation", queued);
            Assert.That(Get(queued, "Streak"), Is.EqualTo(index + 1));
            Assert.That(streakImage.gameObject.activeSelf, Is.EqualTo(index > 0));
            if (index > 0) Assert.That(Get(streakImage, "sprite"), Is.SameAs(sprites[index + 5]));
            Assert.That(Get(leftPreview, "sprite"), Is.SameAs(sprites[14]));
            Assert.That(Call(ui, "ChooseSound", queued).ToString(), Is.EqualTo(sounds[index]));
        }
        Assert.That(Get(queue, "Count"), Is.Zero);
    }

    [TestCase(2, "Blue", 6, "Play_UI_DoubleKillr")]
    [TestCase(3, "Blue", 7, "Play_UI_TripleKillr")]
    [TestCase(4, "Blue", 8, "Play_UI_QuadraKillr")]
    [TestCase(5, "Blue", 9, "Play_UI_PentaKillr")]
    [TestCase(7, "Blue", 9, "Play_UI_PentaKillr")]
    [TestCase(2, "Orange", 10, null)]
    [TestCase(3, "Orange", 11, null)]
    public void StreakArtworkAndAudioUseEventCountAndTeam(int streak, string team, int spriteIndex, string sound)
    {
        var teamVariable = attacker.GetType().GetField("team", Private).GetValue(attacker);
        teamVariable.GetType().GetProperty("Value").SetValue(teamVariable, Activator.CreateInstance(GameType("TeamMember"), Value("Team", team)));
        object info = Snapshot(streak);
        Call(ui, "ApplyPresentation", info);
        Assert.That(Get(streakImage, "sprite"), Is.SameAs(sprites[spriteIndex]));
        Assert.That(Call(ui, "ChooseSound", info)?.ToString(), Is.EqualTo(sound));
        Assert.That(notificationHolder.activeSelf, Is.False);
    }

    [Test]
    public void WildAttackerUsesVictimTeamWithoutPlayerStreak()
    {
        UnityEngine.Object.DestroyImmediate(attacker.gameObject);
        attacker = Actor("Wild", "Wild", "Neutral", false);
        object info = Snapshot(4);
        Call(ui, "ApplyPresentation", info);
        Assert.That(Get(background, "sprite"), Is.SameAs(sprites[4]));
        Assert.That(streakImage.gameObject.activeSelf, Is.False);
        Assert.That(Call(ui, "ChooseSound", info).ToString(), Is.EqualTo("Play_UI_CheckPoint_CommonEvent"));
    }

    [Test]
    public void NullQueueEntriesAreIgnored()
    {
        Call(ui, "EnqueueKill", new object[] { null });
        object queue = ui.GetType().GetField("killQueue", Private).GetValue(ui);
        Assert.That(Get(queue, "Count"), Is.Zero);
    }
}
