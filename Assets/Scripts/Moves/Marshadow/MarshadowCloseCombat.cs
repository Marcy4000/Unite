using UnityEngine;

public class MarshadowCloseCombat : MarshadowMoveBase
{
    protected override bool UsesDashAim => true;
    protected override bool RequiresMovement => true;
    protected override float AimRange => MarshadowMoveRules.RushRange;
    private int strike;
    private readonly MarshadowWallDash dash = new MarshadowWallDash();
    private float windowEnd;
    private bool hasWindow;
    private Vector3 sweepFrom;
    private float sweepTimer;
    public int Strike => strike;
    public bool HasRecast => hasWindow;
    public MarshadowCloseCombat() { Name = "Close Combat"; Cooldown = 9f; }

    public override void Start(PlayerManager controller)
    {
        if (hasWindow && Time.time >= windowEnd) { Complete(); return; }
        base.Start(controller);
    }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        if (hasWindow && Time.time >= windowEnd) { Complete(); return; }
        if (!committed) strike = 0; else strike++;
        hasWindow = false; MarshadowMoveRuntime.ShowWindow(playerManager, 0, 0);
        Begin(); sweepFrom = playerManager.transform.position; sweepTimer = 0;
        dash.Begin(playerManager.PlayerMovement, direction, AimRange);
        // The model has no basic-attack clips. Dash strikes are expressed by
        // authored fist/trail effects; no fabricated attack animation is used.
        playerManager.AnimationManager.SetBool("Walking", false);
        effect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.Rush, 0.35f, stage: strike);
    }
    public override void Update()
    {
        base.Update();
        if (hasWindow && Time.time >= windowEnd) { Cancel(); Complete(); }
    }
    protected override void Advance()
    {
        dash.Advance(AimRange / MarshadowMoveRules.RushDuration * Time.deltaTime);
        bool ended = dash.Finished || elapsed >= MarshadowMoveRules.RushDuration;
        if (ended) dash.End();
        sweepTimer -= Time.deltaTime;
        if (sweepTimer <= 0 || ended)
        {
            Hit(MarshadowHit.Rush, strike, sweepFrom, playerManager.transform.position);
            sweepFrom = playerManager.transform.position; sweepTimer = 0.07f;
        }
        if (ended)
        {
            executing = false; ActionLock.Release();
            MarshadowMoveRuntime.EndEffect(playerManager, ref effect);
            MarshadowMoveRuntime.ReturnToLocomotion(playerManager);
            if (strike == 2) Complete();
            else
            {
                hasWindow = true; windowEnd = Time.time + MarshadowMoveRules.RushWindow;
                MarshadowMoveRuntime.ShowWindow(playerManager, 0, MarshadowMoveRules.RushWindow);
            }
        }
    }
    protected override void Complete()
    {
        dash.End();
        hasWindow = false; MarshadowMoveRuntime.ShowWindow(playerManager, 0, 0);
        base.Complete();
    }
}
