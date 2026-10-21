using UnityEngine;

public class MarshadowDrainPunch : MarshadowMoveBase
{
    private bool struck;
    public MarshadowDrainPunch() { Name = "Drain Punch"; Cooldown = 6f; }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        Begin(); struck = false;
        MarshadowMoveRuntime.PlayAction(playerManager, "pm0883_ba20_buturi01", 2.12f);
        effect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.DrainPunch, 0.5f);
    }
    protected override void Advance()
    {
        if (!struck && elapsed >= 0.25f) { struck = true; Hit(MarshadowHit.DrainPunch); }
        if (elapsed >= 0.43f) Complete();
    }
}
