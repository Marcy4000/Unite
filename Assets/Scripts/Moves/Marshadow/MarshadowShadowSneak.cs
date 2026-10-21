using System;
using UnityEngine;

public class MarshadowShadowSneak : MoveBase
{
    private readonly MarshadowActionLock actionLock = new MarshadowActionLock();
    private bool submerged;
    private float deadline;
    private int effect;
    private Vector3 entry;
    public MarshadowShadowSneak() { Name = "Shadow Sneak"; Cooldown = 6f; }
    public override void Update()
    {
        if (submerged && Time.time >= deadline) Emerge();
    }
    public override void Finish()
    {
        if (!IsActive) return;
        IsActive = false;
        if (submerged) { Emerge(); return; }
        submerged = true; entry = playerManager.transform.position; deadline = Time.time + 3.5f;
        (playerManager.PassiveController.Passive as MarshadwPassive)?.OnMoveCommitted();
        actionLock.Acquire(playerManager, false, 1);
        playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, 3.6f, true, MarshadowMoveRules.SneakInvincible));
        playerManager.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invisible, 3.6f, true, MarshadowMoveRules.SneakInvisible));
        playerManager.Pokemon.AddStatChange(new StatChange(20, Stat.Speed, 3.6f, true, true, true, MarshadowMoveRules.SneakSpeed));
        playerManager.PlayerMovement.IsFlying = true;
        effect = MarshadowMoveRuntime.Effect(playerManager, MarshadowEffect.ShadowSneak, 3.6f);
        MarshadowMoveRuntime.ShowWindow(1, 3.5f);
    }
    private void Emerge()
    {
        if (!submerged) return;
        Vector3 destination;
        if (!playerManager.PlayerMovement.TryMarshadowExit(playerManager.transform.position, out destination)) destination = entry;
        playerManager.transform.position = destination;
        Cleanup();
        wasMoveSuccessful = true; base.Finish();
    }
    private void Cleanup()
    {
        if (playerManager == null) return;
        submerged = false; actionLock.Release();
        playerManager.PlayerMovement.IsFlying = false;
        playerManager.Pokemon.RemoveStatusEffectWithID(MarshadowMoveRules.SneakInvincible);
        playerManager.Pokemon.RemoveStatusEffectWithID(MarshadowMoveRules.SneakInvisible);
        playerManager.Pokemon.RemoveAllStatChangeWithIDRPC(MarshadowMoveRules.SneakSpeed);
        MarshadowMoveRuntime.EndEffect(playerManager, ref effect);
        MarshadowMoveRuntime.ShowWindow(1, 0);
    }
    public override void ResetMove()
    {
        bool used = submerged;
        Cleanup(); IsActive = false; wasMoveSuccessful = used; base.Finish();
    }
}
