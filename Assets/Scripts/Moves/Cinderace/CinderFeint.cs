using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CinderFeint : MoveBase
{
    public CinderFeint()
    {
        Name = "Feint";
        Cooldown = 9.0f;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        controller.AnimationManager.PlayAnimation("ani_spell2b_bat_0815");
        wasMoveSuccessful = true;
    }

    public override void Update()
    {
        // Set status as invincible
    }

    public override void Finish()
    {
        base.Finish();
    }
}
