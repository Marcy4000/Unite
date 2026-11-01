using System.Collections;
using System;
using UnityEngine;
using Unite.Combat;

public sealed class GlaceonExecution : CombatExecution
{
    private readonly GlaceonCharacterRuntime character;
    private GameObject prefab, spawned;
    private CombatRoutine routine, burst;
    private FreezeDryProjectile freeze;
    private GlaceUniteArea area;
    private IDisposable windShardLock;
    private CombatScope spearScope;
    private bool stopped, windConsumed;
    private byte storedCrystals;
    private int stars = 4;
    private Vector3 direction;
    public override bool FollowUpReady => Context.MoveId == AvailableMoves.GlaceIcycleSpear && !stopped;

    public GlaceonExecution(CombatExecutionContext context, GlaceonCharacterRuntime character) : base(context)
    {
        this.character = character;
        direction = context.Direction;
        direction.y = 0;
        direction = direction.sqrMagnitude > 0.001f ? direction.normalized : context.Actor.transform.forward;
    }

    private string Path
    {
        get
        {
            switch (Context.MoveId)
            {
                case AvailableMoves.GlaceSwift:
                case AvailableMoves.GlaceIceShard: return GlaceonCharacterRuntime.StarPath;
                case AvailableMoves.GlaceTailWhip: return GlaceonCharacterRuntime.TailPath;
                case AvailableMoves.GlaceIcyWind: return GlaceonCharacterRuntime.BasicPath;
                case AvailableMoves.GlaceFreezeDry: return "Assets/Prefabs/Objects/Moves/Glaceon/FreezeDryProjectile.prefab";
                case AvailableMoves.GlaceIcycleSpear: return "Assets/Prefabs/Objects/Moves/Glaceon/IcicleSpear.prefab";
                case AvailableMoves.GlaceonUnite: return "Assets/Prefabs/Objects/Moves/Glaceon/GlaceonUnite.prefab";
                default: return null;
            }
        }
    }

    public override IEnumerator Prepare()
    {
        Prepared = false;
        if (!Actor.IsServer || Path == null) yield break;
        yield return Controller.Combat.Spawner.Load(Path, Context.Scope, loaded => prefab = loaded);
        if (prefab == null || Context.Scope.IsDisposed) yield break;
        switch (Context.MoveId)
        {
            case AvailableMoves.GlaceFreezeDry: Prepared = prefab.GetComponent<FreezeDryProjectile>() != null; break;
            case AvailableMoves.GlaceIcycleSpear: Prepared = prefab.GetComponent<IcicleSpearHitbox>() != null; break;
            case AvailableMoves.GlaceonUnite: Prepared = prefab.GetComponent<GlaceUniteArea>() != null; break;
            default: Prepared = prefab.GetComponent<HomingProjectile>() != null; break;
        }
    }

    public override void Start()
    {
        if (!Prepared || stopped || !Actor.IsServer || Context.Scope.IsDisposed) return;
        routine = Context.Scope.Own(new CombatRoutine(Actor, RunLifecycle()));
    }

    private bool Alive => !stopped && Actor != null && Actor.IsSpawned && Actor.Pokemon.CurrentHp > 0;
    private bool Interrupted => !Alive || Actor.Pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions);

    private IEnumerator RunLifecycle()
    {
        yield return null;
        if (Interrupted) { Context.Finish(CastOutcome.Interrupted); yield break; }
        yield return Execute();
        if (!stopped) Context.Finish(Alive ? CastOutcome.Completed : CastOutcome.Interrupted);
    }

    private IEnumerator Execute()
    {
        switch (Context.MoveId)
        {
            case AvailableMoves.GlaceSwift:
                yield return ExecuteSwift();
                break;
            case AvailableMoves.GlaceTailWhip:
                yield return ExecuteTailWhip();
                break;
            case AvailableMoves.GlaceIceShard:
                yield return ExecuteIceShard();
                break;
            case AvailableMoves.GlaceIcyWind:
                yield return ExecuteIcyWind();
                break;
            case AvailableMoves.GlaceFreezeDry:
                yield return ExecuteFreezeDry();
                break;
            case AvailableMoves.GlaceIcycleSpear:
                yield return ExecuteIcicleSpear();
                break;
            case AvailableMoves.GlaceonUnite:
                yield return ExecuteUnite();
                break;
        }
    }

    private IEnumerator ExecuteSwift()
    {
        character.Swift?.End(CastOutcome.Replaced);
        character.Swift = this;
        yield return Duration(5);
    }

    private IEnumerator ExecuteTailWhip()
    {
        if (!GlaceonCharacterRuntime.CanHit(Actor, Context.Target))
        { Context.Finish(CastOutcome.ExecutionFailed); yield break; }
        spawned = Spawn();
        var tail = spawned != null ? spawned.GetComponent<HomingProjectile>() : null;
        if (tail == null) { Context.Finish(CastOutcome.ExecutionFailed); yield break; }
        tail.SetTargetRPC(Context.Target.NetworkObjectId, Damage(0.61f, 6, 190));
        character.FillCharge();
        Face(Context.Target.transform.position - Actor.transform.position);
        Animate(Actor.Pokemon.CurrentLevel >= 3 ? "ani_spell2_bat_0471" : "ani_spell2G_bat_0133");
        yield return MovementLock(0.4f);
        yield return WaitForProjectile(5);
    }

    private IEnumerator ExecuteIceShard()
    {
        character.Shard?.End(CastOutcome.Replaced);
        character.Shard = this;
        character.SetMode(1);
        Actor.Pokemon.AddStatChange(new StatChange(40, Stat.Speed, 0.5f, true, true, true, 0));
        Actor.Pokemon.AddStatChange(new StatChange((short)(Context.Upgraded ? 100 : 60), Stat.AtkSpeed, 2.5f, true, true, true, 0));
        Animate("ani_spell2a_bat_0471");
        yield return Duration(2.5f);
    }

    private IEnumerator ExecuteIcyWind()
    {
        character.Wind?.End(CastOutcome.Replaced);
        character.Wind = this;
        character.SetMode(2);
        if (Controller.Combat.Loadout.Get((int)MoveType.MoveB)?.Definition.Id == (int)AvailableMoves.GlaceIceShard)
            windShardLock = Context.Scope.Own(character.LockShard());
        if (character.Crystals < 1) character.SetCrystals(2);
        character.ResetCrystalTimer();
        Face(direction);
        Animate("ani_spell2b_bat_0471");
        yield return Dash();
        yield return Duration(4.8f);
        if (windConsumed) yield return Duration(0.6f);
    }

    private IEnumerator ExecuteFreezeDry()
    {
        spawned = Spawn();
        freeze = spawned != null ? spawned.GetComponent<FreezeDryProjectile>() : null;
        if (freeze == null) { Context.Finish(CastOutcome.ExecutionFailed); yield break; }
        freeze.OnMoveHit += FreezeHit;
        Context.Scope.Own(new GlaceonCleanup(() => { if (freeze != null) freeze.OnMoveHit -= FreezeHit; }));
        freeze.SetDirection(Actor.transform.position, new Vector2(direction.x, direction.z), Damage(0.65f, 5, 210), 6);
        Face(direction);
        Animate("ani_spell1b_bat_0471");
        yield return MovementLock(0.35f);
        yield return WaitForProjectile(1);
    }

    private IEnumerator ExecuteIcicleSpear()
    {
        yield return ConsumeIcicleSpearCrystals();
    }

    private IEnumerator ExecuteUnite()
    {
        spawned = Spawn();
        area = spawned != null ? spawned.GetComponent<GlaceUniteArea>() : null;
        if (area == null) { Context.Finish(CastOutcome.ExecutionFailed); yield break; }
        area.onGiveGlaceonSpears += GiveCrystal;
        Context.Scope.Own(new GlaceonCleanup(() => { if (area != null) area.onGiveGlaceonSpears -= GiveCrystal; }));
        area.InitializeServer(Actor.transform.position, Quaternion.LookRotation(direction), Damage(1, 7, 320), Actor.CurrentTeam.Team, Actor.Pokemon.NetworkObjectId);
        Animate("ani_spellu_bat_0471");
        Actor.Pokemon.AddStatChange(new StatChange(30, Stat.Speed, 6, true, true, true, 0));
        Actor.Pokemon.AddStatChange(new StatChange(35, Stat.AtkSpeed, 6, true, true, true, 0));
        var flight = Context.Scope.Own(character.AcquireFlight());
        yield return Duration(0.3f);
        flight.Dispose();
        yield return WaitForProjectile(8.5f);
    }

    private IEnumerator Duration(float duration)
    {
        float until = Time.time + duration;
        while (Alive && Time.time < until) yield return null;
    }

    private IEnumerator MovementLock(float duration)
    {
        var lease = Context.Scope.Own(character.AcquireMovementLock());
        yield return Duration(duration);
        lease.Dispose();
    }

    private IEnumerator Dash()
    {
        var lease = Context.Scope.Own(character.AcquireMovementLock());
        character.BeginDash(direction);
        Context.Scope.Own(new GlaceonCleanup(() => character.EndDash(false)));
        float started = Time.time;
        while (!Interrupted && Time.time - started < 0.2f)
        {
            yield return null;
        }
        character.EndDash(!Interrupted);
        lease.Dispose();
    }

    private IEnumerator WaitForProjectile(float timeout)
    {
        float until = Time.time + timeout;
        while (Alive && spawned != null && Time.time < until) yield return null;
    }

    private IEnumerator ConsumeIcicleSpearCrystals()
    {
        Context.Scope.Own(new GlaceonCleanup(() => spearScope?.Dispose()));
        float next = Time.time;
        if (character.Crystals < 1) { character.SetCrystals(2); next += 1; }
        character.ResetCrystalTimer();
        while (!Interrupted && character.Crystals > 0)
        {
            UpdateSpearInput();
            if (stopped) yield break;
            if (spawned != null)
            {
                spawned.transform.SetPositionAndRotation(Actor.transform.position, Quaternion.LookRotation(direction));
                Face(direction);
            }
            if (Time.time >= next)
            {
                character.SetCrystals(character.Crystals - 1);
                character.ResetCrystalTimer();
                next = Time.time + 1;
            }
            yield return null;
        }
        if (Interrupted) Context.Finish(CastOutcome.Interrupted);
    }

    private void UpdateSpearInput()
    {
        if (!Context.InputActive)
        {
            spearScope?.Dispose();
            spearScope = null;
            spawned = null;
            return;
        }
        if (spearScope != null) return;
        spearScope = new CombatScope();
        spawned = Controller.Combat.Spawner.Spawn(prefab, Actor.transform.position, Quaternion.LookRotation(direction), spearScope);
        var hitbox = spawned != null ? spawned.GetComponent<IcicleSpearHitbox>() : null;
        if (hitbox == null) { Context.Finish(CastOutcome.ExecutionFailed); return; }
        hitbox.InitializeServer(Damage(0.64f, 6, 130), Actor.CurrentTeam.Team, Context.Upgraded);
        spearScope.Own(character.LockSpear());
        Actor.Pokemon.AddStatChange(new StatChange(80, Stat.Speed, 0, false, false, true, 3));
        spearScope.Own(new GlaceonCleanup(() =>
        {
            if (Actor != null && Actor.IsSpawned) Actor.Pokemon.RemoveStatChangeWithIDRPC(3);
        }));
        Animate("ani_spell1a1_bat_0471");
    }

    public override void SetInputActive(bool active)
    {
        base.SetInputActive(active);
        if (!active && Context.MoveId == AvailableMoves.GlaceIcycleSpear) UpdateSpearInput();
    }

    public override bool TryRecast(Vector3 value, ulong targetId)
    {
        if (Context.MoveId != AvailableMoves.GlaceIcycleSpear || stopped || !CombatTargeting.Finite(value)) return false;
        value.y = 0;
        if (value.sqrMagnitude <= 0.001f) return false;
        direction = value.normalized;
        Context.InputActive = true;
        return true;
    }

    internal void ShootStars(Pokemon target, int amount)
    {
        if (!Alive || stars <= 0) return;
        for (int index = 0; index < amount && stars > 0; index++)
        {
            if (!character.Launch(target, Damage(0.16f, 6, 110), GlaceonCharacterRuntime.StarPath)) break;
            stars--;
        }
    }

    internal void ConsumeWind(Pokemon target)
    {
        if (!Alive || windConsumed || !GlaceonCharacterRuntime.CanHit(Actor, target)) return;
        windConsumed = true;
        character.SetMode(0);
        windShardLock?.Dispose();
        character.Swift?.ShootStars(target, character.Charge == 0 ? 2 : 1);
        burst = Context.Scope.Own(new CombatRoutine(Actor, WindBurst(target)));
    }

    private IEnumerator WindBurst(Pokemon target)
    {
        yield return null;
        storedCrystals = character.Crystals;
        character.SetCrystals(0);
        Face(target.transform.position - Actor.transform.position);
        Animate("ani_spell1b_bat_0471");
        var lease = Context.Scope.Own(character.AcquireMovementLock());
        float unlockAt = Time.time + 0.25f;
        for (int index = 0; index < storedCrystals && Alive; index++)
        {
            character.Launch(target, Damage(0.20f, 2, 35, DamageProprieties.CanCrit), GlaceonCharacterRuntime.BasicPath);
            yield return new WaitForSeconds(0.01f);
        }
        float restoreAt = Time.time + 0.45f;
        yield return Duration(Mathf.Max(0, unlockAt - Time.time));
        lease.Dispose();
        character.RecoverBasicAnimation("ani_spell1b_bat_0471");
        yield return Duration(Mathf.Max(0, restoreAt - Time.time));
        if (Alive) character.SetCrystals(storedCrystals);
        storedCrystals = 0;
        End(CastOutcome.Completed);
    }

    private void FreezeHit(int count)
    {
        if (!Alive || count <= 0) return;
        Actor.Pokemon.AddStatChange(new StatChange(50, Stat.SpAttack, 3, true, true, true, 0));
        character.SetCrystals(character.Crystals + count * (Context.Upgraded ? 3 : 2));
    }

    private void GiveCrystal() { if (Alive) character.SetCrystals(character.Crystals + 1); }
    private GameObject Spawn() => Controller.Combat.Spawner.Spawn(prefab, Actor.transform.position, Quaternion.LookRotation(direction), Context.Scope);
    private DamageInfo Damage(float ratio, short slider, short basis, DamageProprieties flags = default)
        => new DamageInfo(Actor.Pokemon.NetworkObjectId, ratio, slider, basis, DamageType.Special, flags);
    private void Animate(string animation) { if (Actor.AnimationManager != null) Actor.AnimationManager.PlayAnimation(animation); }
    private void Face(Vector3 value)
    {
        value.y = 0;
        if (value.sqrMagnitude > 0.001f) Actor.transform.rotation = Quaternion.LookRotation(value);
    }
    private void End(CastOutcome reason) { if (!stopped) Context.Finish(reason); }

    public override void Stop(CastOutcome reason)
    {
        if (stopped) return;
        stopped = true;
        routine?.Dispose();
        burst?.Dispose();
        if (character.Swift == this) character.Swift = null;
        if (character.Shard == this) { character.Shard = null; if (character.BasicMode == 1) character.SetMode(0); }
        if (character.Wind == this) { character.Wind = null; if (character.BasicMode == 2) character.SetMode(0); }
    }
}
