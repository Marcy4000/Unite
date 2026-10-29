using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PsyduckRaceDash : MoveBase
{
    private PsyduckRacePassive psyduckRacePassive;
    public override MoveUseType UseType => MoveUseType.StoredUses;
    public override int MaxStoredUses => 2;
    public override float StoredUseInterval => 1f;

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
        if (IsActive && psyduckRacePassive != null)
        {
            wasMoveSuccessful = psyduckRacePassive.TryDash();
        }
        base.Finish();
    }

    public override void ResetMove()
    {
    }
}
