using System;
using System.Collections.Generic;

[Flags]
public enum ActionStatusType
{
    None = 0,
    Cooldown = 1 << 0,
    Disabled = 1 << 1,
    Stunned = 1 << 2,
    Charging = 1 << 3,
    Executing = 1 << 4,
    Busy = 1 << 5
}

public class BattleActionStatus
{
    // Class originally intended to be used for move cooldowns, but can be used for any action

    private float cooldown;
    private ActionStatusType statusType;
    private readonly Dictionary<ActionStatusType, int> scopedStatuses = new Dictionary<ActionStatusType, int>();

    private ActionStatusType EffectiveStatus
    {
        get
        {
            ActionStatusType result = statusType;
            foreach (var pair in scopedStatuses) if (pair.Value > 0) result |= pair.Key;
            return result;
        }
    }

    // A cast can release its restriction without clearing a different cast's,
    // scoring system's, or legacy caller's restriction of the same kind.
    public IDisposable AcquireStatus(ActionStatusType status)
    {
        scopedStatuses.TryGetValue(status, out int count);
        scopedStatuses[status] = count + 1;
        OnStatusChange?.Invoke();
        return new StatusLease(this, status);
    }

    private sealed class StatusLease : IDisposable
    {
        private BattleActionStatus owner;
        private readonly ActionStatusType status;
        public StatusLease(BattleActionStatus owner, ActionStatusType status) { this.owner = owner; this.status = status; }
        public void Dispose()
        {
            if (owner == null) return;
            BattleActionStatus previous = owner;
            owner = null;
            if (--previous.scopedStatuses[status] == 0) previous.scopedStatuses.Remove(status);
            previous.OnStatusChange?.Invoke();
        }
    }

    public float Cooldown { get { return cooldown; } set { cooldown = value; } }
    public event Action OnStatusChange;

    public BattleActionStatus(float cooldown)
    {
        this.cooldown = cooldown;
        statusType = ActionStatusType.None;
    }

    public void AddStatus(ActionStatusType status)
    {
        statusType |= status;
        OnStatusChange?.Invoke();
    }

    public void RemoveStatus(ActionStatusType status)
    {
        statusType &= ~status;
        OnStatusChange?.Invoke();
    }

    public bool HasStatus(ActionStatusType status)
    {
        // If checking for None, return true if no statuses are set
        if (status == ActionStatusType.None)
        {
            return EffectiveStatus == ActionStatusType.None;
        }
        return (EffectiveStatus & status) == status;
    }

    public ActionStatusType GetStatus()
    {
        // Return None (implying Ready) if no other statuses are set
        return EffectiveStatus;
    }
}
