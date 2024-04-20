using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LockedMove : MoveBase
{
    public LockedMove()
    {
        name = "Not Learned";
        cooldown = 0;
    }

    public override void Start(PlayerManager controller)
    {
        Debug.Log("This move is not learned yet.");
    }
}
