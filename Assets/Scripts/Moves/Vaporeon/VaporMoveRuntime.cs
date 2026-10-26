using System;
using System.Collections.Generic;
using UnityEngine;

public static class VaporMoveRuntime
{
    public static bool Living(Pokemon p) => p != null && p.IsSpawned && p.CurrentHp > 0;
    public static bool CanHit(PlayerManager actor, Pokemon p) => Living(p) && !p.TeamMember.IsOnSameTeam(actor.CurrentTeam)
        && !p.HasStatusEffect(StatusType.Invincible) && !p.HasStatusEffect(StatusType.Untargetable);
    // An enemy rejected by CanHit is NEVER implicitly an ally.
    public static bool CanHeal(PlayerManager actor, Pokemon p) => Living(p) && p.Type == PokemonType.Player && p.TeamMember.IsOnSameTeam(actor.CurrentTeam);
    public static HashSet<Pokemon> Targets(Vector3 center, float radius)
    {
        var targets = new HashSet<Pokemon>();
        foreach (Collider hit in Physics.OverlapSphere(center + Vector3.up * 0.6f, radius, LayerMask.GetMask("Players", "WildPokemons")))
        {
            var p = hit.GetComponentInParent<Pokemon>();
            if (p != null) targets.Add(p);
        }
        return targets;
    }
    public static void ReturnToLocomotion(PlayerManager actor)
    {
        if (actor == null || actor.Pokemon.CurrentHp <= 0 || actor.Pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) return;
        if (actor.Pokemon.CurrentLevel >= 3) actor.AnimationManager.SetFloat("VaporeonActionSpeed", 1);
        actor.AnimationManager.SetTrigger("Transition");
    }
    public static void Animate(PlayerManager actor, string vaporeon, string eevee = "ani_leafeonspell1_bat_0133", float speed = 2.5f)
    {
        if (actor.Pokemon.CurrentLevel >= 3) actor.AnimationManager.SetFloat("VaporeonActionSpeed", speed);
        actor.AnimationManager.PlayAnimation(actor.Pokemon.CurrentLevel >= 3 ? vaporeon : eevee);
    }
}

// Each cast owns its restrictions; releasing cannot unlock somebody else's cast/stun/scoring.
public sealed class VaporActionLock : IDisposable
{
    private PlayerManager actor;
    private bool movement;
    private readonly List<IDisposable> leases = new List<IDisposable>();
    public void Acquire(PlayerManager owner, bool root, int exceptSlot = -1)
    {
        if (actor != null) return;
        actor = owner; movement = root;
        if (root) actor.PlayerMovement.AddMovementRestriction();
        for (int i = 0; i < 3; i++) if (i != exceptSlot) leases.Add(actor.MovesController.AcquireMoveStatus(i, ActionStatusType.Disabled));
        leases.Add(actor.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Disabled));
        leases.Add(actor.MovesController.BattleItemStatus.AcquireStatus(ActionStatusType.Disabled));
        leases.Add(actor.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
    }
    public void Dispose()
    {
        if (actor == null) return;
        foreach (IDisposable lease in leases) lease.Dispose();
        leases.Clear();
        if (movement) actor.PlayerMovement.RemoveMovementRestriction();
        if (actor.Pokemon.CurrentHp <= 0) actor.PlayerMovement.CanMove = false;
        actor = null; movement = false;
    }
}

public abstract class VaporMoveBase : MoveBase
{
    public abstract VaporCast Kind { get; }
    private readonly VaporActionLock actionLock = new VaporActionLock();
    private readonly WallCrossingTraversal traversal = new WallCrossingTraversal();
    private Vector3 destination, hopStart;
    private ulong targetId;
    private bool hasTarget, executing, committed;
    private float elapsed;
    private int aimVisual;
    public bool IsExecuting => executing;
    public float RecoveryRemaining => executing ? Mathf.Max(0, VaporMoveRules.Windup(Kind) + VaporMoveRules.Recovery(Kind) - elapsed) : 0;
    protected VaporMoveBase() { Cooldown = VaporMoveRules.Cooldown(Kind); }
    public override void Start(PlayerManager controller)
    {
        if (executing) return;
        base.Start(controller);
        if (VaporMoveRules.IsSupport(Kind)) Aim.Instance.InitializeAutoAim(8, 60, AimTarget.Ally);
        else if (Kind == VaporCast.Tempest) Aim.Instance.InitializeSimpleCircle(VaporMoveRules.TempestRadius);
        else Aim.Instance.InitializeCircleAreaIndicator(VaporMoveRules.Range(Kind));
        ReadAim();
        aimVisual = controller.MovesController.ShowVaporeonAim(Kind, destination, targetId);
    }
    private void ReadAim()
    {
        destination = playerManager.transform.position;
        if (VaporMoveRules.IsSupport(Kind))
        {
            // Aiming into empty ground naturally selects self for Aqua Ring.
            var target = Aim.Instance.SureHitAim();
            hasTarget = target != null && target != playerManager.gameObject && target.TryGetComponent(out PlayerManager ally)
                && VaporMoveRuntime.CanHeal(playerManager, ally.Pokemon);
            targetId = hasTarget ? target.GetComponent<Unity.Netcode.NetworkObject>().NetworkObjectId
                : Kind == VaporCast.AquaRing ? playerManager.NetworkObjectId : ulong.MaxValue;
        }
        else if (Kind != VaporCast.Tempest)
            destination = VaporMoveRules.ClampDestination(playerManager.transform.position, Aim.Instance.CircleAreaAim(), VaporMoveRules.Range(Kind));
    }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        ReadAim(); HideAim(); IsActive = false;
        // Helping Hand deliberately retains its original no-target/no-cost behavior.
        if (Kind == VaporCast.HelpingHand && !hasTarget) { wasMoveSuccessful = false; base.Finish(); return; }
        if (Kind == VaporCast.RainDance) destination = playerManager.PlayerMovement.VaporeonHopDestination(destination);
        hopStart = playerManager.transform.position;
        executing = true; committed = false; elapsed = 0;
        actionLock.Acquire(playerManager, !VaporMoveRules.IsSupport(Kind));
        if (Kind == VaporCast.RainDance) traversal.Begin(playerManager.PlayerMovement);
        playerManager.transform.rotation = Quaternion.LookRotation(VaporMoveRules.Direction(destination - hopStart, playerManager.transform.forward));
        VaporMoveRuntime.Animate(playerManager, Kind == VaporCast.Tempest ? "Armature_pm0134_00_ba02_roar01_gfbanm"
            : Kind == VaporCast.RainDance ? "Armature_pm0134_00_kw35_playA01_gfbanm" : "Armature_pm0134_00_ba21_tokusyu01_gfbanm");
        // Movement is a benefit too: spend Rain's cooldown before moving, not
        // after landing (otherwise cancelling the hop grants free mobility).
        if (VaporMoveRules.CommitOnStart(Kind)) Commit();
    }
    public override void Update()
    {
        if (IsActive && !executing)
        {
            ReadAim(); playerManager.MovesController.UpdateVaporeonAim(aimVisual, destination, targetId); return;
        }
        if (!executing) return;
        if (!VaporMoveRuntime.Living(playerManager.Pokemon) || playerManager.Pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) { ResetMove(); return; }
        elapsed += Time.deltaTime;
        if (Kind == VaporCast.RainDance && elapsed - Time.deltaTime < VaporMoveRules.Windup(Kind))
        {
            Vector3 next = Vector3.Lerp(hopStart, destination, Mathf.Clamp01(elapsed / VaporMoveRules.Windup(Kind)));
            traversal.Move(next - playerManager.transform.position);
            if (elapsed >= VaporMoveRules.Windup(Kind)) traversal.End();
        }
        if (!committed && elapsed >= VaporMoveRules.Windup(Kind)) Commit();
        if (elapsed >= VaporMoveRules.Windup(Kind) + VaporMoveRules.Recovery(Kind)) Complete();
    }
    private void Commit()
    {
        committed = true;
        playerManager.MovesController.CastVaporeonRPC(Kind, destination, targetId, hasTarget);
        wasMoveSuccessful = true; base.Finish();
    }
    private void Complete()
    {
        traversal.End();
        executing = false; committed = false; elapsed = 0; actionLock.Dispose();
        VaporMoveRuntime.ReturnToLocomotion(playerManager);
    }
    private void HideAim()
    {
        if (playerManager != null) playerManager.MovesController.StopVaporeonLocalEffect(aimVisual);
        aimVisual = 0;
        if (Aim.Instance == null) return;
        if (VaporMoveRules.IsSupport(Kind)) Aim.Instance.HideAutoAim();
        else if (Kind == VaporCast.Tempest) Aim.Instance.HideSimpleCircle();
        else Aim.Instance.HideCircleAreaIndicator();
    }
    public override void Cancel()
    {
        if (IsActive) HideAim();
        base.Cancel();
        if (executing && !committed) Complete();
    }
    public override void ResetMove() { HideAim(); base.Cancel(); Complete(); }
}
