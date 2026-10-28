using UnityEngine;

public partial class MovesController
{
    private int meowsticBufferedSlot = -1;
    private bool meowsticBufferedRelease;
    private float meowsticBufferUntil;
    private double meowsticShownReflect;
    private bool meowsticBasicRoot;
    private float meowsticBasicUntil;
    public void BeginMeowsticBasicRecovery()
    {
        meowsticBasicUntil = Time.time + 0.22f * GetAtkSpeedCooldown();
        if (meowsticBasicRoot) return;
        meowsticBasicRoot = true; playerManager.PlayerMovement.AddMovementRestriction();
    }
    public void EndMeowsticBasicRecovery(bool transition)
    {
        if (!meowsticBasicRoot) return;
        meowsticBasicRoot = false;
        if (playerManager != null) playerManager.PlayerMovement.RemoveMovementRestriction();
        if (transition && pokemon.CurrentHp > 0 && !pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts)) playerManager.AnimationManager.SetTrigger("Transition");
    }
    private bool CanRecastMeowstic(int slot)
    {
        return slot == 0 && moves[slot] is MeowsticMReflect && MeowsticReflectAvailable
            && (moveStatuses[slot].GetStatus() & ~ActionStatusType.Cooldown) == ActionStatusType.None;
    }
    private void BufferMeowsticInput(int slot, bool released)
    {
        if (!IsMeowstic || playerManager.PlayerState != PlayerState.Alive) return;
        if (released) { if (meowsticBufferedSlot == slot) meowsticBufferedRelease = true; return; }
        bool reflect = slot == 0 && moves[slot] is MeowsticMReflect && MeowsticReflectAvailable;
        if (moveStatuses[slot].HasStatus(ActionStatusType.Stunned) || (moveStatuses[slot].HasStatus(ActionStatusType.Cooldown) && !reflect)) return;
        bool recovering = false;
        foreach (MoveBase move in moves) if (move is MeowsticMoveBase cast && cast.IsExecuting && cast.RecoveryRemaining <= 0.18f) recovering = true;
        if (uniteMove is MeowsticMoveBase unite && unite.IsExecuting && unite.RecoveryRemaining <= 0.18f) recovering = true;
        if (!recovering) return;
        meowsticBufferedSlot = slot; meowsticBufferedRelease = false; meowsticBufferUntil = Time.time + 0.22f;
    }
    private void ProcessMeowsticBufferedInput()
    {
        if (meowsticBasicRoot && Time.time >= meowsticBasicUntil) EndMeowsticBasicRecovery(true);
        if (IsMeowstic && meowsticShownReflect != meowsticReflectUntil.Value)
        {
            meowsticShownReflect = meowsticReflectUntil.Value;
            if (playerManager.IsLocalHuman && BattleUIManager.instance != null) BattleUIManager.instance.ShowMoveSecondaryCooldown(0, MeowsticReflectRemaining);
        }
        if (meowsticBufferedSlot < 0) return;
        if (!IsMeowstic || Time.time > meowsticBufferUntil || (playerManager.IsLocalHuman && controls.Movement.CancelMove.WasPressedThisFrame()) || pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts)) { ClearMeowsticBufferedInput(); return; }
        int slot = meowsticBufferedSlot;
        if (!moveStatuses[slot].HasStatus(ActionStatusType.None) && !CanRecastMeowstic(slot)) return;
        bool release = meowsticBufferedRelease; ClearMeowsticBufferedInput();
        moves[slot].Start(playerManager); if (release) moves[slot].Finish();
    }
    private void ClearMeowsticBufferedInput() { meowsticBufferedSlot = -1; meowsticBufferedRelease = false; }
}
