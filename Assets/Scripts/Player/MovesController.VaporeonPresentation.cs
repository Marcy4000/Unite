using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private sealed class VaporVisualRequest { public bool Cancelled; public VaporVisual Instance; public Vector3 Position; public ulong Target; }
    private readonly Dictionary<int, VaporVisualRequest> vaporVisuals = new Dictionary<int, VaporVisualRequest>();
    private readonly List<AsyncOperationHandle<GameObject>> vaporWarmAssets = new List<AsyncOperationHandle<GameObject>>();
    private int vaporVisualSequence, vaporLocalSequence;
    private bool vaporDecoration, vaporAssetsRequested;
    private Transform vaporModel;
    private Vector3 vaporModelPosition;
    private float vaporSink;
    private bool vaporModelPosed;
    private double vaporHopStarted = -10;

    private int ShowVaporeonEffect(VaporEffect effect, Vector3 origin, float duration, ulong target = ulong.MaxValue, float size = 1, int detail = 0)
    {
        int id = ++vaporVisualSequence;
        PresentVaporeonEffectRPC(effect, id, origin, duration, target, size, detail, VaporNow);
        return id;
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PresentVaporeonEffectRPC(VaporEffect effect, int id, Vector3 origin, float duration, ulong target, float size, int detail, double started)
    { LoadVaporeonVisual(effect, id, origin, duration, target, size, detail, started); }

    private void LoadVaporeonVisual(VaporEffect effect, int id, Vector3 origin, float duration, ulong target, float size, int detail, double started)
    {
        if (vaporVisuals.ContainsKey(id)) return;
        var request = new VaporVisualRequest { Position = origin, Target = target }; vaporVisuals.Add(id, request);
        Addressables.LoadAssetAsync<GameObject>(VaporMoveRules.EffectPath(effect)).Completed += handle =>
        {
            float age = this != null ? (float)Math.Max(0, VaporNow - started) : duration;
            if (request.Cancelled || this == null || !IsSpawned || (duration > 0 && age >= duration) || handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogWarning($"Vaporeon visual unavailable: {effect}: {handle.OperationException}");
                Addressables.Release(handle);
                if (this != null) vaporVisuals.Remove(id);
                return;
            }
            var go = Instantiate(handle.Result, request.Position, Quaternion.identity);
            request.Instance = go.GetComponent<VaporVisual>();
            if (request.Instance == null)
            {
                Destroy(go); Addressables.Release(handle); vaporVisuals.Remove(id);
                Debug.LogError($"Vaporeon effect has no VaporVisual: {effect}"); return;
            }
            request.Instance.Initialize(playerManager, effect, request.Position, duration, age, request.Target, size, detail,
                handle, () => vaporVisuals.Remove(id));
        };
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void StopVaporeonEffectRPC(int id) { StopVaporeonLocalEffect(id); }
    public void StopVaporeonLocalEffect(int id)
    {
        if (!vaporVisuals.TryGetValue(id, out var request)) return;
        request.Cancelled = true;
        if (request.Instance != null) Destroy(request.Instance.gameObject);
        vaporVisuals.Remove(id);
    }
    public int ShowVaporeonAim(VaporCast kind, Vector3 center, ulong target)
    {
        int id = --vaporLocalSequence;
        float radius = kind == VaporCast.Whirlpool ? VaporMoveRules.WhirlpoolRadius : kind == VaporCast.RainDance ? VaporMoveRules.RainRadius
            : kind == VaporCast.Swift ? 1f : kind == VaporCast.Tempest ? VaporMoveRules.TempestRadius : 0.85f;
        LoadVaporeonVisual(VaporEffect.Aim, id, center, 0, target, radius, (int)kind, VaporNow);
        return id;
    }
    public void UpdateVaporeonAim(int id, Vector3 center, ulong target)
    {
        if (!vaporVisuals.TryGetValue(id, out var request)) return;
        request.Position = center; request.Target = target;
        if (request.Instance != null) request.Instance.UpdateAim(center, target);
    }
    public void EnsureVaporeonBoostDecoration()
    {
        if (vaporDecoration || !IsOwner) return;
        vaporDecoration = true;
        EnsureVaporeonPresentationAssets();
        LoadVaporeonVisual(VaporEffect.BoostReady, --vaporLocalSequence, transform.position, 0, NetworkObjectId, 1, 0, VaporNow);
    }
    private void EnsureVaporeonPresentationAssets()
    {
        if (vaporAssetsRequested || !IsClient) return;
        vaporAssetsRequested = true;
        // Warm on every client, not only the caster: the first enemy telegraph
        // matters too. Gameplay still never waits for these loads.
        foreach (VaporEffect effect in Enum.GetValues(typeof(VaporEffect)))
            vaporWarmAssets.Add(Addressables.LoadAssetAsync<GameObject>(VaporMoveRules.EffectPath(effect)));
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PresentVaporeonHopRPC(double started) { vaporHopStarted = started; }
    private void UpdateVaporeonModel()
    {
        if (pokemon == null || pokemon.BaseStats == null || pokemon.BaseStats.PokemonName != "Vaporeon") return;
        Transform current = pokemon.ActiveModel != null ? pokemon.ActiveModel.transform : null;
        if (current != vaporModel)
        {
            if (vaporModel != null) vaporModel.localPosition = vaporModelPosition;
            vaporModel = current; vaporSink = 0;
            if (current != null) vaporModelPosition = current.localPosition;
        }
        if (vaporModel == null) return;
        float hop = (float)(VaporNow - vaporHopStarted);
        bool hopping = hop >= 0 && hop < 0.35f && pokemon.CurrentHp > 0 && !pokemon.HasAnyStatusEffect(VaporMoveRules.Interrupts);
        bool diving = VaporeonDiving && pokemon.CurrentHp > 0;
        if (!hopping && !diving && vaporSink <= 0)
        {
            if (vaporModelPosed) vaporModel.localPosition = vaporModelPosition;
            else vaporModelPosition = vaporModel.localPosition;
            vaporModelPosed = false; return;
        }
        vaporModelPosed = true;
        vaporSink = Mathf.MoveTowards(vaporSink, diving ? 1.7f : 0, Time.deltaTime * 12);
        float height = hopping ? Mathf.Sin(hop / 0.35f * Mathf.PI) * 0.7f : 0;
        vaporModel.localPosition = vaporModelPosition + Vector3.up * (height - vaporSink);
    }
    private void DisposeVaporeonPresentation()
    {
        foreach (var request in new List<VaporVisualRequest>(vaporVisuals.Values))
        {
            request.Cancelled = true;
            if (request.Instance != null) Destroy(request.Instance.gameObject);
        }
        vaporVisuals.Clear(); vaporDecoration = false; vaporAssetsRequested = false;
        foreach (var handle in vaporWarmAssets) if (handle.IsValid()) Addressables.Release(handle);
        vaporWarmAssets.Clear();
        if (vaporModel != null) vaporModel.localPosition = vaporModelPosition;
        vaporModel = null; vaporSink = 0; vaporModelPosed = false;
    }
}
