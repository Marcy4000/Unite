using System;
using System.Collections.Generic;
using UnityEngine;

// Owner-side aiming and action leases. Gameplay and effect lifetimes live on the server.
public abstract class FlygonMoveBase : MoveBase
{
    public abstract FlygonCast Kind { get; }
    private readonly List<IDisposable> locks = new List<IDisposable>();
    private IDisposable ownSlot;
    private readonly WallCrossingTraversal traversal = new WallCrossingTraversal();
    private Vector3 direction, destination, leapStep;
    private bool pending, executing, rooted, controlsReleased, recastAim, leapFinished, endRequested;
    private float elapsed;
    private int cast;
    private byte phase;
    public int CastId => cast;
    public bool CanRecast => executing && FlygonMoveRules.Mode(Kind) && phase >= 1 && (Kind == FlygonCast.Dig ? phase == 1 : phase <= 4) && !endRequested;
    public bool CanBuffer => executing && !FlygonMoveRules.Mode(Kind) && !controlsReleased
        && FlygonMoveRules.Recovery(Kind) - elapsed <= FlygonMoveRules.InputBuffer;

    protected FlygonMoveBase() { Cooldown = FlygonMoveRules.Cooldown(Kind); }
    public override void Start(PlayerManager controller)
    {
        if (CanRecast) { recastAim = true; return; }
        if (executing && controlsReleased) Complete(); // A cooldown reset may allow another cast before the old field expires.
        if (pending || executing) return;
        base.Start(controller);
        if (FlygonMoveRules.GroundAim(Kind)) playerManager.Aim.InitializeSizedCircleAreaIndicator(FlygonMoveRules.Range(Kind), Kind == FlygonCast.Earthquake ? FlygonMoveRules.QuakeRadius : FlygonMoveRules.StormRadius);
        else if (FlygonMoveRules.DirectionAim(Kind)) playerManager.Aim.InitializeSkillshotAim(FlygonMoveRules.Range(Kind));
        else playerManager.Aim.InitializeSimpleCircle(FlygonMoveRules.Range(Kind));
        ReadAim();
    }
    private void ReadAim()
    {
        direction = FlygonMoveRules.Direction(FlygonMoveRules.DirectionAim(Kind) ? playerManager.Aim.SkillshotAim() : playerManager.transform.forward, playerManager.transform.forward);
        destination = FlygonMoveRules.GroundAim(Kind) ? playerManager.Aim.CircleAreaAim() : playerManager.transform.position;
        if (FlygonMoveRules.GroundAim(Kind)) direction = FlygonMoveRules.Direction(destination - playerManager.transform.position, playerManager.transform.forward);
    }
    public override void Finish()
    {
        if (recastAim && CanRecast)
        {
            recastAim = false; endRequested = true;
            playerManager.MovesController.EndFlygonModeRPC(cast);
            return;
        }
        if (!IsActive || pending || executing) return;
        ReadAim(); HideAim(); IsActive = false;
        pending = true; elapsed = 0; phase = 0; controlsReleased = false; leapFinished = false; endRequested = false;
        cast = playerManager.MovesController.NextFlygonCast();
        Acquire();
        playerManager.MovesController.CastFlygonRPC(Kind, cast, direction, destination);
    }
    public void Accept(int id, bool accepted)
    {
        if (!pending || cast != id) return;
        pending = false;
        if (!accepted) { Complete(); return; }
        executing = true; elapsed = 0;
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        PlayAnimation();
        if (Kind == FlygonCast.Earthquake)
        {
            destination = FlygonMoveRules.Destination(playerManager.transform.position, destination, FlygonMoveRules.Range(Kind));
            destination = playerManager.PlayerMovement.GetWallCrossingDestination(destination);
            traversal.Begin(playerManager.PlayerMovement);
            leapStep = (destination - playerManager.transform.position) / FlygonMoveRules.QuakeLeap;
        }
        wasMoveSuccessful = true;
        base.Finish(); // Spend energy / start cooldown exactly once, after the server accepted.
    }
    private void PlayAnimation()
    {
        string prefix = playerManager.Pokemon.CurrentLevel >= 6 ? "Armature_pm0330_00_" : playerManager.Pokemon.CurrentLevel >= 3 ? "Armature_pm0329_00_" : "Armature_pm0328_";
        string clip = Kind == FlygonCast.Dig ? "ba41_down01" : Kind == FlygonCast.Boomburst || Kind == FlygonCast.Unite ? "ba02_roar01"
            : Kind == FlygonCast.Supersonic ? "ba21_tokusyu01" : Kind == FlygonCast.Earthquake ? "cm10_bawait_kwwait01" : "ba20_buturi01";
        playerManager.AnimationManager.PlayAnimation(prefix + clip);
    }
    public override void Update()
    {
        if (IsActive) { ReadAim(); return; }
        if (!pending && !executing) return;
        elapsed += Time.deltaTime;
        if (playerManager.Pokemon.CurrentHp <= 0 || (!FlygonMoveRules.Mode(Kind) && !controlsReleased && playerManager.Pokemon.HasAnyStatusEffect(FlygonMoveRules.Interrupts)))
        { ResetMove(); return; }
        if (pending) { if (elapsed >= 10) ResetMove(); return; }
        if (Kind == FlygonCast.Earthquake && !leapFinished)
        {
            float step = Mathf.Min(Time.deltaTime, Mathf.Max(0, FlygonMoveRules.QuakeLeap - (elapsed - Time.deltaTime)));
            traversal.Move(leapStep * step);
            if (elapsed >= FlygonMoveRules.QuakeLeap)
            {
                leapFinished = true;
                traversal.End();
                playerManager.MovesController.FlygonLandingRPC(cast, playerManager.transform.position);
            }
        }
        if (!FlygonMoveRules.Mode(Kind) && elapsed >= FlygonMoveRules.Recovery(Kind)) ReleaseControls();
        if (elapsed > 13) ResetMove(); // Also recovers owner locks after a lost despawn/phase message.
    }
    public void SetPhase(int id, byte value)
    {
        if (id != cast || !executing) return;
        phase = value;
        if (Kind == FlygonCast.Unite && value == 5) { Complete(); return; }
        if (FlygonMoveRules.Mode(Kind) && value == 1)
        {
            ShowModeTimer(Kind == FlygonCast.Dig ? FlygonMoveRules.DigDuration : FlygonMoveRules.UniteDuration);
            ownSlot?.Dispose(); ownSlot = null;
            ReleaseRoot();
            if (Kind == FlygonCast.Dig) playerManager.PlayerMovement.BeginFlygonDig();
            if (Kind == FlygonCast.Unite) playerManager.AnimationManager.PlayAnimation("Tornado");
            else playerManager.AnimationManager.SetTrigger("Transition");
        }
        else if (Kind == FlygonCast.Dig && value == 2)
        {
            ShowModeTimer(0);
            Vector3 exit = playerManager.PlayerMovement.EndFlygonDig();
            playerManager.MovesController.FlygonDigExitRPC(cast, exit);
            if (!rooted) { rooted = true; playerManager.PlayerMovement.AddMovementRestriction(); }
        }
    }
    public void End(int id) { if (cast == id) Complete(); }
    private void Acquire()
    {
        rooted = true; playerManager.PlayerMovement.AddMovementRestriction();
        for (int i = 0; i < 3; i++)
            if (i == FlygonMoveRules.Slot(Kind)) ownSlot = playerManager.MovesController.AcquireMoveStatus(i, ActionStatusType.Disabled);
            else locks.Add(playerManager.MovesController.AcquireMoveStatus(i, ActionStatusType.Disabled));
        locks.Add(playerManager.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Disabled));
        locks.Add(playerManager.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
        locks.Add(playerManager.MovesController.BattleItemStatus.AcquireStatus(ActionStatusType.Disabled));
    }
    private void ReleaseRoot()
    {
        if (rooted && playerManager != null) playerManager.PlayerMovement.RemoveMovementRestriction();
        rooted = false;
    }
    private void ReleaseControls()
    {
        if (controlsReleased) return;
        controlsReleased = true; ownSlot?.Dispose(); ownSlot = null;
        foreach (IDisposable lease in locks) lease.Dispose(); locks.Clear();
        ReleaseRoot();
        if (playerManager != null && playerManager.IsSpawned && playerManager.NetworkManager != null && playerManager.NetworkManager.IsListening
            && playerManager.Pokemon.CurrentHp > 0 && !playerManager.Pokemon.HasAnyStatusEffect(FlygonMoveRules.Interrupts))
        {
            // Tornado intentionally shrinks the model and has no Transition exit.
            if (Kind == FlygonCast.Unite) playerManager.AnimationManager.PlayAnimation(FlygonMoveRules.UniteRecoveryAnimation);
            else playerManager.AnimationManager.SetTrigger("Transition");
        }
    }
    private void Complete()
    {
        traversal.End();
        if (Kind == FlygonCast.Dig && playerManager != null) playerManager.PlayerMovement.EndFlygonDig(playerManager.Pokemon.CurrentHp > 0);
        if (FlygonMoveRules.Mode(Kind)) ShowModeTimer(0);
        ReleaseControls(); pending = executing = recastAim = endRequested = false;
    }
    private void HideAim()
    {
        if (playerManager == null || playerManager.Aim == null) return;
        if (FlygonMoveRules.GroundAim(Kind)) playerManager.Aim.HideCircleAreaIndicator();
        else if (FlygonMoveRules.DirectionAim(Kind)) playerManager.Aim.HideSkillshotAim();
        else playerManager.Aim.HideSimpleCircle();
    }
    public override void Cancel() { if (IsActive) HideAim(); recastAim = false; base.Cancel(); }
    private void ShowModeTimer(float remaining)
    {
        if (!playerManager.IsLocalHuman || BattleUIManager.instance == null) return;
        if (Kind == FlygonCast.Unite) BattleUIManager.instance.ShowUniteMoveSecondaryCooldown(remaining);
        else BattleUIManager.instance.ShowMoveSecondaryCooldown(1, remaining);
    }
    public override void ResetMove()
    {
        Cancel();
        if ((pending || executing) && playerManager != null && playerManager.IsSpawned && playerManager.NetworkManager != null && playerManager.NetworkManager.IsListening)
            playerManager.MovesController.CancelFlygonCastRPC(cast);
        Complete();
    }
}
