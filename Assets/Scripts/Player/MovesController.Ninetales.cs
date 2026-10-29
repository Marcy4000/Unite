using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private NinetalesRuntime ninetalesRuntime;
    private readonly NetworkVariable<bool> ninetalesInsideVeil = new NetworkVariable<bool>();
    private readonly Dictionary<ulong, StackMarkerView> ninetalesSnowViews = new Dictionary<ulong, StackMarkerView>();
    private NinetalesPassiveWarningView ninetalesPassiveWarning;
    public bool NinetalesInsideVeil => ninetalesInsideVeil.Value;
    public NinetalesRuntime NinetalesCombat
    {
        get
        {
            if (!IsServer || pokemon == null || pokemon.BaseStats == null
                || pokemon.BaseStats.Passive != AvailablePassives.NinetalesPassive) return null;
            return ninetalesRuntime ?? (ninetalesRuntime = new NinetalesRuntime(playerManager));
        }
    }
    private void UpdateNinetalesRuntime() { if (IsServer) NinetalesCombat?.Tick(); }
    public void SetNinetalesVeilPresence(bool inside) { if (IsServer && IsSpawned) ninetalesInsideVeil.Value = inside; }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    public void PresentNinetalesPassiveWarningRPC(bool active, Vector3 center, double began)
    {
        if (ninetalesPassiveWarning != null) Destroy(ninetalesPassiveWarning.gameObject);
        ninetalesPassiveWarning = null;
        if (!IsClient || !active) return;
        var prefab = Resources.Load<GameObject>(NinetalesMoveRules.Visuals + "SnowWarning");
        if (prefab == null) return;
        ninetalesPassiveWarning = Instantiate(prefab, center, Quaternion.identity).GetComponent<NinetalesPassiveWarningView>();
        float elapsed = (float)System.Math.Max(0, NetworkManager.ServerTime.Time - began);
        ninetalesPassiveWarning.Initialize(elapsed, NinetalesMoveRules.PassiveWarningDuration, NinetalesMoveRules.PassiveRadius);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    public void PresentNinetalesSnowRPC(ulong targetId, int count)
    {
        if (!IsClient) return;
        if (count == 0)
        {
            if (ninetalesSnowViews.TryGetValue(targetId, out var old) && old != null) Destroy(old.gameObject);
            ninetalesSnowViews.Remove(targetId); return;
        }
        if (!ninetalesSnowViews.TryGetValue(targetId, out var view) || view == null)
        {
            if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var target)) return;
            var prefab = Resources.Load<GameObject>(NinetalesMoveRules.Visuals + "SnowStacks");
            if (prefab == null) return;
            view = Instantiate(prefab, target.transform).GetComponent<StackMarkerView>(); ninetalesSnowViews[targetId] = view;
        }
        view.SetCount(count);
    }

    // Each homing attack owns its load and asset handle, not a shared spawn callback.
    public void LaunchNinetalesBasic(Pokemon target, bool boosted)
    {
        if (!IsServer || !NinetalesMoveRules.CanHit(playerManager, target)) return;
        bool cancelled = false;
        Action<DamageInfo> death = _ => cancelled = true;
        pokemon.OnDeath += death;
        Addressables.LoadAssetAsync<GameObject>(NinetalesMoveRules.Folder + "NinetalesBasicProjectile.prefab").Completed += asset =>
        {
            if (pokemon != null) pokemon.OnDeath -= death;
            if (cancelled || this == null || !IsSpawned || !NinetalesMoveRules.Living(pokemon)
                || !NinetalesMoveRules.CanHit(playerManager, target) || asset.Status != AsyncOperationStatus.Succeeded)
            {
                if (asset.Status != AsyncOperationStatus.Succeeded) Debug.LogError("Failed to load Ninetales basic projectile: " + asset.OperationException);
                Addressables.Release(asset); return;
            }
            var root = Instantiate(asset.Result, transform.position + Vector3.up * 0.8f, Quaternion.identity);
            var effect = root.GetComponent<NinetalesBasicProjectile>(); effect.HoldAsset(asset);
            root.GetComponent<NetworkObject>().Spawn(true);
            effect.Initialize(playerManager, transform.forward, target, boosted);
        };
    }

    private void DisposeNinetalesRuntime()
    {
        ninetalesRuntime?.Dispose(); ninetalesRuntime = null;
        if (ninetalesPassiveWarning != null) Destroy(ninetalesPassiveWarning.gameObject);
        ninetalesPassiveWarning = null;
        foreach (var view in ninetalesSnowViews.Values) if (view != null) Destroy(view.gameObject);
        ninetalesSnowViews.Clear();
    }
}
