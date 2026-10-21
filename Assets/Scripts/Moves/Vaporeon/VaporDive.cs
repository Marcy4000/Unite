using System;
using UnityEngine;

public class VaporDive : MoveBase
{
    private readonly VaporActionLock actionLock = new VaporActionLock();
    private IDisposable recastLock;
    private bool submerged, emerging, acknowledged;
    private float elapsed, emergeElapsed;
    public bool IsSubmerged => submerged;
    public VaporDive() { Name = "Dive"; Cooldown = 10f; }
    public override void Start(PlayerManager controller)
    {
        base.Start(controller); Cooldown = VaporMoveRules.DiveCooldown(IsUpgraded);
    }
    public override void Finish()
    {
        if (!IsActive) return;
        IsActive = false;
        if (submerged)
        {
            if (elapsed >= VaporMoveRules.DiveMinimum && !emerging) BeginEmergence();
            return;
        }
        submerged = true; emerging = false; acknowledged = false; elapsed = 0;
        actionLock.Acquire(playerManager, false, 1);
        playerManager.MovesController.BeginVaporeonDiveRPC();
        VaporMoveRuntime.Animate(playerManager, "Armature_pm0134_00_ba01_landA01_gfbanm", speed: 3);
        BattleUIManager.instance.ShowMoveSecondaryCooldown(1, VaporMoveRules.DiveDuration);
        // No normal cooldown/resource event until resurfacing. The B slot remains a recast.
    }
    public override void Update()
    {
        if (!submerged) return;
        if (!VaporMoveRuntime.Living(playerManager.Pokemon)) { ResetMove(); return; }
        elapsed += Time.deltaTime;
        acknowledged |= playerManager.MovesController.VaporeonDiving;
        if (!emerging && (elapsed >= VaporMoveRules.DiveDuration
            || (acknowledged && !playerManager.MovesController.VaporeonDiving) || (!acknowledged && elapsed > 1f))) BeginEmergence();
        if (!emerging) return;
        emergeElapsed += Time.deltaTime;
        if (emergeElapsed >= VaporMoveRules.DiveWarning) Complete();
    }
    private void BeginEmergence()
    {
        emerging = true; emergeElapsed = 0;
        recastLock = playerManager.MovesController.AcquireMoveStatus(1, ActionStatusType.Disabled);
        playerManager.PlayerMovement.AddMovementRestriction();
        playerManager.MovesController.EndVaporeonDiveRPC(true);
        VaporMoveRuntime.Animate(playerManager, "Armature_pm0134_00_ba01_landC01_gfbanm", speed: 3);
        BattleUIManager.instance.ShowMoveSecondaryCooldown(1, 0);
        Cooldown = VaporMoveRules.DiveCooldown(IsUpgraded);
        wasMoveSuccessful = true; base.Finish();
    }
    private void Complete()
    {
        if (emerging && playerManager != null) playerManager.PlayerMovement.RemoveMovementRestriction();
        recastLock?.Dispose(); recastLock = null; actionLock.Dispose();
        submerged = false; emerging = false; acknowledged = false; IsActive = false;
        VaporMoveRuntime.ReturnToLocomotion(playerManager);
    }
    public override void Cancel() { base.Cancel(); } // Cancel aim, not an already committed submerge.
    public override void ResetMove()
    {
        if (submerged && playerManager != null && playerManager.IsSpawned)
            playerManager.MovesController.EndVaporeonDiveRPC(false);
        if (submerged && BattleUIManager.instance != null) BattleUIManager.instance.ShowMoveSecondaryCooldown(1, 0);
        Complete(); elapsed = 0; wasMoveSuccessful = false;
    }
}
