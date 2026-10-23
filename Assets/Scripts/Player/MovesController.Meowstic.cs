using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private readonly NetworkVariable<byte> meowsticAttackCharge = new NetworkVariable<byte>();
    private readonly NetworkVariable<byte> meowsticNearbyEnemies = new NetworkVariable<byte>();
    private readonly NetworkVariable<double> meowsticReflectUntil = new NetworkVariable<double>();
    private readonly NetworkVariable<int> meowsticStoredDamage = new NetworkVariable<int>();
    private readonly AvailableMoves[] meowsticLearned = { AvailableMoves.MeowsticMScratch, AvailableMoves.MeowsticMLeer, AvailableMoves.MeowsticMUnite };
    private readonly double[] meowsticNextCast = new double[3];
    private readonly List<MeowsticScope> meowsticScopes = new List<MeowsticScope>();
    private readonly MeowsticEspurrState meowsticEspurr = new MeowsticEspurrState();
    private MeowsticModifier meowsticPassiveCdr;
    private MeowsticScope meowsticReflect;
    private Action meowsticSelfReflectDeath;
    private bool meowsticBound, meowsticDetonate;
    private double meowsticNextBasic, meowsticLastBasic, meowsticActionUntil, meowsticPassiveTick, meowsticReflectStarted;
    private double MeowsticNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsMeowstic => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.MeowsticMPassive;
    public int MeowsticAttackCharge => meowsticAttackCharge.Value;
    public int MeowsticNearbyEnemies => meowsticNearbyEnemies.Value;
    public int MeowsticStoredDamage => meowsticStoredDamage.Value;
    public float MeowsticReflectRemaining => (float)Math.Max(0, meowsticReflectUntil.Value - MeowsticNow);
    public bool MeowsticReflectAvailable => MeowsticReflectRemaining > 0;

    private void UpdateMeowsticRuntime()
    {
        if (!IsMeowstic) return;
        if (IsClient) EnsureMeowsticProjectileAssets();
        if (IsOwner) EnsureMeowsticDecoration();
        if (!IsServer) return;
        if (!meowsticBound)
        {
            meowsticBound = true;
            pokemon.OnDeath += OnMeowsticDeath;
            pokemon.BeforeStatChange += FilterMeowsticEspurr;
        }
        if (pokemon.CurrentHp <= 0) return;
        if (meowsticAttackCharge.Value > 0 && MeowsticNow - meowsticLastBasic > 5) meowsticAttackCharge.Value = 0;
        if (MeowsticNow < meowsticPassiveTick) return;
        meowsticPassiveTick = MeowsticNow + 0.25;
        int count = 0;
        foreach (Pokemon target in MeowsticMoveRules.Targets(transform.position, 7))
            if (target.Type == PokemonType.Player && MeowsticMoveRules.VisibleEnemy(playerManager, target)) count++;
        meowsticNearbyEnemies.Value = (byte)Mathf.Min(5, count);
        if (pokemon.CurrentLevel < 3) return;
        short amount = (short)MeowsticMoveRules.EnemyCdr(count);
        if (meowsticPassiveCdr == null) meowsticPassiveCdr = new MeowsticModifier(pokemon, Stat.Cdr, amount, percentage: false, family: 1);
        else meowsticPassiveCdr.SetAmount(amount);
    }

    private StatChange? FilterMeowsticEspurr(StatChange change)
    {
        if (pokemon.CurrentHp <= 0) return change;
        StatChange? result = meowsticEspurr.Filter(change, MeowsticNow, pokemon.CurrentLevel >= 3);
        if (!result.HasValue)
            ShowMeowsticEffect(MeowsticEffect.Pulse, transform.position, transform.forward, 0.35f, NetworkObjectId, 0.8f);
        return result;
    }

    private void RecordMeowsticMove(AvailableMoves move)
    {
        if (!IsServer || !IsMeowstic) return;
        foreach (LearnableMove learn in pokemon.BaseStats.LearnableMoves)
            if (!learn.isUpgraded && learn.level <= pokemon.CurrentLevel)
                foreach (MoveAsset asset in learn.moves)
                    if (asset != null && asset.move == move && asset.moveType != MoveType.All) meowsticLearned[(int)asset.moveType] = move;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CastMeowsticRPC(MeowsticCast kind, Vector3 direction, Vector3 destination, ulong targetId)
    {
        if (!IsMeowstic || pokemon.CurrentHp <= 0 || !Enum.IsDefined(typeof(MeowsticCast), kind) || !MeowsticMoveRules.Finite(direction)
            || !MeowsticMoveRules.Finite(destination) || MeowsticNow + 0.04 < meowsticActionUntil || pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts)) return;
        int slot = MeowsticMoveRules.Slot(kind);
        bool starter = kind == MeowsticCast.Scratch || kind == MeowsticCast.Leer;
        if (meowsticLearned[slot] != MeowsticMoveRules.Move(kind) || MeowsticNow + 0.08 < meowsticNextCast[slot]
            || (!starter && pokemon.CurrentLevel < (slot == 0 ? 3 : slot == 1 ? 5 : 7))) return;
        direction = MeowsticMoveRules.Direction(direction, transform.forward);
        destination = transform.position + Vector3.ClampMagnitude(new Vector3(destination.x - transform.position.x, 0, destination.z - transform.position.z), MeowsticMoveRules.Range(kind));
        Pokemon recipient = pokemon;
        if (MeowsticMoveRules.Support(kind) && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
            && obj.TryGetComponent(out Pokemon ally) && MeowsticMoveRules.Ally(playerManager, ally)
            && (ally.transform.position - transform.position).sqrMagnitude <= 7.5f * 7.5f) recipient = ally;
        meowsticActionUntil = MeowsticNow + MeowsticMoveRules.Commitment(kind);
        meowsticNextCast[slot] = MeowsticNow + (slot == 2 ? 6.35f : MeowsticMoveRules.Cooldown(kind) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f));
        bool upgraded = MeowsticMoveRules.Upgraded(kind, pokemon.CurrentLevel);
        var scope = new MeowsticScope(this);
        switch (kind)
        {
            case MeowsticCast.Scratch: RunMeowstic(scope, MeowsticScratch(scope, direction)); break;
            case MeowsticCast.Leer: RunMeowstic(scope, MeowsticLeer(scope, direction)); break;
            case MeowsticCast.Reflect:
                meowsticReflect?.Dispose(); meowsticReflect = scope;
                RunMeowstic(scope, MeowsticReflectRoutine(scope, recipient, upgraded)); break;
            case MeowsticCast.MagicCoat: RunMeowstic(scope, MeowsticCoat(scope, upgraded)); break;
            case MeowsticCast.Psychic: RunMeowstic(scope, MeowsticPsychic(scope, recipient, upgraded)); break;
            case MeowsticCast.WonderRoom: RunMeowstic(scope, MeowsticRoom(scope, destination, direction, upgraded)); break;
            default: RunMeowstic(scope, MeowsticHarmony(scope)); break;
        }
    }

    private void RunMeowstic(MeowsticScope scope, IEnumerator routine)
    {
        meowsticScopes.Add(scope); StartCoroutine(MeowsticRoutine(scope, routine));
    }
    private IEnumerator MeowsticRoutine(MeowsticScope scope, IEnumerator routine)
    {
        try { while (scope.Live && routine.MoveNext()) yield return routine.Current; }
        finally { (routine as IDisposable)?.Dispose(); scope.Dispose(); meowsticScopes.Remove(scope); }
    }
    private IEnumerator MeowsticWait(MeowsticScope scope, double duration)
    {
        double end = MeowsticNow + duration;
        while (scope.Live && MeowsticNow < end) yield return null;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void MeowsticBasicRPC(ulong targetId)
    {
        if (!IsMeowstic || pokemon.CurrentHp <= 0 || MeowsticNow + 0.04 < meowsticActionUntil || MeowsticNow + 0.04 < meowsticNextBasic
            || pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts) || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
            || !obj.TryGetComponent(out Pokemon target) || !MeowsticMoveRules.VisibleEnemy(playerManager, target)
            || (target.transform.position - transform.position).sqrMagnitude > 4.5f * 4.5f) return;
        if (MeowsticNow - meowsticLastBasic > 5) meowsticAttackCharge.Value = 0;
        bool boosted = meowsticAttackCharge.Value == 2;
        meowsticAttackCharge.Value = (byte)(boosted ? 0 : meowsticAttackCharge.Value + 1);
        meowsticLastBasic = MeowsticNow; meowsticNextBasic = MeowsticNow + GetAtkSpeedCooldown();
        string species = pokemon.CurrentLevel >= 3 ? "0734" : "0733";
        playerManager.AnimationManager.PlayAnimation("Armature_pm" + species + "_00_" + (boosted ? "ba21_tokusyu01" : "ba20_buturi01"));
        var scope = new MeowsticScope(this); RunMeowstic(scope, MeowsticHoming(scope, target, boosted));
    }
    private IEnumerator MeowsticHoming(MeowsticScope scope, Pokemon target, bool boosted)
    {
        Vector3 position = MeowsticMoveRules.BasicAttackOrigin(pokemon, boosted);
        var flight = new MeowsticProjectileMotion(position, Time.time);
        scope.Visual(boosted ? MeowsticEffect.Boosted : MeowsticEffect.Basic, position, transform.forward, 2, target.NetworkObjectId);
        double until = MeowsticNow + 2;
        while (scope.Live && MeowsticMoveRules.Enemy(playerManager, target) && MeowsticNow < until)
        {
            Vector3 goal = target.transform.position + Vector3.up * 0.8f;
            position = flight.Step(goal, Time.time);
            if ((goal - position).sqrMagnitude < 0.04f)
            {
                target.TakeDamageRPC(boosted ? new DamageInfo(NetworkObjectId, 1.1f, 3, 70, DamageType.Special, DamageProprieties.IsBasicAttack)
                    : new DamageInfo(NetworkObjectId, 1, 0, 0, DamageType.Physical, DamageProprieties.IsBasicAttack));
                if (boosted && MeowsticMoveRules.Living(target) && !target.HasStatusEffect(StatusType.Unstoppable))
                {
                    target.RemoveAllStatChangeWithIDRPC(7340);
                    target.AddStatChange(new StatChange(15, Stat.Speed, 0.6f, true, false, true, 7340));
                }
                ShowMeowsticEffect(MeowsticEffect.Pulse, goal, transform.forward, 0.2f, size: boosted ? 0.6f : 0.3f);
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator MeowsticScratch(MeowsticScope scope, Vector3 direction)
    {
        scope.Visual(MeowsticEffect.Scratch, transform.position, direction, 0.35f);
        yield return MeowsticWait(scope, 0.12);
        if (!scope.Live || pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts)) yield break;
        var victims = new List<Pokemon>();
        foreach (Pokemon target in MeowsticMoveRules.Targets(transform.position, 2.5f))
            if (MeowsticMoveRules.Enemy(playerManager, target) && MeowsticMoveRules.InCone(transform.position, direction, target.transform.position, 2.5f, 55)) victims.Add(target);
        var damage = new DamageInfo(NetworkObjectId, 0.45f, 5, 160, DamageType.Special);
        foreach (Pokemon target in victims) target.TakeDamageRPC(damage);
        yield return MeowsticWait(scope, 0.16);
        if (scope.Live && !pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts))
            foreach (Pokemon target in victims) if (MeowsticMoveRules.Enemy(playerManager, target)) target.TakeDamageRPC(damage);
    }
    private IEnumerator MeowsticLeer(MeowsticScope scope, Vector3 direction)
    {
        scope.Visual(MeowsticEffect.Leer, transform.position, direction, 0.3f);
        yield return MeowsticWait(scope, 0.1);
        if (!scope.Live || pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts)) yield break;
        foreach (Pokemon target in MeowsticMoveRules.Targets(transform.position, 3))
            if (MeowsticMoveRules.Enemy(playerManager, target) && MeowsticMoveRules.InCone(transform.position, direction, target.transform.position, 3))
            {
                target.RemoveAllStatChangeWithIDRPC(7341); target.RemoveAllStatChangeWithIDRPC(7342);
                target.AddStatChange(new StatChange(20, Stat.Defense, 3, true, false, true, 7341));
                target.AddStatChange(new StatChange(20, Stat.SpDefense, 3, true, false, true, 7342));
                ShowMeowsticEffect(MeowsticEffect.Pulse, target.transform.position, direction, 0.25f, target.NetworkObjectId, 0.7f);
            }
        yield return MeowsticWait(scope, 0.2);
    }

    private IEnumerator MeowsticReflectRoutine(MeowsticScope scope, Pokemon recipient, bool upgraded)
    {
        meowsticDetonate = false; meowsticStoredDamage.Value = 0;
        meowsticReflectStarted = MeowsticNow; meowsticReflectUntil.Value = MeowsticNow + 4;
        scope.Own(new MeowsticModifier(recipient, Stat.PhysicalDamageReduction, 35, percentage: false, family: 2));
        int stored = 0;
        bool exploded = false;
        Vector3 lastPosition = recipient.transform.position;
        void Record(DamageInfo info, int amount)
        {
            // The damage hook runs before HP/death callbacks can teleport a KO'd
            // player to the respawn holding position, including on special KOs.
            lastPosition = recipient.transform.position;
            if (info.type != DamageType.Physical) return;
            stored = MeowsticMoveRules.StoreReflect(stored, amount, info.type, upgraded);
            meowsticStoredDamage.Value = stored;
        }
        recipient.OnResolvedDamage += Record;
        scope.Own(new MeowsticCleanup(() => { if (recipient != null) recipient.OnResolvedDamage -= Record; }));
        void Explode()
        {
            if (exploded) return; exploded = true;
            Vector3 center = MeowsticMoveRules.Living(recipient) ? recipient.transform.position : lastPosition;
            ShowMeowsticEffect(MeowsticEffect.Detonation, center, transform.forward, 0.4f, size: 4);
            foreach (Pokemon target in MeowsticMoveRules.Targets(center, 4))
                if (MeowsticMoveRules.Enemy(playerManager, target) && stored > 0) target.TakeDamageRPC(new DamageInfo(NetworkObjectId, 0, 0, (short)stored, DamageType.True));
        }
        void OnTargetDeath(DamageInfo info) { Explode(); scope.Dispose(); }
        recipient.OnDeath += OnTargetDeath;
        scope.Own(new MeowsticCleanup(() => { if (recipient != null) recipient.OnDeath -= OnTargetDeath; }));
        if (recipient == pokemon) meowsticSelfReflectDeath = Explode;
        scope.Own(new MeowsticCleanup(() => { if (meowsticReflect == scope) { meowsticReflectUntil.Value = 0; meowsticStoredDamage.Value = 0; meowsticReflect = null; meowsticSelfReflectDeath = null; } }));
        scope.Visual(MeowsticEffect.Reflect, recipient.transform.position, transform.forward, 4, recipient.NetworkObjectId, detail: MeowsticMoveRules.ReflectCap(upgraded));
        while (scope.Live && MeowsticMoveRules.Living(recipient) && MeowsticNow < meowsticReflectUntil.Value && !meowsticDetonate)
        {
            lastPosition = recipient.transform.position;
            yield return null;
        }
        if (!scope.Live || recipient == null || !recipient.IsSpawned) yield break;
        Explode();
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void DetonateMeowsticReflectRPC()
    {
        if (IsMeowstic && pokemon.CurrentHp > 0 && MeowsticReflectAvailable && MeowsticNow + 0.04 >= meowsticReflectStarted + MeowsticMoveRules.Commitment(MeowsticCast.Reflect)
            && MeowsticNow + 0.04 >= meowsticActionUntil && !pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts)) meowsticDetonate = true;
    }

    private IEnumerator MeowsticPsychic(MeowsticScope scope, Pokemon recipient, bool upgraded)
    {
        var defense = new MeowsticModifier(recipient, Stat.Defense, 15, family: 3);
        var specialDefense = new MeowsticModifier(recipient, Stat.SpDefense, 15, family: 3);
        scope.Own(defense); scope.Own(specialDefense);
        scope.Visual(MeowsticEffect.Psychic, recipient.transform.position, transform.forward, 3.5f, recipient.NetworkObjectId, MeowsticMoveRules.PsychicRadius);
        var debuffs = new Dictionary<Pokemon, MeowsticPsychicDebuff>();
        double end = MeowsticNow + 3.5, nextPulse = MeowsticNow;
        while (scope.Live && MeowsticMoveRules.Living(recipient) && MeowsticNow < end)
        {
            if (MeowsticNow >= nextPulse)
            {
                nextPulse += 0.8;
                foreach (Pokemon target in MeowsticMoveRules.Targets(recipient.transform.position, MeowsticMoveRules.PsychicRadius))
                    if (MeowsticMoveRules.Enemy(playerManager, target))
                    {
                        target.TakeDamageRPC(new DamageInfo(NetworkObjectId, 0.45f, 3, 55, DamageType.Special));
                        if (!MeowsticMoveRules.Living(target)) continue;
                        if (!debuffs.TryGetValue(target, out var debuff)) { debuff = new MeowsticPsychicDebuff(target); debuffs.Add(target, debuff); scope.Own(debuff); }
                        debuff.Pulse(MeowsticNow);
                    }
                recipient.HealDamageRPC(new DamageInfo(NetworkObjectId, MeowsticMoveRules.PsychicHealingRatio(upgraded), 2, 45, DamageType.Special));
                ShowMeowsticEffect(MeowsticEffect.Pulse, recipient.transform.position, transform.forward, 0.3f, recipient.NetworkObjectId, MeowsticMoveRules.PsychicRadius);
            }
            foreach (var debuff in debuffs.Values) debuff.Tick(MeowsticNow);
            yield return null;
        }
        defense.Dispose(); specialDefense.Dispose();
        while (scope.Live && MeowsticNow < end + 3)
        {
            foreach (var debuff in debuffs.Values) debuff.Tick(MeowsticNow);
            yield return null;
        }
    }
    private sealed class MeowsticPsychicDebuff : IDisposable
    {
        private readonly MeowsticModifier attack, special;
        private readonly MeowsticPsychicStacks stacks = new MeowsticPsychicStacks();
        public MeowsticPsychicDebuff(Pokemon target)
        {
            attack = new MeowsticModifier(target, Stat.Attack, 0, false, family: 4, duration: 3);
            special = new MeowsticModifier(target, Stat.SpAttack, 0, false, family: 4, duration: 3);
        }
        public void Pulse(double now) { short amount = stacks.Pulse(now); attack.RefreshAmount(amount); special.RefreshAmount(amount); }
        public void Tick(double now) { if (stacks.Amount(now) > 0) return; attack.SetAmount(0); special.SetAmount(0); }
        public void Dispose() { attack.Dispose(); special.Dispose(); }
    }

    private IEnumerator MeowsticRoom(MeowsticScope scope, Vector3 center, Vector3 direction, bool upgraded)
    {
        scope.Visual(MeowsticEffect.WonderRoom, center, direction, 6, size: MeowsticMoveRules.RoomHalfSize);
        var occupants = new MeowsticOccupants(); scope.Own(occupants);
        Quaternion rotation = Quaternion.LookRotation(direction);
        double end = MeowsticNow + 6;
        while (scope.Live && MeowsticNow < end)
        {
            var targets = MeowsticMoveRules.Targets(Physics.OverlapBox(center + Vector3.up, new Vector3(MeowsticMoveRules.RoomHalfSize, 1.5f, MeowsticMoveRules.RoomHalfSize), rotation, LayerMask.GetMask("Players")));
            occupants.Refresh(targets, target =>
            {
                var leases = new List<IDisposable>();
                if (MeowsticMoveRules.Ally(playerManager, target))
                {
                    leases.Add(new MeowsticModifier(target, Stat.Defense, MeowsticMoveRules.RoomDefense(upgraded), family: 5));
                    leases.Add(new MeowsticModifier(target, Stat.SpDefense, MeowsticMoveRules.RoomDefense(upgraded), family: 5));
                }
                else if (MeowsticMoveRules.Enemy(playerManager, target))
                {
                    object source = new object(); target.SetWonderRoomSource(source, true);
                    leases.Add(new MeowsticCleanup(() => { if (target != null && target.IsSpawned) target.SetWonderRoomSource(source, false); }));
                    int visual = ShowMeowsticEffect(MeowsticEffect.Inversion, target.transform.position, direction, (float)(end - MeowsticNow), target.NetworkObjectId);
                    leases.Add(new MeowsticCleanup(() => { if (this != null && IsSpawned) StopMeowsticEffectRPC(visual); }));
                }
                return leases;
            }, target => MeowsticMoveRules.Ally(playerManager, target) || MeowsticMoveRules.Enemy(playerManager, target));
            yield return MeowsticWait(scope, 0.1);
        }
    }

    private IEnumerator MeowsticCoat(MeowsticScope scope, bool upgraded)
    {
        float duration = MeowsticMoveRules.CoatDuration(upgraded);
        scope.Own(new MeowsticModifier(pokemon, Stat.Speed, 20, family: 6));
        scope.Visual(MeowsticEffect.MagicCoat, transform.position, transform.forward, duration, NetworkObjectId, MeowsticMoveRules.CoatRadius);
        foreach (NetworkObject obj in NetworkManager.SpawnManager.SpawnedObjects.Values)
        {
            if (!obj.TryGetComponent(out Pokemon ally) || !MeowsticMoveRules.Ally(playerManager, ally)) continue;
            bool Protected() => scope.Live && !MeowsticReflection.Active && MeowsticMoveRules.Ally(playerManager, ally)
                && (ally.transform.position - transform.position).sqrMagnitude <= MeowsticMoveRules.CoatRadius * MeowsticMoveRules.CoatRadius;
            StatChange? StatFilter(StatChange change)
            {
                if (!Protected() || change.IsBuff || !change.IsTimed || !change.CanBeRemoved) return change;
                Pokemon enemy = MeowsticReflectionTarget();
                if (enemy != null)
                {
                    change.ID = 0; MeowsticReflection.Run(() => enemy.AddStatChange(change));
                    ShowMeowsticEffect(MeowsticEffect.Reflection, ally.transform.position + Vector3.up, transform.forward, 0.4f, enemy.NetworkObjectId);
                }
                return null;
            }
            bool StatusFilter(StatusEffect effect)
            {
                if (!Protected() || !MeowsticMoveRules.Reflectable(effect)) return true;
                Pokemon enemy = MeowsticReflectionTarget();
                if (enemy != null)
                {
                    effect.ID = 0; MeowsticReflection.Run(() => enemy.AddStatusEffect(effect));
                    ShowMeowsticEffect(MeowsticEffect.Reflection, ally.transform.position + Vector3.up, transform.forward, 0.4f, enemy.NetworkObjectId);
                }
                return false;
            }
            ally.BeforeStatChange += StatFilter; ally.BeforeStatusEffect += StatusFilter;
            scope.Own(new MeowsticCleanup(() => { if (ally != null) { ally.BeforeStatChange -= StatFilter; ally.BeforeStatusEffect -= StatusFilter; } }));
        }
        yield return MeowsticWait(scope, duration);
    }
    private Pokemon MeowsticReflectionTarget()
    {
        Pokemon best = null; float distance = float.MaxValue;
        foreach (Pokemon enemy in MeowsticMoveRules.Targets(transform.position, 8))
        {
            if (enemy.Type != PokemonType.Player || !MeowsticMoveRules.VisibleEnemy(playerManager, enemy)) continue;
            float squared = (enemy.transform.position - transform.position).sqrMagnitude;
            if (squared < distance || (Mathf.Approximately(squared, distance) && best != null && enemy.NetworkObjectId < best.NetworkObjectId)) { best = enemy; distance = squared; }
        }
        return best;
    }

    private IEnumerator MeowsticHarmony(MeowsticScope scope)
    {
        yield return MeowsticWait(scope, 0.35);
        if (!scope.Live || pokemon.HasAnyStatusEffect(MeowsticMoveRules.Interrupts)) yield break;
        ushort unstoppable = MeowsticEffectIds.Acquire();
        pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, 6, true, unstoppable));
        scope.Own(new MeowsticCleanup(() => { if (pokemon != null && pokemon.IsSpawned) pokemon.RemoveStatusEffectWithID(unstoppable); MeowsticEffectIds.Release(unstoppable); }));
        SetMeowsticScoringRPC(true);
        scope.Own(new MeowsticCleanup(() => { if (this != null && IsSpawned) SetMeowsticScoringRPC(false); }));
        scope.Visual(MeowsticEffect.Unite, transform.position, transform.forward, 6, NetworkObjectId, MeowsticMoveRules.UniteRadius);
        var occupants = new MeowsticOccupants(); scope.Own(occupants);
        var granted = new HashSet<ulong>();
        double end = MeowsticNow + 6, nextDamage = MeowsticNow;
        while (scope.Live && MeowsticNow < end)
        {
            var targets = MeowsticMoveRules.Targets(transform.position, MeowsticMoveRules.UniteRadius);
            if (!targets.Contains(pokemon)) targets.Add(pokemon);
            occupants.Refresh(targets, target =>
            {
                var leases = new List<IDisposable>();
                if (MeowsticMoveRules.Ally(playerManager, target))
                {
                    leases.Add(new MeowsticModifier(target, Stat.Speed, 20, family: 7));
                    leases.Add(new MeowsticModifier(target, Stat.Cdr, 10, percentage: false, family: 7));
                    if (granted.Add(target.NetworkObjectId))
                    {
                        scope.Own(new MeowsticShield(target, (float)(end - MeowsticNow)));
                        ShowMeowsticEffect(MeowsticEffect.Pulse, target.transform.position, transform.forward, 0.35f, target.NetworkObjectId, 1);
                    }
                }
                else if (MeowsticMoveRules.Enemy(playerManager, target) && !target.HasStatusEffect(StatusType.Unstoppable)) leases.Add(new MeowsticModifier(target, Stat.Speed, 25, false, family: 7));
                return leases;
            }, target => MeowsticMoveRules.Ally(playerManager, target) || (MeowsticMoveRules.Enemy(playerManager, target) && !target.HasStatusEffect(StatusType.Unstoppable)));
            if (MeowsticNow >= nextDamage)
            {
                nextDamage += 1;
                foreach (Pokemon target in targets)
                    if (MeowsticMoveRules.Enemy(playerManager, target)) target.TakeDamageRPC(new DamageInfo(NetworkObjectId, 0.5f, 4, 75, DamageType.Special, DamageProprieties.IsUniteMove));
            }
            yield return MeowsticWait(scope, 0.1);
        }
    }

    private IDisposable meowsticScoring;
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void SetMeowsticScoringRPC(bool active)
    {
        meowsticScoring?.Dispose(); meowsticScoring = null;
        if (active) meowsticScoring = playerManager.ScoreStatus.AcquireStatus(ActionStatusType.Busy);
    }
    private void OnMeowsticDeath(DamageInfo info)
    {
        meowsticSelfReflectDeath?.Invoke();
        EndMeowsticBasicRecovery(false);
        foreach (var scope in new List<MeowsticScope>(meowsticScopes)) scope.Dispose();
        meowsticScopes.Clear(); meowsticPassiveCdr?.Dispose(); meowsticPassiveCdr = null;
        meowsticAttackCharge.Value = meowsticNearbyEnemies.Value = 0;
        meowsticActionUntil = meowsticNextBasic = 0;
    }
    private void DisposeMeowsticRuntime()
    {
        EndMeowsticBasicRecovery(false);
        ClearMeowsticBufferedInput();
        foreach (MoveBase move in moves) if (move is MeowsticMoveBase) move.ResetMove();
        if (uniteMove is MeowsticMoveBase) uniteMove.ResetMove();
        foreach (var scope in new List<MeowsticScope>(meowsticScopes)) scope.Dispose();
        meowsticScopes.Clear(); meowsticPassiveCdr?.Dispose(); meowsticPassiveCdr = null;
        meowsticScoring?.Dispose(); meowsticScoring = null;
        if (meowsticBound && pokemon != null) { pokemon.OnDeath -= OnMeowsticDeath; pokemon.BeforeStatChange -= FilterMeowsticEspurr; }
        meowsticBound = false; DisposeMeowsticPresentation();
    }

    private sealed class MeowsticScope : IDisposable
    {
        private readonly MovesController owner;
        private readonly List<IDisposable> leases = new List<IDisposable>();
        private bool disposed;
        public bool Live => !disposed && owner != null && owner.IsSpawned && MeowsticMoveRules.Living(owner.pokemon);
        public MeowsticScope(MovesController owner) { this.owner = owner; }
        public void Own(IDisposable lease) { if (disposed) lease.Dispose(); else leases.Add(lease); }
        public void Visual(MeowsticEffect effect, Vector3 origin, Vector3 direction, float duration, ulong target = ulong.MaxValue, float size = 1, int detail = 0)
        {
            int id = owner.ShowMeowsticEffect(effect, origin, direction, duration, target, size, detail);
            Own(new MeowsticCleanup(() => { if (owner != null && owner.IsSpawned) owner.StopMeowsticEffectRPC(id); }));
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            for (int i = leases.Count - 1; i >= 0; i--) leases[i].Dispose();
            leases.Clear();
        }
    }
    private sealed class MeowsticOccupants : IDisposable
    {
        private readonly Dictionary<Pokemon, List<IDisposable>> active = new Dictionary<Pokemon, List<IDisposable>>();
        public void Refresh(List<Pokemon> targets, Func<Pokemon, List<IDisposable>> enter, Func<Pokemon, bool> eligible = null)
        {
            foreach (Pokemon previous in new List<Pokemon>(active.Keys))
                if (!MeowsticMoveRules.Living(previous) || !targets.Contains(previous) || (eligible != null && !eligible(previous)))
                {
                    foreach (var lease in active[previous]) lease.Dispose(); active.Remove(previous);
                }
            foreach (Pokemon target in targets) if (MeowsticMoveRules.Living(target) && !active.ContainsKey(target) && (eligible == null || eligible(target))) active.Add(target, enter(target));
        }
        public void Dispose() { foreach (var leases in active.Values) foreach (var lease in leases) lease.Dispose(); active.Clear(); }
    }
}
