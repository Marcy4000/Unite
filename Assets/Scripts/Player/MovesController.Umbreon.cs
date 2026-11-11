using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private UmbreonRuntime umbreonRuntime;
    internal int UmbreonSnarlLocks { get; set; }
    private bool PrefabActionsLocked(AvailableMoves move) => ServerActionLocks >
        (move == AvailableMoves.UmbreonFoulPlay ? UmbreonSnarlLocks : 0);
    private readonly NetworkVariable<bool> umbreonMeanBoosted = new NetworkVariable<bool>();
    private double umbreonFoulUntil;
    public float UmbreonFoulRemaining => NetworkManager == null ? 0 : Mathf.Max(0, (float)(umbreonFoulUntil - NetworkManager.ServerTime.Time));
    public bool UmbreonMeanBoosted => umbreonMeanBoosted.Value;
    private bool IsUmbreon => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.UmbreonPassive;
    public UmbreonRuntime UmbreonCombat => IsServer && IsUmbreon ? umbreonRuntime ?? (umbreonRuntime = new UmbreonRuntime(playerManager)) : null;
    public bool HasUmbreonMove(AvailableMoves id) => prefabLearnedMoves[0] == id || prefabLearnedMoves[1] == id || prefabLearnedMoves[2] == id;
    private void UpdateUmbreonRuntime()
    {
        if (!IsServer || !IsUmbreon) return;
        UmbreonCombat?.Tick();
        umbreonMeanBoosted.Value = pokemon.CurrentHp > 0 && UmbreonCombat.InMeanZone;
    }
    private void DisposeUmbreonRuntime() { umbreonRuntime?.Dispose(); umbreonRuntime = null; }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void UmbreonFoulWindowRPC(double expiresAt)
    {
        umbreonFoulUntil = expiresAt;
        if (IsOwner && playerManager != null && playerManager.IsLocalHuman && BattleUIManager.instance != null)
            BattleUIManager.instance.ShowMoveSecondaryCooldown(0, UmbreonFoulRemaining);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void ShowUmbreonMarkRPC(ulong targetId, ushort markId)
    {
        if (!IsClient || !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var obj) || !obj.TryGetComponent(out Pokemon target)) return;
        var visual = TimedCombatVisual.Spawn(UmbreonMoveRules.Visuals + "Mark", target.transform.position + Vector3.up * 1.7f,
            Vector3.forward, Vector3.one * 0.35f, UmbreonMoveRules.SwiftMarkDuration);
        if (visual != null) (visual.GetComponent<UmbreonMarkVisual>() ?? visual.AddComponent<UmbreonMarkVisual>()).Initialize(target, markId);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void UmbreonAcceptedRPC(AvailableMoves id, bool recast, int request)
    {
        if (uniteMove is UmbreonMove unite && unite.MoveId == id && unite.MatchesExecutionRequest(request)) { unite.ReleaseLoadLock(); unite.AcceptExecution(); }
        foreach (var move in moves)
            if (move is UmbreonMove definition && definition.MoveId == id && definition.MatchesExecutionRequest(request))
            {
                definition.ReleaseLoadLock();
                if (!recast) definition.AcceptExecution();
            }
    }
}
