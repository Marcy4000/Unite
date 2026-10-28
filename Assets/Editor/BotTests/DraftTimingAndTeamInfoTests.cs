using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unite.Multiplayer;
using Unity.Netcode;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class DraftTimingAndTeamInfoTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly List<GameObject> roots = new();
    private readonly List<Component> bindings = new();
    private object previousLobby;
    private static Type GameType(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);
    private static object Get(object target, string name) => target.GetType().GetField(name, Fields).GetValue(target);
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Fields).SetValue(target, value);
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(target, args);
    private static void Emit(object target, string name) => ((Action)Get(target, name))?.Invoke();

    private GameObject Node(string name)
    {
        var node = new GameObject(name);
        node.SetActive(false);
        roots.Add(node);
        return node;
    }

    [SetUp]
    public void SetUp() => previousLobby = GameType("LobbyController").GetProperty("Instance").GetValue(null);

    [TearDown]
    public void TearDown()
    {
        foreach (var binding in bindings)
            if (binding != null) Call(binding, binding.GetType().Name == "InfoHolderManager" ? "Unbind" : "Unsubscribe");
        for (int index = roots.Count - 1; index >= 0; index--)
            if (roots[index] != null) Object.DestroyImmediate(roots[index]);
        roots.Clear();
        bindings.Clear();
        GameType("LobbyController").GetProperty("Instance").SetValue(null, previousLobby);
    }

    [TestCase("Banning", 20f)]
    [TestCase("Picking", 25f)]
    [TestCase("Preparation", 30f)]
    public void DraftPhaseTimersKeepOriginalDurations(string phase, float seconds)
    {
        var draft = Node("Draft timers").AddComponent(GameType("DraftSelectController"));
        Assert.That(Call(draft, "GetPhaseTimeLimit", Enum.Parse(GameType("DraftPhase"), phase)), Is.EqualTo(seconds));
    }

    [TestCase(false, true, 6f, 9f)]
    [TestCase(false, false, 3f, 5f)]
    [TestCase(true, true, 6f, 8f)]
    [TestCase(true, false, 3f, 5f)]
    public void BotDelaysHaveBoundedVariation(bool banning, bool humansStillChoosing, float minimum, float maximum)
    {
        Assert.That(DraftTurnRules.BotDelaySeconds(banning, humansStillChoosing, -2f), Is.EqualTo(minimum));
        Assert.That(DraftTurnRules.BotDelaySeconds(banning, humansStillChoosing, 2f), Is.EqualTo(maximum));
        Assert.That(DraftTurnRules.BotDelaySeconds(banning, humansStillChoosing, 0f), Is.InRange(minimum, maximum));
    }

    [Test]
    public void BotPickSchedulingConsidersHumansOnBothTeamsAndRestartsEachPick()
    {
        var draft = Node("Bot draft pacing").AddComponent(GameType("DraftSelectController"));
        Set(draft, "blueTeamPlayers", new List<GameSessionPlayer> { new GameSessionPlayer("blue-bot", true) });
        Set(draft, "orangeTeamPlayers", new List<GameSessionPlayer> { new GameSessionPlayer("orange-human") });
        Assert.That(Call(draft, "HasUncommittedHumanPicks"), Is.True);
        Call(draft, "ScheduleBotPick");
        Assert.That((float)Get(draft, "botActAt") - Time.unscaledTime, Is.InRange(6f, 9f));
        ((Dictionary<string, short>)Get(draft, "committedDraftPicks")).Add("orange-human", 1);
        Assert.That(Call(draft, "HasUncommittedHumanPicks"), Is.False);
        Set(draft, "botActAt", -100f);
        Call(draft, "ScheduleBotPick");
        Assert.That((float)Get(draft, "botActAt") - Time.unscaledTime, Is.InRange(3f, 5f));
    }

    [Test]
    public void BotFastPathNeverRushesOutstandingHumanChoice()
    {
        Assert.That(DraftTurnRules.CanAutoPick(true, 100f, 2f), Is.False);
        Assert.That(DraftTurnRules.CanAutoPick(false, 1f, 2f), Is.False);
        Assert.That(DraftTurnRules.CanAutoPick(false, 2f, 2f), Is.True);
        Assert.That(DraftTurnRules.BotDelaySeconds(false, true, 1f), Is.EqualTo(9f));
        Assert.That(DraftTurnRules.BotDelaySeconds(false, false, 1f), Is.EqualTo(5f));
        Assert.That(DraftTurnRules.BotDelaySeconds(true, true, -1f), Is.EqualTo(6f));
        Assert.That(DraftTurnRules.BotDelaySeconds(true, true, 1f), Is.EqualTo(8f));
    }

    [Test]
    public void ClientCannotAdvanceAuthoritativeDraftClock()
    {
        var draft = Node("Client draft").AddComponent(GameType("DraftSelectController"));
        var timer = (NetworkVariable<float>)Get(draft, "draftTimer");
        timer.Value = 40f;
        Call(draft, "Update");
        Assert.That(timer.Value, Is.EqualTo(40f));
    }

    private Component Player(string name)
    {
        var node = Node(name);
        var pokemon = node.AddComponent(GameType("Pokemon"));
        var player = node.AddComponent(GameType("PlayerManager"));
        Set(player, "pokemon", pokemon);
        return player;
    }

    private Component Row(bool enemy)
    {
        var row = Node(enemy ? "Enemy info" : "Ally info").AddComponent(GameType(enemy ? "EnemyDeathInfoUI" : "AllyInfoUI"));
        Set(row, "portrait", Node("Portrait").AddComponent(GameType("UnityEngine.UI.Image")));
        Set(row, "deathTimer", Node("Timer").AddComponent(GameType("TMPro.TextMeshProUGUI")));
        if (enemy) Set(row, "holder", Node("Dead enemy holder"));
        else
        {
            Set(row, "hpBar", Node("Health").AddComponent(GameType("UnityEngine.UI.Image")));
            Set(row, "deathImage", Node("Death image"));
        }
        bindings.Add(row);
        return row;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RowsHandleLatePortraitsRebindingAndZeroHpBeforeInitialization(bool enemy)
    {
        var first = Player("First player");
        var second = Player("Replacement player");
        var row = Row(enemy);
        Call(row, "InitializeUI", first);
        Call(row, "InitializeUI", first);
        var pokemon = Get(first, "pokemon");
        Assert.That(((Action)Get(pokemon, "OnEvolution")).GetInvocationList().Length, Is.EqualTo(1));
        var portrait = (Component)Get(row, "portrait");
        Assert.That(portrait.GetType().GetProperty("enabled").GetValue(portrait), Is.False);
        var texture = new Texture2D(2, 2);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.zero);
        try
        {
            Set(pokemon, "portrait", sprite);
            Emit(pokemon, "OnPokemonInitialized");
            Assert.That(portrait.GetType().GetProperty("sprite").GetValue(portrait), Is.SameAs(sprite));
            Assert.That(portrait.GetType().GetProperty("enabled").GetValue(portrait), Is.True);
            Call(row, "Update");
            Assert.That(((GameObject)Get(row, enemy ? "holder" : "deathImage")).activeSelf, Is.False);
            if (enemy)
            {
                var state = Get(first, "playerState");
                state.GetType().GetProperty("Value").SetValue(state, Enum.Parse(GameType("PlayerState"), "Dead"));
                Call(row, "Update");
                Assert.That(((GameObject)Get(row, "holder")).activeSelf, Is.True);
                state.GetType().GetProperty("Value").SetValue(state, Enum.Parse(GameType("PlayerState"), "Alive"));
                Call(row, "Update");
                Assert.That(((GameObject)Get(row, "holder")).activeSelf, Is.False);
            }
            Call(row, "InitializeUI", second);
            Assert.That(Get(pokemon, "OnEvolution"), Is.Null);
            Assert.That(Get(pokemon, "OnPokemonInitialized"), Is.Null);
            Assert.That(portrait.GetType().GetProperty("sprite").GetValue(portrait), Is.Null);
            Call(row, "Unsubscribe");
            Assert.That(Get(Get(second, "pokemon"), "OnEvolution"), Is.Null);
        }
        finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RosterReplaysExistingPlayersAndReconcilesLateTeamDataWithoutDuplicates(bool enemy)
    {
        var lobby = Node("Lobby").AddComponent(GameType("LobbyController"));
        var localPlayer = new GameSessionPlayer("local");
        Set(lobby, "localPlayer", localPlayer);
        GameType("LobbyController").GetProperty("Instance").SetValue(null, lobby);
        var manager = Node("Match").AddComponent(GameType("GameManager"));
        var first = Player("Already registered");
        typeof(NetworkBehaviour).GetProperty("IsOwner").SetValue(first, true);
        ((NetworkVariable<bool>)Get(first, "botControlled")).Value = true;
        var second = Player("Registered later");
        var info = Node("Info holder").AddComponent(GameType("InfoHolderManager"));
        bindings.Add(info);
        Set(info, "enemyTeam", enemy);
        Set(info, "infoHolder", info.transform);
        Set(info, "infoPrefab", Row(enemy).gameObject);
        Call(manager, "AddPlayer", first);
        Call(info, "Bind", manager);
        var rows = (IDictionary)Get(info, "rows");
        Assert.That(rows.Count, Is.Zero, "Neutral/uninitialized teams must not be classified as enemies.");
        foreach (var player in new[] { first, second })
        {
            var pokemon = Get(player, "pokemon");
            var team = Get(pokemon, "team");
            team.GetType().GetProperty("Value").SetValue(team, Activator.CreateInstance(GameType("TeamMember"), Enum.Parse(GameType("Team"), enemy ? "Orange" : "Blue")));
        }
        Emit(Get(first, "pokemon"), "OnPokemonInitialized");
        Assert.That(rows.Count, Is.Zero, "Missing local team data must not reveal or misclassify players.");
        localPlayer.Data["PlayerTeam"] = new PlayerProperty("Blue");
        ((Action<GameSessionPlayer>)Get(lobby, "onPlayerDataChanged"))?.Invoke(localPlayer);
        Assert.That(rows.Count, Is.EqualTo(1));
        Call(manager, "AddPlayer", second);
        Call(manager, "AddPlayer", second);
        Call(info, "ReconcilePlayers");
        Call(info, "Bind", manager);
        Assert.That(rows.Count, Is.EqualTo(2));
        foreach (GameObject row in rows.Values)
            bindings.Add(row.GetComponent(GameType(enemy ? "EnemyDeathInfoUI" : "AllyInfoUI")));
        LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
        Call(manager, "RemovePlayer", first);
        Assert.That(rows.Count, Is.EqualTo(1));
        Assert.That(Get(Get(first, "pokemon"), "OnTeamChange"), Is.Null);
        LogAssert.Expect(LogType.Error, "Destroy may not be called from edit mode! Use DestroyImmediate instead.\nDestroying an object in edit mode destroys it permanently.");
        Call(info, "Unbind");
        Assert.That(rows.Count, Is.Zero);
        Assert.That(Get(manager, "OnPlayerAdded"), Is.Null);
    }
}
