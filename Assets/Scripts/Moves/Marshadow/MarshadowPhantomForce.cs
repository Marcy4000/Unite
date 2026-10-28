using UnityEngine;

public class MarshadowPhantomForce : MarshadowMoveBase
{
    private bool ghostly, struck;
    private float deadline;
    private int ghostEffect;
    public bool IsGhostly => ghostly;
    public MarshadowPhantomForce() { Name = "Phantom Force"; Cooldown = 10f; }
    public override void Start(PlayerManager controller)
    {
        if (ghostly && Time.time >= deadline) { BreakStealth(); return; }
        base.Start(controller);
    }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        if (!ghostly)
        {
            HideAim(); IsActive = false;
            committed = true; cast = MarshadowMoveRules.NextCast();
            (playerManager.PassiveController.Passive as MarshadwPassive)?.OnMoveCommitted();
            ghostly = true; deadline = Time.time + 4f;
            playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 0.22f, true, MarshadowMoveRules.PhantomInvincible));
            playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invisible, 4.1f, true, MarshadowMoveRules.PhantomInvisible));
            ghostEffect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.PhantomForce, 4f);
            MarshadowMoveRuntime.ShowWindow(playerManager, 1, 4f);
        }
        else
        {
            EndGhost(); Begin(); struck = false;
            MarshadowMoveRuntime.PlayAction(playerManager, "pm0883_ba20_buturi02", IsUpgraded ? 2.864f : 2.25f);
            effect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.Kick, 0.5f, stage: IsUpgraded ? 1 : 0);
        }
    }
    public override void Update()
    {
        base.Update();
        if (ghostly && Time.time >= deadline) BreakStealth();
    }
    protected override void Advance()
    {
        float startup = IsUpgraded ? 0.22f : 0.28f;
        if (!struck && elapsed >= startup) { struck = true; Hit(MarshadowHit.Kick); }
        if (elapsed >= startup + (IsUpgraded ? 0.13f : 0.2f)) Complete();
    }
    private void EndGhost()
    {
        ghostly = false;
        playerManager.Pokemon.RemoveStatusEffectWithID(MarshadowMoveRules.PhantomInvincible);
        playerManager.Pokemon.RemoveStatusEffectWithID(MarshadowMoveRules.PhantomInvisible);
        MarshadowMoveRuntime.EndEffect(playerManager, ref ghostEffect);
        MarshadowMoveRuntime.ShowWindow(playerManager, 1, 0);
    }
    public void BreakStealth()
    {
        if (!ghostly) return;
        EndGhost(); Cancel(); Complete();
    }
    public override void ResetMove()
    {
        if (playerManager != null) EndGhost();
        base.ResetMove();
    }
}
