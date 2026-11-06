using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private BasicAttackBase serverBasicAttack;
    private string serverAttackName;
    private double serverNextAttack, legacyNextAttack;
    private CombatActionLock serverAttackLock;
    private WallCrossingTraversal attackTraversal;
    private int attackDriveGeneration;
    private readonly MoveUseState[] legacyItemUses = { new MoveUseState(), new MoveUseState() };
    private readonly double[] legacyItemNextCast = new double[2];
    private double legacyItemEnergyTime;
    private float legacyItemEnergy;
    public int ServerActionLocks { get; internal set; }

    public MoveType GetMoveSlot(MoveBase move)
    {
        if (moves[0] == move) return MoveType.MoveA;
        if (moves[1] == move) return MoveType.MoveB;
        return MoveType.UniteMove;
    }

    public void BeginMoveCooldown(MoveBase move)
    {
        if (!IsOwner || move == null) return;
        int slot = (int)GetMoveSlot(move);
        if (slot >= 2 || moves[slot] != move || moveStatuses[slot] == null) return;
        moveStatuses[slot].Cooldown = GetMoveCooldown(move);
        moveStatuses[slot].AddStatus(ActionStatusType.Cooldown);
        UpdateMoveUI(slot);
    }

    public BasicAttackBase ServerBasicAttack
    {
        get
        {
            if (!IsServer || pokemon == null || pokemon.BaseStats == null) return null;
            string name = pokemon.BaseStats.PokemonName;
            if (serverBasicAttack == null || serverAttackName != name)
            {
                DisposeServerAttack();
                var definition = BasicAttacksDatabase.GetBasicAttack(name);
                if (!definition.ServerResolved) return null;
                serverAttackName = name; serverBasicAttack = definition;
                definition.InitializeServer(playerManager);
                pokemon.OnDeath += ResetServerAttack;
            }
            return serverBasicAttack;
        }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void PerformServerBasicAttackRPC(ulong targetId)
    {
        if (pokemon == null || pokemon.CurrentHp <= 0 || ServerActionLocks > 0
            || (ZoroarkNight != null && ZoroarkNight.Finishing)
            || pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)
            || NetworkManager.ServerTime.Time + 0.02 < serverNextAttack) { RejectServerBasicAttackRPC(); return; }
        var attack = ServerBasicAttack;
        if (attack == null) { RejectServerBasicAttackRPC(); return; }
        Pokemon target = NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) ? obj.GetComponent<Pokemon>() : null;
        serverNextAttack = NetworkManager.ServerTime.Time + GetAtkSpeedCooldown();
        pokemon.NotifyOffensiveAction();
        attack.PerformOnServer(target);
        // Also clears a pending request when the definition rejected its target.
        RejectServerBasicAttackRPC();
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void RejectServerBasicAttackRPC() { serverAttackPending = false; }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void SynchronizeBasicAttackRPC(int charge, float empoweredSeconds)
    {
        basicAttack?.ApplyServerState(charge, Time.time + Mathf.Max(0, empoweredSeconds));
    }

    private void ReportLegacyMoveAction(MoveBase move)
    {
        if (pokemon == null || pokemon.BaseStats == null || pokemon.BaseStats.LearnableMoves == null) return;
        foreach (var learn in pokemon.BaseStats.LearnableMoves)
            foreach (var asset in learn.moves)
                if (asset != null && MoveDatabase.GetMove(asset.move)?.GetType() == move.GetType())
                { ReportOffensiveActionRPC(asset.move, false); return; }
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    private void ReportOffensiveActionRPC(AvailableMoves id, bool basic)
    {
        if (pokemon == null || pokemon.CurrentHp <= 0 || ServerActionLocks > 0) return;
        if (basic)
        {
            if (NetworkManager.ServerTime.Time < legacyNextAttack) return;
            legacyNextAttack = NetworkManager.ServerTime.Time + GetAtkSpeedCooldown() * 0.8;
            pokemon.NotifyOffensiveAction(); return;
        }
        if (pokemon.BaseStats == null) return;
        foreach (var learn in pokemon.BaseStats.LearnableMoves)
            foreach (var asset in learn.moves)
                if (asset != null && asset.move == id && learn.level <= pokemon.CurrentLevel)
                {
                    if (!IsServerResolvedItemMove(MoveDatabase.GetMove(asset.move)))
                    {
                        if (!AcceptLegacyItemCast(asset)) return;
                        playerManager.HeldItemManager?.OnMove(asset.moveType);
                    }
                    if (asset.moveLabel != MoveLabels.Buff && asset.moveLabel != MoveLabels.Recovery
                        && asset.moveLabel != MoveLabels.None) pokemon.NotifyOffensiveAction();
                    return;
                }
    }

    private static bool IsServerResolvedItemMove(MoveBase move) => move is PrefabMove || move is VaporMoveBase
        || move is VaporDive || move is CresseliaMoveBase || move is CresseliaUnite || move is CinderMoveBase
        || move is FlareonMoveBase || move is FlygonMoveBase || move is EmboarMoveBase || move is MeowsticMoveBase;

    private bool AcceptLegacyItemCast(MoveAsset asset)
    {
        int slot = (int)asset.moveType;
        if (slot < 0 || slot >= prefabLearnedMoves.Length || prefabLearnedMoves[slot] != asset.move) return false;
        MoveBase definition = MoveDatabase.GetMove(asset.move);
        if (definition == null || IsServerResolvedItemMove(definition)) return false;
        foreach (var learn in pokemon.BaseStats.LearnableMoves)
            if (learn.isUpgraded && learn.level <= pokemon.CurrentLevel)
                foreach (var upgrade in learn.moves)
                    if (upgrade != null && upgrade.move == asset.move) definition.IsUpgraded = true;
        double now = NetworkManager.ServerTime.Time;
        if (slot == 2)
        {
            TickLegacyItemResources();
            if (asset.uniteEnergyCost <= 0 || legacyItemEnergy < asset.uniteEnergyCost) return false;
            legacyItemEnergy = 0;
            return true;
        }
        if (now < legacyItemNextCast[slot]) return false;
        float cooldown = definition.Cooldown * Mathf.Max(0.01f, 1 - pokemon.GetCDR() / 100f);
        if (definition.IsStoredMove)
        {
            legacyItemUses[slot].Tick((float)now, definition.MaxStoredUses, cooldown);
            if (!legacyItemUses[slot].TrySpend((float)now, definition.StoredUseInterval)) return false;
        }
        legacyItemNextCast[slot] = now + (definition.IsStoredMove ? definition.StoredUseInterval : Mathf.Max(0.25f, cooldown));
        return true;
    }

    private int LegacyItemUniteCost()
    {
        if (pokemon == null || pokemon.BaseStats == null) return 0;
        int unlearnedCost = 0;
        foreach (var learn in pokemon.BaseStats.LearnableMoves)
            foreach (var asset in learn.moves)
                if (asset != null && asset.moveType == MoveType.UniteMove && !learn.isUpgraded)
                {
                    if (asset.move == prefabLearnedMoves[2]) return asset.uniteEnergyCost;
                    if (unlearnedCost == 0) unlearnedCost = asset.uniteEnergyCost;
                }
        return unlearnedCost;
    }

    private void TickLegacyItemResources()
    {
        if (!IsServer || !IsSpawned) return;
        double now = NetworkManager.ServerTime.Time;
        if (legacyItemEnergyTime == 0) legacyItemEnergyTime = now;
        int ticks = Mathf.Max(0, (int)(now - legacyItemEnergyTime));
        if (ticks == 0) { SyncGlaceUniteEnergy(); return; }
        legacyItemEnergyTime += ticks;
        int cost = LegacyItemUniteCost();
        legacyItemEnergy = Mathf.Min(cost, legacyItemEnergy + ticks * Mathf.FloorToInt(900 * (1 + pokemon.GetPreciseStatChange(Stat.EnergyRate) / 100f)));
        SyncGlaceUniteEnergy();
    }

    public void GrantLegacyItemEnergy(int amount)
    {
        if (!IsServer || pokemon == null || amount <= 0) return;
        TickLegacyItemResources();
        legacyItemEnergy = Mathf.Min(LegacyItemUniteCost(), legacyItemEnergy
            + Mathf.FloorToInt(amount * (1 + pokemon.GetPreciseStatChange(Stat.EnergyRate) / 100f)));
        SyncGlaceUniteEnergy();
    }

    public void ReduceLegacyItemCooldown(MoveType slot, float seconds)
    {
        int index = (int)slot;
        if (!IsServer || index < 0 || index >= legacyItemNextCast.Length || !float.IsFinite(seconds) || seconds <= 0) return;
        double now = NetworkManager.ServerTime.Time;
        legacyItemNextCast[index] = System.Math.Max(now, legacyItemNextCast[index] - seconds);
        legacyItemUses[index].ReduceRecharge(seconds, (float)now);
    }

    private void ResetServerAttack(DamageInfo damage)
    {
        ResetServerAttackInput();
        serverBasicAttack?.ResetAttack(); serverNextAttack = 0;
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void PresentServerAttackRPC(bool active, Vector3 direction, string animation, float duration = 0, bool cancelMovement = false, float blendDuration = 0)
    {
        serverAttackPending = false; serverAttackPresented = active;
        if (cancelMovement) ClearServerAttackBufferedInput();
        if (!active)
        {
            attackDriveGeneration++;
            if (cancelMovement) attackTraversal?.Cancel(); else attackTraversal?.End();
            attackTraversal = null;
        }
        serverAttackLock?.Dispose(); serverAttackLock = null;
        if (active)
        {
            float window = basicAttack?.MoveInputBufferDuration ?? serverBasicAttack?.MoveInputBufferDuration ?? 0;
            serverAttackLock = new CombatActionLock(playerManager, true, lockMoves: window <= 0);
            if (direction.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(direction);
        }
        if (AnimationManagerAvailable() && !string.IsNullOrEmpty(animation))
        {
            playerManager.AnimationManager.PlayLocalAnimation(animation, duration, blendDuration);
        }
    }

    private bool AnimationManagerAvailable() => playerManager != null && playerManager.AnimationManager != null && playerManager.AnimationManager.Animator != null;

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void DriveBasicAttackRPC(Vector3 origin, Vector3 destination, float duration)
    {
        if (IsOwner || IsServer) StartCoroutine(DriveBasicAttack(origin, destination, duration));
    }

    private System.Collections.IEnumerator DriveBasicAttack(Vector3 origin, Vector3 destination, float duration)
    {
        int generation = ++attackDriveGeneration;
        attackTraversal?.End(); var traversal = new WallCrossingTraversal(); attackTraversal = traversal; traversal.Begin(PlayerMovement);
        float started = Time.time;
        while (generation == attackDriveGeneration && Time.time - started < duration && pokemon.CurrentHp > 0 && !pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions))
        {
            float t = Mathf.Clamp01((Time.time - started) / duration);
            traversal.Move(Vector3.Lerp(origin, destination, t) - transform.position);
            yield return null;
        }
        if (generation == attackDriveGeneration)
        {
            if (pokemon.CurrentHp > 0 && !pokemon.HasAnyStatusEffect(CombatActionLock.Interruptions)) traversal.Move(destination - transform.position);
            traversal.End(); attackTraversal = null;
        }
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void ShowCombatVisualRPC(string resource, Vector3 position, Vector3 direction, Vector3 scale, float duration)
    {
        if (IsClient) TimedCombatVisual.Spawn(resource, position, direction, scale, duration);
    }

    private void DisposeServerAttack()
    {
        ResetServerAttackInput();
        attackDriveGeneration++;
        if (pokemon != null && pokemon.CurrentHp > 0 && IsSpawned) attackTraversal?.End(); else attackTraversal?.Cancel();
        attackTraversal = null;
        serverAttackLock?.Dispose(); serverAttackLock = null;
        if (pokemon != null) pokemon.OnDeath -= ResetServerAttack;
        serverBasicAttack?.Dispose(); serverBasicAttack = null; serverAttackName = null;
    }
}
