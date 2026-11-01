using System;
using NUnit.Framework;

namespace Unite.Combat.Tests
{
    public sealed class UniteCastRefundTests
    {
        [TestCase(0)]
        [TestCase(-1)]
        public void RequiresPositiveSpentEnergy(int energyCost)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new UniteCastRefund(energyCost));
        }

        [TestCase(CastOutcome.Interrupted)]
        [TestCase(CastOutcome.Cancelled)]
        [TestCase(CastOutcome.ExecutionFailed)]
        public void FailedCastBeforeCheckpointRestoresNinetyPercentOnce(CastOutcome outcome)
        {
            var refund = new UniteCastRefund(80000);
            Assert.That(refund.TryRefund(outcome, out int minimumEnergy), Is.True);
            Assert.That(minimumEnergy, Is.EqualTo(72000));
            Assert.That(refund.TryRefund(outcome, out minimumEnergy), Is.False);
            Assert.That(minimumEnergy, Is.Zero);
        }

        [TestCase(CastOutcome.Completed)]
        [TestCase(CastOutcome.Rejected)]
        [TestCase(CastOutcome.Replaced)]
        [TestCase(CastOutcome.ActorDespawned)]
        [TestCase(CastOutcome.PreparationFailed)]
        [TestCase(CastOutcome.None)]
        public void NonRefundableTerminationCannotBeReplayedAsAnInterruption(CastOutcome outcome)
        {
            var refund = new UniteCastRefund(100000);
            Assert.That(refund.TryRefund(outcome, out int minimumEnergy), Is.False);
            Assert.That(minimumEnergy, Is.Zero);
            Assert.That(refund.TryRefund(CastOutcome.Interrupted, out minimumEnergy), Is.False);
        }

        [TestCase(CastOutcome.Interrupted)]
        [TestCase(CastOutcome.Cancelled)]
        [TestCase(CastOutcome.ExecutionFailed)]
        public void ReachingCheckpointPreventsRefundEvenWithoutALandedHit(CastOutcome outcome)
        {
            var refund = new UniteCastRefund(80000);
            refund.MarkSucceeded();
            refund.MarkSucceeded();
            Assert.That(refund.Succeeded, Is.True);
            Assert.That(refund.TryRefund(outcome, out int minimumEnergy), Is.False);
            Assert.That(minimumEnergy, Is.Zero);
        }

        [Test]
        public void LateCheckpointCannotChangeATerminalDecision()
        {
            var refund = new UniteCastRefund(90000);
            Assert.That(refund.TryRefund(CastOutcome.Interrupted, out int minimumEnergy), Is.True);
            Assert.That(minimumEnergy, Is.EqualTo(81000));
            refund.MarkSucceeded();
            Assert.That(refund.Succeeded, Is.False);
        }

        [TestCase(99999, 89999)]
        [TestCase(int.MaxValue, 1932735282)]
        public void RefundUsesCapturedCostAndDoesNotOverflow(int cost, int expected)
        {
            var refund = new UniteCastRefund(cost);
            Assert.That(refund.TryRefund(CastOutcome.Interrupted, out int minimumEnergy), Is.True);
            Assert.That(minimumEnergy, Is.EqualTo(expected));
        }
    }
}
