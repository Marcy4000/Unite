using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private TrevenantRuntime trevenantRuntime;
    private readonly NetworkVariable<float> trevenantGauge = new NetworkVariable<float>(-1);
    private readonly NetworkVariable<int> trevenantEnergy = new NetworkVariable<int>();
    private readonly NetworkVariable<double> trevenantPassiveReadyAt = new NetworkVariable<double>();
    private readonly NetworkVariable<double> trevenantCurseExpiresAt = new NetworkVariable<double>();
    private readonly NetworkVariable<double> trevenantHammerExpiresAt = new NetworkVariable<double>();
    private double displayedTrevenantDurationExpiry;
    private BattleUIManager trevenantDurationUI;
    private bool trevenantGaugeShown;
    private bool trevenantResetAfterEffect;
    private bool IsTrevenant => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.TrevenantPassive;
    public TrevenantRuntime TrevenantCombat => IsServer && IsTrevenant ? trevenantRuntime ?? (trevenantRuntime = new TrevenantRuntime(playerManager)) : null;
    public float TrevenantCurseGauge => trevenantGauge.Value;
    public float TrevenantCurseRemaining => NetworkManager == null ? 0 : Mathf.Max(0, (float)(trevenantCurseExpiresAt.Value - NetworkManager.ServerTime.Time));
    public float TrevenantHammerRemaining => NetworkManager == null ? 0 : Mathf.Max(0, (float)(trevenantHammerExpiresAt.Value - NetworkManager.ServerTime.Time));
    public float TrevenantPassiveRemaining => trevenantPassiveReadyAt.Value < 0 ? -1 : NetworkManager == null ? 0 : Mathf.Max(0, (float)(trevenantPassiveReadyAt.Value - NetworkManager.ServerTime.Time));
    private void UpdateTrevenantRuntime()
    {
        if (!IsTrevenant) { if (trevenantRuntime != null) DisposeTrevenantRuntime(); return; }
        if (IsServer) TrevenantCombat.Tick();
        if (!IsOwner) return;
        if (uniteMoveMaxCharge > 0)
        {
            uniteMoveCharge = trevenantEnergy.Value;
            if (uniteMoveCharge >= uniteMoveMaxCharge) uniteMoveStatus.RemoveStatus(ActionStatusType.Cooldown);
            else if (!uniteMoveStatus.HasStatus(ActionStatusType.Cooldown)) uniteMoveStatus.AddStatus(ActionStatusType.Cooldown);
        }
        bool show = trevenantGauge.Value >= 0 && pokemon.CurrentHp > 0;
        if (playerManager.HPBar != null)
        {
            if (show != trevenantGaugeShown) playerManager.HPBar.ShowGenericGuage(show);
            if (show) playerManager.HPBar.UpdateGenericGuageValue(trevenantGauge.Value, 1);
        }
        trevenantGaugeShown = show;
        UpdateTrevenantSecondaryDuration();
    }
    private void UpdateTrevenantSecondaryDuration()
    {
        if (!IsOwner || playerManager == null || !playerManager.IsLocalHuman || BattleUIManager.instance == null) return;
        double expiresAt = System.Math.Max(trevenantCurseExpiresAt.Value, trevenantHammerExpiresAt.Value);
        if (pokemon.CurrentHp <= 0) expiresAt = 0;
        if (displayedTrevenantDurationExpiry == expiresAt && trevenantDurationUI == BattleUIManager.instance) return;
        displayedTrevenantDurationExpiry = expiresAt;
        trevenantDurationUI = BattleUIManager.instance;
        trevenantDurationUI.ShowMoveSecondaryCooldown(0, NetworkManager == null ? 0 : Mathf.Max(0, (float)(expiresAt - NetworkManager.ServerTime.Time)));
    }
    public void SetTrevenantCurseExpiry(double expiresAt)
    {
        if (!IsServer || !IsSpawned) return;
        trevenantCurseExpiresAt.Value = expiresAt;
        UpdateTrevenantSecondaryDuration();
    }
    public void SetTrevenantHammerExpiry(double expiresAt)
    {
        if (!IsServer || !IsSpawned) return;
        trevenantHammerExpiresAt.Value = expiresAt;
        UpdateTrevenantSecondaryDuration();
    }
    public void SyncTrevenantState(float gauge, double passiveReadyAt)
    { if (IsServer && IsSpawned) { trevenantGauge.Value = gauge; trevenantPassiveReadyAt.Value = passiveReadyAt; } }
    private void SyncTrevenantEnergy() { if (IsServer && IsSpawned && IsTrevenant) trevenantEnergy.Value = Mathf.FloorToInt(legacyItemEnergy); }
    public void ReduceTrevenantCooldown(int slot, float amount)
    {
        if (!IsServer || slot < 0 || slot > 1 || amount <= 0 || double.IsPositiveInfinity(prefabNextCast[slot])) return;
        prefabNextCast[slot] = System.Math.Max(NetworkManager.ServerTime.Time, prefabNextCast[slot] - amount);
        TrevenantCooldownRPC(slot, (float)(prefabNextCast[slot] - NetworkManager.ServerTime.Time), false);
    }
    public void ResetTrevenantSecondMove()
    {
        if (!IsServer) return;
        // A live tether may remain until replacement. Detach its completion
        // generation so it cannot put the new cast back on cooldown.
        if (activePrefabMoves[1] is TrevenantPainSplitEffect)
        { activePrefabMoves[1] = null; prefabGenerations[1]++; prefabNextCast[1] = NetworkManager.ServerTime.Time; }
        if (double.IsPositiveInfinity(prefabNextCast[1])) { trevenantResetAfterEffect = true; return; }
        prefabNextCast[1] = NetworkManager.ServerTime.Time;
        TrevenantCooldownRPC(1, 0, true);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void TrevenantCooldownRPC(int slot, float remaining, bool complete)
    {
        if (slot < 0 || slot >= moves.Length || !(moves[slot] is TrevenantMove move)) return;
        if (complete) move.CompleteExecution(remaining); else SetTrevenantOwnerCooldown(move, remaining);
    }
    public void SetTrevenantOwnerCooldown(TrevenantMove move, float remaining)
    {
        if (!IsOwner) return;
        int slot = (int)GetMoveSlot(move);
        if (slot >= 2 || moveStatuses[slot] == null) return;
        moveStatuses[slot].Cooldown = Mathf.Max(0, remaining);
        if (remaining > 0) moveStatuses[slot].AddStatus(ActionStatusType.Cooldown);
        else moveStatuses[slot].RemoveStatus(ActionStatusType.Cooldown);
        UpdateMoveUI(slot);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void TrevenantAcceptedRPC(AvailableMoves id, bool recast, int request)
    {
        if (uniteMove is TrevenantMove unite && unite.MoveId == id && unite.MatchesExecutionRequest(request)) unite.AcceptExecution();
        foreach (var move in moves)
            if (!recast && move is TrevenantMove definition && definition.MoveId == id && definition.MatchesExecutionRequest(request)) definition.AcceptExecution();
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void TrevenantUniteFinishedRPC()
    { if (uniteMove is TrevenantMove unite) unite.CompleteExecution(0); }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void ShowTrevenantMarkRPC(ulong targetId, ushort markId)
    {
        if (!IsClient || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) || !obj.TryGetComponent(out Pokemon target)) return;
        var visual = TimedCombatVisual.Spawn(TrevenantMoveRules.Visuals + "ForestMark", target.transform.position + Vector3.up * 2,
            Vector3.forward, Vector3.one * 0.5f, TrevenantMoveRules.UniteMarkDuration);
        if (visual != null) visual.GetComponent<TrevenantMarkVisual>().Initialize(target, markId);
    }
    private void DisposeTrevenantRuntime()
    {
        trevenantRuntime?.Dispose(); trevenantRuntime = null;
        if (trevenantDurationUI != null && IsOwner) trevenantDurationUI.ShowMoveSecondaryCooldown(0, 0);
        trevenantDurationUI = null; displayedTrevenantDurationExpiry = 0;
        if (trevenantGaugeShown && playerManager != null && playerManager.HPBar != null) playerManager.HPBar.ShowGenericGuage(false);
        trevenantGaugeShown = false;
    }
}
