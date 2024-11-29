using System;

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
            return statusType == ActionStatusType.None;
        }
        return (statusType & status) == status;
    }

    public ActionStatusType GetStatus()
    {
        // Return None (implying Ready) if no other statuses are set
        return statusType == ActionStatusType.None ? ActionStatusType.None : statusType;
    }
}