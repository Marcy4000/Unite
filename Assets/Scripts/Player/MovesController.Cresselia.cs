using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class MovesController
{
    private readonly NetworkVariable<double> cresseliaBoostReadyAt = new NetworkVariable<double>();
    private readonly NetworkVariable<bool> cresseliaEscort = new NetworkVariable<bool>();

    public CresseliaCharacterRuntime CresseliaRuntime => Combat?.Character as CresseliaCharacterRuntime;

    public bool CresseliaEscorting => CresseliaRuntime?.Escorting ?? cresseliaEscort.Value;
    public float CresseliaBoostRemaining => CresseliaRuntime != null
        ? CresseliaRuntime.BoostRemaining
        : (float)Math.Max(0d, cresseliaBoostReadyAt.Value - CresseliaNow);
    private double CresseliaNow => NetworkManager != null && NetworkManager.IsListening ? NetworkManager.ServerTime.Time : Time.timeAsDouble;
    private bool IsLivingCresselia => pokemon != null && pokemon.BaseStats != null && pokemon.CurrentHp > 0
        && pokemon.BaseStats.Passive == AvailablePassives.CresseliaPassive;

    internal void SyncCresseliaNetworkVariables(double boostReadyAt, bool escorting)
    {
        if (!IsServer || !IsSpawned) return;
        cresseliaBoostReadyAt.Value = boostReadyAt;
        cresseliaEscort.Value = escorting;
    }

    internal void RefundCresseliaBasicCooldown()
    {
        serverNextAttack = 0;
    }


    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void CresseliaSnapshotRPC(
        double boostReadyAt,
        bool escorting,
        bool movementLocked,
        bool uniteLocked,
        int unitePhase,
        double unitePhaseStarted,
        Vector3 unitePoint,
        Vector3 uniteOrigin,
        long revision)
        => CresseliaRuntime?.ReceiveSnapshot(
            boostReadyAt,
            escorting,
            movementLocked,
            uniteLocked,
            unitePhase,
            unitePhaseStarted,
            unitePoint,
            uniteOrigin,
            revision);


    private void DisposeCresseliaRuntime()
    {
        cresseliaDecorationRequested = false;
        DisposeCresseliaPresentation();
    }

    private void DisposeLegacyRuntimesOnDespawn()
    {
        DisposeNinetalesRuntime();
        basicAttack?.Dispose();
        DisposeMeowsticRuntime();
        DisposeEmboarRuntime();
        DisposeFlygonRuntime();
        if (pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.CresseliaPassive)
        {
            DisposeCresseliaRuntime();
        }
    }
}

// Source-specific IDs are leased, not derived by truncating NetworkObjectIds.
// Duplicate Cresselias can protect the same ally without merging conversions.
public static class CresseliaEffectIds
{
    private static readonly HashSet<ushort> used = new HashSet<ushort>();
    public static ushort Acquire()
    {
        for (ushort id = 48000; id < 50000; id++) if (used.Add(id)) return id;
        throw new InvalidOperationException("Cresselia effect ID pool exhausted.");
    }
    public static void Release(ushort id) { used.Remove(id); }
}

public sealed class CresseliaShieldLease : IDisposable
{
    private readonly Pokemon target;
    private bool dead, disposed;
    public ushort Id { get; }
    public int Remaining => !disposed && !dead && CresseliaMoveRuntime.Living(target) ? target.GetShieldWithID(Id)?.Amount ?? 0 : 0;
    public bool Intact => Remaining > 0;
    public CresseliaShieldLease(Pokemon target, int amount)
    {
        this.target = target; Id = CresseliaEffectIds.Acquire();
        target.OnDeath += OnDeath;
        // Conversion is the sole expiry owner, independent of VFX and shield timer ordering.
        target.AddShieldRPC(new ShieldInfo(amount, Id, 0, CresseliaMoveRules.BlessingDuration, false));
    }
    private void OnDeath(DamageInfo damage) { dead = true; Dispose(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (target != null)
        {
            target.OnDeath -= OnDeath;
            if (target.IsSpawned && target.IsServer) target.RemoveShieldWithIDRPC(Id);
        }
        CresseliaEffectIds.Release(Id);
    }
}
