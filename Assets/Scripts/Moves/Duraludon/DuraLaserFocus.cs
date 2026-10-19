using UnityEngine;

public class DuraLaserFocus : MoveBase
{
    public DuraLaserFocus()
    {
        Name = "Laser Focus";
        Cooldown = 9f;
    }

    public override void Update()
    {
    }

    public override void Finish()
    {
        var combat = (playerManager?.MovesController.BasicAttack as DuraBasicAtk)?.Combat;
        if (IsActive && combat != null)
        {
            combat.LaserFocusRPC();
            playerManager.AnimationManager.PlayAnimation("ani_spell1_bat_0884");
            wasMoveSuccessful = true;
        }
        base.Finish();
    }

    public override void ResetMove()
    {
        Cancel(); // The prefab's server-side lifetime handles death/expiry.
    }
}
