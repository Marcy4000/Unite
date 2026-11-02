using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private EspeonRuntime espeonRuntime;
    private readonly NetworkVariable<int> espeonBlasts = new NetworkVariable<int>();
    private readonly NetworkVariable<double> espeonBonusUntil = new NetworkVariable<double>();
    private readonly NetworkVariable<double> espeonBoostAt = new NetworkVariable<double>();
    private readonly NetworkVariable<ulong> espeonMark = new NetworkVariable<ulong>(ulong.MaxValue);
    private IDisposable espeonAmmoLock;
    private EspeonStockView espeonStockView;
    private bool espeonLastBonus;
    private bool IsEspeon => pokemon != null && pokemon.BaseStats != null && pokemon.BaseStats.Passive == AvailablePassives.EspeonPassive;
    public int EspeonBlastCount => espeonBlasts.Value;
    public Pokemon EspeonMarkedTarget => NetworkManager != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(espeonMark.Value, out var obj) ? obj.GetComponent<Pokemon>() : null;
    public void SetEspeonMark(ulong id, bool active) { if (IsServer) espeonMark.Value = active ? id : ulong.MaxValue; }
    public void ClearEspeonMark(Pokemon target)
    {
        if (IsServer && target != null && espeonMark.Value == target.NetworkObjectId) espeonMark.Value = ulong.MaxValue;
    }
    public void ClearEspeonFuture(EspeonFutureSightEffect effect) { espeonRuntime?.Unmark(effect); }
    public bool EspeonBonusReady => IsEspeon && NetworkManager != null && NetworkManager.ServerTime.Time < espeonBonusUntil.Value;
    public bool EspeonBoostReady => NetworkManager != null && NetworkManager.ServerTime.Time >= espeonBoostAt.Value;
    public float EspeonBoostProgress => NetworkManager == null ? 0 : Mathf.Clamp01(1 -
        (float)(espeonBoostAt.Value - NetworkManager.ServerTime.Time) / Mathf.Max(0.01f, EspeonMoveRules.BoostRecharge));
    public EspeonRuntime EspeonCombat => IsServer && IsEspeon ? espeonRuntime ?? (espeonRuntime = new EspeonRuntime(playerManager)) : null;
    public bool HasEspeonMove(AvailableMoves move) => prefabLearnedMoves[0] == move || prefabLearnedMoves[1] == move;
    public void SetEspeonBoostReady(double at) { if (IsServer) espeonBoostAt.Value = at; }
    public void SetEspeonResources(int count, double bonusUntil)
    {
        if (!IsServer) return;
        espeonBlasts.Value = count; espeonBonusUntil.Value = bonusUntil;
    }
    private void UpdateEspeonRuntime()
    {
        if (!IsEspeon) return;
        if (IsServer) EspeonCombat?.Tick();
        if (!IsClient) return;
        if (espeonStockView == null && playerManager.HPBar != null)
        {
            var prefab = Resources.Load<EspeonStockView>(EspeonMoveRules.Visuals + "Stock");
            if (prefab != null) espeonStockView = Instantiate(prefab, transform);
        }
        if (espeonStockView != null) espeonStockView.Present(EspeonBlastCount,
            pokemon.CurrentHp > 0 && pokemon.CurrentLevel >= 3 && (playerManager.Vision == null || playerManager.Vision.IsRendered), EspeonBoostReady, EspeonBonusReady);
        if (!IsOwner || moveStatuses[0] == null) return;
        bool empty = moves[0] is EspeonStoredPower && EspeonBlastCount == 0 && !EspeonBonusReady;
        if (empty && espeonAmmoLock == null) espeonAmmoLock = AcquireMoveStatus(0, ActionStatusType.Disabled);
        else if (!empty) { espeonAmmoLock?.Dispose(); espeonAmmoLock = null; }
        if (espeonLastBonus != EspeonBonusReady)
        {
            espeonLastBonus = EspeonBonusReady;
            if (!espeonLastBonus && moves[0] is EspeonMove && moves[0].IsActive) moves[0].Cancel();
            UpdateMoveUI(0);
            if (playerManager.IsLocalHuman && BattleUIManager.instance != null)
                BattleUIManager.instance.ShowMoveSecondaryCooldown(0, EspeonBonusReady
                    ? (float)(espeonBonusUntil.Value - NetworkManager.ServerTime.Time) : 0);
        }
    }
    private bool CanRecastEspeon(int slot) => slot == 0 && EspeonBonusReady && moves[0] is EspeonMove
        && moveStatuses[0] != null && (moveStatuses[0].GetStatus() & ~ActionStatusType.Cooldown) == ActionStatusType.None;
    public void ReduceEspeonFutureCooldown(float seconds)
    {
        if (!IsServer || prefabLearnedMoves[1] != AvailableMoves.EspeonFutureSight) return;
        prefabNextCast[1] = Math.Max(NetworkManager.ServerTime.Time, prefabNextCast[1] - seconds);
        EspeonCooldownRPC(1, (float)Math.Max(0, prefabNextCast[1] - NetworkManager.ServerTime.Time));
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    private void EspeonCooldownRPC(int slot, float remaining)
    {
        if (slot >= 2 || moveStatuses[slot] == null) return;
        moveStatuses[slot].Cooldown = remaining;
        if (remaining > 0) moveStatuses[slot].AddStatus(ActionStatusType.Cooldown);
        else moveStatuses[slot].RemoveStatus(ActionStatusType.Cooldown);
        UpdateMoveUI(slot);
    }
    public void LaunchEspeonBasic(Pokemon target, bool boosted)
    {
        if (!IsServer || !EspeonMoveRules.CanHit(playerManager, target)) return;
        bool cancelled = false;
        Action<DamageInfo> death = _ => cancelled = true;
        pokemon.OnDeath += death;
        Addressables.LoadAssetAsync<GameObject>(EspeonMoveRules.Folder + "EspeonBasicProjectile.prefab").Completed += asset =>
        {
            if (pokemon != null) pokemon.OnDeath -= death;
            if (cancelled || this == null || !IsSpawned || !EspeonMoveRules.Living(pokemon)
                || !EspeonMoveRules.CanHit(playerManager, target) || asset.Status != AsyncOperationStatus.Succeeded)
            { Addressables.Release(asset); return; }
            var root = Instantiate(asset.Result, transform.position + Vector3.up * 0.8f, Quaternion.identity);
            var effect = root.GetComponent<EspeonBasicProjectile>(); effect.HoldAsset(asset);
            root.GetComponent<NetworkObject>().Spawn(true);
            effect.Initialize(playerManager, transform.forward, target, boosted);
        };
    }
    private void DisposeEspeonRuntime()
    {
        espeonAmmoLock?.Dispose(); espeonAmmoLock = null;
        espeonRuntime?.Dispose(); espeonRuntime = null;
        if (espeonStockView != null) Destroy(espeonStockView.gameObject);
        espeonStockView = null;
    }
}
