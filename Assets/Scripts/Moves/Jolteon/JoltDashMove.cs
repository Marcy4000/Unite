using System.Collections.Generic;
using UnityEngine;

// Shared lifecycle for Fang and Discharge. A pass commits once; only the idle
// recast window expires. Neither aiming nor overcharge can create a third pass.
public abstract class JoltDashMove : MoveBase
{
    protected readonly JoltRecastState Combo = new JoltRecastState();
    private readonly JoltActionLock actionLock = new JoltActionLock();
    private readonly WallCrossingTraversal traversal = new WallCrossingTraversal();
    private readonly HashSet<ulong> directHits = new HashSet<ulong>();
    private readonly HashSet<ulong> slowedTargets = new HashSet<ulong>();
    private Vector3 direction;
    private float startup, distanceRemaining, travelSpeed, elapsed, blockedTime, tickRemaining;
    private bool executing, committed, hitAnything;
    private int effect;
    public bool IsExecuting => executing;
    protected abstract bool IsFang { get; }

    public override void Start(PlayerManager controller)
    {
        if (executing) return;
        if (Combo.Expired(Time.time)) { CompleteSequence(); return; }
        base.Start(controller);
        Aim.Instance.InitializeDashAim(PreviewRange(controller));
        direction = Aim.Instance.DashAim();
    }

    private float PreviewRange(PlayerManager controller)
    {
        if (IsFang) return JoltMoveRules.FangRange;
        bool empowered = Combo.HasWindow ? Combo.Empowered : (controller.PassiveController.Passive as JoltPassive)?.IsPassiveReady == true;
        float range = empowered ? JoltMoveRules.BoostedDischargeRange : JoltMoveRules.DischargeRange;
        return Combo.HasWindow ? range * JoltMoveRules.RecastScale : range;
    }

    public override void Update()
    {
        if (executing && playerManager.Pokemon.HasAnyStatusEffect(JoltMoveRuntime.MovementInterrupts))
        {
            ResetMove();
            return;
        }
        if (executing) AdvancePass();
        else if (Combo.Expired(Time.time))
        {
            if (IsActive) Aim.Instance.HideDashAim();
            CompleteSequence();
        }
        if (IsActive) direction = Aim.Instance.DashAim();
    }

    public override void Finish()
    {
        if (!IsActive || executing) return;
        Aim.Instance.HideDashAim();
        if (direction.sqrMagnitude < 0.001f) { base.Cancel(); return; }
        if (Combo.HasWindow)
        {
            if (!Combo.TryRecast(Time.time)) { CompleteSequence(); return; }
        }
        else Combo.Begin((playerManager.PassiveController.Passive as JoltPassive)?.TryConsumeCharge() == true);

        IsActive = false;
        committed = true;
        executing = true;
        hitAnything = false;
        directHits.Clear();
        slowedTargets.Clear();
        elapsed = blockedTime = 0;
        tickRemaining = 0f;
        startup = IsFang ? JoltMoveRules.FangStartup : 0f;
        float range = IsFang ? JoltMoveRules.FangRange : (Combo.Empowered ? JoltMoveRules.BoostedDischargeRange : JoltMoveRules.DischargeRange);
        if (!IsFang && Combo.IsSecondPass) range *= JoltMoveRules.RecastScale;
        distanceRemaining = range;
        travelSpeed = IsFang ? range / JoltMoveRules.FangDuration
            : playerManager.Pokemon.GetSpeed() / 1000f * (Combo.IsSecondPass ? 3.5f : 2.5f);
        if (IsFang) distanceRemaining = Vector3.Distance(playerManager.transform.position,
            playerManager.PlayerMovement.GetWallCrossingDestination(playerManager.transform.position + direction.normalized * range));
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        actionLock.Acquire(playerManager);
        if (IsFang) traversal.Begin(playerManager.PlayerMovement);
        if (IsFang) playerManager.AnimationManager.PlayAnimation("pm0135_00_ba20_thunderFang");
        else playerManager.AnimationManager.SetBool("Walking", true);
        effect = JoltMoveRuntime.Effect(playerManager, IsFang ? JoltEffect.ThunderFang : JoltEffect.Discharge, Combo.Empowered, IsUpgraded, Combo.IsSecondPass);
        ShowWindow(0);
    }

    private void AdvancePass()
    {
        float delta = Time.deltaTime;
        if (delta <= 0) return;
        if (startup > 0)
        {
            float wait = Mathf.Min(startup, delta);
            startup -= wait;
            delta -= wait;
            if (delta <= 0) return;
        }
        Vector3 from = playerManager.transform.position;
        // Charge's brief preparation slow must not be snapshotted into the
        // entire rush. Web's on-hit movement bonus can accelerate it mid-pass.
        if (!IsFang) travelSpeed = playerManager.Pokemon.GetSpeed() / 1000f * (Combo.IsSecondPass ? 3.5f : 2.5f);
        float requested = Mathf.Min(distanceRemaining, travelSpeed * delta);
        Vector3 movement = IsFang ? traversal.Move(direction * requested) : playerManager.PlayerMovement.MoveWithWalls(direction * requested);
        float actual = new Vector2(movement.x, movement.z).magnitude;
        distanceRemaining -= actual;
        elapsed += delta;
        blockedTime = actual < requested * 0.1f ? blockedTime + delta : 0f;
        Vector3 offset = IsFang ? direction * 0.8f : Vector3.zero;
        foreach (Pokemon target in JoltMoveRuntime.Sweep(playerManager, from + offset, playerManager.transform.position + offset, IsFang ? 1f : 0.75f))
        {
            if (!directHits.Add(target.NetworkObjectId)) continue;
            hitAnything = true;
            DamageInfo damage = IsFang
                ? new DamageInfo(playerManager.NetworkObjectId, Combo.Empowered ? 1.9f : 1.72f, (short)(Combo.Empowered ? 10 : 9), (short)(Combo.Empowered ? 400 : 350), DamageType.Physical, JoltMoveRules.DirectMoveDamage)
                : new DamageInfo(playerManager.NetworkObjectId, 1.6f, 6, 300, DamageType.Physical, JoltMoveRules.DirectMoveDamage);
            target.TakeDamageRPC(damage);
            target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, IsFang ? 0.5f : 0.35f, true, 0));
            if (!IsFang && Combo.IsSecondPass) Slow(target);
        }

        if (!IsFang)
        {
            float radius = JoltMoveRules.DischargeRadius * (Combo.IsSecondPass ? JoltMoveRules.RecastScale : 1f);
            if (Combo.IsSecondPass)
                foreach (Pokemon target in JoltMoveRuntime.Sweep(playerManager, from, playerManager.transform.position, radius)) Slow(target);
            tickRemaining -= delta;
            if (tickRemaining <= 0)
            {
                tickRemaining += JoltMoveRules.DischargeTick;
                foreach (Pokemon target in JoltMoveRuntime.Sweep(playerManager, playerManager.transform.position, playerManager.transform.position, radius))
                {
                    target.TakeDamageRPC(new DamageInfo(playerManager.NetworkObjectId, 1.3f, 3, 150, DamageType.Physical, JoltMoveRules.PeriodicDamage));
                    if (Combo.IsSecondPass) Slow(target);
                }
            }
        }
        if (distanceRemaining <= 0.02f || blockedTime >= 0.06f || elapsed >= (IsFang ? JoltMoveRules.FangDuration : 2f)) EndPass();
    }

    private void Slow(Pokemon target)
    {
        if (!slowedTargets.Add(target.NetworkObjectId)) return;
        JoltMoveRuntime.RefreshStat(target, new StatChange((short)(IsUpgraded ? 35 : 20), Stat.Speed, 1f, true, false, true, JoltMoveRules.DischargeSlow));
    }

    private void EndPass()
    {
        executing = false;
        traversal.End();
        actionLock.Release();
        JoltMoveRuntime.EndEffect(playerManager, ref effect);
        if (IsFang && hitAnything)
            JoltMoveRuntime.RefreshStat(playerManager.Pokemon, new StatChange(60, Stat.Speed, IsUpgraded ? 2f : 1.5f, true, true, true, JoltMoveRules.FangSpeed));
        if (!Combo.IsSecondPass && (IsFang ? Combo.Empowered : hitAnything))
        {
            float duration = IsFang ? JoltMoveRules.FangWindow : JoltMoveRules.DischargeWindow;
            Combo.Open(Time.time, duration);
            ShowWindow(duration);
        }
        else CompleteSequence();
    }

    private void ShowWindow(float duration)
    {
        if (BattleUIManager.instance != null) BattleUIManager.instance.ShowMoveSecondaryCooldown(0, duration);
    }

    private void CompleteSequence()
    {
        Combo.Clear();
        ShowWindow(0);
        bool successful = committed;
        committed = false;
        wasMoveSuccessful = successful;
        base.Finish();
    }

    public override void Cancel()
    {
        // Cancelling aim does not erase an already earned recast or stop a
        // committed pass. Death/interruption uses ResetMove instead.
        if (IsActive) Aim.Instance.HideDashAim();
        base.Cancel();
    }

    public override void ResetMove()
    {
        Cancel();
        executing = false;
        traversal.End();
        actionLock.Release();
        JoltMoveRuntime.EndEffect(playerManager, ref effect);
        directHits.Clear();
        slowedTargets.Clear();
        CompleteSequence();
    }
}
