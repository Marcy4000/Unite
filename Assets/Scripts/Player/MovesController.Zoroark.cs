using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private ZoroarkCharacterRuntime ZoroarkRuntime => Combat?.Character as ZoroarkCharacterRuntime;
    public ZoroarkExecution ZoroarkNight { get => ZoroarkRuntime?.ZoroarkNight; internal set { if (ZoroarkRuntime != null) ZoroarkRuntime.ZoroarkNight = value; } }
    public ZoroarkExecution ZoroarkFeint { get => ZoroarkRuntime?.ZoroarkFeint; internal set { if (ZoroarkRuntime != null) ZoroarkRuntime.ZoroarkFeint = value; } }
    public bool ZoroarkFeintReady => ZoroarkRuntime?.ZoroarkFeintReady ?? false;
    public bool ZoroarkUniqueReady => ZoroarkRuntime?.ZoroarkUniqueReady ?? false;
    public bool ZoroarkAerialOpen => ZoroarkRuntime?.ZoroarkAerialOpen ?? false;
    public Vector2 ZoroarkMovementInput => ZoroarkRuntime?.ZoroarkMovementInput ?? Vector2.zero;
    public AvailableMoves ZoroarkWindowMove => ZoroarkRuntime?.ZoroarkWindowMove ?? default;
    public float ZoroarkWindowRemaining => ZoroarkRuntime?.ZoroarkWindowRemaining ?? 0;
    public bool HasZoroarkDisguise => ZoroarkRuntime?.HasZoroarkDisguise ?? false;
    public Sprite ZoroarkDisguisePortrait => ZoroarkRuntime?.ZoroarkDisguisePortrait;
    public int ZoroarkDisguiseEnergy => ZoroarkRuntime?.ZoroarkDisguiseEnergy ?? 0;
    public bool ZoroarkDisguiseIsPlayer => ZoroarkRuntime?.ZoroarkDisguiseIsPlayer ?? false;
    public Pokemon ZoroarkDisguiseSource => ZoroarkRuntime?.ZoroarkDisguiseSource;
    public bool IsZoroarkEnemyView => ZoroarkRuntime?.IsZoroarkEnemyView ?? false;

    private void UpdateZoroarkRuntime() => ZoroarkRuntime?.Tick();
    private void DisposeZoroarkRuntime() => ZoroarkRuntime?.Dispose();
    private void ActivateZoroarkIllusion() => ZoroarkRuntime?.ActivateZoroarkIllusion();
    private bool CanUseZoroarkIllusion() => ZoroarkRuntime?.CanUseZoroarkIllusion() ?? false;
    private bool CanRecastZoroark(int slot) => ZoroarkRuntime?.CanRecast(slot) ?? false;
    internal BattleActionStatus ZoroarkMoveStatus(int slot) => moveStatuses[slot];
    internal void RefreshZoroarkMoveUI() { if (moves[0] != null && moveStatuses[0] != null) UpdateMoveUI(0); }
    public bool HandleZoroarkMovement(Vector2 input) => ZoroarkRuntime?.HandleMovement(input) ?? false;
    public Vector3 ZoroarkBounceDirection(Vector3 fallback) => ZoroarkRuntime?.ZoroarkBounceDirection(fallback) ?? fallback;
    public void SpendZoroarkAerialReposition() { if (IsServer) ZoroarkRuntime?.SpendZoroarkAerialReposition(); }
    public void ZoroarkNormalMoveStarted(Vector3 origin) => ZoroarkRuntime?.ZoroarkNormalMoveStarted(origin);
    public void ZoroarkNormalMoveHit() => ZoroarkRuntime?.ZoroarkNormalMoveHit();
    public void ResetZoroarkOtherMove() => ZoroarkRuntime?.ResetZoroarkOtherMove();

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    internal void ZoroarkMovementRPC(Vector2 input) => ZoroarkRuntime?.ReceiveMovement(input);
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void ZoroarkWindowRPC(AvailableMoves move, double expiresAt) => ZoroarkRuntime?.ReceiveWindow(move, expiresAt);
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void ZoroarkComboRPC(bool night, int count, bool active, bool uniqueReady, bool finisherReady, bool airborne)
        => ZoroarkRuntime?.ReceiveCombo(night, count, active, uniqueReady, finisherReady, airborne);
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void RequestZoroarkIllusionRPC() => ZoroarkRuntime?.ActivateZoroarkIllusion();
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    internal void PresentZoroarkIllusionRPC(bool active, ulong source, int hp, int maxHp, int level, int shield, int energy, string displayName)
        => ZoroarkRuntime?.PresentIllusion(active, source, hp, maxHp, level, shield, energy, displayName);
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void BreakZoroarkIllusionRPC() => ZoroarkRuntime?.BreakZoroarkIllusion();
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    internal void EndZoroarkIllusionRPC() => ZoroarkRuntime?.EndIllusion();
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    internal void ZoroarkAerialSpentRPC() => ZoroarkRuntime?.SpendZoroarkAerialReposition();
}
