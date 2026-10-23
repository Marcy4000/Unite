using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Unite.Multiplayer;
using UnityEngine.TestTools;

// Keep regression tests outside Assets/Tests, which this project intentionally ignores.
public class NetworkFailureTests
{
    [Test]
    public void FailureCanWaitUntilAnOperationReleasesTheLifecycleGate()
    {
        var guard = new SessionFailureGuard();
        var generation = guard.Activate();
        Assert.That(guard.Report(generation, SessionFailureReason.TransportFailure), Is.True);
        Assert.That(guard.PendingReason, Is.EqualTo(SessionFailureReason.TransportFailure));
        Assert.That(guard.TryBeginHandling(out var reason), Is.True);
        Assert.That(reason, Is.EqualTo(SessionFailureReason.TransportFailure));
    }

    [Test]
    public void OverlappingCallbacksOnlyStartOneCleanup()
    {
        var guard = new SessionFailureGuard();
        var generation = guard.Activate();
        guard.Report(generation, SessionFailureReason.TransportFailure);
        Assert.That(guard.Report(generation, SessionFailureReason.ConnectionLost), Is.False);
        Assert.That(guard.TryBeginHandling(out _), Is.True);
        Assert.That(guard.TryBeginHandling(out _), Is.False);
        Assert.That(guard.Report(generation, SessionFailureReason.SessionDeleted), Is.False);
    }

    [Test]
    public void OldSessionCallbacksCannotTearDownAReplacementSession()
    {
        var guard = new SessionFailureGuard();
        var oldGeneration = guard.Activate();
        var currentGeneration = guard.Activate();
        Assert.That(guard.Report(oldGeneration, SessionFailureReason.HostLost), Is.False);
        guard.BeginIntentionalLeave(oldGeneration);
        Assert.That(guard.Report(currentGeneration, SessionFailureReason.ConnectionLost), Is.True);
    }

    [Test]
    public void IntentionalShutdownDoesNotProduceAFailureNotice()
    {
        var guard = new SessionFailureGuard();
        var generation = guard.Activate();
        guard.Report(generation, SessionFailureReason.ConnectionLost);
        guard.BeginIntentionalLeave(generation);
        Assert.That(guard.TryBeginHandling(out _), Is.False);
        Assert.That(guard.Report(generation, SessionFailureReason.TransportFailure), Is.False);
    }

    [Test]
    public void ReactivatingResetsAnEarlierFailure()
    {
        var guard = new SessionFailureGuard();
        guard.Report(guard.Activate(), SessionFailureReason.HostLost);
        guard.TryBeginHandling(out _);
        var generation = guard.Activate();
        Assert.That(guard.PendingReason, Is.EqualTo(SessionFailureReason.None));
        Assert.That(guard.Report(generation, SessionFailureReason.ContentLoadFailed), Is.True);
        Assert.That(guard.TryBeginHandling(out _), Is.True);
    }

    [Test]
    public void MapAndPokemonMustBothBeLoaded()
    {
        var barrier = new ClientReadinessBarrier(ClientLoadStage.Map | ClientLoadStage.Pokemon);
        barrier.Reset(new ulong[] { 0, 4 });
        barrier.MarkReady(0, ClientLoadStage.Map | ClientLoadStage.Pokemon);
        barrier.MarkReady(4, ClientLoadStage.Map);
        Assert.That(barrier.IsReady, Is.False);
        Assert.That(barrier.PendingClients, Is.EqualTo(new ulong[] { 4 }));
        barrier.MarkReady(4, ClientLoadStage.Pokemon);
        Assert.That(barrier.IsReady, Is.True);
    }

    [Test]
    public void DisconnectingAnUnreadyClientReleasesTheBarrier()
    {
        var barrier = new ClientReadinessBarrier(ClientLoadStage.Ready);
        barrier.Reset(new ulong[] { 0, 4 });
        barrier.MarkReady(0, ClientLoadStage.Ready);
        barrier.Remove(4);
        Assert.That(barrier.IsReady, Is.True);
        Assert.That(barrier.MarkReady(4, ClientLoadStage.Ready), Is.False);
    }

    [Test]
    public void DuplicateOrUnknownReadinessDoesNotChangeTheExpectedRoster()
    {
        var barrier = new ClientReadinessBarrier(ClientLoadStage.Ready);
        barrier.Reset(new ulong[] { 0, 4, 4 });
        Assert.That(barrier.MarkReady(12, ClientLoadStage.Ready), Is.False);
        Assert.That(barrier.MarkReady(0, ClientLoadStage.Map), Is.False);
        barrier.MarkReady(0, ClientLoadStage.Ready);
        barrier.MarkReady(0, ClientLoadStage.Ready);
        Assert.That(barrier.PendingClients, Is.EqualTo(new ulong[] { 4 }));
    }

    [Test]
    public void EmptyOrResetBarrierCannotStartAGame()
    {
        var barrier = new ClientReadinessBarrier(ClientLoadStage.Ready);
        Assert.That(barrier.IsReady, Is.False);
        barrier.Reset(new ulong[] { 0 });
        barrier.MarkReady(0, ClientLoadStage.Ready);
        barrier.Reset(new ulong[] { 0 });
        Assert.That(barrier.IsReady, Is.False);
        barrier.Remove(0);
        Assert.That(barrier.IsReady, Is.False);
    }

    [Test]
    public void AClientCannotAcknowledgeLoadingOnBehalfOfAnotherClient()
    {
        var barrier = new ClientReadinessBarrier(ClientLoadStage.Ready);
        barrier.Reset(new ulong[] { 0, 4 });
        Assert.That(barrier.MarkReady(4, 0, ClientLoadStage.Ready), Is.False);
        Assert.That(barrier.PendingClients, Is.EqualTo(new ulong[] { 0, 4 }));
        Assert.That(barrier.MarkReady(4, 4, ClientLoadStage.Ready), Is.True);
        Assert.That(barrier.PendingClients, Is.EqualTo(new ulong[] { 0 }));
    }

    [Test]
    public void ABarrierRequiresAtLeastOneStage()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ClientReadinessBarrier(ClientLoadStage.None));
    }

    [UnityTest]
    public IEnumerator CompletedSdkTasksDoNotWaitForTheTimeout()
    {
        var task = SessionTaskTimeout.WaitAsync(Task.CompletedTask, TimeSpan.FromSeconds(1), CancellationToken.None);
        yield return AwaitWithoutBlockingEditor(task);
        Assert.That(task.Status, Is.EqualTo(TaskStatus.RanToCompletion));
    }

    [Test]
    public void SdkErrorsAreNotReplacedByTimeoutErrors()
    {
        var expected = new InvalidOperationException("sdk error");
        var actual = Assert.ThrowsAsync<InvalidOperationException>(() =>
            SessionTaskTimeout.WaitAsync(Task.FromException(expected), TimeSpan.FromSeconds(1), CancellationToken.None));
        Assert.That(actual, Is.SameAs(expected));
    }

    [UnityTest]
    public IEnumerator StalledSdkTasksHaveABoundedWaitButAreNotCancelled()
    {
        var pending = new TaskCompletionSource<bool>();
        var wait = SessionTaskTimeout.WaitAsync(pending.Task, TimeSpan.FromMilliseconds(20), CancellationToken.None);
        yield return AwaitWithoutBlockingEditor(wait);
        Assert.That(wait.IsFaulted, Is.True);
        Assert.That(wait.Exception.InnerException, Is.TypeOf<TimeoutException>());
        Assert.That(pending.Task.IsCompleted, Is.False);
        pending.SetResult(true);
    }

    [UnityTest]
    public IEnumerator LifetimeCancellationInterruptsTheWait()
    {
        var pending = new TaskCompletionSource<bool>();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var wait = SessionTaskTimeout.WaitAsync(pending.Task, TimeSpan.FromSeconds(1), cancellation.Token);
        yield return AwaitWithoutBlockingEditor(wait);
        Assert.That(wait.IsCanceled, Is.True);
        pending.SetResult(true);
    }

    private static IEnumerator AwaitWithoutBlockingEditor(Task task)
    {
        var deadline = UnityEngine.Time.realtimeSinceStartupAsDouble + 5d;
        while (!task.IsCompleted && UnityEngine.Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        Assert.That(task.IsCompleted, Is.True, "The test wait must finish without blocking Unity's update loop.");
    }
}
