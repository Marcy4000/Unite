using UnityEngine;

public class EmptyPassive : PassiveBase
{
    // Literally does nothing

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        Debug.Log("Empty Passive");
    }

    public override void Update()
    {
        // Empty
    }
}
