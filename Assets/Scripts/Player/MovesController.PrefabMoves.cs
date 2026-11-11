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
    private readonly int[] prefabOwnerRequests = new int[3];
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
    public void SpawnMoveEffectRPC(AvailableMoves move, Vector3 direction, ulong targetId, int ownerRequest = 0)
    {
        if (pokemon == null || pokemon.BaseStats == null || pokemon.CurrentHp <= 0
            || !float.IsFinite(direction.x) || !float.IsFinite(direction.y) || !float.IsFinite(direction.z)) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        if (pokemon.HasStatusEffect(StatusType.Incapacitated) || pokemon.HasStatusEffect(StatusType.Frozen)
            || pokemon.HasStatusEffect(StatusType.Asleep) || pokemon.HasStatusEffect(StatusType.Bound)) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        var definition = MoveDatabase.GetMove(move) as PrefabMove;
        if (definition == null) return;
        definition.BalanceId = CharactersList.Instance?.GetMoveAsset(move)?.BalanceId;
        MoveAsset learned = null;
        bool upgraded = false;
        foreach (LearnableMove learn in pokemon.BaseStats.LearnableMoves)
            foreach (MoveAsset asset in learn.moves)
                if (asset != null && asset.move == move && learn.level <= pokemon.CurrentLevel)
                {
                    learned = asset;
                    upgraded |= learn.isUpgraded;
                }
        definition.IsUpgraded = upgraded;
        if (learned == null || learned.moveType == MoveType.All) return;
        definition.BalanceId = learned.BalanceId;
        int slot = (int)learned.moveType;
        if (ZoroarkNight != null && ZoroarkNight.Finishing && slot != 0) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        double now = NetworkManager.ServerTime.Time;
        if (prefabLearnedMoves[slot] != move) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        if ((definition is GlaceonMove || definition is UmbreonMove || definition is TrevenantMove) && slot == 2)
        {
            TickLegacyItemResources();
            if (learned.EffectiveUniteEnergyCost <= 0 || legacyItemEnergy < learned.EffectiveUniteEnergyCost) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        }
        direction.y = 0;
        direction = definition.AimStyle == MoveAimStyle.Ground ? Vector3.ClampMagnitude(direction, definition.Range)
            : direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        if (definition.CooldownOnEffectEnd && activePrefabMoves[slot] != null)
        {
            if ((definition is GlaceonMove || definition is UmbreonMove || definition is TrevenantMove) && ownerRequest != prefabOwnerRequests[slot])
            { RejectPrefabExecutionRPC(move, ownerRequest); return; }
            if (!PrefabActionsLocked(move) && activePrefabMoves[slot].TryRecast(direction, targetId))
            {
                if (definition.IsOffensive) pokemon.NotifyOffensiveAction();
                PrefabLoadFinishedRPC(move, ownerRequest);
                if (definition is UmbreonMove) UmbreonAcceptedRPC(move, true, ownerRequest);
                if (definition is TrevenantMove) TrevenantAcceptedRPC(move, true, ownerRequest);
                if (definition is GlaceIcicleSpear) PrefabExecutionStateRPC(move, false, ownerRequest);
            }
            else RejectPrefabExecutionRPC(move, ownerRequest);
            return;
        }
        bool aerial = slot == 0 && ZoroarkNight != null && ZoroarkNight.AerialWindow;
        var espeon = definition is EspeonMove ? EspeonCombat : null;
        bool espeonBonus = espeon != null && EspeonMoveRules.MoveOne(move) && espeon.BonusReady;
        if (definition is EspeonMove && (espeon == null || !espeon.CanCast(move))) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        if (PrefabActionsLocked(move) || (!aerial && !espeonBonus && now + 0.15 < prefabNextCast[slot])) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        if (aerial && !ZoroarkNight.TrySpendAerialReposition()) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        Pokemon target = null;
        if (definition.AimStyle == MoveAimStyle.Target)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj)
                || !obj.TryGetComponent(out target) || target.CurrentHp <= 0
                || (definition.TargetTeam == AimTarget.Ally ? !target.TeamMember.IsOnSameTeam(playerManager.CurrentTeam) || target.Type != PokemonType.Player
                    : target.TeamMember.IsOnSameTeam(playerManager.CurrentTeam))
                || (target == pokemon && !definition.AllowSelfTarget)
                || target.HasStatusEffect(StatusType.Invincible) || target.HasStatusEffect(StatusType.Untargetable)
                || target.HasStatusEffect(StatusType.Invisible)
                || !definition.IsTargetInCastRange(transform.position, target.transform.position)) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        }
        direction.y = 0;
        direction = definition.AimStyle == MoveAimStyle.Ground ? Vector3.ClampMagnitude(direction, definition.Range)
            : direction.sqrMagnitude > 0.001f ? direction.normalized : transform.forward;
        int espeonBlastCount = 0;
        double previousNextCast = prefabNextCast[slot];
        if (definition is GlaceonMove) glaceReservedCooldown[slot] = previousNextCast;
        double espeonBonusExpiry = espeon != null ? espeon.BonusExpiresAt : 0;
        if (espeon != null && !espeon.Spend(move, out espeonBonus, out espeonBlastCount)) { RejectPrefabExecutionRPC(move, ownerRequest); return; }
        if (!aerial && !espeonBonus) prefabNextCast[slot] = now + Mathf.Max(0.25f, definition.ResolveCooldown(definition.EffectiveCooldown(upgraded)) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f));
        if (espeon != null && !espeonBonus && slot < 2) EspeonCooldownRPC(slot, (float)(prefabNextCast[slot] - now));
        if (definition.CooldownOnEffectEnd || definition is GlaceonMove) prefabNextCast[slot] = double.PositiveInfinity;
        int generation = ++prefabGenerations[slot];
        int sequence = ++prefabCastSequences[slot];
        prefabOwnerRequests[slot] = ownerRequest;
        if (definition is GlaceIcicleSpear) GlaceCombat?.BeginSpear(direction);
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
                || pokemon == null || pokemon.CurrentHp <= 0
                || ((definition is UmbreonMove || definition is TrevenantMove) && (PrefabActionsLocked(move) || prefabLearnedMoves[slot] != move
                    || pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)
                    || (target != null && (!UmbreonMoveRules.Living(target) || !definition.IsTargetInCastRange(transform.position, target.transform.position)))))
                || (definition is GlaceonMove && (prefabLearnedMoves[slot] != move || pokemon.HasAnyStatusEffect(GlaceonMoveRules.Interrupts)
                    || (definition.AimStyle == MoveAimStyle.Dash && pokemon.HasStatusEffect(StatusType.Immobilized))
                    || (target != null && (!GlaceonMoveRules.CanHit(playerManager, target) || !definition.IsTargetInCastRange(transform.position, target.transform.position)))))
                || (aerial && (ZoroarkNight == null || !ZoroarkNight.AerialWindow)))
            {
                if (asset.Status != AsyncOperationStatus.Succeeded) Debug.LogError($"Failed to load move {move}: {asset.OperationException}");
                Addressables.Release(asset);
                if (!cancelled && this != null && IsSpawned && espeon != null) espeon.Refund(move, espeonBonus, espeonBlastCount, espeonBonusExpiry);
                if (this != null && IsSpawned && generation == prefabGenerations[slot])
                {
                    if (definition is GlaceonMove || definition is UmbreonMove || definition is TrevenantMove)
                    {
                        prefabNextCast[slot] = previousNextCast;
                        if (definition is GlaceIcicleSpear) glaceonRuntime?.EndSpear();
                        RejectPrefabExecutionRPC(move, ownerRequest);
                    }
                    else FinishPrefabExecution(slot, move, definition, ownerRequest);
                }
                return;
            }
            if (move == AvailableMoves.EspeonStoredPower && !EspeonStoredPowerEffect.HasTargets(playerManager))
            {
                Addressables.Release(asset);
                espeon.Refund(move, espeonBonus, espeonBlastCount, espeonBonusExpiry);
                if (!espeonBonus)
                {
                    prefabNextCast[slot] = previousNextCast;
                    EspeonCooldownRPC(slot, (float)Math.Max(0, previousNextCast - NetworkManager.ServerTime.Time));
                }
                RejectPrefabExecutionRPC(move, ownerRequest);
                return;
            }
            GameObject instance = Instantiate(asset.Result, transform.position + Vector3.up * 0.8f,
                Quaternion.LookRotation(castDirection.sqrMagnitude > 0.001f ? castDirection : transform.forward));
            var effect = instance.GetComponent<PrefabMoveEffect>();
            if (effect == null)
            {
                Destroy(instance); Addressables.Release(asset);
                if (definition is GlaceonMove || definition is UmbreonMove || definition is TrevenantMove)
                {
                    prefabNextCast[slot] = previousNextCast;
                    if (definition is GlaceIcicleSpear) glaceonRuntime?.EndSpear();
                    RejectPrefabExecutionRPC(move, ownerRequest);
                }
                else FinishPrefabExecution(slot, move, definition, ownerRequest);
                return;
            }
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
                effect.RecastReady += () => { if (generation == prefabGenerations[slot]) PrefabExecutionStateRPC(move, false, ownerRequest); };
                effect.Completed += _ => { if (this != null && IsSpawned && generation == prefabGenerations[slot]) FinishPrefabExecution(slot, move, definition, ownerRequest); };
            }
            if (definition is GlaceonMove)
            {
                if (slot == 2) { legacyItemEnergy = 0; SyncGlaceUniteEnergy(); }
                if (!definition.CooldownOnEffectEnd && slot != 2)
                    prefabNextCast[slot] = NetworkManager.ServerTime.Time + definition.ResolveCooldown(definition.EffectiveCooldown(upgraded)) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f);
                if (slot == 2) prefabNextCast[slot] = 0;
                GlaceCastAcceptedRPC(move, definition.CooldownOnEffectEnd || slot == 2 ? 0 : (float)(prefabNextCast[slot] - NetworkManager.ServerTime.Time), ownerRequest);
            }
            playerManager.HeldItemManager?.OnMove((MoveType)slot);
            effect.Initialize(playerManager, castDirection, target, upgraded);
            if (definition is UmbreonMove)
            {
                if (slot == 2) legacyItemEnergy = 0;
                UmbreonAcceptedRPC(move, false, ownerRequest);
            }
            if (definition is TrevenantMove)
            {
                if (slot == 2) { legacyItemEnergy = 0; SyncTrevenantEnergy(); }
                TrevenantAcceptedRPC(move, false, ownerRequest);
            }
            PrefabLoadFinishedRPC(move, ownerRequest);
        };
    }

    private void FinishPrefabExecution(int slot, AvailableMoves move, PrefabMove definition, int ownerRequest = 0)
    {
        activePrefabMoves[slot] = null;
        PrefabLoadFinishedRPC(move, ownerRequest);
        if (!definition.CooldownOnEffectEnd) return;
        bool upgraded = false;
        foreach (var row in pokemon.BaseStats.LearnableMoves)
            if (row.isUpgraded && row.level <= pokemon.CurrentLevel)
                foreach (var asset in row.moves) if (asset != null && asset.move == move) upgraded = true;
        definition.IsUpgraded = upgraded;
        prefabNextCast[slot] = NetworkManager.ServerTime.Time + definition.ResolveCooldown(definition.EffectiveCooldown(upgraded)) * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f);
        if (definition is TrevenantMove)
        {
            if (slot == 1 && trevenantResetAfterEffect) { trevenantResetAfterEffect = false; prefabNextCast[slot] = NetworkManager.ServerTime.Time; }
            if (slot < 2) TrevenantCooldownRPC(slot, (float)(prefabNextCast[slot] - NetworkManager.ServerTime.Time), true);
            else TrevenantUniteFinishedRPC();
            return;
        }
        PrefabExecutionStateRPC(move, true, ownerRequest);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void PrefabLoadFinishedRPC(AvailableMoves move, int ownerRequest = 0)
    {
        if (uniteMove is PrefabMove unite && unite.MoveId == move && unite.MatchesExecutionRequest(ownerRequest)) unite.ReleaseLoadLock();
        foreach (var runtime in moves) if (runtime is PrefabMove prefab && prefab.MoveId == move && prefab.MatchesExecutionRequest(ownerRequest)) prefab.ReleaseLoadLock();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void RejectPrefabExecutionRPC(AvailableMoves move, int ownerRequest = 0)
    {
        if (uniteMove is PrefabMove unite && unite.MoveId == move && unite.MatchesExecutionRequest(ownerRequest)) unite.RejectExecution();
        foreach (var runtime in moves) if (runtime is PrefabMove prefab && prefab.MoveId == move && prefab.MatchesExecutionRequest(ownerRequest)) prefab.RejectExecution();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void PrefabExecutionStateRPC(AvailableMoves move, bool finished, int ownerRequest = 0)
    {
        foreach (var runtime in moves)
            if (runtime is PrefabMove prefab && prefab.MoveId == move && prefab.MatchesExecutionRequest(ownerRequest)) prefab.ReceiveExecutionState(finished);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void CancelPrefabMoveRPC(AvailableMoves move, int ownerRequest = 0)
    {
        var definition = MoveDatabase.GetMove(move) as PrefabMove;
        if (definition == null) return;
        definition.BalanceId = CharactersList.Instance?.GetMoveAsset(move)?.BalanceId;
        for (int slot = 0; slot < prefabLearnedMoves.Length; slot++)
        {
            if (prefabLearnedMoves[slot] != move || ((definition is GlaceonMove || definition is UmbreonMove || definition is TrevenantMove) && ownerRequest != prefabOwnerRequests[slot])) continue;
            foreach (var effect in new System.Collections.Generic.List<PrefabMoveEffect>(prefabEffects))
                if (effect != null && effect.MoveId == move && !(effect.PersistsAfterCasterDeath && pokemon.CurrentHp <= 0)) effect.Despawn();
            if (double.IsPositiveInfinity(prefabNextCast[slot]))
            {
                if (definition is GlaceonMove && activePrefabMoves[slot] == null)
                {
                    prefabNextCast[slot] = glaceReservedCooldown[slot];
                    if (definition is GlaceIcicleSpear) glaceonRuntime?.EndSpear();
                }
                else FinishPrefabExecution(slot, move, definition, ownerRequest);
            }
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
        ReduceLegacyItemCooldown(slot, (float)Math.Max(0, legacyItemNextCast[index] - now) * fraction);
        ReduceMoveCooldownFractionRPC(slot, fraction);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void ReduceMoveCooldownFractionRPC(MoveType slot, float fraction)
    {
        int index = (int)slot;
        if (moveStatuses[index] != null) ReduceMoveCooldown(slot, Mathf.Max(0, moveStatuses[index].Cooldown) * fraction);
    }
}
