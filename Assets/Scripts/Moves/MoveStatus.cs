using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ActionStatusType
{
    Ready,
    Cooldown,
    Disabled
}

public class MoveStatus
{
    private float cooldown;
    private ActionStatusType statusType;

    public float Cooldown { get { return cooldown; } set { cooldown = value; } }
    public ActionStatusType StatusType {  get { return statusType; }  set { statusType = value; } }

    public MoveStatus(float cooldown)
    {
        this.cooldown = cooldown;
        statusType = ActionStatusType.Ready;
    }
}
