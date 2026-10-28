using System;
using System.Collections.Generic;
using UnityEngine;

public static class MarshadowMoveRuntime
{
    public static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound };
    public static bool CanHit(PlayerManager actor, Pokemon target) => actor != null && target != null && target.IsSpawned && target.CurrentHp > 0
        && !target.TeamMember.IsOnSameTeam(actor.CurrentTeam) && !target.HasStatusEffect(StatusType.Invincible) && !target.HasStatusEffect(StatusType.Untargetable);
    public static HashSet<Pokemon> Sweep(PlayerManager actor, Vector3 from, Vector3 to, float radius)
    {
        var result = new HashSet<Pokemon>();
        foreach (Collider collider in Physics.OverlapCapsule(from + Vector3.up * 0.7f, to + Vector3.up * 0.7f, radius))
            if (collider.TryGetComponent(out Pokemon target) && CanHit(actor, target)) result.Add(target);
        return result;
    }
    public static int Effect(PlayerManager actor, MarshadowEffect effect, float duration, ulong target = 0, int stage = 0, Vector3? origin = null)
    {
        int id = MarshadowMoveRules.NextCast();
        actor.MovesController.ShowMarshadowEffectRPC(effect, id, origin ?? actor.transform.position, actor.transform.forward, duration, target, stage);
        return id;
    }
    public static void EndEffect(PlayerManager actor, ref int id)
    {
        if (id != 0 && actor != null && actor.IsSpawned) actor.MovesController.EndMarshadowEffectRPC(id);
        id = 0;
    }
    public static void ShowWindow(PlayerManager actor, int slot, float duration)
    {
        if (actor != null && actor.IsLocalHuman && BattleUIManager.instance != null) BattleUIManager.instance.ShowMoveSecondaryCooldown(slot, duration);
    }
    public static void BreakStealth(PlayerManager actor)
    {
        if (actor.MovesController.GetMove(MoveType.MoveB) is MarshadowPhantomForce phantom) phantom.BreakStealth();
    }
    public static void PlayAction(PlayerManager actor, string state, float speed = 1f)
    {
        actor.AnimationManager.SetFloat("MarshadowActionSpeed", speed);
        actor.AnimationManager.PlayAnimation(state);
    }
    public static void ReturnToLocomotion(PlayerManager actor)
    {
        if (actor == null || actor.Pokemon.CurrentHp <= 0 || actor.Pokemon.HasAnyStatusEffect(Interrupts)) return;
        bool moving = actor.PlayerMovement.CanMove && actor.MovementInput.sqrMagnitude > 0.01f;
        actor.AnimationManager.SetBool("Walking", moving);
        actor.AnimationManager.PlayAnimation(moving ? "pm0883_fi21_run01" : "pm0883_ba10_waitA01");
    }
}

public sealed class MarshadowActionLock
{
    private PlayerManager actor;
    private readonly List<IDisposable> leases = new List<IDisposable>();
    private bool movement;
    public void Acquire(PlayerManager owner, bool restrictMovement = true, int exceptSlot = -1)
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
        actor = null; movement = false;
    }
}

public abstract class MarshadowMoveBase : MoveBase
{
    protected readonly MarshadowActionLock ActionLock = new MarshadowActionLock();
    protected Vector3 direction;
    protected float elapsed;
    protected bool executing;
    protected bool committed;
    protected int cast;
    protected int effect;
    protected virtual bool UsesDashAim => false;
    protected virtual bool RequiresMovement => false;
    protected virtual float AimRange => 2.9f;
    public bool IsExecuting => executing;
    public void SetBufferedDirection(Vector3 value) { direction = MarshadowMoveRules.Direction(value, playerManager.transform.forward); }

    public override void Start(PlayerManager controller)
    {
        if (executing || (RequiresMovement && controller.Pokemon.HasStatusEffect(StatusType.Immobilized))) return;
        base.Start(controller);
        if (UsesDashAim) ActorAim.InitializeDashAim(AimRange); else ActorAim.InitializeSkillshotAim(AimRange);
        direction = ReadAim();
    }
    protected Vector3 ReadAim() => MarshadowMoveRules.Direction(UsesDashAim ? ActorAim.DashAim() : ActorAim.SkillshotAim(), playerManager.transform.forward);
    public override void Update()
    {
        if (executing && (playerManager.Pokemon.HasAnyStatusEffect(MarshadowMoveRuntime.Interrupts)
            || (RequiresMovement && playerManager.Pokemon.HasStatusEffect(StatusType.Immobilized)))) { ResetMove(); return; }
        if (IsActive && !executing) direction = ReadAim();
        if (executing) { elapsed += Time.deltaTime; Advance(); }
    }
    protected abstract void Advance();
    protected void Begin(bool lockMovement = true)
    {
        HideAim(); IsActive = false; executing = true; elapsed = 0;
        if (!committed)
        {
            committed = true; cast = MarshadowMoveRules.NextCast();
            (playerManager.PassiveController.Passive as MarshadwPassive)?.OnMoveCommitted();
        }
        MarshadowMoveRuntime.BreakStealth(playerManager);
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        ActionLock.Acquire(playerManager, lockMovement);
    }
    protected void Hit(MarshadowHit hit, int stage = 0, Vector3? from = null, Vector3? to = null, ulong target = 0)
    {
        Vector3 position = playerManager.transform.position;
        playerManager.MovesController.MarshadowHitRPC(hit, cast, stage, from ?? position, to ?? position, direction, IsUpgraded, target);
    }
    protected virtual void Complete()
    {
        if (IsActive) HideAim();
        executing = false; ActionLock.Release();
        MarshadowMoveRuntime.EndEffect(playerManager, ref effect);
        MarshadowMoveRuntime.ReturnToLocomotion(playerManager);
        bool successful = committed; committed = false;
        wasMoveSuccessful = successful; base.Finish();
    }
    protected void HideAim()
    {
        if (ActorAim == null) return;
        if (UsesDashAim) ActorAim.HideDashAim(); else ActorAim.HideSkillshotAim();
    }
    public override void Cancel() { if (IsActive) HideAim(); base.Cancel(); }
    public override void ResetMove()
    {
        if (committed && playerManager != null && playerManager.IsSpawned) playerManager.MovesController.CancelMarshadowCastRPC(cast);
        Cancel(); Complete();
    }
}
