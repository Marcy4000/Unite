using UnityEngine;

public class DuraludonUnite : MoveBase
{
    public DuraludonUnite()
    {
        Name = "Revolving Ruin";
        Cooldown = 0;
    }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        ActorAim.InitializeSimpleCircle(DuraMoveRules.RuinRadius);
    }

    public override void Update() { }

    public override void Finish()
    {
        var attack = playerManager?.MovesController.BasicAttack as DuraBasicAtk;
        if (IsActive && attack?.Combat != null)
        {
            attack.ExitCannonMode();
            attack.Combat.RevolvingRuinRPC();
            wasMoveSuccessful = true;
        }
        ActorAim.HideSimpleCircle();
        base.Finish();
    }

    public override void Cancel()
    {
        ActorAim.HideSimpleCircle();
        base.Cancel();
    }

    public override void ResetMove()
    {
        Cancel();
    }
}
