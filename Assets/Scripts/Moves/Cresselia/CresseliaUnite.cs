using UnityEngine;

public class CresseliaUnite : MoveBase
{
    private readonly CresseliaActionLock actionLock = new CresseliaActionLock();
    private int phase;
    private bool movementHeld, recastRequested;
    private double started;
    private Vector3 takeoff, landing, descent;
    private bool oldSnap, oldFlying, oldKnockback;
    private double Now => playerManager.NetworkManager.ServerTime.Time;
    public bool CanRecast => phase == 1 && !recastRequested && Now >= started + CresseliaMoveRules.TakeoffDuration;
    public CresseliaUnite() { Name = "Crescent Crash"; Cooldown = 0; }

    public override void Start(PlayerManager controller)
    {
        if (phase != 0 && !CanRecast) return;
        base.Start(controller);
        ActorAim.InitializeSimpleCircle(CresseliaMoveRules.UniteRadius);
    }
    public override void Finish()
    {
        if (!IsActive) return;
        ActorAim.HideSimpleCircle();
        if (phase == 0)
        {
            oldSnap = playerManager.PlayerMovement.SnapToGround;
            oldFlying = playerManager.PlayerMovement.IsFlying;
            oldKnockback = playerManager.PlayerMovement.CanBeKnockedBack;
            takeoff = playerManager.transform.position;
            phase = 1; started = Now; recastRequested = false;
            actionLock.Acquire(playerManager, false, 2);
            HoldMovement();
            playerManager.PlayerMovement.SnapToGround = false;
            playerManager.PlayerMovement.CanBeKnockedBack = false;
            playerManager.AnimationManager.SetFloat("CresseliaActionSpeed", 1.5f);
            playerManager.AnimationManager.PlayAnimation("pm0488_ba02_roar01");
            if (playerManager.IsLocalHuman) BattleUIManager.instance.ShowUniteMoveSecondaryCooldown(CresseliaMoveRules.FlightDuration);
            wasMoveSuccessful = true;
            base.Finish(); // Commit energy exactly once, at takeoff.
            playerManager.MovesController.BeginCresseliaUniteRPC();
        }
        else if (CanRecast)
        {
            recastRequested = true;
            playerManager.MovesController.RecastCresseliaUniteRPC();
            IsActive = false;
        }
    }
    public void ApplyServerPhase(int value, Vector3 point, double time)
    {
        if (value <= 0)
        {
            if (phase == 0) return;
            if (value == 0 && playerManager.Pokemon.CurrentHp > 0) playerManager.transform.position = point;
            Cleanup(); return;
        }
        if (phase == 0) return; // A reset/death must not resurrect a delayed flight RPC.
        started = time;
        if (value == 1) takeoff = point;
        else
        {
            landing = point; descent = playerManager.transform.position;
            HoldMovement();
            playerManager.AnimationManager.SetFloat("CresseliaActionSpeed", 2);
            playerManager.AnimationManager.PlayAnimation("pm0488_ba01_land01");
        }
        phase = value;
        Update();
    }
    public override void Update()
    {
        if (phase == 0) return;
        if (playerManager.Pokemon.CurrentHp <= 0) { ResetMove(); return; }
        float elapsed = Mathf.Max(0, (float)(Now - started));
        if (phase == 1)
        {
            Vector3 position = playerManager.transform.position;
            position.y = Mathf.Max(position.y, takeoff.y + Mathf.SmoothStep(0, 3, elapsed / CresseliaMoveRules.TakeoffDuration));
            playerManager.transform.position = position;
            if (elapsed >= CresseliaMoveRules.TakeoffDuration && movementHeld)
            {
                ReleaseMovement();
                playerManager.PlayerMovement.IsFlying = true;
                playerManager.AnimationManager.PlayAnimation("pm0488_ba10_waitB01");
            }
        }
        else playerManager.transform.position = Vector3.Lerp(descent, landing, Mathf.Clamp01(elapsed / CresseliaMoveRules.LandingDuration));
    }
    private void HoldMovement()
    {
        if (movementHeld) return;
        movementHeld = true; playerManager.PlayerMovement.AddMovementRestriction();
    }
    private void ReleaseMovement()
    {
        if (!movementHeld) return;
        movementHeld = false; playerManager.PlayerMovement.RemoveMovementRestriction();
    }
    private void Cleanup()
    {
        if (IsActive && ActorAim != null) ActorAim.HideSimpleCircle();
        phase = 0; recastRequested = false; IsActive = false;
        ReleaseMovement(); actionLock.Release();
        playerManager.PlayerMovement.SnapToGround = oldSnap;
        playerManager.PlayerMovement.IsFlying = oldFlying;
        playerManager.PlayerMovement.CanBeKnockedBack = oldKnockback;
        if (playerManager.Pokemon.CurrentHp <= 0) playerManager.PlayerMovement.CanMove = false;
        if (playerManager.IsLocalHuman) BattleUIManager.instance.ShowUniteMoveSecondaryCooldown(0);
        CresseliaMoveRuntime.ReturnToLocomotion(playerManager);
    }
    public override void Cancel()
    {
        if (IsActive && ActorAim != null) ActorAim.HideSimpleCircle();
        base.Cancel(); // Cancel aiming, not an already committed Unite.
    }
    public override void ResetMove()
    {
        Cancel();
        if (phase == 0 || playerManager == null) return;
        if (playerManager.IsSpawned) playerManager.MovesController.CancelCresseliaUniteRPC();
        Cleanup();
    }
}
