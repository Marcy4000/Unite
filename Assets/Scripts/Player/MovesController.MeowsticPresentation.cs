using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private sealed class MeowsticVisualRequest
    {
        public bool Cancelled;
        public MeowsticVisual Instance;
    }
    private readonly Dictionary<int, MeowsticVisualRequest> meowsticVisuals = new Dictionary<int, MeowsticVisualRequest>();
    private readonly Dictionary<MeowsticEffect, AsyncOperationHandle<GameObject>> meowsticProjectileAssets = new Dictionary<MeowsticEffect, AsyncOperationHandle<GameObject>>();
    private static readonly MeowsticEffect[] meowsticProjectileEffects = { MeowsticEffect.Basic, MeowsticEffect.Boosted };
    private int meowsticVisualSequence;
    private bool meowsticDecorationRequested;
    private Vector3 meowsticRoomAim;
    private void EnsureMeowsticProjectileAssets()
    {
        foreach (MeowsticEffect effect in meowsticProjectileEffects)
            if (!meowsticProjectileAssets.ContainsKey(effect))
                meowsticProjectileAssets.Add(effect, Addressables.LoadAssetAsync<GameObject>(MeowsticMoveRules.VisualFolder + effect + ".prefab"));
    }
    private int ShowMeowsticEffect(MeowsticEffect effect, Vector3 origin, Vector3 direction, float duration,
        ulong target = ulong.MaxValue, float size = 1, int detail = 0)
    {
        int id = ++meowsticVisualSequence;
        PresentMeowsticEffectRPC(effect, id, origin, direction, duration, target, size, detail, MeowsticNow);
        return id;
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PresentMeowsticEffectRPC(MeowsticEffect effect, int id, Vector3 origin, Vector3 direction,
        float duration, ulong target, float size, int detail, double started)
    {
        LoadMeowsticVisual(effect, id, origin, direction, duration, target, size, detail, started);
    }
    private void LoadMeowsticVisual(MeowsticEffect effect, int id, Vector3 origin, Vector3 direction,
        float duration, ulong target, float size, int detail, double started)
    {
        if (meowsticVisuals.ContainsKey(id)) return;
        var request = new MeowsticVisualRequest(); meowsticVisuals.Add(id, request);
        void Forget()
        {
            if (this != null && meowsticVisuals.TryGetValue(id, out var current) && current == request) meowsticVisuals.Remove(id);
        }
        void Loaded(AsyncOperationHandle<GameObject> handle)
        {
            float age = this != null ? (float)System.Math.Max(0, MeowsticNow - started) : duration;
            if (request.Cancelled || this == null || !IsSpawned || (duration > 0 && age >= duration) || handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogWarning($"Meowstic visual unavailable: {effect}: {handle.OperationException}");
                Addressables.Release(handle); Forget(); return;
            }
            if (effect == MeowsticEffect.RoomAim) { origin = meowsticRoomAim; direction = transform.position - origin; }
            GameObject instance = Instantiate(handle.Result, origin, Quaternion.LookRotation(MeowsticMoveRules.Direction(direction, transform.forward)));
            request.Instance = instance.GetComponent<MeowsticVisual>();
            if (request.Instance == null) { Destroy(instance); Addressables.Release(handle); Forget(); return; }
            request.Instance.Initialize(this, effect, duration, age, target, size, detail, handle, Forget);
        }
        var asset = Addressables.LoadAssetAsync<GameObject>(MeowsticMoveRules.VisualFolder + effect + ".prefab");
        if (asset.IsDone) Loaded(asset); else asset.Completed += Loaded;
    }
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void StopMeowsticEffectRPC(int id) { CancelMeowsticVisual(id); }
    private void CancelMeowsticVisual(int id)
    {
        if (!meowsticVisuals.TryGetValue(id, out var request)) return;
        request.Cancelled = true;
        if (request.Instance != null) Destroy(request.Instance.gameObject);
        meowsticVisuals.Remove(id);
    }
    public void EnsureMeowsticDecoration()
    {
        if (!IsOwner || meowsticDecorationRequested || !IsMeowstic) return;
        meowsticDecorationRequested = true;
        LoadMeowsticVisual(MeowsticEffect.State, -1, transform.position, transform.forward, 0, NetworkObjectId, 1, 0, MeowsticNow);
    }
    public void ShowMeowsticRoomAim(bool show, Vector3 position)
    {
        if (playerManager == null || !playerManager.IsLocalHuman) return;
        meowsticRoomAim = position;
        if (!show) { CancelMeowsticVisual(-2); return; }
        if (!meowsticVisuals.TryGetValue(-2, out var request)) LoadMeowsticVisual(MeowsticEffect.RoomAim, -2, position, transform.position - position, 0, ulong.MaxValue, MeowsticMoveRules.RoomHalfSize, 0, MeowsticNow);
        else if (request.Instance != null) request.Instance.transform.SetPositionAndRotation(position, Quaternion.LookRotation(MeowsticMoveRules.Direction(transform.position - position, transform.forward)));
    }
    private void DisposeMeowsticPresentation()
    {
        foreach (int id in new List<int>(meowsticVisuals.Keys)) CancelMeowsticVisual(id);
        foreach (var asset in meowsticProjectileAssets.Values) if (asset.IsValid()) Addressables.Release(asset);
        meowsticProjectileAssets.Clear();
        meowsticDecorationRequested = false;
    }
}
