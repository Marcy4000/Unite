using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CinderLowSweep : MoveBase
{
    private Vector3 direction;
    private DamageInfo damageInfo;

    public CinderLowSweep()
    {
        name = "Low Sweep";
        cooldown = 7.5f;
    }

    public override void Start(MovesController controller)
    {
        base.Start(controller);
        Aim.Instance.InitializeDashAim(5f);
        Debug.Log("Executed low Sweep!");
    }

    public override void Update()
    {
        direction = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        movesController.PlayerMovement.StartDash(direction);
        wasMoveSuccessful = true;
        Aim.Instance.HideDashAim();
        base.Finish();
    }
}
