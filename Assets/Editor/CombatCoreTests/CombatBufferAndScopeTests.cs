using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Unite.Combat.Tests
{
    public sealed class CombatBufferAndScopeTests
    {
        [Test]
        public void BufferedActionCanBeTakenOnlyOnceBeforeExpiry()
        {
            var buffer = new ActionBuffer<string>();
            buffer.Put(0, 2, "attack", 10, 0.5);
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.TryTake(0, 2, 10.49, out string action), Is.True);
            Assert.That(action, Is.EqualTo("attack"));
            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.TryTake(0, 2, 10.49, out action), Is.False);
            Assert.That(action, Is.Null);
        }

        [TestCase(10.5)]
        [TestCase(11)]
        public void ExpiredActionIsRemovedAtOrAfterBoundary(double now)
        {
            var buffer = new ActionBuffer<string>();
            buffer.Put(0, 2, "attack", 10, 0.5);
            Assert.That(buffer.TryTake(0, 2, now, out string action), Is.False);
            Assert.That(action, Is.Null);
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void RevisionMismatchDiscardsActionWithoutAReplay()
        {
            var buffer = new ActionBuffer<string>();
            buffer.Put(0, 2, "old attack", 10, 1);
            Assert.That(buffer.TryTake(0, 3, 10, out string action), Is.False);
            Assert.That(action, Is.Null);
            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.TryTake(0, 2, 10, out action), Is.False);
        }

        [Test]
        public void NewActionReplacesOnlyItsOwnSlotAndClearIsScoped()
        {
            var buffer = new ActionBuffer<string>();
            buffer.Put(0, 1, "old", 0, 1);
            buffer.Put(1, 1, "other", 0, 1);
            buffer.Put(0, 2, "new", 0, 2);
            Assert.That(buffer.Count, Is.EqualTo(2));
            buffer.Clear(1);
            Assert.That(buffer.TryTake(0, 2, 1, out string action), Is.True);
            Assert.That(action, Is.EqualTo("new"));
            Assert.That(buffer.TryTake(1, 1, 0, out action), Is.False);
            buffer.Put(0, 2, "new", 1, 1);
            buffer.Clear();
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void ReleaseUpdatesBufferedPressWithoutExtendingItsDeadline()
        {
            var buffer = new ActionBuffer<string>();
            buffer.Put(0, 2, "press", 10, 0.22);
            Assert.That(buffer.TryUpdate(0, 2, 10.1, action => action + "+release"), Is.True);
            Assert.That(buffer.TryTake(0, 2, 10.21, out string action), Is.True);
            Assert.That(action, Is.EqualTo("press+release"));
            buffer.Put(0, 2, "press", 10, 0.22);
            Assert.That(buffer.TryUpdate(0, 2, 10.1, value => value + "+release"), Is.True);
            Assert.That(buffer.TryTake(0, 2, 10.22, out _), Is.False);
        }

        [TestCase(3, 10.1)]
        [TestCase(2, 10.22)]
        public void StaleOrExpiredReleaseCannotResurrectBufferedPress(int revision, double now)
        {
            var buffer = new ActionBuffer<string>();
            buffer.Put(0, 2, "press", 10, 0.22);
            Assert.That(buffer.TryUpdate(0, revision, now, action => "release"), Is.False);
            Assert.That(buffer.Count, Is.Zero);
            Assert.That(buffer.TryUpdate(0, 2, now, action => "release"), Is.False);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidBufferDurationDoesNotAddOrReplaceActions(double duration)
        {
            var buffer = new ActionBuffer<string>();
            buffer.Put(0, 1, "original", 0, 1);
            buffer.Put(0, 1, "invalid", 0, duration);
            buffer.Put(1, 1, "invalid", 0, duration);
            Assert.That(buffer.Count, Is.EqualTo(1));
            Assert.That(buffer.TryTake(0, 1, 0, out string action), Is.True);
            Assert.That(action, Is.EqualTo("original"));
        }

        [Test]
        public void ClearingBufferedRequestsReportsEachRemovedCommandOnce()
        {
            var buffer = new ActionBuffer<string>();
            var removed = new List<string>();
            buffer.Put(0, 1, "first", 0, 1);
            buffer.Put(1, 1, "second", 0, 1);
            buffer.Clear(0, removed.Add);
            buffer.Clear(removed.Add);
            buffer.Clear(removed.Add);
            Assert.That(removed, Is.EqualTo(new[] { "first", "second" }));
            Assert.That(buffer.Count, Is.Zero);
        }

        [Test]
        public void SessionTeardownRunsBeforeInnerAssetRelease()
        {
            var session = new CombatScope();
            var assets = session.Own(new CombatScope());
            var order = new List<string>();
            session.Own(new CountingDisposable(() => order.Add("stop execution")));
            assets.Own(new CountingDisposable(() => order.Add("release asset")));
            session.Dispose();
            Assert.That(order, Is.EqualTo(new[] { "stop execution", "release asset" }));
        }

        [Test]
        public void ScopeDisposesInReverseOrderOnceDespiteThrowingDisposers()
        {
            var scope = new CombatScope();
            var order = new List<int>();
            var firstError = new InvalidOperationException("first");
            var secondError = new InvalidOperationException("second");
            var first = scope.Own(new CountingDisposable(() => order.Add(1)));
            var second = scope.Own(new CountingDisposable(() => { order.Add(2); throw firstError; }));
            var third = scope.Own(new CountingDisposable(() => { order.Add(3); throw secondError; }));

            Assert.DoesNotThrow(scope.Dispose);
            Assert.DoesNotThrow(scope.Dispose);

            Assert.That(scope.IsDisposed, Is.True);
            Assert.That(order, Is.EqualTo(new[] { 3, 2, 1 }));
            Assert.That(first.DisposeCount, Is.EqualTo(1));
            Assert.That(second.DisposeCount, Is.EqualTo(1));
            Assert.That(third.DisposeCount, Is.EqualTo(1));
            Assert.That(scope.CleanupErrors, Is.EqualTo(new Exception[] { secondError, firstError }));
        }

        [Test]
        public void OwningAfterDisposalImmediatelyReleasesResource()
        {
            var scope = new CombatScope();
            scope.Dispose();
            var resource = new CountingDisposable();
            Assert.That(scope.Own(resource), Is.SameAs(resource));
            Assert.That(scope.Own<CountingDisposable>(null), Is.Null);
            Assert.That(resource.DisposeCount, Is.EqualTo(1));
            scope.Dispose();
            Assert.That(resource.DisposeCount, Is.EqualTo(1));
        }
    }

    internal sealed class CountingDisposable : IDisposable
    {
        private readonly Action onDispose;
        public int DisposeCount { get; private set; }

        public CountingDisposable(Action onDispose = null)
        {
            this.onDispose = onDispose;
        }

        public void Dispose()
        {
            DisposeCount++;
            onDispose?.Invoke();
        }
    }
}
