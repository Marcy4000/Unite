using System;
using NUnit.Framework;

namespace Unite.Combat.Tests
{
    public sealed class CombatCastCoordinatorTests
    {
        private CombatLoadout loadout;
        private CastCoordinator coordinator;
        private AbilityInstance ability;
        private double now;
        private long nextRequest;

        [SetUp]
        public void SetUp()
        {
            now = 100;
            nextRequest = 0;
            loadout = new CombatLoadout();
            ability = loadout.Install(0, new AbilityDefinition(7, "Move", 10), false);
            coordinator = new CastCoordinator(loadout, () => now);
        }

        [TearDown]
        public void TearDown()
        {
            coordinator.Dispose();
        }

        private void Configure(bool deferred = false, bool storedUses = false, int capacity = 1, double interval = 0)
        {
            ability = loadout.Install(0, new AbilityDefinition(8, "Configured move", 10, deferred, capacity, storedUses, interval), false);
        }

        private CastRequest Request(AbilityInstance instance = null)
        {
            instance = instance ?? ability;
            return new CastRequest(++nextRequest, instance.Slot, instance.Revision, instance.Definition.Id);
        }

        private CastSession Prepare(double cooldown = 10, bool freeUse = false)
        {
            Assert.That(coordinator.TryPrepare(Request(), cooldown, freeUse, out CastSession session, out CastRejection rejection), Is.True);
            Assert.That(rejection, Is.EqualTo(CastRejection.None));
            return session;
        }

        private CastSession Commit(double cooldown = 10, bool freeUse = false)
        {
            CastSession session = Prepare(cooldown, freeUse);
            Assert.That(coordinator.TryCommit(session), Is.True);
            return session;
        }

        [Test]
        public void PreparationReservesWithoutSpendingAndRejectionReleasesReservation()
        {
            Configure(storedUses: true, capacity: 2);
            CastSession session = Prepare();
            AbilityAvailability availability = ability.Resources.Read(now);
            Assert.That(availability.Charges, Is.EqualTo(2));
            Assert.That(availability.Remaining, Is.Zero);
            Assert.That(availability.Reserved, Is.True);
            Assert.That(availability.Ready, Is.False);
            Assert.That(session.Accepted, Is.False);
            Assert.That(session.Phase, Is.EqualTo(CastPhase.Preparing));

            Assert.That(coordinator.TryPrepare(Request(), 10, false, out CastSession blocked, out CastRejection rejection), Is.False);
            Assert.That(blocked, Is.Null);
            Assert.That(rejection, Is.EqualTo(CastRejection.Unavailable));
            coordinator.End(session, CastOutcome.Rejected);
            coordinator.End(session, CastOutcome.Completed);

            availability = ability.Resources.Read(now);
            Assert.That(availability.Charges, Is.EqualTo(2));
            Assert.That(availability.Ready, Is.True);
            Assert.That(session.Outcome, Is.EqualTo(CastOutcome.Rejected));
            Assert.That(coordinator.ActiveCount, Is.Zero);
            Assert.That(coordinator.FindCast(session.CastId), Is.Null);
            Assert.That(coordinator.TryCommit(session), Is.False);
            Assert.That(Prepare().Phase, Is.EqualTo(CastPhase.Preparing));
        }

        [TestCase(CastOutcome.Cancelled)]
        [TestCase(CastOutcome.PreparationFailed)]
        [TestCase(CastOutcome.Interrupted)]
        public void EndingUncommittedDeferredCastDoesNotStartCooldown(CastOutcome outcome)
        {
            Configure(deferred: true);
            CastSession session = Prepare();
            coordinator.End(session, outcome);
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
            Assert.That(ability.Resources.Read(now).AwaitingExecutionEnd, Is.False);
            Assert.That(session.Accepted, Is.False);
        }

        [Test]
        public void CommitStartsImmediateCooldownExactlyOnce()
        {
            CastSession session = Commit(8);
            Assert.That(session.Accepted, Is.True);
            Assert.That(session.Phase, Is.EqualTo(CastPhase.Executing));
            Assert.That(ability.Resources.Read(now).Reserved, Is.False);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(8));
            now += 2;
            Assert.That(coordinator.TryCommit(session), Is.False);
            coordinator.BeginRecovery(session);
            Assert.That(session.Phase, Is.EqualTo(CastPhase.Recovery));
            coordinator.End(session, CastOutcome.Completed);
            now += 1;
            coordinator.End(session, CastOutcome.Interrupted);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(5));
            now += 5;
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [Test]
        public void DeferredCooldownBlocksUntilEndThenStartsExactlyOnce()
        {
            Configure(deferred: true);
            CastSession session = Commit(8);
            now += 30;
            Assert.That(ability.Resources.Read(now).AwaitingExecutionEnd, Is.True);
            Assert.That(ability.Resources.Read(now).Remaining, Is.Zero);
            Assert.That(coordinator.TryPrepare(Request(), 8, false, out _, out CastRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CastRejection.Unavailable));
            coordinator.BeginRecovery(session);
            Assert.That(ability.Resources.Read(now).AwaitingExecutionEnd, Is.True);
            coordinator.End(session, CastOutcome.Completed);
            Assert.That(ability.Resources.Read(now).AwaitingExecutionEnd, Is.False);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(8));
            now += 3;
            coordinator.End(session, CastOutcome.Interrupted);
            Assert.That(coordinator.TryCommit(session), Is.False);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(5));
            now += 5;
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [Test]
        public void DuplicateRequestReturnsSameSessionWithoutReservingOrSpendingTwice()
        {
            Configure(storedUses: true, capacity: 2);
            CastRequest request = Request();
            Assert.That(coordinator.TryPrepare(request, 10, false, out CastSession session, out _), Is.True);
            Assert.That(coordinator.TryPrepare(request, 2, true, out CastSession duplicate, out _), Is.True);
            Assert.That(duplicate, Is.SameAs(session));
            Assert.That(duplicate.Cooldown, Is.EqualTo(10));
            Assert.That(duplicate.FreeUse, Is.False);
            Assert.That(coordinator.ActiveCount, Is.EqualTo(1));
            Assert.That(coordinator.TryCommit(session), Is.True);
            Assert.That(coordinator.TryPrepare(request, 10, false, out duplicate, out _), Is.True);
            Assert.That(coordinator.TryCommit(duplicate), Is.False);
            coordinator.End(session, CastOutcome.Completed);
            Assert.That(coordinator.TryPrepare(request, 10, false, out duplicate, out _), Is.True);
            Assert.That(duplicate, Is.SameAs(session));
            Assert.That(coordinator.TryCommit(duplicate), Is.False);
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(1));
            Assert.That(coordinator.ActiveCount, Is.Zero);
        }

        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidRequestedCooldownIsRejectedWithoutReservation(double cooldown)
        {
            Assert.That(coordinator.TryPrepare(Request(), cooldown, false, out CastSession session, out CastRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CastRejection.InvalidRequest));
            Assert.That(session, Is.Null);
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
            Assert.That(coordinator.ActiveCount, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void NonpositiveRequestIdIsRejected(long requestId)
        {
            var request = new CastRequest(requestId, 0, ability.Revision, ability.Definition.Id);
            Assert.That(coordinator.TryPrepare(request, 10, false, out _, out CastRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CastRejection.InvalidRequest));
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [TestCase(-1, 7, 0, CastRejection.UnknownAbility)]
        [TestCase(3, 7, 0, CastRejection.UnknownAbility)]
        [TestCase(1, 7, 0, CastRejection.UnknownAbility)]
        [TestCase(0, 99, 0, CastRejection.UnknownAbility)]
        [TestCase(0, 7, 1, CastRejection.StaleRevision)]
        public void InvalidLoadoutRequestDoesNotReserve(int slot, int abilityId, int revisionOffset, CastRejection expected)
        {
            var request = new CastRequest(1, slot, ability.Revision + revisionOffset, abilityId);
            Assert.That(coordinator.TryPrepare(request, 10, false, out CastSession session, out CastRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(expected));
            Assert.That(session, Is.Null);
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [Test]
        public void ReplacingSlotInvalidatesPreparationAndReleasesScope()
        {
            CastSession oldSession = Prepare();
            var resource = oldSession.Scope.Own(new CountingDisposable());
            AbilityInstance previous = ability;
            AbilityInstance replacement = loadout.Install(0, new AbilityDefinition(9, "Replacement", 4), false);
            Assert.That(replacement.Revision, Is.GreaterThan(previous.Revision));
            Assert.That(oldSession.Phase, Is.EqualTo(CastPhase.Ended));
            Assert.That(oldSession.Outcome, Is.EqualTo(CastOutcome.Replaced));
            Assert.That(oldSession.Accepted, Is.False);
            Assert.That(resource.DisposeCount, Is.EqualTo(1));
            Assert.That(previous.Resources.Read(now).Ready, Is.True);
            Assert.That(replacement.Resources.Read(now).Ready, Is.True);
            Assert.That(coordinator.TryCommit(oldSession), Is.False);
            Assert.That(coordinator.ActiveCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OldCompletionCannotContaminateReplacementCooldown(bool deferred)
        {
            Configure(deferred: deferred);
            CastSession oldSession = Commit(30);
            now += 2;
            ability = loadout.Install(0, new AbilityDefinition(9, "Replacement", 4), false);
            Assert.That(oldSession.Outcome, Is.EqualTo(CastOutcome.Replaced));
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(deferred ? 30 : 28));
            Assert.That(ability.Resources.Read(now).AwaitingExecutionEnd, Is.False);
            now += deferred ? 30 : 28;
            CastSession newSession = Commit(4);
            now += 1;
            coordinator.End(oldSession, CastOutcome.Completed);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(3));
            Assert.That(coordinator.FindCast(newSession.CastId), Is.SameAs(newSession));
            now += 3;
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [TestCase(0, 0)]
        [TestCase(0, 4)]
        [TestCase(1, 30)]
        public void LearningReplacementPreservesRemainingSlotCooldown(int slot, double replacementCooldown)
        {
            ability = loadout.Install(slot, new AbilityDefinition(7, "Move", 10), false);
            Commit();
            now += 3;
            ability = loadout.Install(slot, new AbilityDefinition(9, "Learned move", replacementCooldown), false);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(7));
            Assert.That(coordinator.TryPrepare(Request(), replacementCooldown, false, out _, out CastRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CastRejection.Unavailable));
            now += 7;
            Commit(replacementCooldown);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(replacementCooldown));
        }

        [Test]
        public void LearningReplacementAfterCooldownExpiresIsImmediatelyReady()
        {
            Commit();
            now += 12;
            ability = loadout.Install(0, new AbilityDefinition(9, "Learned move", 4), false);
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [Test]
        public void LearningReplacementDoesNotChangeOtherSlotCooldown()
        {
            AbilityInstance other = loadout.Install(1, new AbilityDefinition(10, "Other slot", 20), false);
            Assert.That(coordinator.TryPrepare(Request(other), 20, false, out CastSession session, out _), Is.True);
            Assert.That(coordinator.TryCommit(session), Is.True);
            Commit();
            now += 3;
            loadout.Install(0, new AbilityDefinition(9, "Learned move", 4), false);
            Assert.That(loadout.Get(1), Is.SameAs(other));
            Assert.That(other.Resources.Read(now).Remaining, Is.EqualTo(17));
        }

        [Test]
        public void LearningStoredReplacementPreservesSpentChargesAndRechargeDeadline()
        {
            Configure(storedUses: true, capacity: 2, interval: 2);
            Commit();
            now += 2;
            Commit();
            now += 1;
            ability = loadout.Install(0, new AbilityDefinition(9, "Learned move", 4, capacity: 2, storedUses: true, useInterval: 1), false);
            Assert.That(ability.Resources.Read(now).Charges, Is.Zero);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(7));
            now += 7;
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(1));
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
            now += 10;
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LearningAcrossResourceTypesPreservesCooldown(bool previousStored)
        {
            Configure(storedUses: previousStored);
            Commit();
            now += 3;
            ability = loadout.Install(0, new AbilityDefinition(9, "Learned move", 4, storedUses: !previousStored), false);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(7));
            ability.Resources.ReduceCooldown(2, now);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(5));
            now += 5;
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
            Commit(4);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(4));
        }

        [Test]
        public void LearningStoredReplacementPreservesCastIntervalWithAvailableCharges()
        {
            Configure(storedUses: true, capacity: 2, interval: 5);
            Commit();
            now += 2;
            ability = loadout.Install(0, new AbilityDefinition(9, "Learned move", 4, capacity: 2, storedUses: true), false);
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(1));
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(3));
        }

        [Test]
        public void UpgradeKeepsInstanceResourcesRevisionAndPreparation()
        {
            Configure(storedUses: true, capacity: 2);
            CastSession session = Prepare();
            AbilityInstance previous = ability;
            AbilityResources resources = ability.Resources;
            int removals = 0;
            loadout.Removing += _ => removals++;
            ability = loadout.Install(0, ability.Definition, true);
            Assert.That(ability, Is.SameAs(previous));
            Assert.That(ability.Resources, Is.SameAs(resources));
            Assert.That(ability.Revision, Is.EqualTo(session.Request.Revision));
            Assert.That(ability.Upgraded, Is.True);
            Assert.That(ability.Resources.Read(now).Reserved, Is.True);
            Assert.That(removals, Is.Zero);
            Assert.That(coordinator.TryCommit(session), Is.True);
            coordinator.End(session, CastOutcome.Completed);
            Assert.That(loadout.Install(0, ability.Definition, false), Is.SameAs(ability));
            Assert.That(ability.Upgraded, Is.True);
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(1));
            now += 10;
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(2));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InterruptionReleasesAllScopesOnceDespiteCleanupFailure(bool executing)
        {
            Configure(deferred: true);
            CastSession first = executing ? Commit() : Prepare();
            var cleanupError = new InvalidOperationException("cleanup failed");
            var firstResource = first.Scope.Own(new CountingDisposable());
            var throwingResource = first.Scope.Own(new CountingDisposable(() => { throw cleanupError; }));
            AbilityInstance secondAbility = loadout.Install(1, new AbilityDefinition(9, "Second", 10), false);
            Assert.That(coordinator.TryPrepare(Request(secondAbility), 10, false, out CastSession second, out _), Is.True);
            var secondResource = second.Scope.Own(new CountingDisposable());

            Assert.DoesNotThrow(() => coordinator.InterruptAll(CastOutcome.Interrupted));
            coordinator.InterruptAll(CastOutcome.Interrupted);
            coordinator.End(first, CastOutcome.Completed);
            coordinator.Dispose();

            Assert.That(coordinator.ActiveCount, Is.Zero);
            Assert.That(first.Outcome, Is.EqualTo(CastOutcome.Interrupted));
            Assert.That(second.Outcome, Is.EqualTo(CastOutcome.Interrupted));
            Assert.That(firstResource.DisposeCount, Is.EqualTo(1));
            Assert.That(throwingResource.DisposeCount, Is.EqualTo(1));
            Assert.That(secondResource.DisposeCount, Is.EqualTo(1));
            Assert.That(first.Scope.CleanupErrors, Is.EqualTo(new[] { cleanupError }));
            Assert.That(secondAbility.Resources.Read(now).Ready, Is.True);
            Assert.That(ability.Resources.Read(now).Reserved, Is.False);
            Assert.That(ability.Resources.Read(now).AwaitingExecutionEnd, Is.False);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(executing ? 10 : 0));
        }

        [Test]
        public void DisposingCoordinatorEndsCastsAndRejectsNewRequests()
        {
            CastSession session = Prepare();
            var resource = session.Scope.Own(new CountingDisposable());
            coordinator.Dispose();
            coordinator.Dispose();
            Assert.That(session.Outcome, Is.EqualTo(CastOutcome.ActorDespawned));
            Assert.That(resource.DisposeCount, Is.EqualTo(1));
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
            Assert.That(coordinator.FindRequest(session.Request.RequestId), Is.Null);
            Assert.That(coordinator.TryPrepare(Request(), 10, false, out _, out CastRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CastRejection.InvalidRequest));
        }

        [Test]
        public void FreeUseDoesNotConsumeChargesOrAlterExistingCooldown()
        {
            Configure(storedUses: true, capacity: 1);
            CastSession paid = Commit();
            coordinator.End(paid, CastOutcome.Completed);
            now += 2;
            CastSession free = Commit(30, true);
            coordinator.End(free, CastOutcome.Completed);
            Assert.That(ability.Resources.Read(now).Charges, Is.Zero);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(8));
            now += 8;
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [Test]
        public void ChargesRechargeSequentiallyAndRespectUseInterval()
        {
            Configure(storedUses: true, capacity: 2, interval: 2);
            coordinator.End(Commit(), CastOutcome.Completed);
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(1));
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(2));
            Assert.That(coordinator.TryPrepare(Request(), 10, false, out _, out CastRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CastRejection.Unavailable));
            now += 2;
            coordinator.End(Commit(), CastOutcome.Completed);
            Assert.That(ability.Resources.Read(now).Charges, Is.Zero);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(8));
            now = 109.99;
            Assert.That(ability.Resources.Read(now).Charges, Is.Zero);
            now = 110;
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(1));
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
            now = 120;
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(2));
            now = 200;
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(2));
            coordinator.End(Commit(), CastOutcome.Completed);
            now = 202;
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(1));
            now = 210;
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(2));
        }

        [Test]
        public void ChangingRechargeDurationPreservesFractionalProgress()
        {
            Configure(storedUses: true, capacity: 1);
            coordinator.End(Commit(), CastOutcome.Completed);
            now += 4;
            ability.Resources.SetRechargeDuration(5, now);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(3));
            now += 2.99;
            Assert.That(ability.Resources.Read(now).Charges, Is.Zero);
            now = 107;
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(1));
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CooldownReductionAndFractionalReductionRecoverAtBoundary(bool storedUses)
        {
            Configure(storedUses: storedUses);
            coordinator.End(Commit(), CastOutcome.Completed);
            now += 2;
            ability.Resources.ReduceCooldown(2, now);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(6));
            ability.Resources.ReduceCooldownByFraction(0.5, now);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(3));
            now += 3;
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExcessiveCooldownReductionIsBoundedAtFullResources(bool storedUses)
        {
            Configure(storedUses: storedUses, capacity: 2);
            coordinator.End(Commit(), CastOutcome.Completed);
            ability.Resources.ReduceCooldown(1000, now);
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
            Assert.That(ability.Resources.Read(now).Remaining, Is.Zero);
            Assert.That(ability.Resources.Read(now).Charges, Is.EqualTo(storedUses ? 2 : 1));
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void InvalidCooldownReductionDoesNotChangeResources(double amount)
        {
            coordinator.End(Commit(), CastOutcome.Completed);
            ability.Resources.ReduceCooldown(amount, now);
            Assert.That(ability.Resources.Read(now).Remaining, Is.EqualTo(10));
        }

        [Test]
        public void BoundedHistoryEvictsOldSessionsButStillRejectsTheirReplays()
        {
            CastSession oldest = Commit(0);
            coordinator.End(oldest, CastOutcome.Completed);
            for (int index = 0; index < 255; index++)
            {
                coordinator.End(Commit(0), CastOutcome.Completed);
            }
            Assert.That(coordinator.FindRequest(oldest.Request.RequestId), Is.SameAs(oldest));
            CastSession newest = Commit(0);
            coordinator.End(newest, CastOutcome.Completed);
            Assert.That(coordinator.FindRequest(oldest.Request.RequestId), Is.Null);
            Assert.That(coordinator.FindRequest(newest.Request.RequestId), Is.SameAs(newest));
            Assert.That(coordinator.TryPrepare(oldest.Request, 0, false, out CastSession replay, out CastRejection rejection), Is.False);
            Assert.That(replay, Is.Null);
            Assert.That(rejection, Is.EqualTo(CastRejection.StaleRequest));
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
            Assert.That(coordinator.ActiveCount, Is.Zero);
        }

        [Test]
        public void HistoryEvictionDoesNotDiscardStillActiveRequests()
        {
            CastSession active = Commit(0);
            for (int index = 0; index < 257; index++)
            {
                coordinator.End(Commit(0, true), CastOutcome.Completed);
            }
            Assert.That(coordinator.FindRequest(active.Request.RequestId), Is.SameAs(active));
            Assert.That(coordinator.FindCast(active.CastId), Is.SameAs(active));
            Assert.That(coordinator.TryPrepare(active.Request, 0, false, out CastSession duplicate, out _), Is.True);
            Assert.That(duplicate, Is.SameAs(active));
            Assert.That(coordinator.ActiveCount, Is.EqualTo(1));
        }

        [Test]
        public void AdmittedBufferedRequestsCanPrepareInReverseOrder()
        {
            CastRequest older = Request();
            CastRequest newer = Request();
            Assert.That(coordinator.TryAdmit(older.RequestId), Is.True);
            Assert.That(coordinator.TryAdmit(newer.RequestId), Is.True);
            Assert.That(coordinator.TryPrepare(newer, 0, true, out CastSession second, out _), Is.True);
            Assert.That(coordinator.TryPrepare(older, 0, true, out CastSession first, out _), Is.True);
            Assert.That(first.Request.RequestId, Is.EqualTo(older.RequestId));
            Assert.That(second.Request.RequestId, Is.EqualTo(newer.RequestId));
        }

        [Test]
        public void DuplicateAdmissionDoesNotInvalidateQueuedRequest()
        {
            CastRequest request = Request();
            Assert.That(coordinator.TryAdmit(request.RequestId), Is.True);
            Assert.That(coordinator.TryAdmit(request.RequestId), Is.False);
            Assert.That(coordinator.TryPrepare(request, 0, false, out _, out _), Is.True);
        }

        [Test]
        public void DiscardedAdmissionCannotExecuteAfterCancellation()
        {
            CastRequest request = Request();
            Assert.That(coordinator.TryAdmit(request.RequestId), Is.True);
            coordinator.DiscardAdmission(request.RequestId);
            Assert.That(coordinator.TryPrepare(request, 0, false, out _, out CastRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CastRejection.StaleRequest));
        }

        [Test]
        public void FullCooldownResetIsImmediatelyReadyAtFractionalServerTimes()
        {
            now = 0;
            coordinator.End(Commit(4.5), CastOutcome.Completed);
            now = 0.03;
            ability.Resources.ReduceCooldownByFraction(1, now);
            Assert.That(ability.Resources.Read(now).Remaining, Is.Zero);
            Assert.That(ability.Resources.Read(now).Ready, Is.True);
        }

        [Test]
        public void UnavailableRequestCannotBeReplayedAfterReservationIsReleased()
        {
            CastSession session = Prepare();
            CastRequest unavailable = Request();
            Assert.That(coordinator.TryPrepare(unavailable, 10, false, out _, out CastRejection rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CastRejection.Unavailable));
            coordinator.End(session, CastOutcome.Cancelled);
            Assert.That(coordinator.TryPrepare(unavailable, 10, false, out _, out rejection), Is.False);
            Assert.That(rejection, Is.EqualTo(CastRejection.StaleRequest));
            Assert.That(Prepare().Phase, Is.EqualTo(CastPhase.Preparing));
        }
    }
}
