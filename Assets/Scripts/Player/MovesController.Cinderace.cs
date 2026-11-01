using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private CinderaceCharacterRuntime CinderaceRuntime => Combat?.Character as CinderaceCharacterRuntime;
    public int CinderaceAttackCharge => CinderaceRuntime?.Charge ?? 0;

    internal static StatusEffect CreateCinderaceBurnEffect(ulong sourceId, ushort burnId)
        => StatusEffect.DamageOverTime(StatusType.Burned,
            new DamageInfo(sourceId, 0.2f, 0, 0, DamageType.Physical), 3f, 1f, burnId);

    public int GetCinderaceMarks(Pokemon target) => CinderaceRuntime?.GetMarks(target) ?? 0;
    public bool HasCinderaceBurn(Pokemon target) => CinderaceRuntime?.HasBurn(target) ?? false;
    public void CinderaceMarksVisualEnded(Pokemon target) => CinderaceRuntime?.MarksVisualEnded(target);
    public void ApplyCinderaceBurn(Pokemon target) => CinderaceRuntime?.ApplyBurn(target);
    public void CinderBuff(ushort id, Stat stat, short amount, float duration, bool percentage)
        => CinderaceRuntime?.ApplyBuff(id, stat, amount, duration, percentage);
    public void CinderaceUnstoppable(ushort id, float duration) => CinderaceRuntime?.ApplyUnstoppable(id, duration);
    public void EndCinderaceObject(int cast) { }
    public void FillCinderaceCharge() => CinderaceRuntime?.FillCharge();
    public void ArmCinderaceFlameSlow() => CinderaceRuntime?.ArmFlameSlow();
    public bool CinderaceHit(Pokemon target, DamageInfo damage, bool healing = false, bool slow = false)
        => CinderaceRuntime?.ResolveHit(target, damage, healing, slow) ?? false;
    public void ApplyCinderaceFeint(bool upgraded) => CinderaceRuntime?.ApplyFeint(upgraded);
    public void ReduceCinderaceMobilityCooldown() => CinderaceRuntime?.ReduceMobilityCooldown();

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void CinderaceSnapshotRPC(byte charge, bool movementLocked, long revision)
        => CinderaceRuntime?.ReceiveSnapshot(charge, movementLocked, revision);

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void CinderaceDashRPC(Vector3 origin, Vector3 destination, double started, float duration, bool active, bool completed)
        => CinderaceRuntime?.ReceiveDash(origin, destination, started, duration, active, completed);

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    internal void CinderaceMovementRPC(Vector2 input) => CinderaceRuntime?.ReceiveMovement(input);
}
