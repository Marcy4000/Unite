using UnityEngine;

public class LockedMove : MoveBase
{
    public LockedMove()
    {
        Name = "Not Learned";
        Cooldown = 0;
    }

    public override void Start(PlayerManager controller)
    {
        Debug.Log("This move is not learned yet.");
    }

    public override void Update()
    {
    }

    public override void ResetMove()
    {
    }
}
