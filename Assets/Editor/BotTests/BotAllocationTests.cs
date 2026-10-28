using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unite.Multiplayer;
using Unity.Services.Multiplayer;

public class BotAllocationTests
{
    [Test]
    public void DraftSnakeVisitsEverySlotExactlyOnce()
    {
        for (int blue = 1; blue <= 5; blue++) for (int orange = 1; orange <= 5; orange++)
        {
            var visited = new List<string>();
            for (int turn = 0; turn < DraftTurnRules.PickTurns(blue, orange); turn++)
            {
                bool isBlue = DraftTurnRules.IsBlueTurn(turn);
                int start = DraftTurnRules.PickStart(turn);
                for (int i = 0; i < DraftTurnRules.PickCount(isBlue ? blue : orange, turn); i++)
                    visited.Add((isBlue ? "blue" : "orange") + (start + i));
            }
            var expected = Enumerable.Range(0, blue).Select(i => "blue" + i).Concat(Enumerable.Range(0, orange).Select(i => "orange" + i));
            Assert.That(visited, Is.EquivalentTo(expected), $"{blue}v{orange}");
        }
    }

    [Test]
    public void FullDraftUsesOneTwoTwoTwoTwoOnePicks()
    {
        Assert.That(Enumerable.Range(0, DraftTurnRules.PickTurns(5, 5)).Select(turn => DraftTurnRules.PickCount(5, turn)),
            Is.EqualTo(new[] { 1, 2, 2, 2, 2, 1 }));
    }

    private static Dictionary<string, string> Bots(params string[] lanes) => lanes.Select((lane, index) =>
        new { id = "bot-" + index, lane }).ToDictionary(value => value.id, value => value.lane);

    [Test]
    public void FullBotTeamSplitsTwoOneTwo()
    {
        var lanes = BotAllocationRules.AllocateLanes(Array.Empty<string>(), Bots("", "", "", "", ""));
        Assert.That(lanes.Values.Count(lane => lane == "top"), Is.EqualTo(2));
        Assert.That(lanes.Values.Count(lane => lane == "mid"), Is.EqualTo(1));
        Assert.That(lanes.Values.Count(lane => lane == "bottom"), Is.EqualTo(2));
    }

    [Test]
    public void HumansClaimCentralAndTopBeforeBots()
    {
        var lanes = BotAllocationRules.AllocateLanes(new[] { "mid", "top" }, Bots("mid", "bottom", "bottom"));
        Assert.That(lanes.Values, Is.EquivalentTo(new[] { "top", "bottom", "bottom" }));
        Assert.That(lanes["bot-1"], Is.EqualTo("bottom"));
        Assert.That(lanes["bot-2"], Is.EqualTo("bottom"));
    }

    [Test]
    public void AlreadyBalancedBotsDoNotMoveWhenHumanChangesNothing()
    {
        var bots = Bots("top", "bottom", "bottom");
        Assert.That(BotAllocationRules.AllocateLanes(new[] { "mid", "top" }, bots), Is.EquivalentTo(bots));
    }

    [Test]
    public void OverfilledHumanLaneIsNotOverwrittenOrAssignedMoreBots()
    {
        var humans = new[] { "mid", "mid", "mid" };
        var copy = humans.ToArray();
        var lanes = BotAllocationRules.AllocateLanes(humans, Bots("mid", "mid"));
        Assert.That(lanes.Values, Does.Not.Contain("mid"));
        Assert.That(humans, Is.EqualTo(copy));
    }

    [Test]
    public void ClearingHumanCentralChoiceAllowsBotToFillIt()
    {
        var lanes = BotAllocationRules.AllocateLanes(new[] { "top", "bottom", "", "bottom" }, Bots("bottom"));
        Assert.That(lanes["bot-0"], Is.EqualTo("mid"));
    }

    [Test]
    public void AllocationDoesNotDependOnDictionaryInsertionOrder()
    {
        var bots = Bots("", "", "", "", "");
        var reversed = bots.Reverse().ToDictionary(pair => pair.Key, pair => pair.Value);
        Assert.That(BotAllocationRules.AllocateLanes(Array.Empty<string>(), bots),
            Is.EquivalentTo(BotAllocationRules.AllocateLanes(Array.Empty<string>(), reversed)));
    }

    [Test]
    public void HumanPokemonDisplacesBotButPreservesOtherPreferences()
    {
        var preferences = new Dictionary<string, short> { ["a"] = 1, ["b"] = 2, ["c"] = 3 };
        var choices = BotAllocationRules.AllocateCharacters(new short[] { 0, 1, 2, 3, 4 }, new short[] { 1, 4 }, preferences);
        Assert.That(choices["b"], Is.EqualTo(2));
        Assert.That(choices["c"], Is.EqualTo(3));
        Assert.That(choices["a"], Is.EqualTo(0));
        Assert.That(preferences["a"], Is.EqualTo(1), "Private inputs are not mutated by allocation.");
    }

    [Test]
    public void DuplicateBotPreferencesStillProduceUniqueCharacters()
    {
        var choices = BotAllocationRules.AllocateCharacters(new short[] { 0, 1, 2, 3, 4 }, Array.Empty<short>(),
            new Dictionary<string, short> { ["a"] = 1, ["b"] = 1, ["c"] = 1, ["d"] = 1, ["e"] = 1 });
        Assert.That(choices.Values.Distinct().Count(), Is.EqualTo(5));
    }

    [Test]
    public void ExhaustedCharacterPoolFailsInsteadOfLoopingForever()
    {
        Assert.Throws<InvalidOperationException>(() => BotAllocationRules.AllocateCharacters(new short[] { 0, 1 },
            new short[] { 0, 1 }, new Dictionary<string, short> { ["a"] = 0 }));
    }

    [Test]
    public void MembershipBasisChangesOnlyWithHumanIdentitySet()
    {
        Assert.That(BotAllocationRules.MembershipHash(new[] { "a", "b" }),
            Is.EqualTo(BotAllocationRules.MembershipHash(new[] { "b", "a" })));
        Assert.That(BotAllocationRules.MembershipHash(new[] { "a", "b", "new-human" }),
            Is.Not.EqualTo(BotAllocationRules.MembershipHash(new[] { "a", "b" })));
    }

    [Test]
    public void BotViewsHaveExplicitIdentityWithoutPretendingToBeServiceMembers()
    {
        var local = new GameSessionPlayer("human");
        var bot = new GameSessionPlayer("bot-1", true);
        bot.Data["PlayerName"] = new PlayerProperty("Trainer 1");
        var view = new GameSessionView();
        view.ApplyPlayers(new[] { local, bot }, local, new Dictionary<string, PlayerProperty>());
        Assert.That(view.Players[1].IsBot, Is.True);
        Assert.That(view.Players[1].Data["PlayerName"].Value, Is.EqualTo("Trainer 1"));
        view.ApplyPlayers(new[] { local }, local, new Dictionary<string, PlayerProperty>());
        Assert.That(view.Players.Select(player => player.Id), Is.EqualTo(new[] { "human" }));
    }
}
