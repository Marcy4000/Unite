using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Server-owned passive, ammo and Future Sight interactions. No alternate damage/status engine.</summary>
public sealed class EspeonRuntime : IDisposable
{
    private readonly PlayerManager actor;
    private readonly EspeonBlasts blasts = new EspeonBlasts();
    private readonly ushort immunity = EspeonEffectIds.Acquire(), slow = EspeonEffectIds.Acquire();
    private readonly ushort teamBonusId = EspeonEffectIds.Acquire();
    private readonly ushort hasteId = EspeonEffectIds.Acquire();
    private readonly HashSet<Pokemon> slowed = new HashSet<Pokemon>();
    private double passiveNext, immuneUntil, bonusUntil;
    private double nextTeamCheck;
    private bool teamBonus;
    private bool storedLearned, wasAlive = true, evolved, disposed;
    public EspeonFutureSightEffect Future { get; private set; }
    public Pokemon MarkedTarget => Future != null && Future.MarkIntact ? Future.MarkedTarget : null;
    public int BlastCount => blasts.Count;
    public bool BonusReady => Now < bonusUntil;
    public double BonusExpiresAt => bonusUntil;
    private double Now => actor.NetworkManager.ServerTime.Time;

    public EspeonRuntime(PlayerManager actor)
    {
        this.actor = actor;
        blasts.Reset(Now);
        actor.Pokemon.BeforeStatusEffect += FilterStatus;
        actor.Pokemon.BeforeStatChange += FilterStat;
        actor.Pokemon.OnOtherPokemonKilled += Killed;
        actor.Pokemon.OutgoingDamageMultiplier += Multiplier;
        actor.Pokemon.OnDeath += OnDeath;
    }
    public void Tick()
    {
        if (disposed) return;
        bool alive = EspeonMoveRules.Living(actor.Pokemon);
        bool evolvedNow = actor.Pokemon.CurrentLevel >= 3;
        if (alive && !wasAlive) blasts.Reset(Now); // Respawn starts empty, not with time banked while dead.
        if (evolvedNow != evolved || (alive && !wasAlive)) actor.MovesController.SetEspeonBoostReady(0);
        evolved = evolvedNow; wasAlive = alive;
        if (Future != null && !Future.MarkIntact) Future = null;
        bool learned = actor.MovesController.HasEspeonMove(AvailableMoves.EspeonStoredPower);
        if (learned && !storedLearned) blasts.Reset(Now);
        storedLearned = learned;
        if (alive && storedLearned) blasts.Tick(Now);
        actor.MovesController.SetEspeonResources(blasts.Count, bonusUntil);
        if (Now >= nextTeamCheck) { nextTeamCheck = Now + 1; UpdateTeamBonus(); }
    }
    private void UpdateTeamBonus()
    {
        int others = 0; bool allEevees = true;
        if (GameManager.Instance != null)
            foreach (var player in GameManager.Instance.Players)
            {
                if (player == null || player == actor || !player.CurrentTeam.IsOnSameTeam(actor.CurrentTeam)) continue;
                others++;
                string name = player.Pokemon.BaseStats != null ? player.Pokemon.BaseStats.PokemonName.ToLowerInvariant() : "";
                allEevees &= name == "espeon" || name == "sylveon" || name == "glaceon" || name == "leafeon"
                    || name == "umbreon" || name == "vaporeon" || name == "jolteon" || name == "flareon";
            }
        bool active = others == 4 && allEevees;
        if (active != teamBonus || (active && !actor.Pokemon.HasStatChangeWithID(teamBonusId)))
        {
            teamBonus = active;
            actor.Pokemon.SetPersistentStatRPC(new StatChange(1, Stat.SpAttack, 0, false, true, true, teamBonusId, false), active);
        }
    }
    public void AddBlasts(int count)
    {
        if (storedLearned && !disposed && EspeonMoveRules.Living(actor.Pokemon)) { blasts.Add(count); actor.MovesController.SetEspeonResources(blasts.Count, bonusUntil); }
    }
    public void GrantStoredHaste()
    {
        actor.Pokemon.RemoveAllStatChangeWithIDRPC(hasteId);
        actor.Pokemon.AddStatChange(new StatChange(70, Stat.Speed, EspeonMoveRules.StoredHasteDuration, true, true, true, hasteId));
    }
    public bool CanCast(AvailableMoves move) => move != AvailableMoves.EspeonStoredPower
        || ((BonusReady || blasts.Count > 0) && EspeonStoredPowerEffect.HasTargets(actor));
    public bool Spend(AvailableMoves move, out bool empowered, out int count)
    {
        empowered = EspeonMoveRules.MoveOne(move) && BonusReady;
        count = 0;
        if (!CanCast(move)) return false;
        if (empowered) { bonusUntil = 0; count = EspeonMoveRules.EmpoweredBlasts; }
        else if (move == AvailableMoves.EspeonStoredPower) count = blasts.Spend();
        actor.MovesController.SetEspeonResources(blasts.Count, bonusUntil);
        return true;
    }
    public void Refund(AvailableMoves move, bool empowered, int count, double originalBonusExpiry)
    {
        if (disposed || !EspeonMoveRules.Living(actor.Pokemon)) return;
        if (empowered) bonusUntil = Math.Max(bonusUntil, originalBonusExpiry);
        else if (move == AvailableMoves.EspeonStoredPower) blasts.Add(count);
        actor.MovesController.SetEspeonResources(blasts.Count, bonusUntil);
    }
    public void Mark(EspeonFutureSightEffect effect)
    {
        if (Future != null && Future != effect) Future.Despawn();
        Future = effect;
    }
    public void Unmark(EspeonFutureSightEffect effect) { if (Future == effect) Future = null; }
    private void Killed(ulong targetId)
    {
        // The shared kill event runs before death clears the victim's statuses.
        if (disposed) return;
        if (Future != null && Future.MarkedTarget.NetworkObjectId == targetId && Future.MarkIntact && Future.MarkedTarget.Type == PokemonType.Player)
        {
            bonusUntil = Now + EspeonMoveRules.BonusWindow;
            actor.MovesController.SetEspeonResources(blasts.Count, bonusUntil);
        }
    }
    private float Multiplier(Pokemon target, DamageInfo damage) => Future != null && Future.UpgradedMark
        && Future.MarkIntact && Future.MarkedTarget == target ? 1.15f : 1;
    public int Hit(Pokemon target, DamageInfo damage, bool reducesFuture = false)
    {
        if (disposed || !EspeonMoveRules.Living(actor.Pokemon) || !EspeonMoveRules.CanHit(actor, target)) return 0;
        bool marked = reducesFuture && MarkedTarget == target;
        int result = target.ResolveDamage(damage);
        if (result > 0 && marked) actor.MovesController.ReduceEspeonFutureCooldown(EspeonMoveRules.FutureCooldownReduction);
        return result;
    }
    public void Slow(Pokemon target, short amount, float duration)
    {
        if (!EspeonMoveRules.Living(actor.Pokemon) || !EspeonMoveRules.Living(target) || target.HasStatusEffect(StatusType.Unstoppable)
            || target.HasStatusEffect(StatusType.Invincible)) return;
        slowed.Add(target);
        target.RemoveAllStatChangeWithIDRPC(slow);
        target.AddStatChange(new StatChange(amount, Stat.Speed, duration, true, false, true, slow).WithSource(actor.NetworkObjectId));
    }
    private bool FilterStatus(StatusEffect status)
    {
        if (!EspeonMoveRules.TriggersPassive(status)) return true;
        return !Block(status.HasSource, status.SourceId);
    }
    private StatChange? FilterStat(StatChange stat)
    {
        if (stat.Amount > 0 && EspeonMoveRules.TriggersPassive(stat) && Block(stat.HasSource, stat.SourceId)) return null;
        return stat;
    }
    private bool Block(bool hasSource, ulong source)
    {
        if (disposed || !EspeonMoveRules.Living(actor.Pokemon)) return false;
        if (Now < immuneUntil) return true;
        if (Now < passiveNext || actor.Pokemon.HasStatusEffect(StatusType.Invincible)
            || actor.Pokemon.HasStatusEffect(StatusType.Unstoppable)) return false;
        passiveNext = Now + EspeonMoveRules.PassiveCooldown; immuneUntil = Now + EspeonMoveRules.PassiveImmunity;
        actor.Pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, EspeonMoveRules.PassiveImmunity, true, immunity));
        actor.MovesController.ShowCombatVisualRPC(EspeonMoveRules.Visuals + "MagicBounce", actor.transform.position + Vector3.up,
            Vector3.forward, Vector3.one, EspeonMoveRules.PassiveImmunity);
        if (actor.Pokemon.CurrentLevel >= 3 && hasSource && actor.NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(source, out var obj)
            && obj.TryGetComponent(out Pokemon enemy) && EspeonMoveRules.CanHit(actor, enemy))
        {
            Hit(enemy, EspeonMoveRules.Counter(actor.NetworkObjectId));
            Slow(enemy, 30, 1);
            Vector3 delta = enemy.transform.position - actor.transform.position;
            actor.MovesController.ShowCombatVisualRPC(EspeonMoveRules.Visuals + "Beam", actor.transform.position + Vector3.up,
                delta, new Vector3(0.2f, 0.2f, delta.magnitude), 0.25f);
        }
        return true;
    }
    private void OnDeath(DamageInfo damage)
    {
        bonusUntil = 0; blasts.Reset(Now); immuneUntil = 0;
        if (Future != null) Future.Despawn();
        Future = null;
        actor.MovesController.SetEspeonBoostReady(0);
        actor.MovesController.SetEspeonResources(0, 0);
        wasAlive = false;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        actor.Pokemon.BeforeStatusEffect -= FilterStatus; actor.Pokemon.BeforeStatChange -= FilterStat;
        actor.Pokemon.OnOtherPokemonKilled -= Killed; actor.Pokemon.OutgoingDamageMultiplier -= Multiplier;
        actor.Pokemon.OnDeath -= OnDeath;
        if (Future != null) Future.Despawn();
        foreach (var target in slowed) if (target != null && target.IsSpawned) target.RemoveAllStatChangeWithIDRPC(slow);
        if (actor.Pokemon != null && actor.Pokemon.IsSpawned) actor.Pokemon.RemoveStatusEffectWithID(immunity);
        if (actor.Pokemon != null && actor.Pokemon.IsSpawned)
        {
            actor.Pokemon.RemoveAllStatChangeWithIDRPC(hasteId);
            actor.Pokemon.SetPersistentStatRPC(new StatChange(1, Stat.SpAttack, 0, false, true, true, teamBonusId, false), false);
        }
        EspeonEffectIds.Release(immunity); EspeonEffectIds.Release(slow);
        EspeonEffectIds.Release(teamBonusId);
        EspeonEffectIds.Release(hasteId);
    }
}
