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

public abstract class CresseliaMoveBase : CombatMove
{
    protected abstract CresseliaCast Kind { get; }
    public CresseliaCast CastKind => Kind;
    public override AvailableMoves MoveId => CresseliaMoveRules.Move(Kind);
    public override string PrefabPath => null;
    public override MoveAimStyle AimStyle => CresseliaMoveRules.IsSupport(Kind)
        ? MoveAimStyle.None
        : Kind == CresseliaCast.Moonlight
            ? MoveAimStyle.Ground
            : MoveAimStyle.Direction;
    public override float Range => CresseliaMoveRules.Range(Kind);
    public override float AreaRadius => Kind == CresseliaCast.Moonlight ? CresseliaMoveRules.MoonlightRadius(IsUpgraded) : 1f;
    public override bool IsOffensive => !CresseliaMoveRules.IsSupport(Kind) && Kind != CresseliaCast.Moonlight;
    public override bool LockDuringLoad => !CresseliaMoveRules.IsSupport(Kind);
    public override CombatTargetRelation TargetRelation => CresseliaMoveRules.IsSupport(Kind) ? CombatTargetRelation.Ally : base.TargetRelation;
    public override bool TargetOptional => CresseliaMoveRules.IsSupport(Kind);

    protected CresseliaMoveBase()
    {
        Cooldown = CresseliaMoveRules.Cooldown(Kind);
    }

    public override void Start(PlayerManager controller)
    {
        _ = controller?.MovesController?.CresseliaRuntime;
        base.Start(controller);
        if (CresseliaMoveRules.IsSupport(Kind))
        {
            ActorAim?.InitializeAutoAim(Range, 60, AimTarget.Ally);
            ReadAim();
        }
    }

    public void ReadAim()
    {
        if (CresseliaMoveRules.IsSupport(Kind))
        {
            GameObject aimed = ActorAim != null ? ActorAim.SureHitAim() : null;
            if (aimed != null && playerManager != null && aimed != playerManager.gameObject
                && aimed.TryGetComponent(out PlayerManager ally)
                && CresseliaMoveRuntime.Living(ally.Pokemon)
                && ally.CurrentTeam.IsOnSameTeam(playerManager.CurrentTeam))
            {
                target = ally.Pokemon;
                direction = CresseliaMoveRules.Direction(aimed.transform.position - playerManager.transform.position, playerManager.transform.forward);
            }
            else
            {
                target = null;
                direction = playerManager != null
                    ? CresseliaMoveRules.Direction(playerManager.transform.forward, Vector3.forward)
                    : Vector3.forward;
            }
        }
        else
        {
            UpdateAim();
        }
    }

    public override void Update()
    {
        base.Update();
        if (IsActive && CresseliaMoveRules.IsSupport(Kind))
            ReadAim();
    }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        if (CresseliaMoveRules.IsSupport(Kind))
        {
            ReadAim();
            ActorAim?.HideAutoAim();
        }
        base.Finish();
    }

    public override void Cancel()
    {
        if (IsActive && CresseliaMoveRules.IsSupport(Kind))
            ActorAim?.HideAutoAim();
        base.Cancel();
    }

    public override void ResetMove()
    {
        if (IsActive && CresseliaMoveRules.IsSupport(Kind))
            ActorAim?.HideAutoAim();
        base.ResetMove();
    }
}
