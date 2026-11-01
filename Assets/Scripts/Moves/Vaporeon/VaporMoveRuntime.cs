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

public abstract class VaporMoveBase : CombatMove
{
    public abstract VaporCast Kind { get; }
    public override AvailableMoves MoveId => VaporMoveRules.Move(Kind);
    public override MoveAimStyle AimStyle => VaporMoveRules.IsSupport(Kind) ? MoveAimStyle.None
        : Kind == VaporCast.Tempest ? MoveAimStyle.Circle : MoveAimStyle.Ground;
    public override float Range => VaporMoveRules.Range(Kind);
    public override float AreaRadius => Kind == VaporCast.Whirlpool ? VaporMoveRules.WhirlpoolRadius
        : Kind == VaporCast.RainDance ? VaporMoveRules.RainRadius : 1f;
    public override bool IsOffensive => !VaporMoveRules.IsSupport(Kind);
    public override bool LockDuringLoad => !VaporMoveRules.IsSupport(Kind);
    public override CombatTargetRelation TargetRelation => VaporMoveRules.IsSupport(Kind) ? CombatTargetRelation.Ally : CombatTargetRelation.None;
    public override bool TargetOptional => Kind == VaporCast.AquaRing;
    public override bool UsesExplicitUniteCastCheckpoint => Kind == VaporCast.Tempest;

    protected VaporMoveBase() { Cooldown = VaporMoveRules.Cooldown(Kind); }

    public override void Start(PlayerManager controller)
    {
        base.Start(controller);
        if (VaporMoveRules.IsSupport(Kind))
        {
            ActorAim?.InitializeAutoAim(Range, 60, AimTarget.Ally);
            ReadSupportAim();
        }
    }

    private void ReadSupportAim()
    {
        GameObject aimed = ActorAim != null ? ActorAim.SureHitAim() : null;
        target = aimed != null && aimed.TryGetComponent(out PlayerManager ally) && ally.Pokemon != playerManager.Pokemon
            && VaporMoveRuntime.CanHeal(playerManager, ally.Pokemon) ? ally.Pokemon : null;
    }

    public override void Update()
    {
        base.Update();
        if (IsActive && VaporMoveRules.IsSupport(Kind)) ReadSupportAim();
    }

    public override void Finish()
    {
        if (!IsActive || playerManager == null) return;
        if (VaporMoveRules.IsSupport(Kind))
        {
            ReadSupportAim();
            ActorAim?.HideAutoAim();
            if (Kind == VaporCast.HelpingHand && target == null) { base.Cancel(); return; }
        }
        base.Finish();
    }

    public override void Cancel()
    {
        if (IsActive && VaporMoveRules.IsSupport(Kind)) ActorAim?.HideAutoAim();
        base.Cancel();
    }
}
