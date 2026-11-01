using System;
using System.Collections;
using System.Collections.Generic;
using Unite.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class ZoroarkExecution : CombatExecution
{
    private readonly ZoroarkCharacterRuntime character;
    private GameObject prefab;
    private ZoroarkMoveEffect effect;
    private bool stopped;
    private bool phaseInterrupted;
    private bool busy;
    private bool ready;
    private bool requested;
    private bool dashReady;
    private bool uniqueReady;
    private bool finishing;
    private bool hitThisCast;
    private bool lastWasBasic;
    private int steps;
    private float expires;
    private Vector3 recastDirection;
    private Vector3 mark;
    private Pokemon finisherTarget;
    private bool normalMovePending;
    private bool aerialRepositionSpent;
    private float aerialUntil;
    private bool aerialWindow;

    public override bool FollowUpReady => ready && !busy && !requested && Time.time < expires;
    public override PrefabMoveEffect Effect => effect;
    public bool AerialWindow => aerialWindow;
    public float RemainingAirTime => Mathf.Max(0, aerialUntil - Time.time);
    public bool IsNight => Context.MoveId == AvailableMoves.ZoroarkNightSlash;
    public bool IsFeint => Context.MoveId == AvailableMoves.ZoroarkFeintAttack;
    public bool Finishing => finishing;
    public bool UniqueReady => uniqueReady;
    public bool CanSpendAerialReposition => !stopped && Actor.IsServer && IsNight && aerialWindow && !aerialRepositionSpent && RemainingAirTime > 0;
    private bool CastInterrupted => stopped || phaseInterrupted || Actor == null || !Actor.IsSpawned || Actor.Pokemon.CurrentHp <= 0
        || Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);
    private string PrefabPath => ZoroarkMoveRules.Folder + Context.MoveId + ".prefab";

    public ZoroarkExecution(CombatExecutionContext context, ZoroarkCharacterRuntime character) : base(context)
    {
        this.character = character;
    }

    public override IEnumerator Prepare()
    {
        Prepared = false;
        if (!Actor.IsServer || !ZoroarkMoveRules.IsMove(Context.MoveId)) yield break;
        yield return Controller.Combat.Spawner.Load(PrefabPath, Context.Scope, loaded => prefab = loaded);
        Prepared = prefab != null && prefab.GetComponent<NetworkObject>() != null
            && prefab.GetComponent<ZoroarkMoveEffect>() != null && !Context.Scope.IsDisposed;
    }

    public override void Start()
    {
        if (!Prepared || stopped || !Actor.IsServer || Context.Scope.IsDisposed) return;
        Vector3 direction = Context.Direction.sqrMagnitude > 0.001f ? Context.Direction : Actor.transform.forward;
        GameObject instance = Controller.Combat.Spawner.Spawn(prefab,
            Actor.transform.position + Vector3.up * 0.8f, Quaternion.LookRotation(direction), Context.Scope);
        ZoroarkMoveEffect adapter = instance != null ? instance.GetComponent<ZoroarkMoveEffect>() : null;
        if (adapter == null)
        {
            Context.Finish(CastOutcome.ExecutionFailed);
            return;
        }
        adapter.HoldAsset(Addressables.LoadAssetAsync<GameObject>(PrefabPath));
        AttachEffect(adapter);
        adapter.Initialize(Actor, Context.Direction, Context.Target, Context.Upgraded);
    }

    internal void AttachEffect(ZoroarkMoveEffect adapter)
    {
        effect = adapter;
        effect.MoveId = Context.MoveId;
        effect.CastSequence = unchecked((int)Context.CastId);
        effect.BindExecution(this);
        effect.Completed += OnCompleted;
    }

    internal IEnumerator RunLifecycle()
    {
        yield return Execute();
        if (!stopped) effect.Finish(CastInterrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private void OnCompleted(PrefabMoveEffect completed) => Context.Finish(completed.CompletionOutcome);

    private IEnumerator Execute()
    {
        switch (Context.MoveId)
        {
            case AvailableMoves.ZoroarkSlash: yield return ExecuteSlash(); break;
            case AvailableMoves.ZoroarkFurySwipes: yield return ExecuteFurySwipes(); break;
            case AvailableMoves.ZoroarkShadowClaw: yield return ExecuteShadowClaw(); break;
            case AvailableMoves.ZoroarkCut: yield return ExecuteCut(); break;
            case AvailableMoves.ZoroarkNightSlash: yield return ExecuteNightSlash(); break;
            case AvailableMoves.ZoroarkFeintAttack: yield return ExecuteFeintAttack(); break;
            case AvailableMoves.ZoroarkNightfallDaze: yield return ExecuteUnite(); break;
        }
    }

    private IEnumerator ExecuteSlash()
    {
        yield return Dash(Context.Direction, ZoroarkMoveRules.Range(Context.MoveId), value => { });
    }

    private IEnumerator ExecuteFurySwipes()
    {
        bool hit = false;
        yield return Dash(Context.Direction, ZoroarkMoveRules.Range(Context.MoveId), value => hit = value);
        if (!hit || CastInterrupted) yield break;
        expires = Time.time + ZoroarkMoveRules.FuryWindow;
        ShowWindow();
        OfferRecast();
        while (Time.time < expires && !requested && !CastInterrupted) yield return null;
        if (requested && !CastInterrupted)
            yield return Dash(recastDirection, ZoroarkMoveRules.FuryRange, value => { });
    }

    private IEnumerator ExecuteShadowClaw()
    {
        yield return Dash(Context.Direction, ZoroarkMoveRules.Range(Context.MoveId), value => { });
    }

    private IEnumerator ExecuteCut()
    {
        yield return Dash(Context.Direction, ZoroarkMoveRules.Range(Context.MoveId), value => { });
    }

    private IEnumerator ExecuteNightSlash()
    {
        character.ZoroarkNight = this;
        expires = Time.time + ZoroarkMoveRules.NightWindow;
        ShowWindow();
        dashReady = true;
        Publish();
        while (!CastInterrupted && Time.time < expires)
        {
            if (steps >= ZoroarkMoveRules.NightSteps)
            {
                if (Controller.ServerActionLocks > 0) { yield return null; continue; }
                yield return NightFinale();
                yield break;
            }
            if (requested && dashReady && Controller.ServerActionLocks == 0)
            {
                requested = false;
                dashReady = false;
                bool hit = false;
                yield return Dash(recastDirection, ZoroarkMoveRules.NightDashRange, value => hit = value);
                if (hit)
                {
                    ValidHit(false);
                    dashReady = false;
                    uniqueReady = true;
                    Publish();
                }
                if (normalMovePending) { normalMovePending = false; ValidHit(false); }
            }
            yield return null;
        }
    }

    private IEnumerator ExecuteFeintAttack()
    {
        character.ZoroarkFeint = this;
        mark = Actor.transform.position;
        expires = Time.time + ZoroarkMoveRules.FeintWindow;
        bool hit = false;
        yield return Dash(Context.Direction, ZoroarkMoveRules.FeintRange, value => hit = value);
        if (!hit || CastInterrupted) yield break;
        ValidHit(false);
        character.ResetZoroarkOtherMove();
        effect.SetMark(true, mark);
        while (!CastInterrupted && Time.time < expires)
        {
            OfferRecast();
            while (!requested && !CastInterrupted && Time.time < expires) yield return null;
            if (!requested || CastInterrupted || Time.time >= expires) yield break;
            requested = false;
            ready = false;
            if (steps >= ZoroarkMoveRules.FeintSteps)
            {
                yield return FeintFinale();
                yield break;
            }
            Vector3 origin = Actor.transform.position;
            Vector3 delta = mark - origin;
            delta.y = 0;
            yield return Dash(delta.sqrMagnitude > 0.001f ? delta.normalized : Actor.transform.forward, delta.magnitude, value => hit = value);
            if (!hit || CastInterrupted) yield break;
            mark = origin;
            ValidHit(false);
            character.ResetZoroarkOtherMove();
            effect.SetMark(true, mark);
        }
    }

    private IEnumerator ExecuteUnite()
    {
        effect.SetPhaseLock(3);
        effect.Animate(ZoroarkMoveRules.Animation(Actor, "spellu"), ZoroarkMoveRules.UniteDuration, 0.06f);
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, ZoroarkMoveRules.UniteDuration, true, ZoroarkMoveRules.UniteStatus));
        for (int pulse = 0; pulse < 3; pulse++)
        {
            yield return new WaitForSeconds(1f / 3);
            if (Actor == null || !Actor.IsSpawned || Actor.Pokemon.CurrentHp <= 0) yield break;
            Context.MarkCastSucceeded();
            effect.SignalCastSucceeded();
            Vector3 origin = Actor.transform.position;
            Controller.ShowCombatVisualRPC(ZoroarkMoveRules.Visuals + "DarkShockwave", origin, Vector3.forward,
                Vector3.one * ZoroarkMoveRules.UniteRadii[pulse], 0.3f);
            foreach (Pokemon target in CombatTargeting.SphereTargets(origin + Vector3.up * 0.8f, ZoroarkMoveRules.UniteRadii[pulse]))
                if (CanHit(target)) target.ResolveDamage(new DamageInfo(Actor.NetworkObjectId, 1.58f, 9, 430, DamageType.Physical, DamageProprieties.IsUniteMove));
        }
        yield return new WaitForSeconds(ZoroarkMoveRules.UniteDuration - 1);
        Actor.Pokemon.RemoveAllStatChangeWithIDRPC(ZoroarkMoveRules.UniteSpeed);
        Actor.Pokemon.AddStatChange(new StatChange(80, Stat.Speed, 6, true, true, true, ZoroarkMoveRules.UniteSpeed));
        Actor.Pokemon.RemoveShieldWithIDRPC(ZoroarkMoveRules.UniteShield);
        Actor.Pokemon.AddShieldRPC(new ShieldInfo(Mathf.FloorToInt(Actor.Pokemon.GetMaxHp() * 0.2f), ZoroarkMoveRules.UniteShield, 0, 6, true));
        effect.SetPhaseLock(0);
        effect.Animate(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
    }

    private IEnumerator Dash(Vector3 direction, float distance, Action<bool> report)
    {
        busy = true;
        hitThisCast = false;
        Actor.Pokemon.NotifyOffensiveAction();
        bool aerialReposition = character.IsAerialCast(Context)
            || (!IsNight && !IsFeint && character.ZoroarkNight != null && character.ZoroarkNight.Finishing);
        if (aerialReposition) distance = ZoroarkMoveRules.NightDashRange;
        Vector3 origin = Actor.transform.position;
        if (!IsNight && !IsFeint) character.ZoroarkNormalMoveStarted(origin);
        effect.SetPhaseLock(1);
        effect.Face(direction);
        string clip = Context.MoveId == AvailableMoves.ZoroarkSlash ? "spell1" : Context.MoveId == AvailableMoves.ZoroarkFurySwipes ? "spell21"
            : Context.MoveId == AvailableMoves.ZoroarkShadowClaw ? "spell1a" : Context.MoveId == AvailableMoves.ZoroarkCut ? "spell1b"
            : IsNight ? "spell2a1" : "spell2b2";
        if (!aerialReposition) effect.Animate(ZoroarkMoveRules.Animation(Actor, clip), ZoroarkMoveRules.Windup + ZoroarkMoveRules.DashDuration + ZoroarkMoveRules.Recovery, 0.04f);
        yield return CastPause(aerialReposition ? 0 : ZoroarkMoveRules.Windup);
        if (CastInterrupted) { busy = false; yield break; }
        Vector3 destination = Actor.PlayerMovement.GetWallCrossingDestination(origin + direction * distance);
        if (aerialReposition)
        {
            float duration = character.ZoroarkNight != null
                ? Mathf.Min(ZoroarkMoveRules.DashDuration, character.ZoroarkNight.RemainingAirTime) : 0;
            if (duration > 0)
            {
                character.ZoroarkNight.PauseAirSteering(duration + ZoroarkMoveRules.Recovery);
                effect.DriveTo(destination, duration);
                yield return new WaitForSeconds(duration);
            }
        }
        else
        {
            effect.SetDashVisible(true);
            var hit = new HashSet<Pokemon>();
            var endpointHits = new HashSet<Pokemon>();
            Vector3 previous = origin + Vector3.up * 0.8f;
            effect.DriveTo(destination, ZoroarkMoveRules.DashDuration);
            float until = Time.time + ZoroarkMoveRules.DashDuration;
            do
            {
                yield return null;
                if (CastInterrupted) break;
                Physics.SyncTransforms();
                Vector3 current = Actor.transform.position + Vector3.up * 0.8f;
                foreach (Pokemon target in CombatTargeting.SweptSphereTargets(previous, current, ZoroarkMoveRules.DashRadius))
                {
                    if (!CanHit(target) || !hit.Add(target)) continue;
                    if (target.ResolveDamage(ZoroarkMoveRules.Dash(Actor.NetworkObjectId, Context.MoveId)) <= 0) continue;
                    hitThisCast = true;
                    if (Context.MoveId == AvailableMoves.ZoroarkShadowClaw)
                    {
                        if (Vector3.Distance(target.transform.position, destination) <= ZoroarkMoveRules.ClawEndpointLength + ZoroarkMoveRules.DashRadius)
                            endpointHits.Add(target);
                        else target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.05f, true, ZoroarkMoveRules.ClawStatus));
                    }
                    if (Context.MoveId == AvailableMoves.ZoroarkCut)
                        Actor.Pokemon.HealDamageRPC(ZoroarkMoveRules.CutHeal(Actor.Pokemon.GetAttack(), Actor.Pokemon.CurrentLevel, Context.Upgraded), Actor.NetworkObjectId);
                }
                previous = current;
            } while (Time.time < until);
            foreach (Pokemon target in endpointHits)
                if (target != null && target.CurrentHp > 0 && !target.HasStatusEffect(StatusType.Unstoppable))
                {
                    float duration = Context.Upgraded ? ZoroarkMoveRules.ClawThrowPlus : ZoroarkMoveRules.ClawThrow;
                    target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, duration, true, ZoroarkMoveRules.ClawStatus));
                    target.ApplyKnockupRPC(2, duration);
                }
            effect.SetDashVisible(false);
        }
        yield return CastPause(ZoroarkMoveRules.Recovery);
        effect.SetPhaseLock(0);
        busy = false;
        if (!aerialReposition && !IsNight && !IsFeint && hitThisCast) character.ZoroarkNormalMoveHit();
        if (!aerialReposition && !CastInterrupted) effect.Animate(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
        report(hitThisCast && !CastInterrupted);
    }

    private IEnumerator NightFinale()
    {
        finishing = true;
        busy = true;
        uniqueReady = false;
        ClearWindow();
        aerialWindow = true;
        aerialUntil = Time.time + ZoroarkMoveRules.NightAirTime;
        Actor.Pokemon.NotifyOffensiveAction();
        Actor.Pokemon.CleanseConditions();
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, ZoroarkMoveRules.NightInvincible, true, ZoroarkMoveRules.NightStatus));
        effect.SetPhaseLock(2);
        Publish();
        effect.Animate(ZoroarkMoveRules.Animation(Actor, "spell2a4"), ZoroarkMoveRules.NightInvincible, 0.04f);
        effect.SetAirSteering(true, Controller.NetworkManager.ServerTime.Time + ZoroarkMoveRules.NightAirTime);
        yield return new WaitForSeconds(ZoroarkMoveRules.NightAirTime);
        effect.SetAirSteering(false, 0);
        aerialWindow = false;
        effect.SetPhaseLock(1);
        Publish();
        var damaged = new HashSet<Pokemon>();
        Vector3 origin = Actor.transform.position;
        int heals = 0;
        for (int diagonal = 0; diagonal < 2; diagonal++)
        {
            bool contact = false;
            Quaternion rotation = Quaternion.Euler(0, Actor.transform.eulerAngles.y + (diagonal == 0 ? -45 : 45), 0);
            foreach (Pokemon target in CombatTargeting.BoxTargets(origin + Vector3.up * 0.8f,
                new Vector3(ZoroarkMoveRules.CrossWidth / 2, 2, ZoroarkMoveRules.CrossLength / 2), rotation))
            {
                if (!CanHit(target)) continue;
                contact = true;
                damaged.Add(target);
            }
            if (contact) heals++;
        }
        foreach (Pokemon target in damaged) target.ResolveDamage(ZoroarkMoveRules.Finisher(Actor, target, false, lastWasBasic));
        for (int heal = 0; heal < heals; heal++)
            Actor.Pokemon.HealDamageRPC(ZoroarkMoveRules.NightHeal(Actor.Pokemon.GetAttack(), Actor.Pokemon.CurrentLevel), Actor.NetworkObjectId);
        Controller.ShowCombatVisualRPC(ZoroarkMoveRules.Visuals + "CrossSlash", origin, Actor.transform.forward, Vector3.one, 0.35f);
        yield return new WaitForSeconds(ZoroarkMoveRules.NightInvincible - ZoroarkMoveRules.NightAirTime);
        effect.SetPhaseLock(0);
        effect.Animate(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
    }

    private IEnumerator FeintFinale()
    {
        if (!CanHit(finisherTarget, true)) yield break;
        finishing = true;
        busy = true;
        effect.SetPhaseLock(1);
        Vector3 anticipation = finisherTarget.transform.position - Actor.transform.position;
        anticipation.y = 0;
        if (anticipation.sqrMagnitude > 0.001f) effect.Face(anticipation.normalized);
        effect.Animate(ZoroarkMoveRules.Animation(Actor, "spell2b21"), ZoroarkMoveRules.FeintFinalWindup, 0.04f);
        yield return CastPause(ZoroarkMoveRules.FeintFinalWindup);
        if (CastInterrupted || !CanHit(finisherTarget, true)
            || Vector3.Distance(Actor.transform.position, finisherTarget.transform.position) > ZoroarkMoveRules.FeintTargetRange + 0.5f)
        {
            if (!CastInterrupted) effect.Animate(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
            yield break;
        }
        Actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, 0.5f, true, ZoroarkMoveRules.FeintStatus));
        Vector3 direction = finisherTarget.transform.position - Actor.transform.position;
        direction.y = 0;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Context.Direction;
        Vector3 bounce = character.ZoroarkBounceDirection(direction);
        effect.Face(direction);
        effect.Animate(ZoroarkMoveRules.Animation(Actor, "spell2b3"), 0.5f, 0.04f);
        effect.DriveTo(Actor.PlayerMovement.GetWallCrossingDestination(finisherTarget.transform.position - direction * 0.5f), 0.2f);
        yield return new WaitForSeconds(0.2f);
        var hit = new HashSet<Pokemon>();
        if (CanHit(finisherTarget))
        {
            hit.Add(finisherTarget);
            finisherTarget.ResolveDamage(ZoroarkMoveRules.Finisher(Actor, finisherTarget, true, false));
        }
        Vector3 previous = Actor.transform.position + Vector3.up * 0.8f;
        Vector3 destination = Actor.PlayerMovement.GetWallCrossingDestination(Actor.transform.position + bounce * ZoroarkMoveRules.FeintBounce);
        effect.DriveTo(destination, 0.3f);
        float until = Time.time + 0.3f;
        do
        {
            yield return null;
            Physics.SyncTransforms();
            Vector3 current = Actor.transform.position + Vector3.up * 0.8f;
            foreach (Pokemon target in CombatTargeting.SweptSphereTargets(previous, current, ZoroarkMoveRules.DashRadius))
                if (CanHit(target) && hit.Add(target)) target.ResolveDamage(ZoroarkMoveRules.Finisher(Actor, target, true, false));
            previous = current;
        } while (Time.time < until && !CastInterrupted);
        effect.SetPhaseLock(0);
        effect.Animate(ZoroarkMoveRules.Animation(Actor, "idle"), blendDuration: 0.06f);
    }

    private IEnumerator CastPause(float duration)
    {
        float until = Time.time + Mathf.Max(0, duration);
        while (Time.time < until)
        {
            if (CastInterrupted) { phaseInterrupted = true; yield break; }
            yield return null;
        }
    }

    private bool CanHit(Pokemon target, bool acquire = false) => effect != null && effect.ValidTarget(target, acquire);

    private void OfferRecast()
    {
        ready = true;
        Controller.Combat.PublishSlot(Context.Slot);
    }

    public override bool TryRecast(Vector3 direction, ulong targetId)
    {
        if (!ready || busy || requested || !CombatTargeting.Finite(direction) || Time.time >= expires) return false;
        if (IsFeint && steps >= ZoroarkMoveRules.FeintSteps)
        {
            if (!Controller.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out NetworkObject targetObject)
                || !targetObject.TryGetComponent(out finisherTarget) || !CanHit(finisherTarget, true)
                || Vector3.Distance(Actor.transform.position, finisherTarget.transform.position) > ZoroarkMoveRules.FeintTargetRange + 0.5f) return false;
        }
        ready = false;
        requested = true;
        recastDirection = direction;
        ClearWindow();
        return true;
    }

    public bool TrySpendAerialReposition()
    {
        if (!CanSpendAerialReposition) return false;
        aerialRepositionSpent = true;
        return true;
    }

    public void PauseAirSteering(float duration) => effect?.PauseAirSteering(duration);

    public void Movement(Vector2 input)
    {
        if (stopped || !Actor.IsServer || !IsNight || busy || finishing || requested || input.sqrMagnitude < 0.01f) return;
        if (steps >= ZoroarkMoveRules.NightSteps || Time.time >= expires || Controller.ServerActionLocks > 0) return;
        if (!dashReady)
        {
            if (!uniqueReady) effect.Finish(CastOutcome.Cancelled);
            return;
        }
        requested = true;
        recastDirection = new Vector3(input.x, 0, input.y).normalized;
    }

    public void MoveMark(Vector3 origin)
    {
        if (!IsFeint || busy || finishing || Time.time >= expires) return;
        mark = origin;
        effect.SetMark(true, mark);
    }

    public void ValidHit(bool basic)
    {
        if (stopped || !Actor.IsServer || finishing || Time.time >= expires) return;
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

    private void Publish() => Controller.ZoroarkComboRPC(IsNight, steps, true, uniqueReady,
        steps >= (IsNight ? ZoroarkMoveRules.NightSteps : ZoroarkMoveRules.FeintSteps), IsNight && aerialWindow);

    private void ShowWindow() => Controller.ZoroarkWindowRPC(Context.MoveId,
        Controller.NetworkManager.ServerTime.Time + Mathf.Max(0, expires - Time.time));

    private void ClearWindow()
    {
        if (IsNight || IsFeint || Context.MoveId == AvailableMoves.ZoroarkFurySwipes)
            Controller.ZoroarkWindowRPC(Context.MoveId, 0);
    }

    public override void Stop(CastOutcome reason)
    {
        if (stopped) return;
        stopped = true;
        ready = false;
        if (character.ZoroarkNight == this) character.ZoroarkNight = null;
        if (character.ZoroarkFeint == this) character.ZoroarkFeint = null;
        if (Actor != null && Actor.IsSpawned && Controller.IsSpawned)
        {
            ClearWindow();
            if (IsNight || IsFeint) Controller.ZoroarkComboRPC(IsNight, 0, false, false, false, false);
            if (IsFeint) Actor.Pokemon.RemoveStatusEffectWithID(ZoroarkMoveRules.FeintStatus);
            if (Context.MoveId == AvailableMoves.ZoroarkNightfallDaze) Actor.Pokemon.RemoveStatusEffectWithID(ZoroarkMoveRules.UniteStatus);
            if (IsNight) Actor.Pokemon.RemoveStatusEffectWithID(ZoroarkMoveRules.NightStatus);
        }
        if (effect == null) return;
        effect.Completed -= OnCompleted;
        effect.Finish(reason);
        effect = null;
    }
}
