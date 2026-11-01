using System;
using System.Collections;
using Unite.Combat;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;

public sealed class SylveonExecution : CombatExecution
{
    private readonly SylveonCharacterRuntime character;
    private Vector3 direction;
    private GameObject prefab;
    private PrefabMoveEffect effect;
    private HypervoiceHitbox hitbox;
    private CombatRoutine routine;
    private bool stopped;

    public SylveonExecution(CombatExecutionContext context, SylveonCharacterRuntime character) : base(context)
    {
        this.character = character;
        Vector3 flat = new Vector3(context.Direction.x, 0f, context.Direction.z);
        direction = flat.sqrMagnitude > 0.001f ? flat.normalized : context.Actor.transform.forward;
    }

    private string Path
    {
        get
        {
            switch (Context.MoveId)
            {
                case AvailableMoves.SylvSwift: return SylvMoveRules.SwiftPath;
                case AvailableMoves.SylvBabyDollEyes: return SylvMoveRules.BabyDollEyesPath;
                case AvailableMoves.SylvHyperVoice: return SylvMoveRules.HyperVoicePath;
                case AvailableMoves.SylvMysticalCringe: return SylvMoveRules.MysticalFirePath;
                case AvailableMoves.SylvDrainingKiss: return SylvMoveRules.DrainingKissPath;
                case AvailableMoves.SylvCalmMind: return SylvMoveRules.CalmMindPath;
                case AvailableMoves.SylveonUnite: return SylvMoveRules.UnitePath;
                default: return null;
            }
        }
    }

    public override IEnumerator Prepare()
    {
        Prepared = false;
        if (!Actor.IsServer || Path == null) yield break;
        yield return Controller.Combat.Spawner.Load(Path, Context.Scope, loaded => prefab = loaded);
        if (prefab == null || Context.Scope.IsDisposed || prefab.GetComponent<NetworkObject>() == null) yield break;
        switch (Context.MoveId)
        {
            case AvailableMoves.SylvSwift:
                Prepared = prefab.GetComponent<SylvSwiftProjectile>() != null;
                break;
            case AvailableMoves.SylvBabyDollEyes:
                Prepared = prefab.GetComponent<HomingMoveEffect>() != null;
                break;
            case AvailableMoves.SylvHyperVoice:
                Prepared = prefab.GetComponent<HypervoiceHitbox>() != null;
                break;
            case AvailableMoves.SylvMysticalCringe:
                Prepared = prefab.GetComponent<SylvMysticalFireEffect>() != null;
                break;
            case AvailableMoves.SylvDrainingKiss:
                Prepared = prefab.GetComponent<SylvDrainingKissEffect>() != null;
                break;
            case AvailableMoves.SylvCalmMind:
                Prepared = prefab.GetComponent<SylvCalmMindEffect>() != null;
                break;
            case AvailableMoves.SylveonUnite:
                Prepared = prefab.GetComponent<SylvFairyFrolicEffect>() != null;
                break;
        }
    }

    public override void Start()
    {
        if (!Prepared || stopped || !Actor.IsServer || Context.Scope.IsDisposed) return;
        routine = Context.Scope.Own(new CombatRoutine(Actor, Execute()));
    }

    private bool Alive => !stopped && Actor != null && Actor.IsSpawned && Actor.Pokemon != null && Actor.Pokemon.CurrentHp > 0;
    private bool Interrupted
    {
        get
        {
            if (!Alive) return true;
            if (Context.MoveId == AvailableMoves.SylveonUnite) return false;
            if (Actor.Pokemon.HasAnyStatusEffect(SylvMoveRules.Interrupts)) return true;
            if (Context.MoveId == AvailableMoves.SylvMysticalCringe && Actor.Pokemon.HasStatusEffect(StatusType.Immobilized)) return true;
            return false;
        }
    }

    private IEnumerator Execute()
    {
        switch (Context.MoveId)
        {
            case AvailableMoves.SylvSwift:
                yield return ExecuteSwift();
                break;
            case AvailableMoves.SylvBabyDollEyes:
                yield return ExecuteBabyDollEyes();
                break;
            case AvailableMoves.SylvHyperVoice:
                yield return ExecuteHyperVoice();
                break;
            case AvailableMoves.SylvMysticalCringe:
                yield return ExecuteMysticalFire();
                break;
            case AvailableMoves.SylvDrainingKiss:
                yield return ExecuteDrainingKiss();
                break;
            case AvailableMoves.SylvCalmMind:
                yield return ExecuteCalmMind();
                break;
            case AvailableMoves.SylveonUnite:
                yield return ExecuteUnite();
                break;
        }
    }

    private IEnumerator ExecuteSwift()
    {
        Context.Scope.Own(new CombatActionLock(Actor, false));
        Face(direction);
        Animate(SylvMoveRules.SwiftAnimation);

        DamageInfo damage = SylvMoveRules.SwiftDamage(Actor.Pokemon.NetworkObjectId);
        for (int shot = 0; shot < SylvMoveRules.SwiftShotCount; shot++)
        {
            yield return WaitDuration(SylvMoveRules.SwiftShotInterval, direction);
            if (Interrupted)
            {
                if (!stopped) Context.Finish(CastOutcome.Interrupted);
                yield break;
            }
            Vector3 spawnPos = Actor.transform.position + Vector3.up * 0.8f;
            GameObject spawned = Controller.Combat.Spawner.Spawn(prefab, spawnPos, Quaternion.LookRotation(direction), Context.Scope, false);
            SylvSwiftProjectile star = spawned != null ? spawned.GetComponent<SylvSwiftProjectile>() : null;
            if (star != null)
            {
                star.HoldAsset(Addressables.LoadAssetAsync<GameObject>(SylvMoveRules.SwiftPath));
                star.InitializeServer(Actor, spawnPos, direction, damage, SylvMoveRules.SwiftRange);
                character.TrackSwiftProjectile(star);
            }
        }

        if (!Interrupted) character.RecoverAnimation(SylvMoveRules.SwiftAnimation);
        if (!stopped) Context.Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteBabyDollEyes()
    {
        if (!SylvMoveRules.CanHit(Actor, Context.Target))
        {
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        Context.Scope.Own(new CombatActionLock(Actor, true));
        IDisposable movementLease = Context.Scope.Own(character.AcquireMovementLock());
        Vector3 toTarget = Context.Target.transform.position - Actor.transform.position;
        Face(toTarget);
        Animate(SylvMoveRules.BabyDollEyesAnimation);

        effect = SpawnEffect(SylvMoveRules.BabyDollEyesPath, false);
        if (effect == null)
        {
            movementLease.Dispose();
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }

        yield return WaitDuration(SylvMoveRules.BabyDollEyesCastLock);
        movementLease.Dispose();
        if (!Interrupted) character.RecoverAnimation(SylvMoveRules.BabyDollEyesAnimation);
        if (!stopped) Context.Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteHyperVoice()
    {
        Context.Scope.Own(new CombatActionLock(Actor, false));
        Context.Scope.Own(character.AcquireHyperVoiceChannel(direction, Controller.Combat.Now + SylvMoveRules.HyperVoiceDuration));
        Face(direction);
        Animate(SylvMoveRules.HyperVoiceAnimation);

        GameObject spawned = Controller.Combat.Spawner.Spawn(prefab, Actor.transform.position, Quaternion.LookRotation(direction), Context.Scope, true);
        hitbox = spawned != null ? spawned.GetComponent<HypervoiceHitbox>() : null;
        if (hitbox == null)
        {
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }

        hitbox.HoldAsset(Addressables.LoadAssetAsync<GameObject>(SylvMoveRules.HyperVoicePath));
        ulong attackerId = Actor.Pokemon.NetworkObjectId;
        hitbox.InitializeServer(
            Actor,
            direction,
            SylvMoveRules.HyperVoiceCloseDamage(attackerId),
            SylvMoveRules.HyperVoiceFarDamage(attackerId),
            Actor.CurrentTeam.Team,
            SylvMoveRules.HyperVoiceWaveCount(Context.Upgraded));

        float started = Time.time;
        while (!Interrupted && Time.time - started < SylvMoveRules.HyperVoiceDuration)
        {
            Face(direction);
            if (hitbox != null)
                hitbox.transform.SetPositionAndRotation(Actor.transform.position, Quaternion.LookRotation(direction));
            yield return null;
        }

        if (hitbox != null)
        {
            hitbox.DespawnSelf();
            hitbox = null;
        }
        if (!Interrupted) character.RecoverAnimation(SylvMoveRules.HyperVoiceAnimation);
        if (!stopped) Context.Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteMysticalFire()
    {
        Context.Scope.Own(new CombatActionLock(Actor, true));
        IDisposable movementLease = Context.Scope.Own(character.AcquireMovementLock());
        Face(direction);
        Animate(SylvMoveRules.MysticalFireAnimation);

        effect = SpawnEffect(SylvMoveRules.MysticalFirePath, false);
        if (effect == null)
        {
            movementLease.Dispose();
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }

        character.BeginDash(direction, SylvMoveRules.FireDashRange, SylvMoveRules.FireDashDuration);
        Context.Scope.Own(new CombatCallbackLease(() => character.EndDash(false)));
        float started = Time.time;
        while (!Interrupted && Time.time - started < SylvMoveRules.FireDashDuration)
            yield return null;

        bool completed = !Interrupted;
        character.EndDash(completed);
        movementLease.Dispose();
        if (completed) character.RecoverAnimation(SylvMoveRules.MysticalFireAnimation);
        if (!stopped) Context.Finish(completed ? CastOutcome.Completed : CastOutcome.Interrupted);
    }

    private IEnumerator ExecuteDrainingKiss()
    {
        if (!SylvMoveRules.CanHit(Actor, Context.Target))
        {
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        Context.Scope.Own(new CombatActionLock(Actor, true));
        IDisposable movementLease = Context.Scope.Own(character.AcquireMovementLock());
        Face(Context.Target.transform.position - Actor.transform.position);
        Animate(SylvMoveRules.DrainingKissAnimation);

        effect = SpawnEffect(SylvMoveRules.DrainingKissPath, false);
        if (effect == null)
        {
            movementLease.Dispose();
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }

        yield return WaitDuration(SylvMoveRules.KissCastLock);
        movementLease.Dispose();
        if (!Interrupted) character.RecoverAnimation(SylvMoveRules.DrainingKissAnimation);
        if (!stopped) Context.Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteCalmMind()
    {
        Animate(SylvMoveRules.CalmMindAnimation);
        effect = SpawnEffect(SylvMoveRules.CalmMindPath, false);
        if (effect == null)
        {
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }
        yield return null;
        if (!Interrupted) character.RecoverAnimation(SylvMoveRules.CalmMindAnimation);
        if (!stopped) Context.Finish(Interrupted ? CastOutcome.Interrupted : CastOutcome.Completed);
    }

    private IEnumerator ExecuteUnite()
    {
        effect = SpawnEffect(SylvMoveRules.UnitePath, false);
        SylvFairyFrolicEffect frolic = effect as SylvFairyFrolicEffect;
        if (frolic == null)
        {
            Context.Finish(CastOutcome.ExecutionFailed);
            yield break;
        }

        bool landed = false;
        Action onLanded = () => landed = true;
        frolic.Landed += onLanded;
        Context.Scope.Own(new CombatCallbackLease(() =>
        {
            if (frolic != null) frolic.Landed -= onLanded;
        }));

        float timeout = Time.time + SylvMoveRules.UniteAirborneDuration + 0.5f;
        while (Alive && !landed && frolic != null && frolic.IsSpawned && Time.time < timeout)
            yield return null;

        if (!Alive)
        {
            if (!stopped) Context.Finish(CastOutcome.Interrupted);
            yield break;
        }

        character.RecoverAnimation(SylvMoveRules.UniteAnimation);
        if (!stopped) Context.Finish(CastOutcome.Completed);
    }

    private PrefabMoveEffect SpawnEffect(string path, bool despawnWithScope)
    {
        Vector3 spawnPos = Actor.transform.position + Vector3.up * 0.8f;
        GameObject spawned = Controller.Combat.Spawner.Spawn(prefab, spawnPos, Quaternion.LookRotation(direction), Context.Scope, despawnWithScope);
        if (spawned == null) return null;
        PrefabMoveEffect spawnedEffect = spawned.GetComponent<PrefabMoveEffect>();
        if (spawnedEffect == null) return null;
        spawnedEffect.HoldAsset(Addressables.LoadAssetAsync<GameObject>(path));
        spawnedEffect.MoveId = Context.MoveId;
        spawnedEffect.CastSequence = unchecked((int)Context.CastId);
        character.ConfigureEffect(spawnedEffect, Context);
        character.RegisterEffect(Context.MoveId, spawnedEffect);
        spawnedEffect.Initialize(Actor, direction, Context.Target, Context.Upgraded);
        return spawnedEffect;
    }

    private IEnumerator WaitDuration(float duration)
    {
        float started = Time.time;
        while (!Interrupted && Time.time - started < duration)
            yield return null;
    }

    private IEnumerator WaitDuration(float duration, Vector3 faceDirection)
    {
        float started = Time.time;
        while (!Interrupted && Time.time - started < duration)
        {
            Face(faceDirection);
            yield return null;
        }
    }

    private void Animate(string state) => Actor.AnimationManager?.PlayAnimation(state);

    private void Face(Vector3 value)
    {
        value.y = 0f;
        if (value.sqrMagnitude > 0.001f)
            Actor.transform.rotation = Quaternion.LookRotation(value.normalized);
    }

    public override void Stop(CastOutcome reason)
    {
        if (stopped) return;
        stopped = true;
        routine?.Dispose();
        routine = null;

        if (hitbox != null)
        {
            hitbox.DespawnSelf();
            hitbox = null;
        }

        if (reason != CastOutcome.Completed && effect != null && effect.IsSpawned)
        {
            if (Context.MoveId == AvailableMoves.SylvMysticalCringe || Context.MoveId == AvailableMoves.SylveonUnite)
                effect.Despawn();
        }
    }
}
