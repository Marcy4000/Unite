using System;

namespace Unite.Combat
{
    public sealed class UniteCastRefund
    {
        private readonly int energyCost;
        private bool settled;
        public bool Succeeded { get; private set; }

        public UniteCastRefund(int energyCost)
        {
            if (energyCost <= 0) throw new ArgumentOutOfRangeException(nameof(energyCost));
            this.energyCost = energyCost;
        }

        public void MarkSucceeded()
        {
            if (!settled) Succeeded = true;
        }

        public bool TryRefund(CastOutcome outcome, out int minimumEnergy)
        {
            minimumEnergy = 0;
            if (settled) return false;
            settled = true;
            if (Succeeded || (outcome != CastOutcome.Interrupted && outcome != CastOutcome.Cancelled
                && outcome != CastOutcome.ExecutionFailed)) return false;
            minimumEnergy = (int)((long)energyCost * 90 / 100);
            return true;
        }
    }
}
