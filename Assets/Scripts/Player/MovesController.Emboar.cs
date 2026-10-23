using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private readonly EmboarRecklessState emboarReckless = new EmboarRecklessState();
    private readonly EmboarAttackState emboarAttacks = new EmboarAttackState();
    private readonly NetworkVariable<int> emboarCharge = new NetworkVariable<int>();
    private readonly NetworkVariable<double> emboarReadyUntil = new NetworkVariable<double>();
    private readonly NetworkVariable<double> emboarRearmUntil = new NetworkVariable<double>();
    private readonly NetworkVariable<int> emboarPledgeHits = new NetworkVariable<int>();
    private readonly Dictionary<int, EmboarRequest> emboarRequests = new Dictionary<int, EmboarRequest>();
    private readonly Dictionary<ulong, double> emboarDamageCharge = new Dictionary<ulong, double>();
    private readonly double[] emboarNextCast = new double[3];
    private double emboarActionUntil, emboarNextBasic, emboarNextTakenCharge, emboarNextReadyVisual;
    private int emboarSerial, emboarLastCast, emboarGeneration, emboarBufferedSlot = -1;
    private bool emboarBound, emboarEarlyDefense, emboarReadyLoading, emboarBufferedRelease;
    private float emboarBufferUntil;
    private EmboarMoveObject emboarReadyVisual;
    private double EmboarNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsEmboar => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.EmboarPassive;
    public int EmboarRecklessCharge => emboarCharge.Value;
    public bool EmboarRecklessReady => emboarCharge.Value == 100 && EmboarNow < emboarReadyUntil.Value;
    public float EmboarRecklessRearm => (float)Math.Max(0, emboarRearmUntil.Value - EmboarNow);
    public int EmboarPledgeHits => emboarPledgeHits.Value;
    public int NextEmboarCast() => ++emboarSerial;

    private sealed class EmboarRequest
    {
        public EmboarCast Kind;
        public int Generation;
        public bool Cancelled, Started;
        public EmboarMoveObject Effect;
    }
    private void UpdateEmboarRuntime()
    {
        if (!IsServer || !IsEmboar) return;
        if (!emboarBound)
        {
            emboarBound = true;
            pokemon.OnDeath += OnEmboarDeath;
            pokemon.OnDamageTaken += OnEmboarDamageTaken;
            pokemon.OnDamageDealt += OnEmboarDamageDealt;
        }
        if (pokemon.CurrentHp <= 0) return;
        bool early = pokemon.CurrentLevel < 6;
        if (early != emboarEarlyDefense)
        {
            emboarEarlyDefense = early;
            pokemon.RemoveAllStatChangeWithIDRPC(EmboarMoveRules.EarlyDefense);
            if (early) pokemon.AddStatChange(new StatChange(20, Stat.Defense, 0, false, true, true, EmboarMoveRules.EarlyDefense));
        }
        emboarReckless.Tick(EmboarNow); emboarAttacks.Tick(EmboarNow); SyncEmboarState();
        if (EmboarRecklessReady && emboarReadyVisual == null && !emboarReadyLoading && EmboarNow >= emboarNextReadyVisual)
            SpawnEmboarReadyVisual();
        if (!EmboarRecklessReady && emboarReadyVisual != null) emboarReadyVisual.Despawn();
    }
    private void SyncEmboarState()
    {
        emboarCharge.Value = emboarReckless.Charge; emboarReadyUntil.Value = emboarReckless.ReadyUntil;
        emboarRearmUntil.Value = emboarReckless.RearmUntil; emboarPledgeHits.Value = emboarAttacks.PledgeHits;
    }
    private void OnEmboarDamageTaken(DamageInfo damage)
    {
        if (!IsServer || !IsEmboar || pokemon.CurrentHp <= 0 || pokemon.CurrentLevel < 6 || EmboarNow < emboarNextTakenCharge
            || (damage.proprieties & (DamageProprieties.IsHeal | DamageProprieties.IsMuscleBand)) != 0) return;
        emboarNextTakenCharge = EmboarNow + 0.25;
        emboarReckless.Add(5, EmboarNow); SyncEmboarState();
    }
    private void OnEmboarDamageDealt(ulong targetId, DamageInfo damage)
    {
        if (!IsServer || !IsEmboar || pokemon.CurrentHp <= 0 || pokemon.CurrentLevel < 6
            || (damage.proprieties & (DamageProprieties.IsHeal | DamageProprieties.IsMuscleBand)) != 0
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) || !obj.TryGetComponent(out Pokemon target)) return;
        if (emboarDamageCharge.TryGetValue(targetId, out double next) && EmboarNow < next) return;
        emboarDamageCharge[targetId] = EmboarNow + 0.25;
        emboarReckless.Add(target.Type == PokemonType.Player ? 8 : 2, EmboarNow); SyncEmboarState();
    }
    private void OnEmboarDeath(DamageInfo damage)
    {
        emboarGeneration++; emboarEarlyDefense = false;
        emboarReckless.Clear(); emboarAttacks.Clear(); emboarDamageCharge.Clear(); SyncEmboarState();
        emboarActionUntil = emboarNextBasic = emboarNextTakenCharge = 0;
        foreach (int id in new List<int>(emboarRequests.Keys)) CancelEmboarRequest(id);
        if (emboarReadyVisual != null) emboarReadyVisual.Despawn();
        ClearEmboarBufferedInput();
    }
    public void FillEmboarReckless()
    {
        if (!IsServer || !IsEmboar || pokemon.CurrentHp <= 0 || pokemon.CurrentLevel < 6) return;
        emboarReckless.Fill(EmboarNow); SyncEmboarState();
    }
    public void ArmEmboarPledge()
    {
        if (!IsServer) return;
        emboarAttacks.Arm(EmboarNow); SyncEmboarState();
        EmboarBuff(EmboarMoveRules.PledgeSpeed, Stat.Speed, 25, EmboarMoveRules.PledgeDuration, true);
        EmboarBuff(EmboarMoveRules.PledgeAttackSpeed, Stat.AtkSpeed, 20, EmboarMoveRules.PledgeDuration, false);
    }
    public void EmboarBuff(ushort id, Stat stat, short amount, float duration, bool percentage)
    {
        pokemon.RemoveAllStatChangeWithIDRPC(id);
        pokemon.AddStatChange(new StatChange(amount, stat, duration, true, true, percentage, id));
    }
    public void EmboarUnstoppable(float duration)
    {
        // Remove with notifications before immunity; avoid the legacy in-place status-list iteration.
        var negative = new List<StatusEffect>();
        foreach (StatusEffect effect in pokemon.StatusEffects) if (effect.IsNegativeStatus()) negative.Add(effect);
        foreach (StatusEffect effect in negative) pokemon.RemoveStatusEffectRPC(effect);
        pokemon.AddStatusEffect(new StatusEffect(StatusType.Unstoppable, duration, true, EmboarMoveRules.Immunity));
    }
    public bool HitEmboarTarget(Pokemon target, DamageInfo damage)
    {
        if (!IsServer || !EmboarMoveRules.CanHit(playerManager, target)) return false;
        bool applied = false;
        void Record(Pokemon victim, DamageInfo info, int healthDamage)
        {
            if (victim == target && info.attackerId == damage.attackerId && info.ratio == damage.ratio && info.baseDmg == damage.baseDmg) applied = true;
        }
        pokemon.OnHealthDamageDealt += Record;
        try { target.TakeDamageRPC(damage); }
        finally { pokemon.OnHealthDamageDealt -= Record; }
        return applied;
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void EmboarBasicRPC(ulong primaryId, Vector3 direction)
    {
        if (!IsEmboar || pokemon.CurrentHp <= 0 || EmboarNow < emboarActionUntil || EmboarNow + 0.04 < emboarNextBasic
            || !EmboarMoveRules.Finite(direction) || pokemon.HasAnyStatusEffect(EmboarMoveRules.Interrupts)
            || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(primaryId, out var obj) || !obj.TryGetComponent(out Pokemon primary)
            || !EmboarMoveRules.CanHit(playerManager, primary)) return;
        float range = EmboarMoveRules.BasicRange(pokemon.CurrentLevel);
        if ((primary.transform.position - transform.position).sqrMagnitude > (range + 0.5f) * (range + 0.5f)) return;
        direction = EmboarMoveRules.Direction(primary.transform.position - transform.position, direction);
        emboarNextBasic = EmboarNow + GetAtkSpeedCooldown();
        bool boosted = emboarAttacks.Boosted(EmboarNow), launcher = emboarAttacks.Launcher(EmboarNow), connected = false;
        Vector3 center = transform.position + direction * range * 0.52f + Vector3.up * 0.6f;
        var targets = EmboarMoveRules.Targets(Physics.OverlapSphere(center, range * 0.5f));
        targets.Add(primary);
        foreach (Pokemon target in targets)
        {
            if (!HitEmboarTarget(target, EmboarMoveRules.Damage(NetworkObjectId, EmboarCast.Impact, boosted ? 1 : 0))) continue;
            connected = true;
            if (launcher && EmboarMoveRules.CanControl(playerManager, target))
            {
                target.AddStatusEffect(new StatusEffect(StatusType.Incapacitated, 0.55f, true, 0));
                target.ApplyKnockupRPC(0.8f, 0.55f);
            }
        }
        if (connected) { emboarAttacks.Confirm(EmboarNow); SyncEmboarState(); }
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CastEmboarRPC(EmboarCast kind, int cast, Vector3 direction, Vector3 destination, ulong targetId)
    {
        if (kind > EmboarCast.Unite || cast <= emboarLastCast || cast <= 0 || !IsEmboar || pokemon.CurrentHp <= 0
            || !EmboarMoveRules.Finite(direction) || !EmboarMoveRules.Finite(destination) || EmboarNow + 0.04 < emboarActionUntil
            || pokemon.HasAnyStatusEffect(EmboarMoveRules.Interrupts) || (EmboarMoveRules.Driven(kind) && pokemon.HasStatusEffect(StatusType.Immobilized)))
        { AcceptEmboarRPC(kind, cast, false, false, Vector3.zero); return; }
        int slot = EmboarMoveRules.Slot(kind);
        if (prefabLearnedMoves[slot] != EmboarMoveRules.Move(kind) || EmboarNow + 0.04 < emboarNextCast[slot])
        { AcceptEmboarRPC(kind, cast, false, false, Vector3.zero); return; }
        foreach (var existing in emboarRequests.Values)
            if (!existing.Started && !existing.Cancelled) { AcceptEmboarRPC(kind, cast, false, false, Vector3.zero); return; }
        Pokemon target = null;
        if (kind == EmboarCast.Ember && (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) || !obj.TryGetComponent(out target)
            || !EmboarMoveRules.CanHit(playerManager, target) || target.HasStatusEffect(StatusType.Invisible)
            || (target.transform.position - transform.position).sqrMagnitude > 36))
        { AcceptEmboarRPC(kind, cast, false, false, Vector3.zero); return; }
        emboarLastCast = cast;
        var request = new EmboarRequest { Kind = kind, Generation = emboarGeneration };
        emboarRequests.Add(cast, request);
        Vector3 aim = EmboarMoveRules.Direction(direction, transform.forward);
        Vector3 point = EmboarMoveRules.Destination(transform.position, destination, EmboarMoveRules.Range(kind));
        Addressables.LoadAssetAsync<GameObject>(EmboarMoveRules.Path(kind)).Completed += handle =>
        {
            bool invalid = handle.Status != AsyncOperationStatus.Succeeded || request.Cancelled || this == null || !IsSpawned
                || request.Generation != emboarGeneration || !IsEmboar || pokemon.CurrentHp <= 0 || pokemon.HasAnyStatusEffect(EmboarMoveRules.Interrupts)
                || (EmboarMoveRules.Driven(kind) && pokemon.HasStatusEffect(StatusType.Immobilized))
                || prefabLearnedMoves[slot] != EmboarMoveRules.Move(kind) || (kind == EmboarCast.Ember && !EmboarMoveRules.CanHit(playerManager, target));
            if (invalid)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogError($"Failed loading Emboar {kind}: {handle.OperationException}");
                Addressables.Release(handle);
                if (this != null) { emboarRequests.Remove(cast); if (IsSpawned && !request.Cancelled) AcceptEmboarRPC(kind, cast, false, false, Vector3.zero); }
                return;
            }
            var go = Instantiate(handle.Result, transform.position, Quaternion.LookRotation(aim));
            var effect = go.GetComponent<EmboarMoveObject>();
            if (effect == null)
            {
                Destroy(go); Addressables.Release(handle); emboarRequests.Remove(cast);
                Debug.LogError($"Emboar {kind} prefab has no EmboarMoveObject."); AcceptEmboarRPC(kind, cast, false, false, Vector3.zero); return;
            }
            bool upgraded = EmboarMoveRules.Upgraded(kind, pokemon.CurrentLevel);
            bool empowered = emboarReckless.Consume(kind, EmboarNow); SyncEmboarState();
            request.Started = true; request.Effect = effect;
            emboarActionUntil = EmboarNow + EmboarMoveRules.Recovery(kind);
            emboarNextCast[slot] = EmboarNow + (slot == 2 ? 1 : EmboarMoveRules.Cooldown(kind, upgraded) * Math.Max(0.01, 1 - pokemon.GetCDR() / 100f));
            effect.HoldAsset(handle); go.GetComponent<NetworkObject>().Spawn(true);
            effect.Initialize(this, playerManager, kind, cast, aim, point, target, upgraded, empowered);
            AcceptEmboarRPC(kind, cast, true, empowered, point);
        };
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void AcceptEmboarRPC(EmboarCast kind, int cast, bool accepted, bool empowered, Vector3 destination)
    {
        if (kind > EmboarCast.Unite) return;
        if (GetMove((MoveType)EmboarMoveRules.Slot(kind)) is EmboarMoveBase move && move.Kind == kind) move.Accept(cast, accepted, empowered, destination);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void EmboarMovementRPC(int cast, bool finished, Vector3 point)
    {
        if (!EmboarMoveRules.Finite(point) || !emboarRequests.TryGetValue(cast, out var request) || request.Effect == null || request.Cancelled) return;
        request.Effect.ReportMovement(finished, point);
        if (request.Kind == EmboarCast.FlameCharge && request.Effect.MovementEnded)
            emboarActionUntil = Math.Min(emboarActionUntil, EmboarNow + 0.08);
    }
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CancelEmboarCastRPC(int cast) { CancelEmboarRequest(cast); }
    private void CancelEmboarRequest(int cast)
    {
        if (!emboarRequests.TryGetValue(cast, out var request)) return;
        request.Cancelled = true;
        if (request.Effect != null) request.Effect.Despawn();
        emboarRequests.Remove(cast);
    }
    public void EndEmboarObject(int cast) { if (cast > 0) emboarRequests.Remove(cast); }
    private void SpawnEmboarReadyVisual()
    {
        emboarReadyLoading = true; emboarNextReadyVisual = EmboarNow + 2;
        int generation = emboarGeneration;
        Addressables.LoadAssetAsync<GameObject>(EmboarMoveRules.Path(EmboarCast.Reckless)).Completed += handle =>
        {
            if (this != null) emboarReadyLoading = false;
            if (handle.Status != AsyncOperationStatus.Succeeded || this == null || !IsSpawned || generation != emboarGeneration || !EmboarRecklessReady || pokemon.CurrentHp <= 0)
            { Addressables.Release(handle); return; }
            var go = Instantiate(handle.Result, transform.position, transform.rotation);
            emboarReadyVisual = go.GetComponent<EmboarMoveObject>();
            if (emboarReadyVisual == null) { Destroy(go); Addressables.Release(handle); return; }
            emboarReadyVisual.HoldAsset(handle); go.GetComponent<NetworkObject>().Spawn(true);
            emboarReadyVisual.Initialize(this, playerManager, EmboarCast.Reckless, 0, transform.forward, transform.position, null, false, false);
        };
    }
    private bool CanRecastEmboar(int slot) => slot == 1 && moves[slot] is EmboarMoveBase move && move.CanBrake
        && (moveStatuses[slot].GetStatus() & ~ActionStatusType.Cooldown) == ActionStatusType.None;
    private void BufferEmboarInput(int slot, bool released)
    {
        if (slot < 0 || slot > 1 || !(moves[slot] is EmboarMoveBase) || moveStatuses[slot].HasStatus(ActionStatusType.Cooldown)
            || pokemon.HasAnyStatusEffect(EmboarMoveRules.Interrupts)) return;
        bool near = uniteMove is EmboarMoveBase unite && unite.CanBuffer;
        foreach (var entry in moves) if (entry is EmboarMoveBase move && move.CanBuffer) near = true;
        if (!near) return;
        if (emboarBufferedSlot != slot) emboarBufferedRelease = false;
        emboarBufferedSlot = slot; emboarBufferedRelease |= released; emboarBufferUntil = Time.time + EmboarMoveRules.InputBuffer;
    }
    private void ProcessEmboarBufferedInput()
    {
        if (emboarBufferedSlot < 0) return;
        if (Time.time > emboarBufferUntil || !IsEmboar || pokemon.CurrentHp <= 0 || pokemon.HasAnyStatusEffect(EmboarMoveRules.Interrupts)) { ClearEmboarBufferedInput(); return; }
        int slot = emboarBufferedSlot;
        if (!moveStatuses[slot].HasStatus(ActionStatusType.None)) return;
        bool released = emboarBufferedRelease; ClearEmboarBufferedInput(); moves[slot].Start(playerManager);
        if (released) moves[slot].Finish();
    }
    private void ClearEmboarBufferedInput() { emboarBufferedSlot = -1; emboarBufferedRelease = false; }
    private void DisposeEmboarRuntime()
    {
        ClearEmboarBufferedInput();
        if (IsOwner)
        {
            foreach (MoveBase move in moves) if (move is EmboarMoveBase) move.ResetMove();
            if (uniteMove is EmboarMoveBase) uniteMove.ResetMove();
        }
        if (!emboarBound) return;
        emboarBound = false; emboarGeneration++;
        if (pokemon != null) { pokemon.OnDeath -= OnEmboarDeath; pokemon.OnDamageTaken -= OnEmboarDamageTaken; pokemon.OnDamageDealt -= OnEmboarDamageDealt; }
        if (IsServer)
        {
            foreach (int id in new List<int>(emboarRequests.Keys)) CancelEmboarRequest(id);
            if (emboarReadyVisual != null) emboarReadyVisual.Despawn();
        }
    }
}
