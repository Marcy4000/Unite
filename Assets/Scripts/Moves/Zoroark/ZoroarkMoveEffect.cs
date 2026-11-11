using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>Server-owned cast/chain. Authored prefab children provide all effect art.</summary>
public class ZoroarkMoveEffect : PrefabMoveEffect
{
    [SerializeField] private AvailableMoves move;
    [SerializeField] private GameObject dashVisual, markVisual;
    private CombatActionLock phaseLock;
    private bool busy, ready, requested, dashReady, uniqueReady, finishing, hitThisCast, lastWasBasic;
    private int steps;
    private float expires;
    private Vector3 recastDirection, mark;
    private Pokemon finisherTarget;
    private bool normalMovePending;
    private bool aerialRepositionSpent;
    private float aerialUntil;
    private bool aerialWindow;
    private bool airSteering;
    private double airSteeringUntil, airSteeringPausedUntil;
    private float airSteeringDistance;
    public bool AerialCast { get; set; }
    public bool AerialWindow => aerialWindow;
    public float RemainingAirTime => Mathf.Max(0, aerialUntil - Time.time);
    public bool IsNight => move == AvailableMoves.ZoroarkNightSlash;
    public bool IsFeint => move == AvailableMoves.ZoroarkFeintAttack;
    public bool Finishing => finishing;
    public bool UniqueReady => uniqueReady;
    public bool TrySpendAerialReposition()
    {
        if (!IsServer || !IsNight || !aerialWindow || aerialRepositionSpent) return false;
        aerialRepositionSpent = true;
        return true;
    }

    protected override void BeginPresentation()
    {
        if (dashVisual != null) dashVisual.SetActive(false);
        if (markVisual != null) markVisual.SetActive(false);
    }

    public override bool TryRecast(Vector3 direction, ulong targetId)
    {
        if (!ready || busy || requested || !CombatTargeting.Finite(direction) || Time.time >= expires) return false;
        if (IsFeint && steps >= ZoroarkMoveRules.FeintSteps)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
                || !obj.TryGetComponent(out finisherTarget) || !CanHit(finisherTarget, true)
                || Vector3.Distance(Actor.transform.position, finisherTarget.transform.position) > ZoroarkMoveRules.FeintTargetRange + 0.5f) return false;
        }
        ready = false; requested = true; recastDirection = direction;
        ClearWindow();
        return true;
    }

    public void Movement(Vector2 input)
    {
        if (!IsServer || !IsNight || busy || finishing || requested || input.sqrMagnitude < 0.01f) return;
        // The sixth hit has already earned the leap, even in the frame before
        // NightChain enters its finishing phase. Input must not cancel it.
        if (steps >= ZoroarkMoveRules.NightSteps || Time.time >= expires || Actor.MovesController.ServerActionLocks > 0) return;
        if (!dashReady)
        {
            // A held stick/key is not a fresh failed dash. Keep the successful
            // dash's special basic available without requiring a neutral stick.
            if (!uniqueReady) Despawn();
            return;
        }
        requested = true;
        recastDirection = new Vector3(input.x, 0, input.y).normalized;
    }

    public void MoveMark(Vector3 origin)
    {
        if (!IsFeint || busy || finishing || Time.time >= expires) return;
        mark = origin;
        MarkRPC(true, mark);
    }

    // Called once per successful cast/basic, never once per collider or victim.
    public void ValidHit(bool basic)
    {
        if (!IsServer || finishing || Time.time >= expires) return;
        if (IsNight && !basic && busy) { normalMovePending = true; return; }
        steps = Mathf.Min(steps + 1, IsNight ? ZoroarkMoveRules.NightSteps : ZoroarkMoveRules.FeintSteps);
        lastWasBasic = basic;
        expires = Time.time + (IsNight ? ZoroarkMoveRules.NightWindow : ZoroarkMoveRules.FeintWindow);
        ShowWindow();
        if (IsNight)
        {
            if (basic) uniqueReady = false;
            dashReady = true;
        }
        Publish();
    }

    public void UniqueAttackLanded(bool hit)
    {
        if (!uniqueReady || finishing) return;
        uniqueReady = false;
        if (hit) ValidHit(true); else Publish();
    }

    private void Publish()
    {
        Actor.MovesController.ZoroarkComboRPC(IsNight, steps, true, uniqueReady,
            steps >= (IsNight ? ZoroarkMoveRules.NightSteps : ZoroarkMoveRules.FeintSteps), IsNight && aerialWindow);
    }

    private void ShowWindow()
    {
        Actor.MovesController.ZoroarkWindowRPC(move, NetworkManager.ServerTime.Time + Mathf.Max(0, expires - Time.time));
    }
    private void ClearWindow()
    {
        if (IsNight || IsFeint || move == AvailableMoves.ZoroarkFurySwipes)
            Actor.MovesController.ZoroarkWindowRPC(move, 0);
    }

    protected override IEnumerator Execute()
    {
        if (IsNight) { yield return NightChain(); yield break; }
        if (IsFeint) { yield return FeintChain(); yield break; }
        if (move == AvailableMoves.ZoroarkNightfallDaze) { yield return Unite(); yield break; }
        bool hit = false;
        yield return Dash(Direction, ZoroarkMoveRules.DashDistance(move), false, value => hit = value);
        if (move != AvailableMoves.ZoroarkFurySwipes || !hit || CastInterrupted) yield break;
        expires = Time.time + ZoroarkMoveRules.FuryWindow;
        ShowWindow();
        ready = true; OfferRecast();
        while (Time.time < expires && !requested && !CastInterrupted) yield return null;
        if (requested && !CastInterrupted) yield return Dash(recastDirection, ZoroarkMoveRules.FuryRange, false, _ => { });
    }

    private IEnumerator NightChain()
    {
        Actor.MovesController.ZoroarkNight = this;
        expires = Time.time + ZoroarkMoveRules.NightWindow;
        ShowWindow();
        dashReady = true;
        Publish();
        while (!CastInterrupted && Time.time < expires)
        {
            if (steps >= ZoroarkMoveRules.NightSteps)
            {
                // Finish the attack/other move which earned the final step first.
                if (Actor.MovesController.ServerActionLocks > 0) { yield return null; continue; }
                yield return NightFinale(); yield break;
            }
            if (requested && dashReady && Actor.MovesController.ServerActionLocks == 0)
            {
                requested = false; dashReady = false;
                bool hit = false;
                yield return Dash(recastDirection, ZoroarkMoveRules.NightDashRange, false, value => hit = value);
                if (hit)
                {
                    ValidHit(false);
                    dashReady = false; uniqueReady = true;
                    Publish();
                }
                if (normalMovePending) { normalMovePending = false; ValidHit(false); }
            }
            yield return null;
        }
    }

    private IEnumerator FeintChain()
    {
        Actor.MovesController.ZoroarkFeint = this;
        mark = Actor.transform.position;
        expires = Time.time + ZoroarkMoveRules.FeintWindow;
        bool hit = false;
        yield return Dash(Direction, ZoroarkMoveRules.FeintRange, false, value => hit = value);
        if (!hit || CastInterrupted) yield break;
        ValidHit(false);
        Actor.MovesController.ResetZoroarkOtherMove();
        MarkRPC(true, mark);
        while (!CastInterrupted && Time.time < expires)
        {
            ready = true; OfferRecast();
            while (!requested && !CastInterrupted && Time.time < expires) yield return null;
            if (!requested || CastInterrupted || Time.time >= expires) yield break;
            requested = false; ready = false;
            if (steps >= ZoroarkMoveRules.FeintSteps)
            {
                yield return FeintFinale(); yield break;
            }
            Vector3 origin = Actor.transform.position;
            Vector3 delta = mark - origin; delta.y = 0;
            // A return dash travels to the actual mark, including a mark moved by slot A.
            yield return Dash(delta.sqrMagnitude > 0.001f ? delta.normalized : Actor.transform.forward, delta.magnitude, true, value => hit = value);
            if (!hit || CastInterrupted) yield break;
            mark = origin;
            ValidHit(false);
            Actor.MovesController.ResetZoroarkOtherMove();
            MarkRPC(true, mark);
        }
    }

    private IEnumerator Dash(Vector3 direction, float distance, bool returning, Action<bool> report)
    {
        busy = true; hitThisCast = false;
        Actor.Pokemon.NotifyOffensiveAction();
        bool aerialReposition = AerialCast || (!IsNight && !IsFeint && Actor.MovesController.ZoroarkNight != null && Actor.MovesController.ZoroarkNight.Finishing);
        if (aerialReposition) distance = ZoroarkMoveRules.NightDashRange;
        Vector3 origin = Actor.transform.position;
        if (!IsNight && !IsFeint) Actor.MovesController.ZoroarkNormalMoveStarted(origin);
        PhaseLockRPC(1);
        FaceRPC(direction);
        string clip = move == AvailableMoves.ZoroarkSlash ? "spell1" : move == AvailableMoves.ZoroarkFurySwipes ? "spell21"
            : move == AvailableMoves.ZoroarkShadowClaw ? "spell1a" : move == AvailableMoves.ZoroarkCut ? "spell1b"
            : IsNight ? "spell2a1" : "spell2b2";
        if (!aerialReposition) PlayAnimation(ZoroarkMoveRules.Animation(Actor, clip), ZoroarkMoveRules.Windup + ZoroarkMoveRules.DashDuration + ZoroarkMoveRules.Recovery, 0.04f);
        yield return CastPause(aerialReposition ? 0 : ZoroarkMoveRules.Windup);
        if (CastInterrupted) { busy = false; yield break; }
        Vector3 destination = Actor.PlayerMovement.GetWallCrossingDestination(origin + direction * distance);
        if (aerialReposition)
        {
            // Slot A becomes a damage-free reposition during the invincible leap.
            float duration = Actor.MovesController.ZoroarkNight != null
                ? Mathf.Min(ZoroarkMoveRules.DashDuration, Actor.MovesController.ZoroarkNight.RemainingAirTime) : 0;
            if (duration > 0)
            {
                Actor.MovesController.ZoroarkNight.PauseAirSteering(duration + ZoroarkMoveRules.Recovery);
                Drive(destination, duration);
                yield return new WaitForSeconds(duration);
            }
        }
        else
        {
            DashVisualRPC(true);
            var hit = new HashSet<Pokemon>();
            var damaged = new HashSet<Pokemon>();
            Vector3 previous = origin + Vector3.up * 0.8f;
            Drive(destination, ZoroarkMoveRules.DashDuration);
            float until = Time.time + ZoroarkMoveRules.DashDuration;
            do
            {
                yield return null;
                if (CastInterrupted) break;
                Physics.SyncTransforms();
                Vector3 current = Actor.transform.position + Vector3.up * 0.8f;
                foreach (var target in ZoroarkMoveRules.DashTargets(move, previous, current, direction, returning))
                {
                    if (!CanHit(target) || !hit.Add(target)) continue;
                    if (!DamageDashTarget(target)) continue;
                    damaged.Add(target);
                    if (move == AvailableMoves.ZoroarkShadowClaw)
                        target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.05f, true, ZoroarkMoveRules.ClawStatus).WithSource(Actor.NetworkObjectId));
                }
                previous = current;
            } while (Time.time < until);
            // S11 has an independent forward box, not a distance-to-destination sphere.
            if (move == AvailableMoves.ZoroarkShadowClaw && !CastInterrupted)
            {
                Physics.SyncTransforms();
                Vector3 center = Actor.transform.position + Vector3.up * 0.8f + direction * ZoroarkMoveRules.ClawEndpointOffset;
                foreach (var target in CombatTargeting.BoxTargets(center, ZoroarkMoveRules.ClawEndpointSize * 0.5f, Quaternion.LookRotation(direction)))
                {
                    if (!CanHit(target)) continue;
                    if (hit.Add(target) && DamageDashTarget(target)) damaged.Add(target);
                    if (!damaged.Contains(target) || target.CurrentHp <= 0 || target.HasStatusEffect(StatusType.Unstoppable)) continue;
                    float duration = Upgraded ? ZoroarkMoveRules.ClawThrowPlus : ZoroarkMoveRules.ClawThrow;
                    target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, duration, true, ZoroarkMoveRules.ClawStatus).WithSource(Actor.NetworkObjectId));
                    target.ApplyKnockupRPC(2, duration);
                }
            }
            DashVisualRPC(false);
        }
        yield return CastPause(ZoroarkMoveRules.Recovery);
        PhaseLockRPC(0);
        busy = false;
        if (!aerialReposition && !IsNight && !IsFeint && hitThisCast) Actor.MovesController.ZoroarkNormalMoveHit();
        if (!aerialReposition && !CastInterrupted) PlayAnimation(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
        report(hitThisCast && !CastInterrupted);
    }

    private bool DamageDashTarget(Pokemon target)
    {
        if (target.ResolveDamage(ZoroarkMoveRules.Dash(Actor.NetworkObjectId, move)) <= 0) return false;
        hitThisCast = true;
        if (move == AvailableMoves.ZoroarkCut)
            Actor.Pokemon.HealDamageRPC(ZoroarkMoveRules.CutHeal(Actor.Pokemon.GetAttack(), Actor.Pokemon.CurrentLevel, Upgraded), Actor.NetworkObjectId);
        return true;
    }

    private IEnumerator NightFinale()
    {
        finishing = true; busy = true; uniqueReady = false;
        ClearWindow();
        aerialWindow = true;
        aerialUntil = Time.time + ZoroarkMoveRules.NightAirTime;
        Actor.Pokemon.NotifyOffensiveAction();
        Actor.Pokemon.CleanseConditions();
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, ZoroarkMoveRules.NightInvincible, true, ZoroarkMoveRules.NightStatus));
        PhaseLockRPC(2);
        Publish();
        PlayAnimation(ZoroarkMoveRules.Animation(Actor, "spell2a4"), ZoroarkMoveRules.NightInvincible, 0.04f);
        AirSteeringRPC(true, NetworkManager.ServerTime.Time + ZoroarkMoveRules.NightAirTime);
        yield return new WaitForSeconds(ZoroarkMoveRules.NightAirTime);
        AirSteeringRPC(false, 0);
        aerialWindow = false;
        PhaseLockRPC(1);
        Publish();
        // Sample at landing. Repositioning changes the whole cross, not just its art.
        var damaged = new HashSet<Pokemon>();
        Vector3 origin = Actor.transform.position;
        int heals = 0;
        for (int diagonal = 0; diagonal < 2; diagonal++)
        {
            bool contact = false;
            Quaternion rotation = Quaternion.Euler(0, Actor.transform.eulerAngles.y + (diagonal == 0 ? -45 : 45), 0);
            foreach (var target in CombatTargeting.BoxTargets(origin + Vector3.up * 0.8f,
                ZoroarkMoveRules.CrossBoxSize * 0.5f, rotation))
            {
                if (!CanHit(target)) continue;
                contact = true;
                damaged.Add(target);
            }
            if (contact) heals++;
        }
        foreach (var target in damaged) target.ResolveDamage(ZoroarkMoveRules.Finisher(Actor, target, false, lastWasBasic));
        for (int i = 0; i < heals; i++) Actor.Pokemon.HealDamageRPC(ZoroarkMoveRules.NightHeal(Actor.Pokemon.GetAttack(), Actor.Pokemon.CurrentLevel), Actor.NetworkObjectId);
        Actor.MovesController.ShowCombatVisualRPC(ZoroarkMoveRules.Visuals + "CrossSlash", origin, Actor.transform.forward, Vector3.one, 0.35f);
        yield return new WaitForSeconds(ZoroarkMoveRules.NightInvincible - ZoroarkMoveRules.NightAirTime);
        PhaseLockRPC(0);
        PlayAnimation(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
    }

    private IEnumerator FeintFinale()
    {
        if (!CanHit(finisherTarget, true)) yield break;
        finishing = true; busy = true;
        PhaseLockRPC(1);
        Vector3 anticipation = finisherTarget.transform.position - Actor.transform.position;
        anticipation.y = 0;
        if (anticipation.sqrMagnitude > 0.001f) FaceRPC(anticipation.normalized);
        PlayAnimation(ZoroarkMoveRules.Animation(Actor, "spell2b21"), ZoroarkMoveRules.FeintFinalWindup, 0.04f);
        yield return CastPause(ZoroarkMoveRules.FeintFinalWindup);
        if (CastInterrupted || !CanHit(finisherTarget, true)
            || Vector3.Distance(Actor.transform.position, finisherTarget.transform.position) > ZoroarkMoveRules.FeintTargetRange + 0.5f)
        {
            if (!CastInterrupted) PlayAnimation(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
            yield break;
        }
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, 0.5f, true, ZoroarkMoveRules.FeintStatus));
        Vector3 direction = finisherTarget.transform.position - Actor.transform.position;
        direction.y = 0; direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Direction;
        Vector3 bounce = Actor.MovesController.ZoroarkBounceDirection(direction);
        FaceRPC(direction);
        PlayAnimation(ZoroarkMoveRules.Animation(Actor, "spell2b3"), 0.5f, 0.04f);
        Drive(Actor.PlayerMovement.GetWallCrossingDestination(finisherTarget.transform.position - direction * 0.5f), 0.2f);
        yield return new WaitForSeconds(0.2f);
        var hit = new HashSet<Pokemon>();
        if (CanHit(finisherTarget)) { hit.Add(finisherTarget); finisherTarget.ResolveDamage(ZoroarkMoveRules.Finisher(Actor, finisherTarget, true, false)); }
        Vector3 previous = Actor.transform.position + Vector3.up * 0.8f;
        Vector3 destination = Actor.PlayerMovement.GetWallCrossingDestination(Actor.transform.position + bounce * ZoroarkMoveRules.FeintBounce);
        Drive(destination, 0.3f);
        float until = Time.time + 0.3f;
        do
        {
            yield return null;
            Physics.SyncTransforms();
            Vector3 current = Actor.transform.position + Vector3.up * 0.8f;
            foreach (var target in CombatTargeting.SweptSphereTargets(previous, current, ZoroarkMoveRules.DashRadius))
                if (CanHit(target) && hit.Add(target)) target.ResolveDamage(ZoroarkMoveRules.Finisher(Actor, target, true, false));
            previous = current;
        } while (Time.time < until && !CastInterrupted);
        PhaseLockRPC(0);
        PlayAnimation(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
    }

    private IEnumerator Unite()
    {
        PhaseLockRPC(3);
        PlayAnimation(ZoroarkMoveRules.Animation(Actor, "spellu"), ZoroarkMoveRules.UniteDuration, 0.06f);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, ZoroarkMoveRules.UniteDuration, true, ZoroarkMoveRules.UniteStatus));
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(1f / 3);
            Vector3 origin = Actor.transform.position;
            Actor.MovesController.ShowCombatVisualRPC(ZoroarkMoveRules.Visuals + "DarkShockwave", origin, Vector3.forward,
                Vector3.one * ZoroarkMoveRules.UniteRadii[i], 0.3f);
            foreach (var target in CombatTargeting.SphereTargets(origin + Vector3.up * 0.8f, ZoroarkMoveRules.UniteRadii[i]))
                if (CanHit(target)) target.ResolveDamage(DamageInfo.Tuned("zoroark.zoroarkmoveeffect.unite", new DamageInfo(Actor.NetworkObjectId, 1.58f, 9, 430, DamageType.Physical, DamageProprieties.IsUniteMove)));
        }
        yield return new WaitForSeconds(ZoroarkMoveRules.UniteDuration - 1);
        RefreshStat(Actor.Pokemon, new StatChange(80, Stat.Speed, 6, true, true, true, ZoroarkMoveRules.UniteSpeed));
        Actor.Pokemon.RemoveShieldWithIDRPC(ZoroarkMoveRules.UniteShield);
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), ZoroarkMoveRules.UniteShield, 0, 6, true));
        PhaseLockRPC(0);
        PlayAnimation(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PhaseLockRPC(int mode)
    {
        phaseLock?.Dispose(); phaseLock = null;
        if (mode != 0 && Actor != null)
            phaseLock = new CombatActionLock(Actor, true, lockServer: mode != 2, lockBattleItems: mode != 3, unlockedMoveSlot: mode == 2 ? 0 : -1);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void DashVisualRPC(bool visible) { if (dashVisual != null) dashVisual.SetActive(visible); }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void MarkRPC(bool visible, Vector3 location)
    {
        if (markVisual == null) return;
        markVisual.SetActive(visible && (Actor.IsOwner || (LobbyController.Instance != null && Actor.CurrentTeam.IsOnSameTeam(LobbyController.Instance.GetLocalPlayerTeam()))));
        markVisual.transform.position = location + Vector3.up * 0.08f;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void AirSteeringRPC(bool active, double endsAt)
    {
        airSteering = active;
        airSteeringUntil = endsAt;
        if (active) { airSteeringDistance = 0; airSteeringPausedUntil = 0; }
    }
    public void PauseAirSteering(float duration)
    {
        if (IsServer) PauseAirSteeringRPC(NetworkManager.ServerTime.Time + duration);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void PauseAirSteeringRPC(double until) { airSteeringPausedUntil = until; }

    protected override void Update()
    {
        base.Update();
        if (Actor != null && dashVisual != null) dashVisual.transform.SetPositionAndRotation(Actor.transform.position, Actor.transform.rotation);
        if (!airSteering || Actor == null || !Actor.IsSpawned || (!IsServer && !Actor.IsOwner) || Actor.Pokemon.CurrentHp <= 0) return;
        double now = NetworkManager.ServerTime.Time;
        if (now >= airSteeringUntil || now < airSteeringPausedUntil || airSteeringDistance >= ZoroarkMoveRules.NightAirSteerDistance) return;
        var input = Vector2.ClampMagnitude(Actor.IsOwner ? Actor.MovementInput : Actor.MovesController.ZoroarkMovementInput, 1);
        float step = Mathf.Min(ZoroarkMoveRules.NightAirSteerSpeed * Time.deltaTime,
            ZoroarkMoveRules.NightAirSteerDistance - airSteeringDistance);
        Vector3 delta = new Vector3(input.x, 0, input.y) * step;
        if (delta.sqrMagnitude > 0)
            airSteeringDistance += Actor.PlayerMovement.MoveWithWalls(delta).magnitude;
    }
    protected override void Cleanup()
    {
        phaseLock?.Dispose(); phaseLock = null;
        airSteering = false;
        if (Actor == null || !IsServer) return;
        if (IsNight && Actor.MovesController.ZoroarkNight == this) Actor.MovesController.ZoroarkNight = null;
        if (IsFeint && Actor.MovesController.ZoroarkFeint == this) Actor.MovesController.ZoroarkFeint = null;
        if (!Actor.IsSpawned || !Actor.MovesController.IsSpawned) return;
        ClearWindow();
        if (IsNight || IsFeint) Actor.MovesController.ZoroarkComboRPC(IsNight, 0, false, false, false, false);
        if (IsFeint) Actor.Pokemon.RemoveStatusEffectWithID(ZoroarkMoveRules.FeintStatus);
        if (move == AvailableMoves.ZoroarkNightfallDaze) Actor.Pokemon.RemoveStatusEffectWithID(ZoroarkMoveRules.UniteStatus);
        if (IsNight) Actor.Pokemon.RemoveStatusEffectWithID(ZoroarkMoveRules.NightStatus);
    }
}
