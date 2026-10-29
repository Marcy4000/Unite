using NUnit.Framework;
using Unite.Multiplayer;

public class MatchTransitionBarrierTests
{
    [Test]
    public void AllHumanClientsMustCloseBeforeLoading()
    {
        var barrier = new MatchTransitionBarrier(1, "CharacterSelect", new ulong[] { 0, 5, 9 });
        barrier.MarkClosed(1, 0);
        barrier.MarkClosed(1, 5);
        Assert.That(barrier.StartLoading(), Is.False);
        barrier.MarkClosed(1, 9);
        Assert.That(barrier.StartLoading(), Is.True);
        Assert.That(barrier.StartLoading(), Is.False);
    }

    [Test]
    public void OpeningRequiresBothNetworkSceneCompletionAndLocalScreenReadiness()
    {
        var barrier = new MatchTransitionBarrier(3, "DraftSelect", new ulong[] { 0, 7 });
        barrier.MarkClosed(3, 0);
        barrier.MarkClosed(3, 7);
        barrier.StartLoading();
        barrier.MarkReady(3, 0);
        barrier.MarkSceneLoaded("DraftSelect");
        Assert.That(barrier.ScheduleOpen(), Is.False);
        barrier.MarkReady(3, 7);
        Assert.That(barrier.ScheduleOpen(), Is.True);
        Assert.That(barrier.ScheduleOpen(), Is.False);
    }

    [Test]
    public void LocalReadinessCannotReplaceNetworkSceneCompletion()
    {
        var barrier = new MatchTransitionBarrier(4, "RacingReadyScreen", new ulong[] { 0 });
        barrier.MarkReady(4, 0);
        barrier.MarkClosed(4, 0);
        barrier.StartLoading();
        barrier.MarkSceneLoaded("CharacterSelect");
        Assert.That(barrier.CanOpen, Is.False);
        barrier.MarkReady(4, 0);
        Assert.That(barrier.CanOpen, Is.False);
        barrier.MarkSceneLoaded("RacingReadyScreen");
        Assert.That(barrier.CanOpen, Is.True);
    }

    [Test]
    public void PreviousMatchAndUnknownClientAcknowledgementsAreIgnored()
    {
        var barrier = new MatchTransitionBarrier(2, "CharacterSelect", new ulong[] { 0, 1 });
        barrier.MarkClosed(1, 0);
        barrier.MarkClosed(2, 99);
        barrier.MarkClosed(2, 1);
        Assert.That(barrier.CanLoad, Is.False);
        barrier.MarkClosed(2, 0);
        barrier.StartLoading();
        barrier.MarkSceneLoaded("CharacterSelect");
        barrier.MarkReady(1, 0);
        barrier.MarkReady(2, 99);
        barrier.MarkReady(2, 1);
        Assert.That(barrier.CanOpen, Is.False);
        barrier.MarkReady(2, 0);
        Assert.That(barrier.CanOpen, Is.True);
    }

    [Test]
    public void DisconnectedClientsDoNotBlockEitherBarrier()
    {
        var barrier = new MatchTransitionBarrier(1, "CharacterSelect", new ulong[] { 0, 1, 2 });
        barrier.MarkClosed(1, 0);
        barrier.MarkClosed(1, 1);
        barrier.RemoveClient(2);
        Assert.That(barrier.StartLoading(), Is.True);
        barrier.MarkReady(1, 0);
        barrier.MarkSceneLoaded("CharacterSelect");
        barrier.RemoveClient(1);
        Assert.That(barrier.ScheduleOpen(), Is.True);
    }

    [Test]
    public void RepeatedMatchUsesAFreshParticipantSnapshot()
    {
        for (int match = 1; match <= 2; match++)
        {
            var barrier = new MatchTransitionBarrier(match, "CharacterSelect", new ulong[] { 0, 1 });
            Assert.That(barrier.CanLoad, Is.False);
            Assert.That(barrier.CanOpen, Is.False);
            foreach (ulong client in new ulong[] { 0, 1 }) barrier.MarkClosed(match, client);
            barrier.StartLoading();
            foreach (ulong client in new ulong[] { 0, 1 }) barrier.MarkReady(match, client);
            barrier.MarkSceneLoaded("CharacterSelect");
            Assert.That(barrier.ScheduleOpen(), Is.True);
        }
    }
}
