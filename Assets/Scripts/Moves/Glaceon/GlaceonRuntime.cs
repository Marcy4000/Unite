using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GlaceonShot { Basic, Boosted, Shard, Star, Tail, Wind }
public sealed class GlaceonWindVolley
{
    private readonly Dictionary<Pokemon, int> hits = new Dictionary<Pokemon, int>();
    public int Hit(Pokemon target) { hits.TryGetValue(target, out int count); hits[target] = ++count; return count; }
}

// All resource changes, basic attack modes, and passive interception run on the server.
public sealed class GlaceonRuntime : IDisposable
{
    private readonly PlayerManager actor;
    private readonly GlaceonCrystalState crystals = new GlaceonCrystalState();
    private readonly List<Coroutine> volleys = new List<Coroutine>();
    private Coroutine basic;
    private int charge, stars;
    private double chargeUntil, swiftUntil, shardUntil, windUntil, cloakReady;
    private bool disposed, wasAlive = true, evolved;
    private readonly ushort shardSlow = GlaceonEffectIds.Acquire(), windSlow = GlaceonEffectIds.Acquire();
    private readonly ushort tailSpeed = GlaceonEffectIds.Acquire(), tailDefense = GlaceonEffectIds.Acquire(), tailSpecialDefense = GlaceonEffectIds.Acquire();
    private double Now => actor.NetworkManager.ServerTime.Time;
    public int Generation { get; private set; }
    public int Count => crystals.Count;
    public bool WindReady => Now < windUntil;
    public bool SpearHeld { get; private set; }
    public Vector3 SpearDirection { get; private set; }
    public IcicleSpearHitbox Spear { get; set; }
    public GlaceonRuntime(PlayerManager actor)
    {
        this.actor = actor;
        actor.Pokemon.OnDeath += Died;
        actor.Pokemon.BeforeStatusEffect += FilterStatus;
        actor.Pokemon.BeforeStatChange += FilterStat;
    }
    public void Tick()
    {
        if (disposed) return;
        bool alive = GlaceonMoveRules.Living(actor.Pokemon);
        bool nowEvolved = GlaceonMoveRules.Evolved(actor);
        if (alive && !wasAlive) cloakReady = 0;
        wasAlive = alive;
        if (nowEvolved && !evolved) { stars = 0; swiftUntil = 0; }
        evolved = nowEvolved;
        crystals.Tick(Now);
        if (Now >= chargeUntil) charge = 0;
        if (!actor.MovesController.HasGlaceMove(AvailableMoves.GlaceSwift)) stars = 0;
        if (!actor.MovesController.HasGlaceMove(AvailableMoves.GlaceIcyWind)) windUntil = 0;
        if (!actor.MovesController.HasGlaceMove(AvailableMoves.GlaceIceShard)) shardUntil = 0;
        Sync();
    }
    private void Sync() => actor.MovesController.SyncGlaceState(crystals.Count, crystals.ExpiresAt, charge,
        WindReady ? 2 : Now < shardUntil ? 1 : 0);
    public void AddCrystals(int count)
    {
        if (!disposed && GlaceonMoveRules.Living(actor.Pokemon) && GlaceonMoveRules.Evolved(actor)) { crystals.Add(count, Now); Sync(); }
    }
    public void RefreshCrystals() { crystals.Refresh(Now); Sync(); }
    public void ConsumeCrystal() { crystals.Consume(Now); Sync(); }
    public void BeginSpear(Vector3 direction) { SpearHeld = true; SpearDirection = GlaceonMoveRules.Direction(direction, actor.transform.forward); }
    public void SetSpearInput(Vector3 direction, bool released)
    {
        SpearDirection = GlaceonMoveRules.Direction(direction, actor.transform.forward);
        if (released) { SpearHeld = false; Spear?.ReleaseChannel(); }
    }
    public void EndSpear() { SpearHeld = false; Spear = null; }
    public void Swift() { stars = 4; swiftUntil = Now + 5; }
    public void TailReady() { charge = 2; chargeUntil = Now + 4.5; Sync(); }
    public void IceShard(bool upgraded)
    {
        if (WindReady || Spear != null) AddCrystals(2);
        shardUntil = Now + 2.5;
        Refresh(actor.Pokemon, new StatChange(40, Stat.Speed, 1, true, true, true, GlaceonMoveRules.ShardSpeed));
        Refresh(actor.Pokemon, new StatChange((short)(upgraded ? 100 : 60), Stat.AtkSpeed, 2.5f, true, true, true, GlaceonMoveRules.ShardAttackSpeed));
        Sync();
    }
    public void IcyWind() { windUntil = Now + 5; if (Count == 0) AddCrystals(2); RefreshCrystals(); Sync(); }
    public static void Refresh(Pokemon target, StatChange stat) { target.RemoveAllStatChangeWithIDRPC(stat.ID); target.AddStatChange(stat); }
    public void Basic(Pokemon target)
    {
        float range = WindReady ? GlaceonMoveRules.WindRange : GlaceonMoveRules.BasicRange;
        if (disposed || basic != null || !GlaceonMoveRules.CanHit(actor, target) || target.HasStatusEffect(StatusType.Invisible)
            || Vector3.Distance(actor.transform.position, target.transform.position) > range + 0.5f) return;
        basic = actor.StartCoroutine(Attack(target));
    }
    private IEnumerator Attack(Pokemon target)
    {
        int generation = Generation;
        bool wind = WindReady, shard = !wind && Now < shardUntil, boosted = !wind && !shard && charge == 2;
        Vector3 direction = GlaceonMoveRules.Direction(target.transform.position - actor.transform.position, actor.transform.forward);
        string clip = wind || shard ? "ani_spell1b_bat_0471" : evolved ? $"ani_atk{charge + 1}_bat_0471" : $"ani_atk{charge + 4}_bat_0133";
        float duration = Mathf.Clamp(actor.MovesController.GetAtkSpeedCooldown() * 0.5f, 0.1f, 0.5f);
        actor.MovesController.PresentServerAttackRPC(true, direction, clip, duration);
        try
        {
            yield return new WaitForSeconds(Mathf.Min(0.1f, duration));
            if (generation != Generation || !GlaceonMoveRules.Living(actor.Pokemon) || actor.Pokemon.HasAnyStatusEffect(GlaceonMoveRules.Interrupts)
                || !GlaceonMoveRules.CanHit(actor, target)) yield break;
            if (wind)
            {
                int count = crystals.TakeAll(Now); windUntil = 0;
                bool upgraded = actor.Pokemon.CurrentLevel >= 9;
                var volley = new GlaceonWindVolley();
                Coroutine running = null;
                IEnumerator TrackedVolley()
                {
                    yield return WindVolley(target, count, upgraded, volley, generation);
                    volleys.Remove(running);
                }
                running = actor.StartCoroutine(TrackedVolley()); volleys.Add(running);
            }
            else
            {
                actor.MovesController.LaunchGlaceProjectile(target, shard ? GlaceonShot.Shard : boosted ? GlaceonShot.Boosted : GlaceonShot.Basic);
                LaunchSwiftStars(target, boosted);
                if (!shard) { charge = (charge + 1) % 3; chargeUntil = Now + 4.5; }
            }
            Sync();
            yield return new WaitForSeconds(Mathf.Max(0, duration - 0.1f));
        }
        finally
        {
            basic = null;
            if (actor != null && actor.MovesController.IsSpawned) actor.MovesController.PresentServerAttackRPC(false, Vector3.zero, "", 0, actor.Pokemon.CurrentHp <= 0);
        }
    }
    private IEnumerator WindVolley(Pokemon target, int count, bool upgraded, GlaceonWindVolley volley, int generation)
    {
        for (int i = 0; i < count; i++)
        {
            if (generation != Generation || !GlaceonMoveRules.Living(actor.Pokemon)) yield break;
            if (GlaceonMoveRules.CanHit(actor, target)) actor.MovesController.LaunchGlaceProjectile(target, GlaceonShot.Wind, upgraded, volley);
            yield return new WaitForSeconds(0.01f);
        }
        yield return new WaitForSeconds(0.45f);
        if (generation == Generation) AddCrystals(count);
    }
    public void Hit(Pokemon target, GlaceonShot shot, bool upgraded, GlaceonWindVolley volley)
    {
        if (disposed || !GlaceonMoveRules.Living(actor.Pokemon) || !GlaceonMoveRules.CanHit(actor, target)) return;
        var move = shot == GlaceonShot.Shard ? AvailableMoves.GlaceIceShard : shot == GlaceonShot.Star ? AvailableMoves.GlaceSwift
            : shot == GlaceonShot.Tail ? AvailableMoves.GlaceTailWhip : AvailableMoves.GlaceIcyWind;
        DamageInfo damage = shot == GlaceonShot.Basic || shot == GlaceonShot.Boosted
            ? GlaceonMoveRules.Basic(actor.NetworkObjectId, shot == GlaceonShot.Boosted, evolved) : GlaceonMoveRules.Damage(actor.NetworkObjectId, move);
        short extra = shot == GlaceonShot.Wind && upgraded ? GlaceonMoveRules.WindExtraDamage(target.CurrentHp, target.Type == PokemonType.Wild) : (short)0;
        // One impact/crit roll and one set of on-hit procs for each crystal.
        damage.baseDmg = (short)Mathf.Clamp(damage.baseDmg + extra, 0, short.MaxValue);
        if (target.ResolveDamage(damage) <= 0) return;
        if (shot == GlaceonShot.Boosted || shot == GlaceonShot.Shard) AddCrystals(2);
        if (shot == GlaceonShot.Shard) Slow(target, 30, 1, shardSlow);
        if (shot == GlaceonShot.Tail)
        {
            Slow(target, 20, 1.5f, tailSpeed);
            Refresh(target, new StatChange(30, Stat.Defense, 2, true, false, true, tailDefense).WithSource(actor.NetworkObjectId));
            Refresh(target, new StatChange(30, Stat.SpDefense, 2, true, false, true, tailSpecialDefense).WithSource(actor.NetworkObjectId));
        }
        if (shot == GlaceonShot.Wind)
        {
            int slow = GlaceonMoveRules.WindSlow(volley?.Hit(target) ?? 1);
            if (slow > 0) Slow(target, (short)slow, 1, windSlow);
        }
    }
    private void LaunchSwiftStars(Pokemon target, bool boosted)
    {
        if (Now >= swiftUntil || stars <= 0 || !actor.MovesController.HasGlaceMove(AvailableMoves.GlaceSwift)) return;
        int count = Mathf.Min(stars, boosted ? 2 : 1);
        stars -= count;
        for (int i = 0; i < count; i++) actor.MovesController.LaunchGlaceProjectile(target, GlaceonShot.Star);
    }
    private void Slow(Pokemon target, short amount, float duration, ushort id)
    {
        if (!GlaceonMoveRules.Living(target) || target.HasStatusEffect(StatusType.Unstoppable)) return;
        Refresh(target, new StatChange(amount, Stat.Speed, duration, true, false, true, id).WithSource(actor.NetworkObjectId));
    }
    private bool FilterStatus(StatusEffect effect) => !effect.IsHindrance() || !TryCloak();
    private StatChange? FilterStat(StatChange stat)
    {
        if (!stat.IsBuff && stat.EffectiveAmount > 0 && stat.AffectedStat == Stat.Speed && stat.ID != GlaceonMoveRules.SpearSlow
            && (!stat.HasSource || stat.SourceId != actor.NetworkObjectId) && TryCloak()) return null;
        return stat;
    }
    private bool TryCloak()
    {
        if (disposed || Now < cloakReady || !GlaceonMoveRules.Living(actor.Pokemon) || actor.Pokemon.HasStatusEffect(StatusType.Invincible)
            || actor.Pokemon.HasStatusEffect(StatusType.Unstoppable)) return false;
        cloakReady = Now + 70;
        float duration = GlaceonMoveRules.Evolved(actor) ? 0.5f : 1;
        actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, duration, true, GlaceonMoveRules.Cloak));
        if (GlaceonMoveRules.Evolved(actor)) actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Invisible, duration, true, GlaceonMoveRules.Cloak));
        return true;
    }
    private void Died(DamageInfo info)
    {
        Generation++; wasAlive = false;
        if (basic != null) actor.StopCoroutine(basic);
        basic = null;
        foreach (var routine in volleys) if (routine != null) actor.StopCoroutine(routine);
        volleys.Clear();
        charge = stars = 0; chargeUntil = swiftUntil = shardUntil = windUntil = 0;
        crystals.Clear(); EndSpear(); Sync();
        if (actor.MovesController.IsSpawned) actor.MovesController.PresentServerAttackRPC(false, Vector3.zero, "", 0, true);
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Died(default);
        actor.Pokemon.OnDeath -= Died; actor.Pokemon.BeforeStatusEffect -= FilterStatus; actor.Pokemon.BeforeStatChange -= FilterStat;
        foreach (ushort id in new[] { shardSlow, windSlow, tailSpeed, tailDefense, tailSpecialDefense }) GlaceonEffectIds.ReleaseAfter(id, 4);
    }
}
