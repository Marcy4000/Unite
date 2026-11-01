using System;

namespace Unite.Combat
{
    public enum CastPhase { Preparing, Executing, Recovery, Ended }
    public enum CastOutcome { None, Completed, Rejected, Cancelled, Interrupted, Replaced, ActorDespawned, PreparationFailed, ExecutionFailed }
    public enum CastRejection { None, InvalidRequest, StaleRequest, UnknownAbility, StaleRevision, Unavailable, Invalidated }

    public readonly struct CastRequest
    {
        public long RequestId { get; }
        public int Slot { get; }
        public int Revision { get; }
        public int AbilityId { get; }

        public CastRequest(long requestId, int slot, int revision, int abilityId)
        {
            RequestId = requestId;
            Slot = slot;
            Revision = revision;
            AbilityId = abilityId;
        }
    }

    public sealed class AbilityDefinition
    {
        public int Id { get; }
        public string Name { get; }
        public double Cooldown { get; }
        public bool CooldownOnEnd { get; }
        public int Capacity { get; }
        public bool StoredUses { get; }
        public double UseInterval { get; }

        public AbilityDefinition(int id, string name, double cooldown, bool cooldownOnEnd = false,
            int capacity = 1, bool storedUses = false, double useInterval = 0)
        {
            if (double.IsNaN(cooldown) || double.IsInfinity(cooldown) || cooldown < 0) throw new ArgumentOutOfRangeException(nameof(cooldown));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (double.IsNaN(useInterval) || double.IsInfinity(useInterval) || useInterval < 0) throw new ArgumentOutOfRangeException(nameof(useInterval));
            Id = id;
            Name = name ?? string.Empty;
            Cooldown = cooldown;
            CooldownOnEnd = cooldownOnEnd;
            Capacity = capacity;
            StoredUses = storedUses;
            UseInterval = useInterval;
        }
    }

    public readonly struct AbilityAvailability
    {
        public int Charges { get; }
        public double Remaining { get; }
        public bool Reserved { get; }
        public bool AwaitingExecutionEnd { get; }
        public bool Ready => Charges > 0 && Remaining <= 0 && !Reserved && !AwaitingExecutionEnd;

        public AbilityAvailability(int charges, double remaining, bool reserved, bool awaitingExecutionEnd)
        {
            Charges = charges;
            Remaining = Math.Max(0, remaining);
            Reserved = reserved;
            AwaitingExecutionEnd = awaitingExecutionEnd;
        }
    }
}
