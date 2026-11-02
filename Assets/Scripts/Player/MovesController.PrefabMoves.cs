using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private readonly AvailableMoves[] prefabLearnedMoves = new AvailableMoves[3];
    private readonly double[] prefabNextCast = new double[3];
    private readonly PrefabMoveEffect[] activePrefabMoves = new PrefabMoveEffect[3];
    private readonly int[] prefabGenerations = new int[3];
    private readonly int[] prefabCastSequences = new int[3];
    private readonly System.Collections.Generic.HashSet<PrefabMoveEffect> prefabEffects = new System.Collections.Generic.HashSet<PrefabMoveEffect>();
    public System.Collections.Generic.IEnumerable<PrefabMoveEffect> ActivePrefabEffects => prefabEffects;

    private void RecordPrefabMove(AvailableMoves move)
    {
        if (!IsServer || pokemon == null || pokemon.BaseStats == null) return;
        foreach (LearnableMove learn in pokemon.BaseStats.LearnableMoves)
            foreach (MoveAsset asset in learn.moves)
                if (asset != null && asset.move == move && asset.moveType != MoveType.All
                    && !learn.isUpgraded && learn.level <= pokemon.CurrentLevel)
                    prefabLearnedMoves[(int)asset.moveType] = move;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void SpawnMoveEffectRPC(AvailableMoves move, Vector3 direction, ulong targetId)
    {
        if (pokemon == null || pokemon.BaseStats == null || pokemon.CurrentHp <= 0
            || !float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z)) { RejectPrefabExecutionRPC(move); return; }
        if (pokemon.HasStatusEffect(StatusType.Incapacitated) || pokemon.HasStatusEffect(StatusType.Frozen)
            || pokemon.HasStatusEffect(StatusType.Asleep) || pokemon.HasStatusEffect(StatusType.Bound)) { RejectPrefabExecutionRPC(move); return; }
        var definition = MoveDatabase.GetMove(move) as PrefabMove;
        if (definition == null) return;
        MoveAsset learned = null;
        bool upgraded = false;
        foreach (LearnableMove learn in pokemon.BaseStats.LearnableMoves)
            foreach (MoveAsset asset in learn.moves)
                if (asset != null && asset.move == move && learn.level <= pokemon.CurrentLevel)
                {
                    learned = asset;
                    upgraded |= learn.isUpgraded;
                }
        if (learned == null || learned.moveType == MoveType.All) return;
        int slot = (int)learned.moveType;
        if (ZoroarkNight != null && ZoroarkNight.Finishing && slot != 0) { RejectPrefabExecutionRPC(move); return; }
        double now = NetworkManager.ServerTime.Time;
        if (prefabLearnedMoves[slot] != move) { RejectPrefabExecutionRPC(move); return; }
        direction.y = 0;
        direction = definition.AimStyle == MoveAimStyle.Ground ? Vector3.ClampMagnitude(direction, definition.Range)
            : direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        if (definition.CooldownOnEffectEnd && activePrefabMoves[slot] != null)
        {
            if (ServerActionLocks == 0 && activePrefabMoves[slot].TryRecast(direction, targetId))
            {
                if (definition.IsOffensive) pokemon.NotifyOffensiveAction();
                PrefabLoadFinishedRPC(move);
            }
            else RejectPrefabExecutionRPC(move);
            return;
        }
        bool aerial = slot == 0 && ZoroarkNight != null && ZoroarkNight.AerialWindow;
        var espeon = definition is EspeonMove ? EspeonCombat : null;
        bool espeonBonus = espeon != null && EspeonMoveRules.MoveOne(move) && espeon.BonusReady;
        if (definition is EspeonMove && (espeon == null || !espeon.CanCast(move))) { RejectPrefabExecutionRPC(move); return; }
        if (ServerActionLocks > 0 || (!aerial && !espeonBonus && now + 0.15 < prefabNextCast[slot])) { RejectPrefabExecutionRPC(move); return; }
        if (aerial && !ZoroarkNight.TrySpendAerialReposition()) { RejectPrefabExecutionRPC(move); return; }
        Pokemon target = null;
        if (definition.AimStyle == MoveAimStyle.Target)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
                || !obj.TryGetComponent(out target) || target.CurrentHp <= 0
                || target.TeamMember.IsOnSameTeam(playerManager.CurrentTeam)
                || target.HasStatusEffect(StatusType.Invincible) || target.HasStatusEffect(StatusType.Untargetable)
                || target.HasStatusEffect(StatusType.Invisible)
                || !definition.IsTargetInCastRange(transform.position, target.transform.position)) { RejectPrefabExecutionRPC(move); return; }
        }
        direction.y = 0;
        direction = definition.AimStyle == MoveAimStyle.Ground ? Vector3.ClampMagnitude(direction, definition.Range)
            : direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        int espeonBlastCount = 0;
        double espeonBonusExpiry = espeon != null ? espeon.BonusExpiresAt : 0;
        if (espeon != null && !espeon.Spend(move, out espeonBonus, out espeonBlastCount)) { RejectPrefabExecutionRPC(move); return; }
        if (!aerial && !espeonBonus) prefabNextCast[slot] = now + Mathf.Max(0.25f, definition.EffectiveCooldown(upgraded) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f));
        if (espeon != null && !espeonBonus && slot < 2) EspeonCooldownRPC(slot, (float)(prefabNextCast[slot] - now));
        if (definition.CooldownOnEffectEnd) prefabNextCast[slot] = double.PositiveInfinity;
        int generation = ++prefabGenerations[slot];
        int sequence = ++prefabCastSequences[slot];
        if (definition.IsOffensive) pokemon.NotifyOffensiveAction();
        // Capture each cast independently. Death invalidates a pending load even
        // if the actor respawns before Addressables completes.
        bool cancelled = false;
        Action<DamageInfo> onDeath = _ => cancelled = true;
        pokemon.OnDeath += onDeath;
        if (target != null) target.OnDeath += onDeath;
        Vector3 castDirection = direction;
        Addressables.LoadAssetAsync<GameObject>(definition.PrefabPath).Completed += asset =>
        {
            if (pokemon != null) pokemon.OnDeath -= onDeath;
            if (target != null) target.OnDeath -= onDeath;
            if (asset.Status != AsyncOperationStatus.Succeeded || cancelled || this == null || !IsSpawned || generation != prefabGenerations[slot]
                || pokemon == null || pokemon.CurrentHp <= 0 || (aerial && (ZoroarkNight == null || !ZoroarkNight.AerialWindow)))
            {
                if (asset.Status != AsyncOperationStatus.Succeeded) Debug.LogError($"Failed to load move {move}: {asset.OperationException}");
                Addressables.Release(asset);
                if (!cancelled && this != null && IsSpawned && espeon != null) espeon.Refund(move, espeonBonus, espeonBlastCount, espeonBonusExpiry);
                if (this != null && IsSpawned && generation == prefabGenerations[slot]) FinishPrefabExecution(slot, move, definition);
                return;
            }
            GameObject instance = Instantiate(asset.Result, transform.position + Vector3.up * 0.8f,
                Quaternion.LookRotation(castDirection.sqrMagnitude > 0.001f ? castDirection : transform.forward));
            var effect = instance.GetComponent<PrefabMoveEffect>();
            if (effect == null) { Destroy(instance); Addressables.Release(asset); FinishPrefabExecution(slot, move, definition); return; }
            effect.HoldAsset(asset);
            effect.MoveId = move;
            effect.CastSequence = sequence;
            if (effect is EspeonMoveEffect espeonEffect) { espeonEffect.Empowered = espeonBonus; espeonEffect.BlastCount = espeonBlastCount; }
            if (effect is ZoroarkMoveEffect zoroarkEffect) zoroarkEffect.AerialCast = aerial;
            instance.GetComponent<NetworkObject>().Spawn(true);
            prefabEffects.Add(effect);
            effect.Completed += completed => prefabEffects.Remove(completed);
            if (definition.CooldownOnEffectEnd)
            {
                activePrefabMoves[slot] = effect;
                effect.RecastReady += () => { if (generation == prefabGenerations[slot]) PrefabExecutionStateRPC(move, false); };
                effect.Completed += _ => { if (this != null && IsSpawned && generation == prefabGenerations[slot]) FinishPrefabExecution(slot, move, definition); };
            }
            effect.Initialize(playerManager, castDirection, target, upgraded);
            PrefabLoadFinishedRPC(move);
        };
    }

    private void FinishPrefabExecution(int slot, AvailableMoves move, PrefabMove definition)
    {
        activePrefabMoves[slot] = null;
        PrefabLoadFinishedRPC(move);
        if (!definition.CooldownOnEffectEnd) return;
        bool upgraded = false;
        foreach (var row in pokemon.BaseStats.LearnableMoves)
            if (row.isUpgraded && row.level <= pokemon.CurrentLevel)
                foreach (var asset in row.moves) if (asset != null && asset.move == move) upgraded = true;
        prefabNextCast[slot] = NetworkManager.ServerTime.Time + definition.EffectiveCooldown(upgraded) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f);
        PrefabExecutionStateRPC(move, true);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void PrefabLoadFinishedRPC(AvailableMoves move)
    {
        if (uniteMove is PrefabMove unite && unite.MoveId == move) unite.ReleaseLoadLock();
        foreach (var runtime in moves) if (runtime is PrefabMove prefab && prefab.MoveId == move) prefab.ReleaseLoadLock();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void RejectPrefabExecutionRPC(AvailableMoves move)
    {
        if (uniteMove is PrefabMove unite && unite.MoveId == move) unite.RejectExecution();
        foreach (var runtime in moves) if (runtime is PrefabMove prefab && prefab.MoveId == move) prefab.RejectExecution();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void PrefabExecutionStateRPC(AvailableMoves move, bool finished)
    {
        foreach (var runtime in moves)
            if (runtime is PrefabMove prefab && prefab.MoveId == move) prefab.ReceiveExecutionState(finished);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CancelPrefabMoveRPC(AvailableMoves move)
    {
        var definition = MoveDatabase.GetMove(move) as PrefabMove;
        if (definition == null) return;
        for (int slot = 0; slot < prefabLearnedMoves.Length; slot++)
        {
            if (prefabLearnedMoves[slot] != move) continue;
            foreach (var effect in new System.Collections.Generic.List<PrefabMoveEffect>(prefabEffects))
                if (effect != null && effect.MoveId == move) effect.Despawn();
            if (double.IsPositiveInfinity(prefabNextCast[slot])) FinishPrefabExecution(slot, move, definition);
            prefabGenerations[slot]++;
        }
    }

    // Hit-confirmed percentage reduction uses the remaining cooldown, not the
    // base cooldown. Existing absolute reductions and stored moves are unchanged.
    public void ReduceMoveCooldownByFraction(MoveType slot, float fraction)
    {
        if (!IsServer || slot == MoveType.All || slot == MoveType.UniteMove) return;
        fraction = Mathf.Clamp01(fraction);
        int index = (int)slot;
        double now = NetworkManager.ServerTime.Time;
        prefabNextCast[index] = now + Math.Max(0, prefabNextCast[index] - now) * (1 - fraction);
        ReduceMoveCooldownFractionRPC(slot, fraction);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void ReduceMoveCooldownFractionRPC(MoveType slot, float fraction)
    {
        int index = (int)slot;
        if (moveStatuses[index] != null) ReduceMoveCooldown(slot, Mathf.Max(0, moveStatuses[index].Cooldown) * fraction);
    }
}
