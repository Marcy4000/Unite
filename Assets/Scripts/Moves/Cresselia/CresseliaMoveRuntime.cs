using System;
using System.Collections.Generic;
using UnityEngine;

// Restrictions are owned by this cast; releasing them cannot clear another cast,
// scoring, stun, or a legacy caller's Disabled flag.
public sealed class CresseliaActionLock
{
    private PlayerManager actor;
    private readonly List<IDisposable> leases = new List<IDisposable>();
    private bool movement;
    public void Acquire(PlayerManager owner, bool restrictMovement, int exceptSlot = -1)
    {
        if (actor != null) return;
        actor = owner; movement = restrictMovement;
        if (movement) actor.PlayerMovement.AddMovementRestriction();
        for (int i = 0; i < 3; i++) if (i != exceptSlot) leases.Add(actor.MovesController.AcquireMoveStatus(i, ActionStatusType.Disabled));
        leases.Add(actor.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Disabled));
        leases.Add(actor.MovesController.BattleItemStatus.AcquireStatus(ActionStatusType.Disabled));
        leases.Add(actor.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
    }
    public void Release()
    {
        if (actor == null) return;
        foreach (IDisposable lease in leases) lease.Dispose();
        leases.Clear();
        if (movement) actor.PlayerMovement.RemoveMovementRestriction();
        if (actor.Pokemon.CurrentHp <= 0) actor.PlayerMovement.CanMove = false;
        actor = null; movement = false;
    }
}

public static class CresseliaMoveRuntime
{
    public static bool Living(Pokemon target) => target != null && target.IsSpawned && target.CurrentHp > 0;
    public static bool CanHit(PlayerManager actor, Pokemon target) => Living(target)
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible)
        && !target.HasStatusEffect(StatusType.Untargetable);
    public static HashSet<Pokemon> Targets(Collider[] colliders)
    {
        var result = new HashSet<Pokemon>();
        foreach (Collider collider in colliders)
        {
            var target = collider.GetComponentInParent<Pokemon>();
            if (target != null) result.Add(target);
        }
        return result;
    }
    public static void Cleanse(Pokemon target)
    {
        // NetworkList mutates synchronously on a host. Never remove while walking it.
        var statuses = new List<StatusEffect>();
        foreach (StatusEffect effect in target.StatusEffects) if (CresseliaMoveRules.IsCleansable(effect)) statuses.Add(effect);
        var stats = new List<StatChange>();
        foreach (StatChange change in target.StatChanges) if (!change.IsBuff && change.IsTimed && change.CanBeRemoved) stats.Add(change);
        foreach (StatusEffect effect in statuses) target.RemoveStatusEffectRPC(effect);
        foreach (StatChange change in stats) target.RemoveStatChangeRPC(change);
    }
    public static void ReturnToLocomotion(PlayerManager actor)
    {
        if (actor == null || actor.Pokemon.CurrentHp <= 0 || actor.Pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts)) return;
        actor.AnimationManager.SetFloat("CresseliaActionSpeed", 1);
        bool walking = actor.PlayerMovement.CanMove && actor.MovementInput.sqrMagnitude > 0.01f;
        actor.AnimationManager.SetBool("Walking", walking);
        actor.AnimationManager.PlayAnimation(walking ? "pm0488_fi21_run01" : "pm0488_ba10_waitA01");
    }
}

public abstract class CresseliaMoveBase : MoveBase
{
    protected abstract CresseliaCast Kind { get; }
    private readonly CresseliaActionLock actionLock = new CresseliaActionLock();
    private Vector3 direction, destination;
    private ulong targetId;
    private bool hasTarget, committed, executing;
    private float elapsed;
    public bool IsExecuting => executing;
    public float RecoveryRemaining => executing ? Mathf.Max(0, CresseliaMoveRules.Windup(Kind) + CresseliaMoveRules.Recovery(Kind) - elapsed) : 0;
    protected CresseliaMoveBase() { Cooldown = CresseliaMoveRules.Cooldown(Kind); }

    public override void Start(PlayerManager controller)
    {
        if (executing) return;
        base.Start(controller);
        if (CresseliaMoveRules.IsSupport(Kind)) ActorAim.InitializeAutoAim(CresseliaMoveRules.Range(Kind), 60, AimTarget.Ally);
        else if (Kind == CresseliaCast.Moonlight) ActorAim.InitializeCircleAreaIndicator(CresseliaMoveRules.Range(Kind));
        else ActorAim.InitializeSkillshotAim(CresseliaMoveRules.Range(Kind));
        ReadAim();
    }
    public void ReadAim()
    {
        if (CresseliaMoveRules.IsSupport(Kind))
        {
            var target = ActorAim.SureHitAim();
            hasTarget = target != null && target != playerManager.gameObject && target.TryGetComponent(out PlayerManager ally)
                && CresseliaMoveRuntime.Living(ally.Pokemon) && ally.CurrentTeam.IsOnSameTeam(playerManager.CurrentTeam);
            targetId = hasTarget ? target.GetComponent<Unity.Netcode.NetworkObject>().NetworkObjectId : 0;
            direction = hasTarget ? target.transform.position - playerManager.transform.position : playerManager.transform.forward;
        }
        else if (Kind == CresseliaCast.Moonlight)
        {
            destination = ActorAim.CircleAreaAim();
            direction = destination - playerManager.transform.position;
        }
        else direction = ActorAim.SkillshotAim();
        direction = CresseliaMoveRules.Direction(direction, playerManager.transform.forward);
    }
    public override void Update()
    {
        if (IsActive && !executing) ReadAim();
        if (!executing) return;
        if (playerManager.Pokemon.CurrentHp <= 0 || playerManager.Pokemon.HasAnyStatusEffect(CresseliaMoveRules.Interrupts)) { ResetMove(); return; }
        elapsed += Time.deltaTime;
        if (!committed && elapsed >= CresseliaMoveRules.Windup(Kind)) Commit();
        if (elapsed >= CresseliaMoveRules.Windup(Kind) + CresseliaMoveRules.Recovery(Kind)) Complete();
    }
    public override void Finish()
    {
        if (!IsActive || executing) return;
        ReadAim(); HideAim(); IsActive = false;
        executing = true; committed = false; elapsed = 0;
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        // Protective spells do not root their caster. Offensive windups remain readable.
        actionLock.Acquire(playerManager, !CresseliaMoveRules.IsSupport(Kind));
        playerManager.AnimationManager.SetFloat("CresseliaActionSpeed", CresseliaMoveRules.IsSupport(Kind) ? 3.5f : 2.5f);
        playerManager.AnimationManager.PlayAnimation(CresseliaMoveRules.IsSupport(Kind) ? "pm0488_ba21_tokusyu02" : "pm0488_ba21_tokusyu01");
        if (CresseliaMoveRules.Windup(Kind) == 0) Commit();
    }
    private void Commit()
    {
        committed = true;
        playerManager.MovesController.CastCresseliaRPC(Kind, direction, destination, targetId, hasTarget);
        wasMoveSuccessful = true;
        base.Finish();
    }
    private void Complete()
    {
        executing = false; committed = false; elapsed = 0;
        actionLock.Release();
        CresseliaMoveRuntime.ReturnToLocomotion(playerManager);
    }
    private void HideAim()
    {
        if (ActorAim == null) return;
        if (CresseliaMoveRules.IsSupport(Kind)) ActorAim.HideAutoAim();
        else if (Kind == CresseliaCast.Moonlight) ActorAim.HideCircleAreaIndicator();
        else ActorAim.HideSkillshotAim();
    }
    public override void Cancel()
    {
        if (IsActive) HideAim();
        base.Cancel();
        if (executing && !committed) Complete();
    }
    public override void ResetMove() { Cancel(); Complete(); }
}
