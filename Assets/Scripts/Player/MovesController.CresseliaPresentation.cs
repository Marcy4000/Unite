using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public partial class MovesController
{
    private readonly Dictionary<int, CresseliaVisualRequest> cresseliaVisuals = new Dictionary<int, CresseliaVisualRequest>();
    private int cresseliaVisualSequence;
    private bool cresseliaDecorationRequested;

    private int ShowCresseliaEffect(CresseliaEffect effect, Vector3 origin, Vector3 direction, float duration,
        ulong target = 0, float size = 1, int detail = 0)
    {
        int id = ++cresseliaVisualSequence;
        PresentCresseliaEffectRPC(effect, id, origin, direction, duration, target, size, detail, CresseliaNow);
        return id;
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PresentCresseliaEffectRPC(CresseliaEffect effect, int id, Vector3 origin, Vector3 direction,
        float duration, ulong target, float size, int detail, double started)
    {
        LoadCresseliaVisual(effect, id, origin, direction, duration, target, size, detail, started);
    }

    private void LoadCresseliaVisual(CresseliaEffect effect, int id, Vector3 origin, Vector3 direction,
        float duration, ulong target, float size, int detail, double started)
    {
        if (cresseliaVisuals.ContainsKey(id)) return;
        var request = new CresseliaVisualRequest();
        cresseliaVisuals.Add(id, request);
        Addressables.LoadAssetAsync<GameObject>(CresseliaMoveRules.EffectPath(effect)).Completed += handle =>
        {
            float age = this != null ? (float)System.Math.Max(0, CresseliaNow - started) : duration;
            if (request.Cancelled || this == null || !IsSpawned || (effect != CresseliaEffect.Passive && age >= duration)
                || handle.Status != AsyncOperationStatus.Succeeded)
            {
                if (handle.Status != AsyncOperationStatus.Succeeded) Debug.LogWarning($"Cresselia visual unavailable: {effect}: {handle.OperationException}");
                Addressables.Release(handle);
                if (this != null) cresseliaVisuals.Remove(id);
                return;
            }
            var go = Instantiate(handle.Result, origin, Quaternion.LookRotation(CresseliaMoveRules.Direction(direction, transform.forward)));
            request.Instance = go.GetComponent<CresseliaVisual>();
            if (request.Instance == null)
            {
                Destroy(go); Addressables.Release(handle); cresseliaVisuals.Remove(id);
                Debug.LogError($"Cresselia visual prefab has no CresseliaVisual: {effect}"); return;
            }
            request.Instance.Initialize(playerManager, effect, origin, direction, duration, age, target, size, detail,
                handle, () => cresseliaVisuals.Remove(id));
        };
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void StopCresseliaEffectRPC(int id)
    {
        if (!cresseliaVisuals.TryGetValue(id, out var request)) return;
        request.Cancelled = true;
        if (request.Instance != null) Destroy(request.Instance.gameObject);
        cresseliaVisuals.Remove(id);
    }

    public void EnsureCresseliaDecoration()
    {
        if (cresseliaDecorationRequested || !IsOwner) return;
        cresseliaDecorationRequested = true;
        LoadCresseliaVisual(CresseliaEffect.Passive, -1, transform.position, transform.forward, 0, NetworkObjectId, 1, 0, CresseliaNow);
    }

    private void DisposeCresseliaPresentation()
    {
        var requests = new List<CresseliaVisualRequest>(cresseliaVisuals.Values);
        cresseliaVisuals.Clear();
        foreach (var request in requests)
        {
            request.Cancelled = true;
            if (request.Instance != null) Destroy(request.Instance.gameObject);
        }
    }
    private sealed class CresseliaVisualRequest
    {
        public bool Cancelled;
        public CresseliaVisual Instance;
    }
}
