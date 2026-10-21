using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private readonly NetworkVariable<byte> vaporAttackCharge = new NetworkVariable<byte>();
    private readonly NetworkVariable<double> vaporDiveEnds = new NetworkVariable<double>();
    private readonly AvailableMoves[] vaporLearned = { AvailableMoves.VaporSwift, AvailableMoves.VaporHelpingHand, AvailableMoves.VaporeonUnite };
    private readonly double[] vaporNextCast = new double[3];
    private readonly List<VaporScope> vaporScopes = new List<VaporScope>();
    private readonly Dictionary<ulong, VaporScope> vaporRings = new Dictionary<ulong, VaporScope>();
    private static readonly Dictionary<ulong, float> vaporPullReady = new Dictionary<ulong, float>();
    private VaporScope vaporRain, vaporWhirlpool, vaporDive;
    private double vaporNextBasic, vaporLastBasic, vaporHydrationReady, vaporDiveStarted;
    private bool vaporBound, vaporEmergeRequested;
    private double VaporNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsLivingVaporeon => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.PokemonName == "Vaporeon" && pokemon.CurrentHp > 0;
    public int VaporeonAttackCharge => vaporAttackCharge.Value == 2 ? 3 : vaporAttackCharge.Value;
    public bool VaporeonDiving => vaporDiveEnds.Value > VaporNow;

    private void UpdateVaporeonRuntime()
    {
        UpdateVaporeonModel();
        if (IsClient && pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.PokemonName == "Vaporeon")
            EnsureVaporeonPresentationAssets();
        if (!IsServer || pokemon == null || pokemon.BaseStats == null || pokemon.BaseStats.PokemonName != "Vaporeon") return;
        if (!vaporBound) { vaporBound = true; pokemon.OnDeath += OnVaporeonDeath; }
        if (vaporAttackCharge.Value > 0 && VaporNow - vaporLastBasic >= VaporMoveRules.BoostExpiry) vaporAttackCharge.Value = 0;
    }

    private void RecordVaporeonMove(AvailableMoves move)
    {
        if (!IsServer) return;
        switch (move)
        {
            case AvailableMoves.VaporSwift:
            case AvailableMoves.VaporAquaRing:
            case AvailableMoves.VaporRainDance: vaporLearned[0] = move; break;
            case AvailableMoves.VaporHelpingHand:
            case AvailableMoves.VaporDive:
            case AvailableMoves.VaporWhirlpool: vaporLearned[1] = move; break;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CastVaporeonRPC(VaporCast kind, Vector3 destination, ulong targetId, bool hasTarget)
    {
        if (!IsLivingVaporeon || !Enum.IsDefined(typeof(VaporCast), kind) || VaporeonDiving
            || pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) return;
        int slot = VaporMoveRules.Slot(kind);
        if (vaporLearned[slot] != VaporMoveRules.Move(kind) || VaporNow + 0.2 < vaporNextCast[slot]) return;
        bool starter = kind == VaporCast.Swift || kind == VaporCast.HelpingHand;
        if (!starter && pokemon.CurrentLevel < (slot == 0 ? 3 : slot == 1 ? 5 : 7)) return;
        Pokemon recipient = pokemon;
        if (VaporMoveRules.IsSupport(kind))
        {
            bool valid = hasTarget && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
                && obj.TryGetComponent(out Pokemon ally) && VaporMoveRuntime.CanHeal(playerManager, ally)
                && (ally.transform.position - transform.position).sqrMagnitude <= 8.5f * 8.5f;
            if (valid) recipient = NetworkManager.SpawnManager.SpawnedObjects[targetId].GetComponent<Pokemon>();
            else if (kind == VaporCast.HelpingHand) return;
        }
        bool upgraded = VaporMoveRules.Upgraded(kind, pokemon.CurrentLevel);
        vaporNextCast[slot] = VaporNow + (slot == 2 ? 1 : VaporMoveRules.Cooldown(kind) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f));
        destination = VaporMoveRules.ClampDestination(transform.position, destination, VaporMoveRules.Range(kind));
        var scope = new VaporScope(this);
        switch (kind)
        {
            case VaporCast.Swift: RunVaporeon(scope, VaporeonSwift(scope, destination)); break;
            case VaporCast.HelpingHand: RunVaporeon(scope, VaporeonHelpingHand(scope, recipient)); break;
            case VaporCast.AquaRing:
                if (vaporRings.TryGetValue(recipient.NetworkObjectId, out var previous)) previous.Dispose();
                vaporRings[recipient.NetworkObjectId] = scope;
                RunVaporeon(scope, VaporeonRing(scope, recipient, upgraded)); break;
            case VaporCast.RainDance:
                vaporRain?.Dispose(); vaporRain = scope;
                PresentVaporeonHopRPC(VaporNow);
                RunVaporeon(scope, VaporeonRainField(scope, playerManager.PlayerMovement.VaporeonHopDestination(destination), upgraded)); break;
            case VaporCast.Whirlpool:
                vaporWhirlpool?.Dispose(); vaporWhirlpool = scope;
                RunVaporeon(scope, VaporeonWhirlpool(scope, destination, upgraded)); break;
            default: RunVaporeon(scope, VaporeonTempest(scope, transform.position)); break;
        }
    }

    private void RunVaporeon(VaporScope scope, IEnumerator routine)
    {
        vaporScopes.Add(scope); StartCoroutine(VaporeonRoutine(scope, routine));
    }
    private IEnumerator VaporeonRoutine(VaporScope scope, IEnumerator routine)
    {
        try { while (scope.Live && routine.MoveNext()) yield return routine.Current; }
        finally { (routine as IDisposable)?.Dispose(); scope.Dispose(); vaporScopes.Remove(scope); }
    }
    private IEnumerator VaporeonWait(VaporScope scope, double seconds)
    {
        double until = VaporNow + seconds;
        while (scope.Live && VaporNow < until) yield return null;
    }
    private int HealVaporeonAlly(Pokemon recipient, int amount)
    {
        if (!IsLivingVaporeon || !VaporMoveRuntime.CanHeal(playerManager, recipient)) return 0;
        amount = Mathf.Min(Mathf.Max(0, amount), recipient.GetMissingHp());
        if (amount <= 0) return 0;
        recipient.HealDamageRPC(amount); SupportVaporeonAlly(recipient);
        return amount;
    }
    private void SupportVaporeonAlly(Pokemon recipient)
    {
        if (!IsLivingVaporeon || pokemon.CurrentLevel < 3 || recipient == pokemon || VaporNow < vaporHydrationReady) return;
        vaporHydrationReady = VaporNow + 6;
        pokemon.RemoveAllStatChangeWithIDRPC(VaporMoveRules.HydrationId);
        pokemon.AddStatChange(new StatChange(10, Stat.Speed, 1.5f, true, true, true, VaporMoveRules.HydrationId));
        ShowVaporeonEffect(VaporEffect.Hydration, transform.position, 1.5f, NetworkObjectId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void VaporeonBasicRPC(ulong targetId)
    {
        if (!IsLivingVaporeon || VaporeonDiving || pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)
            || VaporNow + 0.08 < vaporNextBasic || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
            || !obj.TryGetComponent(out Pokemon target) || !VaporMoveRuntime.CanHit(playerManager, target)
            || (target.transform.position - transform.position).sqrMagnitude > 6.5f * 6.5f) return;
        if (VaporNow - vaporLastBasic >= VaporMoveRules.BoostExpiry) vaporAttackCharge.Value = 0;
        bool boosted = vaporAttackCharge.Value == 2;
        vaporAttackCharge.Value = (byte)(boosted ? 0 : vaporAttackCharge.Value + 1);
        vaporLastBasic = VaporNow; vaporNextBasic = VaporNow + GetAtkSpeedCooldown();
        VaporMoveRuntime.Animate(playerManager, boosted ? "Armature_pm0134_00_ba21_tokusyu02_gfbanm" : "Armature_pm0134_00_ba21_tokusyu01_gfbanm",
            boosted ? "ani_atk6_bat_0133" : "ani_atk4_bat_0133", 3f);
        var scope = new VaporScope(this); RunVaporeon(scope, VaporeonHoming(scope, target, boosted));
    }
    private IEnumerator VaporeonHoming(VaporScope scope, Pokemon target, bool boosted)
    {
        Vector3 position = transform.position + Vector3.up * 0.8f;
        scope.Visual(boosted ? VaporEffect.Boosted : VaporEffect.Basic, position, 3, target.NetworkObjectId);
        double end = VaporNow + 3;
        while (scope.Live && VaporMoveRuntime.CanHit(playerManager, target) && VaporNow < end)
        {
            Vector3 goal = target.transform.position + Vector3.up * 0.7f;
            position = Vector3.MoveTowards(position, goal, VaporMoveRules.BasicSpeed * Time.deltaTime);
            if ((position - goal).sqrMagnitude < 0.04f)
            {
                target.TakeDamageRPC(VaporMoveRules.BasicDamage(NetworkObjectId, boosted));
                ShowVaporeonEffect(VaporEffect.DiveSplash, goal, 0.2f, size: boosted ? 0.6f : 0.3f); yield break;
            }
            yield return null;
        }
    }

    private IEnumerator VaporeonSwift(VaporScope scope, Vector3 center)
    {
        scope.Visual(VaporEffect.Swift, center, 2.8f, size: 1);
        for (int volley = 0; volley < 4 && scope.Live; volley++)
        {
            yield return VaporeonWait(scope, 0.7);
            if (!scope.Live) yield break;
            foreach (Pokemon target in VaporMoveRuntime.Targets(center, 1))
                if (VaporMoveRuntime.CanHit(playerManager, target)) target.TakeDamageRPC(new DamageInfo(NetworkObjectId, 0.41f, 5, 170, DamageType.Special));
        }
    }
    private IEnumerator VaporeonHelpingHand(VaporScope scope, Pokemon target)
    {
        scope.Own(new VaporModifierLease(target, Stat.Attack, 35, true));
        scope.Own(new VaporModifierLease(target, Stat.SpAttack, 35, true));
        scope.Visual(VaporEffect.HelpingHand, target.transform.position, 3.5f, target.NetworkObjectId);
        SupportVaporeonAlly(target);
        double end = VaporNow + 3.5;
        while (scope.Live && VaporMoveRuntime.Living(target) && VaporNow < end) yield return null;
    }
    private IEnumerator VaporeonRing(VaporScope scope, Pokemon target, bool upgraded)
    {
        ulong recipientId = target.NetworkObjectId;
        var shield = new VaporShieldLease(target, Mathf.FloorToInt(target.GetMaxHp() * VaporMoveRules.ShieldRatio(upgraded)));
        scope.Own(shield);
        var attack = new VaporModifierLease(target, Stat.Attack, 25, true);
        var special = new VaporModifierLease(target, Stat.SpAttack, 25, true);
        scope.Own(attack); scope.Own(special);
        scope.Visual(VaporEffect.AquaRing, target.transform.position, 6, target.NetworkObjectId, 1, shield.Id);
        SupportVaporeonAlly(target);
        double start = VaporNow;
        int pulses = 0;
        // Same eight-pulse healing budget as before; the first pulse arrives sooner.
        while (scope.Live && shield.Intact && VaporNow - start < VaporMoveRules.RingDuration)
        {
            double age = VaporNow - start;
            if (age >= 4.5) { attack.Dispose(); special.Dispose(); }
            if (pulses < 8 && age >= 0.15 + pulses * VaporMoveRules.RingTick)
            {
                pulses++;
                HealVaporeonAlly(target, VaporMoveRules.Heal(pokemon, 0.51f, 6, 300));
                ShowVaporeonEffect(VaporEffect.HealPulse, target.transform.position, 0.3f, target.NetworkObjectId, 0.85f);
            }
            yield return null;
        }
        if (scope.Live && VaporMoveRuntime.Living(target) && !shield.Intact)
            ShowVaporeonEffect(VaporEffect.ShieldBreak, target.transform.position, 0.3f, target.NetworkObjectId);
        if (vaporRings.TryGetValue(recipientId, out var current) && current == scope) vaporRings.Remove(recipientId);
    }

    private IEnumerator VaporeonRainField(VaporScope scope, Vector3 center, bool upgraded)
    {
        double landingAt = VaporNow + VaporMoveRules.Windup(VaporCast.RainDance);
        while (scope.Live && VaporNow < landingAt)
        {
            if (pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) yield break;
            yield return null;
        }
        if (!scope.Live || pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) yield break;
        scope.Visual(VaporEffect.Rain, center, VaporMoveRules.RainDuration, size: VaporMoveRules.RainRadius);
        var occupants = new VaporOccupants(); scope.Own(occupants);
        double start = VaporNow, nextScan = start, nextHeal = start + 1.2;
        while (scope.Live && VaporNow - start < VaporMoveRules.RainDuration)
        {
            if (VaporNow >= nextScan)
            {
                nextScan = VaporNow + 0.1;
                occupants.Refresh(VaporMoveRuntime.Targets(center, VaporMoveRules.RainRadius), target =>
                {
                    var leases = new List<IDisposable>();
                    if (VaporMoveRuntime.CanHeal(playerManager, target))
                    {
                        leases.Add(new VaporModifierLease(target, Stat.Defense, VaporMoveRules.RainDefense(upgraded), true));
                        leases.Add(new VaporModifierLease(target, Stat.SpDefense, VaporMoveRules.RainDefense(upgraded), true));
                    }
                    else if (CanVaporeonSlow(target)) leases.Add(new VaporModifierLease(target, Stat.Speed, 20, false));
                    return leases;
                }, target => VaporMoveRuntime.CanHeal(playerManager, target) || CanVaporeonSlow(target));
            }
            if (VaporNow >= nextHeal)
            {
                nextHeal += 1.2;
                foreach (Pokemon target in VaporMoveRuntime.Targets(center, VaporMoveRules.RainRadius))
                    HealVaporeonAlly(target, VaporMoveRules.Heal(pokemon, 0.34f, 6, 250));
                ShowVaporeonEffect(VaporEffect.HealPulse, center, 0.35f, size: VaporMoveRules.RainRadius);
            }
            yield return null;
        }
    }
    private bool CanVaporeonSlow(Pokemon target) => VaporMoveRuntime.CanHit(playerManager, target) && target.Type != PokemonType.Objective
        && !target.HasStatusEffect(StatusType.Unstoppable);

    private IEnumerator VaporeonWhirlpool(VaporScope scope, Vector3 center, bool upgraded)
    {
        scope.Visual(VaporEffect.Whirlpool, center, VaporMoveRules.WhirlpoolDuration, size: VaporMoveRules.WhirlpoolRadius);
        var occupants = new VaporOccupants(); scope.Own(occupants);
        double start = VaporNow, nextScan = start;
        int pulses = 0; bool collapsed = false;
        while (scope.Live && VaporNow - start < VaporMoveRules.WhirlpoolDuration)
        {
            double age = VaporNow - start;
            var targets = VaporMoveRuntime.Targets(center, VaporMoveRules.WhirlpoolRadius);
            if (VaporNow >= nextScan)
            {
                nextScan = VaporNow + 0.1;
                occupants.Refresh(targets, target => new List<IDisposable> { new VaporModifierLease(target, Stat.Speed, VaporMoveRules.WhirlpoolSlow(upgraded), false) }, CanVaporeonSlow);
            }
            while (pulses < 6 && age >= pulses * 0.5)
            {
                pulses++;
                foreach (Pokemon target in targets)
                    if (VaporMoveRuntime.CanHit(playerManager, target)) target.TakeDamageRPC(VaporMoveRules.WhirlpoolTick(NetworkObjectId));
            }
            if (!collapsed && age >= VaporMoveRules.WhirlpoolCollapse)
            {
                collapsed = true; ShowVaporeonEffect(VaporEffect.Collapse, center, 0.3f, size: VaporMoveRules.WhirlpoolRadius);
                var expired = new List<ulong>();
                foreach (var entry in vaporPullReady) if (entry.Value <= Time.time) expired.Add(entry.Key);
                foreach (ulong id in expired) vaporPullReady.Remove(id);
                foreach (Pokemon target in targets)
                {
                    if (!CanVaporeonSlow(target) || target.Type != PokemonType.Player || target.HasStatusEffect(StatusType.HindranceResistance)
                        || vaporPullReady.ContainsKey(target.NetworkObjectId) || !target.TryGetComponent(out PlayerMovement movement)) continue;
                    vaporPullReady[target.NetworkObjectId] = Time.time + VaporMoveRules.PullImmunity;
                    movement.VaporeonDisplaceRPC(center, VaporMoveRules.PullDistance, true);
                }
            }
            yield return null;
        }
    }

    private IEnumerator VaporeonTempest(VaporScope scope, Vector3 center)
    {
        scope.Visual(VaporEffect.TempestWarning, center, VaporMoveRules.TempestWindup, size: VaporMoveRules.TempestRadius);
        double until = VaporNow + VaporMoveRules.TempestWindup;
        while (scope.Live && VaporNow < until)
        {
            if (pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) yield break;
            yield return null;
        }
        if (!scope.Live || pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) yield break;
        ShowVaporeonEffect(VaporEffect.TempestImpact, center, 0.45f, size: VaporMoveRules.TempestRadius);
        foreach (Pokemon target in VaporMoveRuntime.Targets(center, VaporMoveRules.TempestRadius))
        {
            if (VaporMoveRuntime.CanHit(playerManager, target))
            {
                target.TakeDamageRPC(VaporMoveRules.TempestDamage(NetworkObjectId));
                if (!VaporMoveRuntime.Living(target)) continue;
                scope.Own(new VaporModifierLease(target, Stat.Defense, 35, false));
                scope.Own(new VaporModifierLease(target, Stat.SpDefense, 35, false));
            }
            else if (VaporMoveRuntime.CanHeal(playerManager, target)) HealVaporeonAlly(target, VaporMoveRules.Heal(pokemon, 2.6f, 5, 840));
        }
        yield return VaporeonWait(scope, 5);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void BeginVaporeonDiveRPC()
    {
        if (!IsLivingVaporeon || pokemon.CurrentLevel < 5 || vaporLearned[1] != AvailableMoves.VaporDive || vaporDive != null
            || VaporNow + 0.3 < vaporNextCast[1] || pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts)) return;
        vaporDiveStarted = VaporNow; vaporDiveEnds.Value = VaporNow + VaporMoveRules.DiveDuration + VaporMoveRules.DiveWarning;
        vaporEmergeRequested = false;
        var scope = new VaporScope(this); vaporDive = scope;
        RunVaporeon(scope, VaporeonSubmerge(scope));
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void EndVaporeonDiveRPC(bool attack)
    {
        if (vaporDive == null) return;
        if (!attack)
        {
            vaporDive.Dispose(); vaporDive = null; vaporDiveEnds.Value = 0; return;
        }
        if (VaporNow - vaporDiveStarted >= VaporMoveRules.DiveMinimum) vaporEmergeRequested = true;
    }
    private IEnumerator VaporeonSubmerge(VaporScope scope)
    {
        ushort invincible = VaporEffectIds.Acquire();
        pokemon.AddStatusEffect(new StatusEffect(StatusType.Invincible, VaporMoveRules.DiveDuration + 0.5f, true, invincible));
        scope.Own(new VaporCleanup(() => { if (pokemon != null && pokemon.IsSpawned) pokemon.RemoveStatusEffectWithID(invincible); VaporEffectIds.Release(invincible); }));
        scope.Visual(VaporEffect.DiveWake, transform.position, VaporMoveRules.DiveDuration + VaporMoveRules.DiveWarning, NetworkObjectId, VaporMoveRules.DiveRadius);
        while (scope.Live && !vaporEmergeRequested && VaporNow - vaporDiveStarted < VaporMoveRules.DiveDuration) yield return null;
        if (scope.Live)
        {
            Vector3 center = transform.position;
            scope.Visual(VaporEffect.DiveWarning, center, VaporMoveRules.DiveWarning, size: VaporMoveRules.DiveRadius);
            vaporNextCast[1] = VaporNow + VaporMoveRules.DiveCooldown(pokemon.CurrentLevel >= 11) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f);
            yield return VaporeonWait(scope, VaporMoveRules.DiveWarning);
            if (scope.Live)
            {
                ShowVaporeonEffect(VaporEffect.DiveSplash, center, 0.35f, size: VaporMoveRules.DiveRadius);
                foreach (Pokemon target in VaporMoveRuntime.Targets(center, VaporMoveRules.DiveRadius))
                {
                    if (!VaporMoveRuntime.CanHit(playerManager, target)) continue;
                    target.TakeDamageRPC(new DamageInfo(NetworkObjectId, 0.7f, 6, 200, DamageType.Special));
                    if (!VaporMoveRuntime.Living(target) || target.Type == PokemonType.Objective) continue;
                    target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.25f, true, 0));
                    if (target.TryGetComponent(out PlayerMovement movement)) movement.VaporeonDisplaceRPC(center, 0.6f, false);
                }
            }
        }
        if (vaporDive == scope) { vaporDiveEnds.Value = 0; vaporDive = null; }
    }

    private void OnVaporeonDeath(DamageInfo damage)
    {
        foreach (var scope in new List<VaporScope>(vaporScopes)) scope.Dispose();
        vaporRings.Clear(); vaporRain = null; vaporWhirlpool = null; vaporDive = null;
        vaporDiveEnds.Value = 0; vaporAttackCharge.Value = 0;
        ClearVaporeonBufferedInput();
    }
    private void DisposeVaporeonRuntime()
    {
        ClearVaporeonBufferedInput();
        if (vaporBound && pokemon != null) pokemon.OnDeath -= OnVaporeonDeath;
        foreach (var scope in new List<VaporScope>(vaporScopes)) scope.Dispose();
        vaporScopes.Clear(); vaporRings.Clear(); vaporBound = false;
        DisposeVaporeonPresentation();
    }
    private void OnVaporeonNetworkDespawn()
    {
        if (pokemon == null || pokemon.BaseStats == null || pokemon.BaseStats.PokemonName != "Vaporeon") return;
        if (IsOwner)
        {
            foreach (MoveBase move in moves) if (move is VaporMoveBase || move is VaporDive) move.ResetMove();
            if (uniteMove is VaporeonUnite) uniteMove.ResetMove();
        }
        DisposeVaporeonRuntime();
    }
    private sealed class VaporScope : IDisposable
    {
        private readonly MovesController owner;
        private readonly List<IDisposable> leases = new List<IDisposable>();
        private readonly List<int> visuals = new List<int>();
        private bool disposed;
        public bool Live => !disposed && owner != null && owner.IsSpawned && owner.IsLivingVaporeon;
        public VaporScope(MovesController owner) { this.owner = owner; }
        public void Own(IDisposable lease) { if (disposed) lease.Dispose(); else leases.Add(lease); }
        public void Visual(VaporEffect effect, Vector3 position, float duration, ulong target = ulong.MaxValue, float size = 1, int detail = 0)
        { if (Live) visuals.Add(owner.ShowVaporeonEffect(effect, position, duration, target, size, detail)); }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var lease in leases) lease.Dispose(); leases.Clear();
            if (owner != null && owner.IsSpawned) foreach (int id in visuals) owner.StopVaporeonEffectRPC(id);
            visuals.Clear();
            if (owner != null)
            {
                foreach (ulong id in new List<ulong>(owner.vaporRings.Keys))
                    if (owner.vaporRings[id] == this) owner.vaporRings.Remove(id);
                if (owner.vaporRain == this) owner.vaporRain = null;
                if (owner.vaporWhirlpool == this) owner.vaporWhirlpool = null;
                if (owner.vaporDive == this)
                {
                    owner.vaporDive = null;
                    if (owner.IsServer && owner.IsSpawned) owner.vaporDiveEnds.Value = 0;
                }
            }
        }
    }
    private sealed class VaporCleanup : IDisposable
    {
        private Action cleanup;
        public VaporCleanup(Action cleanup) { this.cleanup = cleanup; }
        public void Dispose() { var action = cleanup; cleanup = null; action?.Invoke(); }
    }
    private sealed class VaporOccupants : IDisposable
    {
        private readonly Dictionary<Pokemon, List<IDisposable>> occupants = new Dictionary<Pokemon, List<IDisposable>>();
        public void Refresh(HashSet<Pokemon> targets, Func<Pokemon, List<IDisposable>> create, Func<Pokemon, bool> valid)
        {
            foreach (var entry in new List<Pokemon>(occupants.Keys))
                if (!targets.Contains(entry) || !valid(entry)) { foreach (var lease in occupants[entry]) lease.Dispose(); occupants.Remove(entry); }
            foreach (Pokemon target in targets) if (valid(target) && !occupants.ContainsKey(target)) occupants.Add(target, create(target));
        }
        public void Dispose() { foreach (var leases in occupants.Values) foreach (var lease in leases) lease.Dispose(); occupants.Clear(); }
    }
}
