using UnityEngine;

public class MarshadowSpectralThief : MarshadowMoveBase
{
    protected override bool UsesDashAim => true;
    protected override bool RequiresMovement => true;
    protected override float AimRange => MarshadowMoveRules.SpectralRange;
    private readonly MarshadowWallDash dash = new MarshadowWallDash();
    private bool struck;
    public MarshadowSpectralThief() { Name = "Spectral Thief"; Cooldown = 8f; }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        Begin(); struck = false;
        dash.Begin(playerManager.PlayerMovement, direction, AimRange);
        playerManager.AnimationManager.PlayAnimation("pm0883_kw36_mad01");
        effect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.SpectralThief, 0.5f,
            origin: dash.Destination);
    }
    protected override void Advance()
    {
        if (!struck)
        {
            dash.Advance(AimRange / 0.28f * Time.deltaTime);
            if (dash.Finished || elapsed >= 0.28f)
            {
                dash.End();
                struck = true; elapsed = 0;
                Hit(MarshadowHit.SpectralThief);
                MarshadowMoveRuntime.EndEffect(playerManager, ref effect);
                MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.SpectralThief, 0.25f);
            }
        }
        else if (elapsed >= 0.18f) Complete();
    }
    protected override void Complete()
    {
        dash.End();
        base.Complete();
    }
}
