using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PsyduckRaceDash : MoveBase
{
    private PsyduckRacePassive psyduckRacePassive;

    public PsyduckRaceDash()
    {
        Name = "Dash";
        Cooldown = 30.0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        psyduckRacePassive = playerManager.PassiveController.Passive as PsyduckRacePassive;
    }

    public override void Update()
    {
    }

    public override void Finish()
    {
        if (IsActive)
        {
            psyduckRacePassive.Dash();
            wasMoveSuccessful = true;
        }
        base.Finish();
    }

    public override void ResetMove()
    {
    }
}
