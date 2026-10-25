using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SerperiorBasicAtk : BasicAttackBase
{
    private const float DashDuration = 0.35f, DashCooldown = 7.5f;
    private static readonly StatusType[] Interrupts = { StatusType.Incapacitated, StatusType.Asleep, StatusType.Frozen, StatusType.Bound, StatusType.Immobilized };
    private DamageInfo normalDamage, boostedDamage;
    public bool nextAttackDashes = false;
    private float dashCooldownTimer = DashCooldown;
    private Coroutine dashCoroutine;
    private readonly List<IDisposable> leases = new List<IDisposable>();
    private bool rooted, subscribed, dashing;
    private int dashGeneration;
    public bool IsDashing => dashing;

    public override void Initialize(PlayerManager manager)
    {
        Dispose(); base.Initialize(manager);
        playerManager.Pokemon.OnEvolution += OnEvolution;
        playerManager.Pokemon.OnDeath += OnDeath; subscribed = true;
        OnEvolution();
        DamageProprieties flags = DamageProprieties.IsBasicAttack | DamageProprieties.CanCrit;
        normalDamage = new DamageInfo(manager.NetworkObjectId, 0.45f, 4, 130, DamageType.Physical, flags);
        boostedDamage = new DamageInfo(manager.NetworkObjectId, 0.65f, 6, 175, DamageType.Physical, flags);
        if (manager.HPBar != null) { manager.HPBar.ShowGenericGuage(true); manager.HPBar.UpdateGenericGuageValue(0); }
    }
    private void OnEvolution() { range = playerManager.Pokemon.CurrentLevel >= 6 ? 3.25f : playerManager.Pokemon.CurrentLevel >= 4 ? 2.75f : 2.25f; }
    public override void Perform(bool wildPriority)
    {
        if (dashing || playerManager.Pokemon.CurrentHp <= 0) return;
        GameObject closest = Aim.Instance.AimInCircle(range, wildPriority ? PokemonType.Wild : PokemonType.Player);
        if (closest != null)
        {
            Vector3 facing = closest.transform.position - playerManager.transform.position; facing.y = 0;
            if (facing.sqrMagnitude > 0.001f) playerManager.transform.rotation = Quaternion.LookRotation(facing);
        }
        else if (playerManager.PlayerMovement.IsMoving) return;
        float scale = range / 2.5f;
        Vector3 origin = playerManager.transform.position;
        var targets = Aim.Instance.AimInCircleAtPosition(origin + playerManager.transform.forward * (1.377f * scale), scale, AimTarget.NonAlly);
        bool boosted = nextAttackDashes;
        Pokemon dashTarget = null;
        foreach (GameObject hit in targets)
        {
            if (!hit.TryGetComponent(out Pokemon target) || !CombatTargeting.WithinMeleeRange(origin, target.transform.position, range)) continue;
            target.TakeDamageRPC(boosted ? boostedDamage : normalDamage);
            if (boosted && dashTarget == null) dashTarget = target;
        }
        if (dashTarget != null)
        {
            nextAttackDashes = false;
            Vector3 delta = dashTarget.transform.position - origin; delta.y = 0;
            StartDash(delta, delta.magnitude);
        }
        // The dash owns its recovery. Do not also start an untracked timer that
        // could release a movement restriction after death/reset cancelled it.
        if (!dashing) playerManager.StopMovementForTime(0.5f * playerManager.MovesController.GetAtkSpeedCooldown(), false);
    }
    private void StartDash(Vector3 direction, float distance)
    {
        if (playerManager == null || dashing || playerManager.Pokemon.CurrentHp <= 0 || playerManager.Pokemon.HasAnyStatusEffect(Interrupts)
            || !CombatTargeting.Finite(direction) || !float.IsFinite(distance)) return;
        direction.y = 0;
        distance = Mathf.Clamp(distance, 0, range);
        if (direction.sqrMagnitude < 0.001f || distance <= 0) return;
        dashing = true;
        int generation = ++dashGeneration;
        dashCoroutine = playerManager.StartCoroutine(DoDash(direction.normalized, distance, generation));
    }
    private IEnumerator DoDash(Vector3 direction, float distance, int generation)
    {
        Acquire();
        playerManager.transform.rotation = Quaternion.LookRotation(direction);
        float elapsed = 0;
        try
        {
            while (elapsed < DashDuration && generation == dashGeneration && playerManager != null && playerManager.Pokemon.CurrentHp > 0
                && !playerManager.Pokemon.HasAnyStatusEffect(Interrupts) && !playerManager.PlayerMovement.IsKnockedUp)
            {
                float step = Mathf.Min(Time.deltaTime, DashDuration - elapsed); elapsed += step;
                Vector3 displacement = direction * (distance * step / DashDuration);
                Vector3 actual = playerManager.PlayerMovement.MoveWithWalls(displacement);
                if (displacement.sqrMagnitude > 0.0001f && actual.sqrMagnitude < displacement.sqrMagnitude * 0.02f) break;
                yield return null;
            }
        }
        finally { if (generation == dashGeneration) { dashCoroutine = null; Release(); } }
    }
    private void Acquire()
    {
        rooted = true; playerManager.PlayerMovement.AddMovementRestriction();
        for (int i = 0; i < 3; i++) leases.Add(playerManager.MovesController.AcquireMoveStatus(i, ActionStatusType.Busy));
        leases.Add(playerManager.MovesController.BasicAttackStatus.AcquireStatus(ActionStatusType.Busy));
        leases.Add(playerManager.MovesController.BattleItemStatus.AcquireStatus(ActionStatusType.Busy));
        leases.Add(playerManager.ScoreStatus.AcquireStatus(ActionStatusType.Busy));
    }
    private void Release()
    {
        foreach (IDisposable lease in leases) lease.Dispose(); leases.Clear();
        if (rooted && playerManager != null && playerManager.PlayerMovement != null) playerManager.PlayerMovement.RemoveMovementRestriction();
        rooted = dashing = false;
        if (playerManager != null && playerManager.Pokemon != null && playerManager.Pokemon.CurrentHp <= 0 && playerManager.PlayerMovement != null) playerManager.PlayerMovement.CanMove = false;
    }
    private void OnDeath(DamageInfo damage) { ResetAttack(); }
    public override void ResetAttack()
    {
        dashGeneration++;
        if (dashCoroutine != null && playerManager != null) playerManager.StopCoroutine(dashCoroutine);
        dashCoroutine = null; Release(); nextAttackDashes = false; dashCooldownTimer = DashCooldown;
    }
    public override void Dispose()
    {
        ResetAttack();
        if (subscribed && playerManager != null && playerManager.Pokemon != null)
        { playerManager.Pokemon.OnEvolution -= OnEvolution; playerManager.Pokemon.OnDeath -= OnDeath; }
        subscribed = false; playerManager = null;
    }
    public override void Update()
    {
        if (playerManager == null || playerManager.Pokemon.CurrentHp <= 0) return;
        if (!nextAttackDashes)
        {
            dashCooldownTimer = Mathf.Max(0, dashCooldownTimer - Time.deltaTime);
            if (dashCooldownTimer <= 0) { nextAttackDashes = true; dashCooldownTimer = DashCooldown; }
        }
        if (playerManager.HPBar != null) playerManager.HPBar.UpdateGenericGuageValue(nextAttackDashes ? 1 : 1 - dashCooldownTimer / DashCooldown);
    }
}
